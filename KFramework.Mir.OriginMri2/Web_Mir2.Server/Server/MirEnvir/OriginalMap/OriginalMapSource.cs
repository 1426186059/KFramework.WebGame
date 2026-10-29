using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Server.MirEnvir
{
    /// <summary>
    /// 原始传奇地图数据源：直接复用提取工具的逻辑。
    /// 加载本地 map10.25341.1.dat（zlib 压缩包，内含全部地图），解压后用 OriginalMapDecoder 解码。
    /// 配置表(0config*.json 的 map)提供每图像素宽高，用于反推网格 cols/rows。
    /// 这是服务器地图加载的主要数据源（见 Map.LoadCells）。
    /// </summary>
    public static class OriginalMapSource
    {
        private static byte[] _datBytes;
        private static readonly Dictionary<int, (int offset, int len)> _index = new();
        private static readonly Dictionary<int, (int width, int height, string name)> _config = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            string datPath = Settings.OriginalMapDatPath;
            if (File.Exists(datPath))
            {
                byte[] raw = File.ReadAllBytes(datPath);
                _datBytes = DecompressIfNeeded(raw);
                RebuildOffsets();
            }

            string cfgPath = Settings.OriginalMapConfigPath;
            if (File.Exists(cfgPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
                    if (doc.RootElement.TryGetProperty("map", out var mapEl))
                    {
                        foreach (var entry in mapEl.EnumerateObject())
                        {
                            if (!entry.Value.TryGetProperty("id", out var idEl)) continue;
                            int id = idEl.GetInt32();
                            int w = entry.Value.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 0;
                            int h = entry.Value.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 0;
                            string name = entry.Value.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
                            _config[id] = (w, h, name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageQueue.Instance.Enqueue("原始地图配置解析失败: " + ex.Message);
                }
            }
        }

        private static void RebuildOffsets()
        {
            _index.Clear();
            int count = (_datBytes[0] << 8) | _datBytes[1];
            int pos = 2;
            for (int i = 0; i < count && pos < _datBytes.Length; i++)
            {
                if (pos + 8 > _datBytes.Length) break;
                int id = (_datBytes[pos] << 24) | (_datBytes[pos + 1] << 16) | (_datBytes[pos + 2] << 8) | _datBytes[pos + 3];
                int len = (_datBytes[pos + 4] << 24) | (_datBytes[pos + 5] << 16) | (_datBytes[pos + 6] << 8) | _datBytes[pos + 7];
                pos += 8;
                if (len < 0 || len > _datBytes.Length - pos) break;
                if (!_index.ContainsKey(id)) _index[id] = (pos, len);
                pos += len;
            }
        }

        private static byte[] DecompressIfNeeded(byte[] raw)
        {
            if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b)
            {
                using var ms = new MemoryStream(raw);
                using var gz = new GZipStream(ms, CompressionMode.Decompress);
                using var o = new MemoryStream(); gz.CopyTo(o); return o.ToArray();
            }
            if (raw.Length > 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda))
            {
                using var ms = new MemoryStream(raw);
                using var zl = new ZLibStream(ms, CompressionMode.Decompress);
                using var o = new MemoryStream(); zl.CopyTo(o); return o.ToArray();
            }
            return raw;
        }

        public static bool Contains(int id) { Initialize(); return _index.ContainsKey(id); }

        public static bool TryGetMap(int id, out OriginalMapData data)
        {
            data = null;
            Initialize();
            if (_datBytes == null || !_index.TryGetValue(id, out _)) return false;

            byte[] block = OriginalMapDecoder.ExtractMapBlock(_datBytes, id, out _);
            if (block == null) return false;

            _config.TryGetValue(id, out var cfg);
            data = OriginalMapDecoder.Decode(id, block, cfg.width, cfg.height, cfg.name, 48, 32);
            return true;
        }

        public static bool TryGetMapByName(string name, out OriginalMapData data)
        {
            data = null;
            Initialize();
            if (_datBytes == null) return false;
            foreach (var kv in _config)
            {
                if (kv.Value.name == name)
                    return TryGetMap(kv.Key, out data);
            }
            return false;
        }
    }
}
