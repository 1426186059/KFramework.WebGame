using System.IO;
using System.IO.Compression;
using System.Text;
using KFramework.MonoGame;
using System.Text.Json;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇地图数据客户端加载器。
    /// 服务端合并 map10.25341.1.dat 每格仅 4-bit 碰撞标志位(墙/遮挡/安全区)，不含瓦片图像；
    /// 地图视觉由 MapControl 从 CDN 的 assets/resource/zipmap/{img}/{index}.jpg 真实瓦片拼出。
    /// 解码与服务端 OriginalMapSource/OriginalMapDecoder 及提取工具 MapDecoder 完全一致。
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
        public int Cols;
        public int Rows;
        public byte[] CellFlags;   // 每格 4-bit 标志（半字节），长度 = Cols*Rows
        public int Walkable;
        public int Cover;
        public int Safe;
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

        public static List<(int Id, int Len)> ParseIndex(byte[] data)
        {
            var list = new List<(int, int)>();
            if (data == null || data.Length < 2) return list;
            int count = (data[0] << 8) | data[1];
            int pos = 2;
            for (int i = 0; i < count && pos + 8 <= data.Length; i++)
            {
                int id = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                int len = (data[pos + 4] << 24) | (data[pos + 5] << 16) | (data[pos + 6] << 8) | data[pos + 7];
                pos += 8;
                if (len < 0 || len > data.Length - pos) break;
                list.Add((id, len));
                pos += len;
            }
            return list;
        }

        public static byte[] ExtractMapBlock(byte[] data, int id, out int rawLen)
        {
            rawLen = 0;
            var idx = ParseIndex(data);
            foreach (var (Id, Len) in idx)
            {
                if (Id != id) continue;
                int pos = 2;
                foreach (var (Id2, Len2) in idx)
                {
                    if (Id2 == id) break;
                    pos += 8 + Len2;
                }
                rawLen = Len;
                var block = new byte[Len];
                Buffer.BlockCopy(data, pos, block, 0, Len);
                return block;
            }
            return null;
        }

        // gridW/gridH = 单格像素(48/32)。Cols 由配置像素宽推算，Rows 由总格数反推，保证 Cols*Rows 与块长一致。
        public static Mir2MapData Decode(int id, byte[] block, int pixelWidth, int pixelHeight, int gridW, int gridH)
        {
            var md = new Mir2MapData();
            int totalCells = block.Length * 2;
            int cols = (int)Math.Ceiling(pixelWidth / (double)gridW);
            if (cols <= 0) cols = 1;
            int rows = totalCells / cols;
            md.Cols = cols;
            md.Rows = rows;
            md.CellFlags = new byte[totalCells];
            int idx = 0;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    byte b = block[idx / 2];
                    byte nib = (idx % 2 == 0) ? (byte)(b & 0x0F) : (byte)((b >> 4) & 0x0F);
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

        // 属性名 UTF-8 字面量：Utf8JsonReader.ValueTextEquals 直接比字节，避免每个属性都做一次 string→UTF8 编码。
        private static readonly byte[] MapProp = Encoding.UTF8.GetBytes("map");
        private static readonly byte[] NameProp = Encoding.UTF8.GetBytes("name");
        private static readonly byte[] ImgProp = Encoding.UTF8.GetBytes("img");
        private static readonly byte[] WidthProp = Encoding.UTF8.GetBytes("width");
        private static readonly byte[] HeightProp = Encoding.UTF8.GetBytes("height");

        private static async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                // 0config 内含 map 表：{id:{id,img,width,height,name,...}}
                //
                // 【性能】早先用 JsonDocument.Parse 解析整份分片：0config 有 6.7MB / 175 张表，
                // Parse 会为全部内容建完整 DOM（几百万个 JsonElement 元数据），实测耗时 15.76 秒，
                // 主线程一帧卡死 17.7 秒 → 服务端心跳超时（10s）直接断开，表现为「第一次进 GameScene 就掉线」。
                // 而我们只要其中一张 map 表（991 条）。改为 Utf8JsonReader 流式前向扫描：
                // 非目标属性直接 Skip，零 DOM、零中间字符串，代价仅为线性扫一遍字节。
                byte[] json = await Mir2OriginResource.GetConfigShardBytesAsync(0, ct).ConfigureAwait(false);

                var cfgT0 = DateTime.Now;
                var dict = new Dictionary<int, Mir2MapEntry>();
                ParseMapTable(json, dict);
                _entries = dict;
                CMain.LogStall($"[Map] 0config map表 流式解析（{json.Length} 字节，{dict.Count} 张图）", (DateTime.Now - cfgT0).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                PrintTool.Log("[Map] 加载地图配置失败(map表): " + ex.Message);
            }
        }

        /// <summary>只抽取根对象的 map 表，其余全部 Skip（不建 DOM）。</summary>
        private static void ParseMapTable(byte[] json, Dictionary<int, Mir2MapEntry> dict)
        {
            var reader = new Utf8JsonReader(json, new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return;      // 根对象结束，没有 map 表
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                if (!reader.ValueTextEquals(MapProp)) { reader.Skip(); continue; }

                ParseMapEntries(ref reader, dict);
                return;
            }
        }

        /// <summary>reader 已定位在 "map" 属性名上：读其值对象 { "id": {...}, ... }。</summary>
        private static void ParseMapEntries(ref Utf8JsonReader reader, Dictionary<int, Mir2MapEntry> dict)
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return;
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                // 属性名即地图 id（字符串形式）
                if (!int.TryParse(reader.GetString(), out int id)) { reader.Skip(); continue; }

                var entry = new Mir2MapEntry { Id = id };
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) continue;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals(NameProp)) entry.Name = ReadStringValue(ref reader);
                    else if (reader.ValueTextEquals(ImgProp)) entry.Img = ReadIntValue(ref reader, id);
                    else if (reader.ValueTextEquals(WidthProp)) entry.Width = ReadIntValue(ref reader, 0);
                    else if (reader.ValueTextEquals(HeightProp)) entry.Height = ReadIntValue(ref reader, 0);
                    else reader.Skip();
                }

                dict[id] = entry;
            }
        }

        /// <summary>把当前属性的值读成字符串（数字也按字符串收，保险）。</summary>
        private static string ReadStringValue(ref Utf8JsonReader reader)
        {
            if (!reader.Read()) return string.Empty;
            if (reader.TokenType == JsonTokenType.String) return reader.GetString() ?? string.Empty;
            if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt32(out int n) ? n.ToString() : string.Empty;
            reader.Skip();
            return string.Empty;
        }

        /// <summary>把当前属性的值读成 int（配置里数字可能是 JSON number 也可能是字符串）。</summary>
        private static int ReadIntValue(ref Utf8JsonReader reader, int dflt)
        {
            if (!reader.Read()) return dflt;
            if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt32(out int v) ? v : dflt;
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return int.TryParse(s, out int r) ? r : dflt;
            }
            reader.Skip();
            return dflt;
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
            GameProfiler.TestStart();
            await Mir2MapConfig.EnsureLoadedAsync(ct).ConfigureAwait(false);
            Mir2MapConfig.TryGetEntry(id, out var entry);
            int img = entry?.Img ?? id;
            int pw = entry?.Width ?? 0;
            int ph = entry?.Height ?? 0;

            byte[] dat = await EnsureDatAsync(ct).ConfigureAwait(false);
            if (dat == null) { GameProfiler.TestFinishAndLog($"[Map] LoadAsync(地图{id}) dat为空"); return (null, entry); }

            int rawLen;
            GameProfiler.TestStart();
            byte[] block = Mir2MapDecoder.ExtractMapBlock(dat, id, out rawLen);
            GameProfiler.TestFinishAndLog($"[Map] ExtractMapBlock block={block?.Length ?? 0}字节");
            if (block == null || rawLen == 0) { GameProfiler.TestFinishAndLog($"[Map] LoadAsync(地图{id}) block为空"); return (null, entry); }

            GameProfiler.TestStart();
            var md = Mir2MapDecoder.Decode(id, block, pw, ph, 48, 32);
            GameProfiler.TestFinishAndLog($"[Map] Decode cols={md.Cols} rows={md.Rows} 总格={md.Cols * md.Rows}");
            GameProfiler.TestFinishAndLog($"[Map] LoadAsync(地图{id}) 完成");
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
                GameProfiler.TestStart();
                byte[] data = null;
                string[] candidates = { "map10.25487.1.dat?ver=1.0.2", "map10.25487.1.dat" };
                foreach (var p in candidates)
                {
                    try { data = await Mir2OriginResource.GetDataBytesAsync(p, ct).ConfigureAwait(false); }
                    catch { data = null; }
                    if (data != null && data.Length > 0)
                    {
                        var d = Mir2MapDecoder.DecompressIfNeeded(data);
                        GameProfiler.TestFinishAndLog($"[Map] dat 下载+解压 原始{data.Length}→解压{d.Length}字节");
                        return d;
                    }
                }
                GameProfiler.TestFinishAndLog($"[Map] dat 下载+解压 未取到数据");
                return null;
            });
            var result = await _datTask.ConfigureAwait(false);
            _datBytes = result;
            return result;
        }
    }
}
