using Client.MirObjects;
using SlimDX;
using WebGame.Mir2.MonoGame.Client;
using Frame = Client.MirObjects.Frame;

namespace Client.MirGraphics
{
    public sealed class MLibrary : IDisposable
    {
        public const string Extention = ".Lib";
        public const int LibVersion = 3;

        private readonly string _fileName;

        // 只读暴露库文件名：UI 层归属断言等诊断日志需要它来定位控件取自哪个 .Lib。
        public string FileName { get { return _fileName; } }

        private MImage[] _images;
        private FrameSet _frames;
        private int[] _indexList;
        private int _count;
        private bool _initialized;
        private bool _loading;   // 正在异步拉取字节（幂等，避免重复请求）
        private bool _loaded;    // 字节已就绪且索引已解析，绘制可用
        private bool _failed;    // 加载失败（缺资源/404），不再重试，避免每帧 404 风暴

        private BinaryReader _reader;
        private Stream _stream;

        public FrameSet Frames
        {
            get { return _frames; }
        }

        public MLibrary(string filename)
        {
            // 浏览器 WASM 下 System.IO.Path 不把 '\' 当作目录分隔符，导致 ChangeExtension 把
            // ".\Data\ChrSel" 误判为“扩展名”，整体替换成 ".Lib"（命中 InitializeAsync 的跳过分支，库永不加载 → 粉屏）。
            // 统一规范化为正斜杠相对路径并去掉开头的 "./" 前缀，ChangeExtension 即可正确得到 "Data/ChrSel.Lib"。
            if (!string.IsNullOrEmpty(filename))
            {
                filename = filename.Replace('\\', '/');
                while (filename.StartsWith("./")) filename = filename.Substring(2);
            }
            _fileName = Path.ChangeExtension(filename, Extention);
        }

