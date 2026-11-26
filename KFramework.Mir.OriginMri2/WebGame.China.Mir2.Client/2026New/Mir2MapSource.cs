using System.IO.Compression;

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇地图数据客户端加载器。
    /// 服务端合并 map10.25341.1.dat 每格仅 4-bit 碰撞标志位(墙/遮挡/安全区)，不含瓦片图像；
    /// 地图视觉由 MapControl 从 CDN 的 assets/resource/zipmap/{img}/{index}.jpg 真实瓦片拼出。
    /// 解码与服务端 OriginalMapSource/OriginalMapDecoder 及提取工具 MapDecoder 完全一致：
    /// 每块开头丢弃 4 字节块头；单元格按「偶格取高 nibble、奇格取低 nibble」还原（逆向自原版 JS MapData.readData，bit3=墙）。
    /// </summary>

    public sealed class Mir2MapEntry
    {
        public int Id;
        public string Name = string.Empty;
        public int Img;      // 瓦片组(img)，zipmap 目录名；缺省回退到 Id
        public int Width;    // 配置像素宽
        public int Height;   // 配置像素高
    }

    public sealed class Mir2MapData
    {
        public int Width;        // 像素宽（来自配置）
        public int Height;       // 像素高（来自配置）
        public int Cols;
        public int Rows;
        public byte[] CellFlags;   // 每格 4-bit 标志（半字节），长度 = Cols*Rows
        public int Walkable;
        public int Cover;
        public int Safe;
        public string Warn;        // 网格估算告警（配置与数据不符时）
    }

    internal static class Mir2MapDecoder
    {
        // 与服务端 OriginalMapSource.DecompressIfNeeded 一致：dat 可能是 zlib(0x78) 或 gzip(0x1f8b) 压缩包，
        // 解压后才是对齐明文（头2字节=地图数count，随后每8字节一项 id+len）。不解压会导致索引全乱、查不到地图。
        public static byte[] DecompressIfNeeded(byte[] raw)
        {
            if (raw != null && raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b)
            {
                using var ms = new MemoryStream(raw);
                using var gz = new GZipStream(ms, CompressionMode.Decompress);
                using var o = new MemoryStream(); gz.CopyTo(o); return o.ToArray();
            }
            if (raw != null && raw.Length > 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda))
            {
                using var ms = new MemoryStream(raw);
                using var zl = new ZLibStream(ms, CompressionMode.Decompress);
                using var o = new MemoryStream(); zl.CopyTo(o); return o.ToArray();
            }
            return raw;
        }

        public static byte[] ExtractMapBlock(byte[] data, int id, out int rawLen)
        {
            rawLen = 0;
            if (data == null || data.Length < 2) return null;
            int count = (data[0] << 8) | data[1];
            int pos = 2;
            for (int i = 0; i < count && pos + 8 <= data.Length; i++)
            {
                if (pos + 8 > data.Length) break;
                int mid = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                int len = (data[pos + 4] << 24) | (data[pos + 5] << 16) | (data[pos + 6] << 8) | data[pos + 7];
                pos += 8;                              // 越过 8 字节条目头(id+len)
                if (len < 0 || len > data.Length - pos) break;
                if (mid == id)
                {
                    if (len <= 4) return null;          // 至少 1 字节数据才算有效块
                    rawLen = len - 4;                   // 再丢弃每块开头的 4 字节块头（与原版 JS / 服务端一致）
                    var block = new byte[len - 4];
                    Buffer.BlockCopy(data, pos + 4, block, 0, len - 4);
                    return block;
                }
                pos += len;
            }
            return null;
        }

        // gridW/gridH = 单格像素(48/32)。Cols 由配置像素宽推算，Rows 由总格数反推，保证 Cols*Rows 与块长一致。
        public static Mir2MapData Decode(int id, byte[] block, int pixelWidth, int pixelHeight, int gridW, int gridH)
        {
            var md = new Mir2MapData
            {
                Width = pixelWidth,
                Height = pixelHeight,
            };
            int rawCells = block.Length * 2;
            // 优先用配置格（宽高换算），与服务器 OriginalMapDecoder 完全一致；数据不足以铺满才退化按数据长度估算。
            int cfgCols = pixelWidth > 0 ? (int)Math.Ceiling(pixelWidth / (double)gridW) : 0;
            int cfgRows = pixelHeight > 0 ? (int)Math.Ceiling(pixelHeight / (double)gridH) : 0;
            int cfgCells = cfgCols * cfgRows;
            int cols = cfgCols, rows = cfgRows;
            if (cfgCells <= 0 || rawCells < cfgCells)
            {
                cols = cfgCols > 0 ? cfgCols : 1;
                rows = rawCells / cols;
                md.Warn = $"地图{id} 配置格数({cfgCells})与数据块格数({rawCells})不符，已按数据长度估算网格 {cols}x{rows}";
            }
            md.Cols = cols;
            md.Rows = rows;
            int maxCells = cols * rows;
            md.CellFlags = new byte[maxCells];
            int idx = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    byte b = block[idx / 2];
                    byte nib = (idx % 2 == 0) ? (byte)((b >> 4) & 0x0F) : (byte)(b & 0x0F);   // 高 nibble 在前：偶格取高、奇格取低（与原版 JS 一致）
                    md.CellFlags[idx++] = nib;
                    if ((nib & 8) == 0) md.Walkable++;   // bit3=0 可走
                    if ((nib & 4) != 0) md.Cover++;        // bit2 遮挡
                    if ((nib & 2) != 0) md.Safe++;         // bit1 安全区
                }
            }
            return md;
        }
    }

    internal static class Mir2MapConfig
    {
        private static Dictionary<int, Mir2MapEntry> _entries;
        private static Task _loadTask;
        private static readonly object _lock = new object();

        public static async Task EnsureLoadedAsync(CancellationToken ct)
        {
            if (_entries != null) return;
            Task t;
            lock (_lock)
            {
                if (_entries != null) return;
                if (_loadTask != null) { t = _loadTask; }
                else { t = _loadTask = LoadAsync(ct); }
            }
            await t.ConfigureAwait(false);
        }

        private static async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                // 复用 ExcelTableMgr 已解析的 0config「map 表」（991 条），不再单独下载 + 扫描 6.7MB 分片。
                // 早期 JsonDocument 整份解析曾卡主线程 17.7s → 心跳超时；即便改 Utf8JsonReader 流式扫描，
                // 重复扫 6.7MB 仍是不必要的主线程开销（且会与 ExcelTableMgr 各下一份）。
                // MapTable 的 id/img/name/width/height 与 Mir2MapEntry 一一对应，直接转换即可。
                await ExcelTableMgr.InitializeAsync(ct).ConfigureAwait(false);
                var maps = ExcelTableMgr.Maps;
                if (maps == null || maps.Count == 0) return; // 未就绪则不设 _entries，保留重试机会

                var dict = new Dictionary<int, Mir2MapEntry>(maps.Count);
                foreach (var kv in maps)
                {
                    var m = kv.Value;
                    dict[kv.Key] = new Mir2MapEntry
                    {
                        Id = m.id,
                        Name = m.name ?? string.Empty,
                        Img = m.img,
                        Width = m.width,
                        Height = m.height,
                    };
                }
                _entries = dict;
            }
            catch (Exception ex)
            {
                //PrintTool.Log("[Map] 加载地图配置失败(map表): " + ex.Message);
            }
        }

        public static bool TryGetEntry(int id, out Mir2MapEntry entry)
        {
            if (_entries != null && _entries.TryGetValue(id, out entry)) return true;
            entry = null;
            return false;
        }


    }

    internal static class Mir2MapDataLoader
    {
        private static byte[] _datBytes;
        private static Task<byte[]> _datTask;
        private static readonly object _lock = new object();

        public static async Task<(Mir2MapData data, Mir2MapEntry entry)> LoadAsync(int id, CancellationToken ct)
        {
            await Mir2MapConfig.EnsureLoadedAsync(ct).ConfigureAwait(false);
            Mir2MapConfig.TryGetEntry(id, out var entry);
            int img = entry?.Img ?? id;
            int pw = entry?.Width ?? 0;
            int ph = entry?.Height ?? 0;

            byte[] dat = await EnsureDatAsync(ct).ConfigureAwait(false);
            if (dat == null) return (null, entry);

            int rawLen;
            byte[] block = Mir2MapDecoder.ExtractMapBlock(dat, id, out rawLen);
            if (block == null || rawLen == 0) return (null, entry);

            var md = Mir2MapDecoder.Decode(id, block, pw, ph, 48, 32);
            return (md, entry);
        }

        private static async Task<byte[]> EnsureDatAsync(CancellationToken ct)
        {
            if (_datBytes != null) return _datBytes;
            if (_datTask != null) { var r = await _datTask.ConfigureAwait(false); _datBytes = r; return r; }
            // 合并地图数据：服务端合并全地图碰撞数据，直接在 CDN 根提供（无 Maps/ 前缀），
            // 文件名版本号随游戏更新（如 10.25487.1），并带 ?ver= 缓存破坏参数。
            _datTask = Task.Run(async () =>
            {
                byte[] data = null;
                string[] candidates = { "map10.25487.1.dat?ver=1.0.2", "map10.25487.1.dat" };
                foreach (var p in candidates)
                {
                    try { data = await Mir2OriginResource.GetDataBytesAsync(p, ct).ConfigureAwait(false); }
                    catch { data = null; }
                    if (data != null && data.Length > 0)
                    {
                        return Mir2MapDecoder.DecompressIfNeeded(data);
                    }
                }
                return null;
            });
            var result = await _datTask.ConfigureAwait(false);
            _datBytes = result;
            return result;
        }
    }
}
