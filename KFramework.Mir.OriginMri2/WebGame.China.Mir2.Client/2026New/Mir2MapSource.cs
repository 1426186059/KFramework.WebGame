using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KFramework.MonoGame;

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

        private static async Task LoadAsync(CancellationToken ct)
        {
            try
            {
                // 0config 内含 map 表：{id:{id,img,width,height,name,...}}
                string json = await Mir2OriginResource.GetConfigShardTextAsync(0, ct).ConfigureAwait(false);
                var dict = new Dictionary<int, Mir2MapEntry>();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("map", out var mapEl) && mapEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in mapEl.EnumerateObject())
                    {
                        if (!int.TryParse(prop.Name, out int mid)) continue;
                        var obj = prop.Value;
                        var entry = new Mir2MapEntry { Id = mid };
                        if (obj.TryGetProperty("name", out var n)) entry.Name = n.GetString() ?? string.Empty;
                        entry.Img = GetInt(obj, "img", mid);
                        entry.Width = GetInt(obj, "width", 0);
                        entry.Height = GetInt(obj, "height", 0);
                        dict[mid] = entry;
                    }
                }
                _entries = dict;
            }
            catch (Exception ex)
            {
                PrintTool.Log("[Map] 加载地图配置失败(map表): " + ex.Message);
            }
        }

        public static bool TryGetEntry(int id, out Mir2MapEntry entry)
        {
            if (_entries != null && _entries.TryGetValue(id, out entry)) return true;
            entry = null;
            return false;
        }

        private static int GetInt(JsonElement obj, string key, int dflt)
        {
            if (obj.TryGetProperty(key, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int r)) return r;
            }
            return dflt;
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
                    if (data != null && data.Length > 0) return data;
                }
                return null;
            });
            var result = await _datTask.ConfigureAwait(false);
            _datBytes = result;
            return result;
        }
    }
}