        /// <summary>
        /// 异步加载：经 HTTP 拉取字节并解析索引表。幂等（加载中或已加载会直接返回）。
        /// 浏览器端资源在远程 HTTP 服务上，本地文件系统为空，因此不再用 File.Exists 判断存在性。
        /// </summary>
        public async Task InitializeAsync()
        {
            // 已加载/已失败则幂等返回（加载成功后永久缓存；默认 lib 通道为统一资源，不按地图区分，无需换图重载）。
            if (_loaded || _failed) return;
            if (_loading) return;
            _loading = true;
            try
            {
                // 空文件名 / 仅扩展名的占位库（如 MapLibs 里未配置的空槽 new MLibrary("")，
                // 其 _fileName 为 ".Lib"）不发起请求，否则会每帧向资源服务器请求 "/.Lib" 造成 404 风暴。
                if (string.IsNullOrWhiteSpace(_fileName) || _fileName == Extention)
                {
                    _initialized = false;
                    _failed = true;
                    return;
                }

                KFramework.MonoGame.PrintTool.Log($"[Mir][lib] load {_fileName}");
                byte[] bytes = await BrowserResource.GetBytesAsync(_fileName);
                if (bytes == null || bytes.Length == 0)
                {
                    KFramework.MonoGame.PrintTool.Log($"[Mir][lib] empty: {_fileName}");
                    _initialized = false;
                    // 仅当资源确属缺失（BrowserResource 多次重试后仍失败）才永久失败；
                    // 瞬时失败在 GetBytesAsync 内已重试自愈，不会走这里，故不标记 _failed，
                    // 下次 CheckImage 会重新触发加载，避免 AOT 突发并发下整片黑地板。
                    _failed = BrowserResource.IsMissing(_fileName);
                    return;
                }

                ParseIndex(bytes);
                _loaded = true;
                _initialized = true;

                // 输出图数与首图尺寸：用于确认地砖规格（Tiles 为 96x96 时底图按 2x2 绘制，
                // 48x48 时每格一张），排查"地板不显示"时非常关键。
                string sizeInfo = string.Empty;
                try
                {
                    if (_images != null && _images.Length > 0)
                    {
                        Size s0 = GetSize(0);
                        sizeInfo = $"，图数={_images.Length}，首图={s0.Width}x{s0.Height}";
                    }
                }
                catch { }

                KFramework.MonoGame.PrintTool.Log($"[Mir][lib] ok: {_fileName}（{bytes.Length} 字节{sizeInfo}）");
                Libraries.OnLibraryLoaded();
            }
            catch (Exception ex)
            {
                _initialized = false;
                // 同上：仅确属缺失才永久失败；瞬时异常（连接尖峰下的偶发失败）不拉黑，留待重试。
                _failed = BrowserResource.IsMissing(_fileName);
                KFramework.MonoGame.PrintTool.LogError($"[Mir] 库加载失败 {_fileName}: {ex.Message} {ex.StackTrace}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>解析 .Lib 头部与索引表（与版本相关的结构）。</summary>
        private void ParseIndex(byte[] bytes)
        {
            _stream = new MemoryStream(bytes);
            _reader = new BinaryReader(_stream);
            int currentVersion = _reader.ReadInt32();
            if (currentVersion < 2)
            {
                KFramework.MonoGame.PrintTool.Log("Wrong lib version: " + _fileName + " expecting " + LibVersion + " found " + currentVersion);
                return;
            }
            _count = _reader.ReadInt32();

            int frameSeek = 0;
            if (currentVersion >= 3)
            {
                frameSeek = _reader.ReadInt32();
            }

            _images = new MImage[_count];
            _indexList = new int[_count];

            for (int i = 0; i < _count; i++)
                _indexList[i] = _reader.ReadInt32();

            if (currentVersion >= 3)
            {
                _stream.Seek(frameSeek, SeekOrigin.Begin);

                var frameCount = _reader.ReadInt32();

                if (frameCount > 0)
                {
                    _frames = new FrameSet();
                    for (int i = 0; i < frameCount; i++)
                    {
                        _frames.Add((MirAction)_reader.ReadByte(), new Frame(_reader));
                    }
                }
            }
        }

        private static long _lastMissingLogTime;
        private static int _missingImageCount;

        // 限频输出"图索引不存在"，用于判断"很多格子画不出来"是资源版本不匹配还是单纯缺图。
        private void LogMissingImage(int index)
        {
            _missingImageCount++;
            if (CMain.Time <= _lastMissingLogTime) return;

            _lastMissingLogTime = CMain.Time + 3000;
            KFramework.MonoGame.PrintTool.Log("[Img] 图不存在(不画): " + _fileName +
                " index=" + index + " 图片数=" + (_images != null ? _images.Length : 0) +
                " 累计=" + _missingImageCount);
        }

        // 缺失贴图占位（马赛克）统一由 MosaicManager 管理，这里不再各自生成。

        /// <summary>
        /// 返回可用于绘制的图像：
        /// - MosaicManager.MosaicImage = 尚未就绪（正在按需异步加载）/ 加载失败 / 索引越界 / 图像损坏，
        ///   统一画马赛克占位，避免地图格子与物件在资源下载完成前是一大片空洞；
        ///   库加载完成后 LibraryLoaded 会触发场景重绘，自动替换为真实贴图。
        /// - 正常 MImage = 已就绪。
        /// 坚决不在绘制路径做同步网络请求（会冻结主线程/触发心跳超时）。
        /// </summary>
        public MImage CheckImage(int index)
        {
            if (!_loaded)
            {
                // 加载失败：直接不画，避免"加载完还残留马赛克"。
                if (_failed) return null;
                if (!_loading) _ = InitializeAsync();
                // 真正"正在加载"：用马赛克占位，加载完成后自动换成真实贴图。
                return MosaicManager.MosaicImage;
            }

            if (_images == null) return null;

            // index < 0（常见为 -1）是 Mir2 里"该层没有图"的正常标记（如 MiddleImage-1），
            // 不算缺失，直接静默跳过，否则会把正常情况刷成海量日志。
            if (index < 0) return null;

            if (index >= _images.Length)
            {
                // 库已就绪但图号超出范围（资源版本不匹配 / 图缺失）：不画，不留马赛克。
                LogMissingImage(index);
                return null;
            }

            if (_images[index] == null)
            {
                if (_indexList[index] + 17 > _stream.Length)
                    KFramework.MonoGame.PrintTool.Log($"[Mir][坏图] {_fileName} idx={index} off={_indexList[index]} len={_stream.Length}");
                _stream.Position = _indexList[index];
                _images[index] = new MImage(_reader);
            }

            MImage mi = _images[index];
            if (!mi.TextureValid)
            {
                if (mi.Width == 0 || mi.Height == 0)
                    return MosaicManager.MosaicImage;
                _stream.Seek(_indexList[index] + 17, SeekOrigin.Begin);
                mi.CreateTexture(_reader);
            }

            return mi;
        }

        public Point GetOffSet(int index)
        {
            if (!_loaded) return Point.Empty;

            if (_images == null || index < 0 || index >= _images.Length)
                return Point.Empty;

            if (_images[index] == null)
            {
                if (_indexList[index] + 17 > _stream.Length)
                    KFramework.MonoGame.PrintTool.Log($"[Mir][坏图] {_fileName} idx={index} off={_indexList[index]} len={_stream.Length}");
                _stream.Seek(_indexList[index], SeekOrigin.Begin);
                _images[index] = new MImage(_reader);
            }

            return new Point(_images[index].X, _images[index].Y);
        }
        // 是否「还在等资源」= 未加载且未失败。
        // 烘焙侧需要区分三种情况，而 GetSize 对它们一律返回 Empty，无法分辨：
        //   加载中 → true（将来会有资源，值得再等/再烘一次）
        //   已失败 → false（永远不会有资源，不必再等）
        //   已加载 → false（有资源）
        public bool IsPending => !_loaded && !_failed;

        public Size GetSize(int index)
        {
            if (!_loaded) return Size.Empty;
            if (_images == null || index < 0 || index >= _images.Length)
                return Size.Empty;

            if (_images[index] == null)
            {
                if (_indexList[index] + 17 > _stream.Length)
                    KFramework.MonoGame.PrintTool.Log($"[Mir][坏图] {_fileName} idx={index} off={_indexList[index]} len={_stream.Length}");
                _stream.Seek(_indexList[index], SeekOrigin.Begin);
                _images[index] = new MImage(_reader);
            }

            return new Size(_images[index].Width, _images[index].Height);
        }
        public Size GetTrueSize(int index)
        {
            if (!_loaded) return Size.Empty;

            if (_images == null || index < 0 || index >= _images.Length)
                return Size.Empty;

            if (_images[index] == null)
            {
                if (_indexList[index] + 17 > _stream.Length)
                    KFramework.MonoGame.PrintTool.Log($"[Mir][坏图] {_fileName} idx={index} off={_indexList[index]} len={_stream.Length}");
                _stream.Position = _indexList[index];
                _images[index] = new MImage(_reader);
            }
            MImage mi = _images[index];
            if (mi.TrueSize.IsEmpty)
            {
                if (!mi.TextureValid)
                {
                    if ((mi.Width == 0) || (mi.Height == 0))
                        return Size.Empty;

                    _stream.Seek(_indexList[index] + 17, SeekOrigin.Begin);
                    mi.CreateTexture(_reader);
                }
                return mi.GetTrueSize();
            }
            return mi.TrueSize;
        }

        public void Draw(int index, int x, int y)
        {
            if (x >= Settings.ScreenWidth || y >= Settings.ScreenHeight)
                return;

            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (x + mi.Width < 0 || y + mi.Height < 0)
                return;


            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)x, (float)y, 0.0F), Color.White);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Point point, Color colour, bool offSet = false)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void Draw(int index, Point point, Color colour, bool offSet, float opacity)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.DrawOpaque(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour, opacity); 

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawBlend(int index, Point point, Color colour, bool offSet = false, float rate = 1)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(true, rate);

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            DXManager.SetBlend(oldBlend);
            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void DrawBlend(int index, Point point, Size size, Color colour, float rate = 1)
        {
            MImage mi = CheckImage(index);
            if (mi == null) return;

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + size.Width < 0 || point.Y + size.Height < 0) return;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(true, rate);
            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new RectangleF(point.X, point.Y, size.Width, size.Height), colour);
            DXManager.SetBlend(oldBlend);
            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Rectangle section, Point point, Color colour, bool offSet)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            if (section.Right > mi.Width)
                section.Width -= section.Right - mi.Width;

            if (section.Bottom > mi.Height)
                section.Height -= section.Bottom - mi.Height;

            DXManager.Draw(mi.Image, section, new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Rectangle section, Point point, Color colour, float opacity)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            if (section.Right > mi.Width)
                section.Width -= section.Right - mi.Width;

            if (section.Bottom > mi.Height)
                section.Height -= section.Bottom - mi.Height;

            DXManager.DrawOpaque(mi.Image, section, new Vector3((float)point.X, (float)point.Y, 0.0F), colour, opacity); 

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void Draw(int index, Point point, Size size, Color colour)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + size.Width < 0 || point.Y + size.Height < 0)
                return;

            float scaleX = (float)size.Width / mi.Width;
            float scaleY = (float)size.Height / mi.Height;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new RectangleF(point.X, point.Y, size.Width, size.Height), colour);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void Draw(int index, Rectangle section, RectangleF destRect, Color colour, float opacity = 1f)
        {
            MImage mi = CheckImage(index);
            if (mi == null) return;

            if (destRect.X >= Settings.ScreenWidth || destRect.Y >= Settings.ScreenHeight || destRect.X + destRect.Width < 0 || destRect.Y + destRect.Height < 0) return;

            Color c = colour;
            if (opacity < 1f)
                c = Color.FromArgb((byte)(c.A * opacity), c.R, c.G, c.B);

            DXManager.Draw(mi.Image, section, destRect, c);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawTinted(int index, Point point, Color colour, Color Tint, bool offSet = false)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            if (offSet) point.Offset(mi.X, mi.Y);

            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), colour);

            if (mi.HasMask)
            {
                DXManager.Draw(mi.MaskImage, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), Tint);
            }

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public void DrawUp(int index, int x, int y)
        {
            if (x >= Settings.ScreenWidth)
                return;

            MImage mi = CheckImage(index);
            if (mi == null)
                return;
            y -= mi.Height;
            if (y >= Settings.ScreenHeight)
                return;
            if (x + mi.Width < 0 || y + mi.Height < 0)
                return;

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3(x, y, 0.0F), Color.White);

            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }
        public void DrawUpBlend(int index, Point point)
        {
            MImage mi = CheckImage(index);
            if (mi == null)
                return;

            point.Y -= mi.Height;


            if (point.X >= Settings.ScreenWidth || point.Y >= Settings.ScreenHeight || point.X + mi.Width < 0 || point.Y + mi.Height < 0)
                return;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(true, 1);

            DXManager.Draw(mi.Image, new Rectangle(0, 0, mi.Width, mi.Height), new Vector3((float)point.X, (float)point.Y, 0.0F), Color.White);

            DXManager.SetBlend(oldBlend);
            mi.CleanTime = CMain.Time + Settings.CleanDelay;
        }

        public bool VisiblePixel(int index, Point point, bool accuate)
        {
            MImage mi = CheckImage(index);
            if (mi == null || mi.Image == null || !mi.TextureValid || mi.Data == null)
                return false;

            if (accuate)
                return mi.VisiblePixel(point);

            int accuracy = 2;

            for (int x = -accuracy; x <= accuracy; x++)
                for (int y = -accuracy; y <= accuracy; y++)
                    if (mi.VisiblePixel(new Point(point.X + x, point.Y + y)))
                        return true;

            return false;
        }

        /// <summary>是否正在异步加载（绘制路径外用于判断能否安全 Dispose）。</summary>
        public bool IsLoading => _loading;

        /// <summary>
        /// 释放本库持有的全部贴图（MImage）与解析用的字节流/读取器，复位加载状态，
        /// 以便切换地图后本库能被重新 InitializeAsync 拉取属于新地图的那份 Lib。
        /// 仅释放、不销毁库对象本身（_fileName 保留，供按需重载）。幂等。
        /// </summary>
        public void Dispose()
        {
            if (_images != null)
            {
                for (int i = 0; i < _images.Length; i++)
                    _images[i]?.Dispose();
            }
            _images = null;
            _indexList = null;
            _frames = null;

            if (_reader != null) { _reader.Dispose(); _reader = null; }
            if (_stream != null) { _stream.Dispose(); _stream = null; }

            _loaded = false;
            _initialized = false;
            _loading = false;
            _failed = false;
        }

    }
}
