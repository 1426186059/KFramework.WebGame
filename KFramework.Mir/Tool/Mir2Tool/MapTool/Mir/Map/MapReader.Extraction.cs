using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Mir.Map
{
    /// <summary>
    /// 移植自 Unity 工程 Assets/Model/Script/MirGraphics/MapReader.Editor.cs
    /// 负责把地图引用的贴图从素材池拷贝到目标目录，并渲染出 LargeMap2.png（小地图）。
    /// Unity 的 Texture2D / RenderTexture / EncodeToPNG 全部替换为 System.Drawing。
    /// </summary>
    public partial class MapReader
    {
        int count = 0;
        int missCount = 0;
        // 分类统计: [layer]_[fileIndex] → (ok, miss)
        Dictionary<string, (int ok, int miss)> _copyStats = new Dictionary<string, (int, int)>();

        string FileIndexToDir(int fi)
        {
            if (fi == 0) return "Tiles";
            if (fi == 1) return "SmTiles";
            if (fi == 2) return "Objects";
            return "Objects" + (fi - 1);
        }

        void RecordCopyStat(string layer, int fileIndex, bool success)
        {
            string key = layer + "_" + FileIndexToDir(fileIndex);
            if (!_copyStats.TryGetValue(key, out var s)) s = (0, 0);
            if (success) s.ok++; else s.miss++;
            _copyStats[key] = s;
        }

        public const int CellWidth = 48;
        public const int CellHeight = 32;
        public List<Door> Doors = new List<Door>();
        public int AnimationCount;
        public string SourcePath = "";       // 图片池路径
        public string DestinationPath = "";  // 图片目标路径
        public string MapName = "";          // 地图名字
        public string MinimapPath = "";      // 小地图资源路径（原版 Data\mmap）
        public int MinimapIndex = -1;   // 小地图库索引，-1 表示自动用 MapName 匹配
        public int BigMapIndex = -1;    // 大地图库索引，-1 表示不拷贝
        public byte[] RawBytes => Bytes;  // 原始 .map 字节，用于输出 .bytes 文件
        public bool CancelRequested = false;  // 取消标记

        /// <summary>日志回调（由宿主注入，用于收集到界面）</summary>
        public Action<string>? LogSink;
        /// <summary>进度回调：进度(0~1) + 描述</summary>
        public Action<float, string>? ProgressSink;

        void Log(string msg) { LogSink?.Invoke(msg); MirLog.Log(msg); }
        void LogWarning(string msg) { LogSink?.Invoke("[WARN] " + msg); MirLog.LogWarning(msg); }
        void LogError(string msg) { LogSink?.Invoke("[ERROR] " + msg); MirLog.LogError(msg); }
        void ReportProgress(string info, float p) { ProgressSink?.Invoke(p, info); }

        /// <summary>
        /// 根据地图格式返回素材库文件夹名，用于目标路径：WemadeMir2 / ShandaMir2 / WemadeMir3
        /// </summary>
        public string FormatFolderName
        {
            get
            {
                switch (MapFormatName)
                {
                    case "WemadeMir2": return "WemadeMir2";
                    case "ShandaMir2": return "ShandaMir2";
                    case "WemadeMir3":
                    case "ShandaMir3":
                    case "Heroes": return "WemadeMir3";
                    default: return "WemadeMir2";
                }
            }
        }

        public void DrawFloor()
        {
            CancelRequested = false;
            _copyStats.Clear();
            _backMissLogged = 0;
            int index;
            int backDebugCount = 0;
            int backDebugMax = 10;
            for (int y = 0; y < Height; y++)
            {
                if (y % 2 == 1) continue;

                for (int x = 0; x < Width; x++)
                {
                    if (x % 2 == 1) continue;
                    if ((MapCells[x][y].BackImage & 0x1FFFFFFF) == 0 || MapCells[x][y].BackIndex == -1) continue;
                    index = (MapCells[x][y].BackImage & 0x1FFFFFFF) - 1;

                    if (backDebugCount < backDebugMax)
                    {
                        Log($"[{MapName}] Back({x},{y}) rawBackImage=0x{MapCells[x][y].BackImage:X8} fileIndex={MapCells[x][y].BackIndex} imgIdx={index}");
                        backDebugCount++;
                    }

                    CopyImgBack(MapCells[x][y].BackIndex, index);
                    if (CancelRequested) break;
                }
                if (CancelRequested) break;
            }
            if (CancelRequested) return;

            // Middle 层拷贝（原版 Crystal 在一次遍历中画 Back→Middle→Front，此处补齐 Middle 层）
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int midIndex = MapCells[x][y].MiddleImage - 1;
                    if (midIndex >= 0 && MapCells[x][y].MiddleIndex != -1)
                    {
                        CopyImgMiddle(MapCells[x][y].MiddleIndex, midIndex);
                        if (CancelRequested) break;
                    }
                }
                if (CancelRequested) break;
            }
            if (CancelRequested) return;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    byte animation;

                    #region Draw front layer
                    int fileIndex;
                    index = (MapCells[x][y].FrontImage & 0x7FFF) - 1;

                    if (index < 0) continue;

                    fileIndex = MapCells[x][y].FrontIndex;
                    if (fileIndex == -1) continue;
                    animation = MapCells[x][y].FrontAnimationFrame;

                    bool blend;
                    if ((animation & 0x80) > 0)
                    {
                        blend = true;
                        animation &= 0x7F;
                    }
                    else
                        blend = false;

                    if (animation > 0)
                    {
                        byte animationTick = MapCells[x][y].FrontAnimationTick;
                        index += (AnimationCount % (animation + (animation * animationTick))) / (1 + animationTick);
                    }

                    if (MapCells[x][y].DoorIndex > 0)
                    {
                        Door? DoorInfo = GetDoor(MapCells[x][y].DoorIndex);
                        if (DoorInfo == null)
                        {
                            DoorInfo = new Door() { index = MapCells[x][y].DoorIndex, DoorState = 0, ImageIndex = 0, LastTick = Environment.TickCount64 };
                            Doors.Add(DoorInfo);
                        }
                        else
                        {
                            if (DoorInfo.DoorState != 0)
                            {
                                index += (DoorInfo.ImageIndex + 1) * MapCells[x][y].DoorOffset;
                            }
                        }
                    }

                    if (blend)
                    {
                        Log("未实现地图效果");
                    }
                    else
                    {
                        CopyImgFront(fileIndex, index);
                        if (CancelRequested) break;
                    }
                    #endregion
                }
                if (CancelRequested) break;
            }
            // 分类打印拷贝统计
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{MapName}] 成功拷贝:{count}次  失败:{missCount}次  共:{count + missCount}次");
            sb.AppendLine("  分类明细:");
            foreach (var kv in _copyStats)
            {
                if (kv.Value.ok + kv.Value.miss == 0) continue;
                sb.AppendLine($"    {kv.Key}: 成功{kv.Value.ok} 缺失{kv.Value.miss}");
            }
            Log(sb.ToString());

            // 拷贝地图二进制数据 (.map.bytes)
            string bytesDir = DestinationPath + MapName + "/";
            if (!Directory.Exists(bytesDir)) Directory.CreateDirectory(bytesDir);
            string bytesPath = bytesDir + MapName + ".map.bytes";
            File.WriteAllBytes(bytesPath, RawBytes);
            Log($"地图 .bytes 已保存: {bytesPath}");

            // 拷贝地图图片（比较 MiniMap / BigMap 实际尺寸，选大的那张）
            string mmapDir = DestinationPath + MapName + "/MMap/";
            if (!Directory.Exists(mmapDir)) Directory.CreateDirectory(mmapDir);

            // 原版小地图 → 存为 LargeMap.png（保留做对比）
            int bestIdx = PickLargerMapImage();
            if (bestIdx > 0)
            {
                string? mmapFile = FindMapImageFile(bestIdx);
                if (mmapFile != null)
                {
                    string dstFile = mmapDir + "LargeMap.png";
                    File.Copy(mmapFile, dstFile, true);
                    Log($"[{MapName}] 原版小地图已保存 (索引={bestIdx}): {dstFile}");
                }
                else
                {
                    LogWarning($"[{MapName}] 原版地图图片不存在: 索引={bestIdx}");
                }
            }
            else
            {
                LogWarning($"[{MapName}] 无可用地图图片 (BigMap={BigMapIndex}, MiniMap={MinimapIndex})，无原版 LargeMap");
            }

            // 渲染地图 → 统一叫 LargeMap2.png
            GenerateMinimap("LargeMap2.png");

            ClearTexCache();
        }

        // 查找 mmap 目录下给定索引的图片文件（png/bmp，忽略大小写）
        string? FindMapImageFile(int idx)
        {
            string basePath = MinimapPath.TrimEnd('\\', '/') + "/" + idx;
            string[] exts = { ".png", ".PNG", ".bmp", ".BMP" };
            foreach (var ext in exts)
                if (File.Exists(basePath + ext)) return basePath + ext;
            return null;
        }

        // 读取图片尺寸（支持 png/bmp）
        (int w, int h, bool ok) ReadImageSize(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath)) return (0, 0, false);
                using var img = Image.FromFile(imagePath);
                return (img.Width, img.Height, true);
            }
            catch { return (0, 0, false); }
        }

        // 比较 BigMap 和 MiniMap 两张图片的像素面积，返回较大者的索引
        int PickLargerMapImage()
        {
            int[] candidates = (BigMapIndex > 0 && MinimapIndex >= 0)
                ? new int[] { BigMapIndex, MinimapIndex }
                : (BigMapIndex > 0 ? new int[] { BigMapIndex }
                : MinimapIndex >= 0 ? new int[] { MinimapIndex }
                : new int[0]);

            // 没有 MirDB 数据时，用 MapName 回退
            if (candidates.Length == 0)
            {
                if (int.TryParse(MapName, out int p)) candidates = new int[] { p };
            }

            int bestIdx = 0;
            int bestArea = 0;

            foreach (int idx in candidates)
            {
                string? path = FindMapImageFile(idx);
                if (path == null)
                {
                    LogWarning($"[{MapName}] 地图图片不存在: 索引={idx}");
                    continue;
                }
                var (w, h, ok) = ReadImageSize(path);
                if (!ok)
                {
                    LogWarning($"[{MapName}] 地图图片读取失败: {path}");
                    continue;
                }
                int area = w * h;
                Log($"[{MapName}] 索引={idx} 尺寸={w}x{h} 面积={area}");
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIdx = idx;
                }
            }

            if (bestArea == 0)
                LogWarning($"[{MapName}] 所有候选地图图片均不可用 (candidates={string.Join(",", candidates)})");
            return bestIdx;
        }

        /// <summary>
        /// 仿 Crystal.MapEditor MiniMap: 以每格12×8绘制 (tile缩到1/4)，再整体 Bilinear 缩放输出。
        /// Crystal: new Bitmap(mapWidth*12, mapHeight*8) → 画三层 → new Bitmap(src, src.Width/8, src.Height/8)
        /// </summary>
        void GenerateMinimap(string outputName = "LargeMap.png")
        {
            string mmapDir = DestinationPath + MapName + "/MMap/";
            if (!Directory.Exists(mmapDir)) Directory.CreateDirectory(mmapDir);
            string dst = mmapDir + outputName;

            // 绘制分辨率: 每格 12×8 (Crystal: mapWidth*12 × mapHeight*8)
            const int TILE_SCALE = 4;
            _drawCW = CellWidth / TILE_SCALE;   // 48/4=12
            _drawCH = CellHeight / TILE_SCALE;  // 32/4=8
            _tileScale = TILE_SCALE;

            int canvasW = Width * _drawCW;
            int canvasH = Height * _drawCH;

            if (canvasW <= 0 || canvasH <= 0)
            {
                LogError($"[{MapName}] 渲染尺寸异常: Width={Width} Height={Height}, 跳过 GenerateMinimap");
                _tileScale = 1;
                return;
            }

            // Step 1: 创建绘制画布 (tile 已缩到1/4)，绘制三层
            Bitmap canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Transparent);
            }

            _loadSuccess = 0; _loadFail = 0;
            DrawLayer_Back(canvas);
            int backOk = _loadSuccess, backFail = _loadFail;

            _loadSuccess = 0; _loadFail = 0;
            DrawLayer_Middle(canvas);
            int midOk = _loadSuccess, midFail = _loadFail;

            _loadSuccess = 0; _loadFail = 0;
            DrawLayer_Front(canvas);
            int frontOk = _loadSuccess, frontFail = _loadFail;

            // Step 2: 仿 Crystal 整体 Bilinear 缩放 (new Bitmap(src, targetW, targetH))
            const int targetMax = 1024;
            float downscale = Math.Max((float)canvasW / targetMax, (float)canvasH / targetMax);
            int finalW = canvasW, finalH = canvasH;
            Bitmap final = canvas;

            if (downscale > 1f)
            {
                finalW = (int)MathF.Round(canvasW / downscale);
                finalH = (int)MathF.Round(canvasH / downscale);
                Log($"[{MapName}] MiniMap缩放: ({canvasW}×{canvasH}) → ({finalW}×{finalH})");

                final = new Bitmap(finalW, finalH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(final))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(canvas, new Rectangle(0, 0, finalW, finalH), 0, 0, canvasW, canvasH, GraphicsUnit.Pixel);
                }
                canvas.Dispose();
            }

            // Step 3: 导出 PNG
            final.Save(dst, ImageFormat.Png);
            final.Dispose();

            _tileScale = 1;
            Log($"[{MapName}] MiniMap完成: {finalW}×{finalH} → {dst}\n" +
                $"  Back:   {backOk}加载 / {backFail}缺失\n" +
                $"  Middle: {midOk}加载 / {midFail}缺失\n" +
                $"  Front:  {frontOk}加载 / {frontFail}缺失");
        }

        /// <summary>获取平坦网格绘制坐标，仿 Crystal.MapEditor</summary>
        /// <param name="texH">贴图高度</param>
        /// <param name="isStandard">是否为标准地板尺寸 (12×8 或 24×16，即缩放后的 48×32 / 96×64)</param>
        void GetCellDrawPos(int x, int y, int texW, int texH, bool isStandard, out int dx, out int dy)
        {
            dx = x * _drawCW;
            if (isStandard)
                dy = y * _drawCH;
            else
                dy = (y + 1) * _drawCH - texH;
        }

        // 将贴图按 1:1 绘制到画布（GDI+ DrawImage 自带裁剪与 Alpha 混合）
        static void DrawTile(Graphics g, Bitmap tile, int dstX, int dstY)
        {
            g.DrawImage(tile, new Rectangle(dstX, dstY, tile.Width, tile.Height), 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel);
        }

        static Graphics CreateLayerGraphics(Bitmap canvas)
        {
            var g = Graphics.FromImage(canvas);
            g.CompositingMode = CompositingMode.SourceOver;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            return g;
        }

        // ============ DrawBack: 偶数行列，平坦网格 ============
        void DrawLayer_Back(Bitmap canvas)
        {
            int total = (Width / 2) * (Height / 2);
            int done = 0;
            using var g = CreateLayerGraphics(canvas);
            for (int y = 0; y < Height; y++)
            {
                if (y % 2 != 0) continue;
                for (int x = 0; x < Width; x++)
                {
                    if (x % 2 != 0) continue;
                    done++;
                    if (done % 100 == 0)
                    {
                        ReportProgress($"[{MapName}] Back层绘制... {done}/{total}", 0.1f + 0.2f * done / Math.Max(1, total));
                        if (CancelRequested) return;
                    }

                    int raw = MapCells[x][y].BackImage & 0x1FFFFFFF;
                    if (raw <= 0) continue;
                    short libIdx = MapCells[x][y].BackIndex;
                    int imgIdx = raw - 1;
                    if (libIdx < 0) continue;

                    string path = ResolveTilePath(libIdx, imgIdx);
                    var tex = LoadTileBitmap(path);
                    if (tex == null) continue;

                    // 平坦网格: 直接 x*cellW, y*cellH
                    int drawX = x * _drawCW;
                    int drawY = y * _drawCH;
                    DrawTile(g, tex, drawX, drawY);
                }
            }
        }

        // ============ DrawMiddle: 所有格子；非标准尺寸 Y 偏移 ============
        void DrawLayer_Middle(Bitmap canvas)
        {
            int total = Width * Height;
            int done = 0;
            using var g = CreateLayerGraphics(canvas);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    done++;
                    if (done % 200 == 0)
                    {
                        ReportProgress($"[{MapName}] Middle层绘制... {done}/{total}", 0.3f + 0.25f * done / Math.Max(1, total));
                        if (CancelRequested) return;
                    }

                    int midIdx = MapCells[x][y].MiddleImage - 1;
                    short libIdx = MapCells[x][y].MiddleIndex;
                    if (midIdx < 0 || libIdx < 0) continue;

                    string path = ResolveTilePath(libIdx, midIdx);
                    var tex = LoadTileBitmap(path);
                    if (tex == null) continue;

                    // 仿 Crystal: 标准 48x32 或 96x64 地板放平坦位置，否则 Y 偏移
                    int tw = tex.Width, th = tex.Height;
                    bool isStd = (tw == _drawCW && th == _drawCH) ||
                                 (tw == _drawCW * 2 && th == _drawCH * 2);
                    GetCellDrawPos(x, y, tw, th, isStd, out int drawX, out int drawY);
                    DrawTile(g, tex, drawX, drawY);
                }
            }
        }

        // ============ DrawFront: 非标准尺寸 Y 偏移，区分 blend ============
        void DrawLayer_Front(Bitmap canvas)
        {
            int total = Width * Height;
            int done = 0;
            using var g = CreateLayerGraphics(canvas);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    done++;
                    if (done % 200 == 0)
                    {
                        ReportProgress($"[{MapName}] Front层绘制... {done}/{total}", 0.55f + 0.35f * done / Math.Max(1, total));
                        if (CancelRequested) return;
                    }

                    short libIdx = MapCells[x][y].FrontIndex;
                    int rawAnim = MapCells[x][y].FrontAnimationFrame;
                    // 仿 Crystal: 跳过 Blend/动画 tiles (bit7=1)，MiniMap 不绘制
                    if ((rawAnim & 0x80) != 0) continue;

                    int frontIdx = (MapCells[x][y].FrontImage & 0x7FFF) - 1;
                    if (frontIdx < 0 || libIdx == -1 || libIdx == 200) continue;

                    // 门动画
                    if (MapCells[x][y].DoorIndex > 0)
                    {
                        Door? doorInfo = GetDoor(MapCells[x][y].DoorIndex);
                        if (doorInfo != null && doorInfo.DoorState != 0)
                        {
                            frontIdx += (doorInfo.ImageIndex + 1) * MapCells[x][y].DoorOffset;
                        }
                    }

                    string path = ResolveTilePath(libIdx, frontIdx);
                    var tex = LoadTileBitmap(path);
                    if (tex == null) continue;

                    // 仿 Crystal: 标准地板平坦，非标准 Y 偏移
                    int tw = tex.Width, th = tex.Height;
                    bool isStd = (tw == _drawCW && th == _drawCH) ||
                                 (tw == _drawCW * 2 && th == _drawCH * 2);
                    GetCellDrawPos(x, y, tw, th, isStd, out int drawX, out int drawY);

                    DrawTile(g, tex, drawX, drawY);
                }
            }
        }

        // ---- MiniMap 辅助字段与方法 ----
        int _drawCW, _drawCH;     // 绘制格子尺寸 (48/4=12, 32/4=8 仿Crystal)
        int _tileScale = 1;       // tile加载时缩放倍数
        int _loadSuccess, _loadFail;
        int _missLogMax = 5;

        // 贴图缓存 (路径 → Bitmap)，tile 加载时按 _tileScale 缩小
        readonly Dictionary<string, Bitmap> _texCache = new Dictionary<string, Bitmap>();
        readonly HashSet<string> _texMiss = new HashSet<string>();

        /// <summary>获取 (libIdx, imgIdx) 对应的 PNG 文件路径</summary>
        string ResolveTilePath(short libIdx, int imgIdx)
        {
            string tileRoot = DestinationPath + MapName + "/" + FormatFolderName + "/";
            if (libIdx == 0) return tileRoot + "Tiles/" + imgIdx + ".png";
            if (libIdx == 1) return tileRoot + "SmTiles/" + imgIdx + ".png";
            if (libIdx == 2) return tileRoot + "Objects/" + imgIdx + ".png";
            return tileRoot + "Objects" + (libIdx - 1) + "/" + imgIdx + ".png";
        }

        Bitmap? LoadTileBitmap(string pngPath)
        {
            if (_texCache.TryGetValue(pngPath, out var cached) && cached != null)
                return cached;
            if (_texMiss.Contains(pngPath))
                return null;

            // Step 1: 先尝试目标路径
            Bitmap? result = TryLoadScaled(pngPath);

            // Step 2: 回退到源路径 (部分 tile 未拷贝到目标)
            if (result == null)
            {
                string dstRoot = DestinationPath + MapName + "/" + FormatFolderName + "/";
                if (pngPath.StartsWith(dstRoot))
                {
                    string relative = pngPath.Substring(dstRoot.Length);
                    string srcPath = SourcePath + relative;
                    result = TryLoadScaled(srcPath);
                }
            }

            if (result != null)
            {
                _loadSuccess++;
                _texCache[pngPath] = result;
                return result;
            }

            _loadFail++;
            if (_loadFail <= _missLogMax)
                LogWarning($"[{MapName}] tile缺失 (第{_loadFail}个): {pngPath}");
            _texMiss.Add(pngPath);
            return null;
        }

        Bitmap? TryLoadScaled(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                byte[] data = File.ReadAllBytes(path);
                using var ms = new MemoryStream(data);
                using var src = new Bitmap(ms);

                if (_tileScale > 1)
                {
                    int nw = Math.Max(1, src.Width / _tileScale);
                    int nh = Math.Max(1, src.Height / _tileScale);
                    var scaled = new Bitmap(nw, nh, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, new Rectangle(0, 0, nw, nh), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
                    }
                    return scaled;
                }

                // 复制一份，避免依赖外部 stream / 锁定文件
                var copy = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(copy))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
                }
                return copy;
            }
            catch { return null; }
        }

        void ClearTexCache()
        {
            foreach (var t in _texCache.Values)
                if (t != null) t.Dispose();
            _texCache.Clear();
            _texMiss.Clear();
        }

        int _backMissLogged = 0;
        //背景图专用
        void CopyImgBack(int fileIndex, int imgIndex)
        {
            if (fileIndex >= 100)
            {
                LogError("少文件夹了" + fileIndex);
                return;
            }

            string sourcesFullPath, destinationFullPath, subDir;

            if (fileIndex == 0)
                subDir = "Tiles/";
            else if (fileIndex == 1)
                subDir = "SmTiles/";
            else if (fileIndex == 2)
                subDir = "Objects/";
            else
                subDir = "Objects" + (fileIndex - 1) + "/";

            sourcesFullPath = SourcePath + subDir + imgIndex + ".png";
            destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/" + subDir + imgIndex + ".png";

            string dstDir = DestinationPath + MapName + "/" + FormatFolderName + "/" + subDir;
            if (!Directory.Exists(dstDir)) Directory.CreateDirectory(dstDir);

            FileInfo file = new FileInfo(sourcesFullPath);
            if (file.Exists)
            {
                file.CopyTo(destinationFullPath, true);
                count++;
            }
            else
            {
                if (_backMissLogged < 3)
                    LogWarning("源背景图片不存在:" + sourcesFullPath);
                _backMissLogged++;
                missCount++;
            }
            RecordCopyStat("Back", fileIndex, file.Exists);
            ReportProgress("拷贝背景图 - " + MapName, (float)(count + missCount) / Math.Max(1, Width * Height));
        }

        //前景图专用
        void CopyImgFront(int fileIndex, int imgIndex)
        {
            string sourcesFullPath;
            string destinationFullPath;

            if (fileIndex == 0)
            {
                sourcesFullPath = SourcePath + "Tiles/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Tiles/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Tiles")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Tiles");
            }
            else if (fileIndex == 1)
            {
                sourcesFullPath = SourcePath + "SmTiles/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles");
            }
            else if (fileIndex == 2)
            {
                sourcesFullPath = SourcePath + "Objects/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Objects/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Objects/")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Objects/");
            }
            else
            {
                sourcesFullPath = SourcePath + "Objects" + (fileIndex - 1) + "/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1) + "/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1))) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1));
            }

            FileInfo file = new FileInfo(sourcesFullPath);
            bool fexists = file.Exists;
            if (fexists)
            {
                file.CopyTo(destinationFullPath, true);
                count++;
            }
            else
            {
                LogWarning("源前景图片不存在:" + sourcesFullPath);
                missCount++;
            }
            RecordCopyStat("Front", fileIndex, fexists);
            ReportProgress("拷贝前景图 - " + MapName, (float)(count + missCount) / Math.Max(1, Width * Height));
        }

        //中景图专用（Middle 层，如 SmTiles 等中间物件）
        void CopyImgMiddle(int fileIndex, int imgIndex)
        {
            string sourcesFullPath;
            string destinationFullPath;

            if (fileIndex == 0)
            {
                sourcesFullPath = SourcePath + "Tiles/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Tiles/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Tiles")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Tiles");
            }
            else if (fileIndex == 1)
            {
                sourcesFullPath = SourcePath + "SmTiles/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/SmTiles");
            }
            else if (fileIndex == 2)
            {
                sourcesFullPath = SourcePath + "Objects/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Objects/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Objects/")) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Objects/");
            }
            else
            {
                sourcesFullPath = SourcePath + "Objects" + (fileIndex - 1) + "/" + imgIndex + ".png";
                destinationFullPath = DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1) + "/" + imgIndex + ".png";
                if (!Directory.Exists(DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1))) Directory.CreateDirectory(DestinationPath + MapName + "/" + FormatFolderName + "/Objects" + (fileIndex - 1));
            }

            FileInfo file = new FileInfo(sourcesFullPath);
            bool mexists = file.Exists;
            if (mexists)
            {
                file.CopyTo(destinationFullPath, true);
                count++;
            }
            else
            {
                LogWarning("源中景图片不存在:" + sourcesFullPath);
                missCount++;
            }
            RecordCopyStat("Middle", fileIndex, mexists);
            ReportProgress("拷贝中景图 - " + MapName, (float)(count + missCount) / Math.Max(1, Width * Height));
        }

        public Door? GetDoor(byte Index)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                if (Doors[i].index == Index)
                    return Doors[i];
            }
            return null;
        }
    }
}
