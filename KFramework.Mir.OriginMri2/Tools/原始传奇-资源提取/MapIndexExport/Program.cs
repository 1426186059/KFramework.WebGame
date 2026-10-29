using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// 简单工具：
/// 1) 复用提取工具 Mir2ReverseWeb 的索引解析逻辑（与 MapDecoder.ParseIndex 逐字节一致），
///    从原始传奇 .dat 取出全部地图索引 id，并和 config*.json 的 map 表（含中文名/像素宽高）按 id 匹配，
///    导出 MapIndex.json 放到服务器 Maps 目录下，供制作 MapInfo 时参考。
/// 2) 生成 MapInfo_Fix.json：以“新 dat 真实 id”为权威源重建 MapInfo（882 张全可加载），
///    并把原版 MapInfo（MirDBToJson 导出）里能按 id 对上的条目玩法元数据带过来（中英对照）。
///    中文以 \u 转义写出，规避编码乱码。
/// </summary>
class Program
{
    static void Main()
    {
        string baseDir = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2";
        string mapsDir = Path.Combine(baseDir, @"Web_Mir2.Server.Version\Maps");
        string refDir = Path.Combine(baseDir, @"Tools\原始传奇-资源提取\原始传奇-资源参考");
        string datPath = Path.Combine(mapsDir, "map10.25341.1.dat");

        if (!File.Exists(datPath))
        {
            Console.WriteLine("找不到 dat: " + datPath);
            return;
        }

        byte[] raw = File.ReadAllBytes(datPath);
        byte[] dec = DecompressIfNeeded(raw);
        var index = ParseIndex(dec);
        Console.WriteLine($"dat 解压后字节: {dec.Length}, 索引条目数(按 count): {index.Count}");

        // 聚合所有 config 的 map 表：id -> (name,width,height)
        var configMap = new Dictionary<int, (string name, int width, int height)>();
        var cfgDirs = new[] { mapsDir, refDir };
        foreach (var dir in cfgDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var cfg in Directory.GetFiles(dir, "*config*.json"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(cfg, Encoding.UTF8));
                    if (doc.RootElement.TryGetProperty("map", out var mapEl))
                    {
                        foreach (var entry in mapEl.EnumerateObject())
                        {
                            if (!entry.Value.TryGetProperty("id", out var idEl)) continue;
                            int id = idEl.GetInt32();
                            string name = entry.Value.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "";
                            int w = entry.Value.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 0;
                            int h = entry.Value.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 0;
                            configMap[id] = (name, w, h);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析配置失败 {cfg}: {ex.Message}");
                }
            }
        }
        Console.WriteLine($"config 聚合地图数: {configMap.Count}");

        var matched = new HashSet<int>();
        var maps = new List<Dictionary<string, object>>();
        foreach (var e in index)
        {
            configMap.TryGetValue(e.Id, out var cfg);
            matched.Add(e.Id);
            maps.Add(new Dictionary<string, object>
            {
                ["id"] = e.Id,
                ["name"] = cfg.name,
                ["width"] = cfg.width,
                ["height"] = cfg.height,
                ["kb"] = e.Kb,
                ["inConfig"] = cfg.name != ""
            });
        }

        var onlyConfig = new List<int>();
        foreach (var k in configMap.Keys)
            if (!matched.Contains(k)) onlyConfig.Add(k);

        var outObj = new Dictionary<string, object>
        {
            ["datIndexCount"] = index.Count,
            ["configCount"] = configMap.Count,
            ["maps"] = maps,
            ["onlyInConfig"] = onlyConfig
        };

        string outPath = Path.Combine(mapsDir, "MapIndex.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(outObj, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"已导出: {outPath}  (dat匹配 {maps.Count} 张, config独有 {onlyConfig.Count} 张)");

        GenerateMapInfoFix(baseDir, mapsDir, index, configMap);
    }

    /// <summary>以真实 dat id 为权威源生成 MapInfo_Fix.json，并对照原版 MapInfo 带回可匹配的玩法元数据。</summary>
    static void GenerateMapInfoFix(string baseDir, string mapsDir, List<(int Id, int Len, double Kb)> index, Dictionary<int, (string name, int width, int height)> configMap)
    {
        // 原版 MapInfo（MirDBToJson 导出）：FileName(旧编号/英文名) -> 条目，按整数 id 建立对照
        var origById = new Dictionary<int, JsonElement>();
        string origPath = Path.Combine(baseDir, @"Tools\MirDBToJson\ExportedConfig\MapInfo.json");
        if (File.Exists(origPath))
        {
            using var od = JsonDocument.Parse(File.ReadAllText(origPath, Encoding.UTF8));
            if (od.RootElement.TryGetProperty("$values", out var arr))
            {
                foreach (var e in arr.EnumerateArray())
                {
                    if (e.TryGetProperty("FileName", out var fn) && fn.ValueKind == JsonValueKind.String)
                    {
                        if (int.TryParse(fn.GetString(), out int fid)) origById[fid] = e.Clone();
                    }
                }
            }
            Console.WriteLine($"原版 MapInfo 按整数 id 可对照条目: {origById.Count}");
        }

        var entries = new List<Dictionary<string, object>>();
        int idCounter = 2; // $id 从 2 起，避免与根 $id=1 冲突
        int overlayCount = 0;

        foreach (var it in index)
        {
            int mapId = it.Id;
            configMap.TryGetValue(mapId, out var cfg);
            string name = cfg.name ?? "";
            string eid = idCounter++.ToString();

            var entry = new Dictionary<string, object>
            {
                ["$id"] = eid,
                ["Index"] = mapId,
                ["FileName"] = mapId.ToString(),
                ["Title"] = name,
                ["MiniMap"] = mapId <= 65535 ? mapId : 0,
                ["BigMap"] = mapId <= 65535 ? mapId : 0,
                ["Music"] = 0,
                ["Light"] = 0,
                ["MapDarkLight"] = 0,
                ["MineIndex"] = 0,
                ["GTIndex"] = 0,
                ["RequiredGroupSize"] = 0,
                ["FireWallCount"] = 0,
                ["NoReconnectMap"] = "",
                ["FireDamage"] = 0,
                ["LightningDamage"] = 0,
            };

            // 中英对照：原版里能按 id 对上的，带回其玩法元数据（FileName/Title/Index/SafeZones 用我们重建的）
            if (origById.TryGetValue(mapId, out var oe))
            {
                OverlayScalars(entry, oe);
                overlayCount++;
            }

            // 安全区：在地图中心格放一个起始点（格数 = 像素宽高 / (48*32)）
            int cols = cfg.width > 0 ? cfg.width / 48 : 0;
            int rows = cfg.height > 0 ? cfg.height / 32 : 0;
            int cx = cols > 0 ? cols / 2 : 100;
            int cy = rows > 0 ? rows / 2 : 100;
            string szId = idCounter++.ToString();
            string zId = idCounter++.ToString();
            var zone = new Dictionary<string, object>
            {
                ["$id"] = zId,
                ["Location"] = new Dictionary<string, object> { ["X"] = cx, ["Y"] = cy },
                ["Size"] = 10,
                ["StartPoint"] = true,
                ["Info"] = new Dictionary<string, object> { ["$ref"] = eid },
            };
            entry["SafeZones"] = new Dictionary<string, object> { ["$id"] = szId, ["$values"] = new[] { zone } };

            entries.Add(entry);
        }

        var root = new Dictionary<string, object> { ["$id"] = "1", ["$values"] = entries };

        // 普通序列化即可：中文会被 \u 转义（ASCII 安全），手写 $id/$values/$ref 信封与服务器读取格式一致
        string fixPath = Path.Combine(mapsDir, "MapInfo_Fix.json");
        File.WriteAllText(fixPath, JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"已生成: {fixPath}  (共 {entries.Count} 张, 其中对照原版带回元数据 {overlayCount} 张)");
    }

    /// <summary>把原版条目里的标量玩法字段拷到新条目（保留新条目的 FileName/Title/Index/SafeZones）。</summary>
    static void OverlayScalars(Dictionary<string, object> dst, JsonElement src)
    {
        foreach (var p in src.EnumerateObject())
        {
            if (p.Name == "FileName" || p.Name == "Title" || p.Name == "Index" || p.Name == "SafeZones" || p.Name == "$id")
                continue;
            switch (p.Value.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    dst[p.Name] = p.Value.GetBoolean();
                    break;
                case JsonValueKind.Number:
                    if (p.Value.TryGetInt32(out int iv)) dst[p.Name] = iv;
                    else if (p.Value.TryGetDouble(out double dv)) dst[p.Name] = dv;
                    break;
                case JsonValueKind.String:
                    dst[p.Name] = p.Value.GetString() ?? "";
                    break;
            }
        }
    }

    /// <summary>与 Mir2ReverseWeb MapDecoder.ParseIndex 逐字节一致的索引解析</summary>
    static List<(int Id, int Len, double Kb)> ParseIndex(byte[] data)
    {
        var list = new List<(int, int, double)>();
        if (data == null || data.Length < 2) return list;
        int count = (data[0] << 8) | data[1];
        int pos = 2;
        for (int i = 0; i < count && pos < data.Length; i++)
        {
            if (pos + 8 > data.Length) break;
            int id = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            int len = (data[pos + 4] << 24) | (data[pos + 5] << 16) | (data[pos + 6] << 8) | data[pos + 7];
            pos += 8;
            if (len < 0 || len > data.Length - pos) break;
            list.Add((id, len, Math.Round(len / 1024.0, 2)));
            pos += len;
        }
        return list;
    }

    static byte[] DecompressIfNeeded(byte[] raw)
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
}
