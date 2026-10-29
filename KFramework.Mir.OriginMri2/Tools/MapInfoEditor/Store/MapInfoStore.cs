using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MapInfoEditor
{
    /// <summary>单张地图在 JSON 树中的轻量包装（Node 即 MapInfo_Fix.json 里的那条 $values 元素）。</summary>
    public class MapEntry
    {
        public JsonObject Node;
        public int Index;
        public string FileName = "";
        public string Title = "";
    }

    /// <summary>安全区视图（直接读写底层 JsonObject）。</summary>
    public class SzRow
    {
        public JsonObject Node;
        public int X, Y, Size;
        public bool Start;
    }

    /// <summary>怪物刷新视图。</summary>
    public class RespawnRow
    {
        public JsonObject Node;
        public int MonsterIndex, X, Y, Count, Spread, Delay, Direction;
    }

    /// <summary>NPC 视图。</summary>
    public class NpcRow
    {
        public JsonObject Node;
        public int Index, X, Y, Rate, Image;
        public string FileName = "", Name = "";
    }

    public class MapInfoStore
    {
        public const string VersionRoot = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version";
        public string MapInfoPath = Path.Combine(VersionRoot, "Maps", "MapInfo_Fix.json");
        public string MapIndexPath = Path.Combine(VersionRoot, "Maps", "MapIndex.json");
        public string DatPath = Path.Combine(VersionRoot, "ExcelConfig", "map10.25341.1.dat");
        public string MonsterInfoPath = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\MirDBToJson\ExportedConfig\MonsterInfo.json";

        public JsonNode Root;
        public JsonArray MapsArray;
        public List<MapEntry> Maps = new List<MapEntry>();
        public Dictionary<int, MapEntry> ByIndex = new Dictionary<int, MapEntry>();

        public Dictionary<int, (int w, int h)> ConfigDims = new Dictionary<int, (int, int)>();
        public Dictionary<int, (string Name, bool IsBoss)> Monsters = new Dictionary<int, (string, bool)>();

        private byte[] _datBytes;

        public void Load()
        {
            Root = JsonNode.Parse(File.ReadAllText(MapInfoPath, Encoding.UTF8));
            MapsArray = (JsonArray)Root["$values"];
            Maps.Clear();
            ByIndex.Clear();
            foreach (var n in MapsArray)
            {
                if (n is not JsonObject obj) continue;
                int idx = obj["Index"]?.GetValue<int>() ?? 0;
                var e = new MapEntry
                {
                    Node = obj,
                    Index = idx,
                    FileName = obj["FileName"]?.GetValue<string>() ?? "",
                    Title = obj["Title"]?.GetValue<string>() ?? ""
                };
                Maps.Add(e);
                if (!ByIndex.ContainsKey(idx)) ByIndex[idx] = e;
            }

            // MapIndex.json: 取每张图的像素宽高（用于反推网格）
            if (File.Exists(MapIndexPath))
            {
                var idxJson = JsonNode.Parse(File.ReadAllText(MapIndexPath, Encoding.UTF8));
                if (idxJson["maps"] is JsonArray marr)
                {
                    foreach (var m in marr)
                    {
                        int id = m["id"]?.GetValue<int>() ?? -1;
                        if (id < 0) continue;
                        int w = m["width"]?.GetValue<int>() ?? 0;
                        int h = m["height"]?.GetValue<int>() ?? 0;
                        ConfigDims[id] = (w, h);
                    }
                }
            }

            // MonsterInfo.json: 建立 怪物索引 -> (名称, 是否Boss) 查表（用于预览着色与名称显示）
            Monsters.Clear();
            if (File.Exists(MonsterInfoPath))
            {
                var mj = JsonNode.Parse(File.ReadAllText(MonsterInfoPath, Encoding.UTF8));
                if (mj["$values"] is JsonArray mv)
                {
                    foreach (var m in mv)
                    {
                        if (m is not JsonObject mo) continue;
                        int mi = mo["Index"]?.GetValue<int>() ?? -1;
                        if (mi < 0) continue;
                        string name = mo["Name"]?.GetValue<string>() ?? "";
                        bool boss = mo["IsBoss"]?.GetValue<bool>() ?? false;
                        Monsters[mi] = (name, boss);
                    }
                }
            }

            _datBytes = File.Exists(DatPath) ? DecompressIfNeeded(File.ReadAllBytes(DatPath)) : null;
        }

        /// <summary>与服务器 OriginalMapSource 一致：dat 可能是 zlib/gzip 压缩包，按魔术字头解压后再解析索引。</summary>
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

        public void Save()
        {
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(MapInfoPath, Root.ToJsonString(opts), Encoding.UTF8);
        }

        public MapEntry GetMap(int index) => ByIndex.TryGetValue(index, out var e) ? e : null;

        // Boss 判定：优先 IsBoss 标志；当前导出数据该标志多为 false，故回退到经典 Boss 名关键词（仅用于预览着色，不写回数据）。
        private static readonly HashSet<string> BossKeywords = new HashSet<string>
        {
            "教主", "魔王", "Boss", "暗之", "触龙神", "牛魔王", "魔龙", "虹魔教主",
            "沃玛教主", "祖玛教主", "赤月恶魔", "双头", "暗殿", "尸王", "白猪王", "虹魔"
        };

        public bool IsBoss(int monsterIndex, string name)
        {
            if (Monsters.TryGetValue(monsterIndex, out var m) && m.IsBoss) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return BossKeywords.Any(k => name.Contains(k));
        }

        /// <summary>解码某张地图的单元格（可走/不可走）。无数据块返回 null。</summary>
        public OriginalMapData DecodeMap(int id)
        {
            if (_datBytes == null) return null;
            ConfigDims.TryGetValue(id, out var dim);
            int rawLen;
            var block = OriginalMapDecoder.ExtractMapBlock(_datBytes, id, out rawLen);
            if (block == null) return null;
            string name = ByIndex.TryGetValue(id, out var e) ? e.Title : "";
            return OriginalMapDecoder.Decode(id, block, dim.w, dim.h, name, 48, 32);
        }

        // ---------- $values 信封获取 / 新建 ----------
        public JsonArray GetValues(MapEntry e, string field)
        {
            if (e.Node[field] is JsonObject env && env["$values"] is JsonArray arr) return arr;
            var newEnv = new JsonObject
            {
                ["$id"] = JsonValue.Create(NextId()),
                ["$values"] = new JsonArray()
            };
            e.Node[field] = newEnv;
            return (JsonArray)newEnv["$values"];
        }

        private int _maxIdCache = -1;
        public int NextId()
        {
            if (_maxIdCache < 0)
            {
                _maxIdCache = 1;
                ScanIds(Root);
            }
            _maxIdCache++;
            return _maxIdCache;
        }

        private void ScanIds(JsonNode n)
        {
            if (n is JsonObject o)
            {
                if (o.TryGetPropertyValue("$id", out var idn) && idn is JsonValue jv)
                {
                    if (jv.TryGetValue<int>(out int v)) _maxIdCache = Math.Max(_maxIdCache, v);
                }
                foreach (var kv in o) ScanIds(kv.Value);
            }
            else if (n is JsonArray a)
            {
                foreach (var item in a) ScanIds(item);
            }
        }

        // ---------- 读取视图 ----------
        public List<SzRow> GetSafeZones(MapEntry e)
        {
            var list = new List<SzRow>();
            foreach (JsonObject z in GetValues(e, "SafeZones"))
            {
                if (z["Location"] is not JsonObject loc) continue;
                list.Add(new SzRow
                {
                    Node = z,
                    X = loc["X"]?.GetValue<int>() ?? 0,
                    Y = loc["Y"]?.GetValue<int>() ?? 0,
                    Size = z["Size"]?.GetValue<int>() ?? 0,
                    Start = z["StartPoint"]?.GetValue<bool>() ?? false
                });
            }
            return list;
        }

        public List<RespawnRow> GetRespawns(MapEntry e)
        {
            var list = new List<RespawnRow>();
            foreach (JsonObject r in GetValues(e, "Respawns"))
            {
                if (r["Location"] is not JsonObject loc) continue;
                list.Add(new RespawnRow
                {
                    Node = r,
                    MonsterIndex = r["MonsterIndex"]?.GetValue<int>() ?? 0,
                    X = loc["X"]?.GetValue<int>() ?? 0,
                    Y = loc["Y"]?.GetValue<int>() ?? 0,
                    Count = r["Count"]?.GetValue<int>() ?? 0,
                    Spread = r["Spread"]?.GetValue<int>() ?? 0,
                    Delay = r["Delay"]?.GetValue<int>() ?? 0,
                    Direction = r["Direction"]?.GetValue<int>() ?? 0
                });
            }
            return list;
        }

        public List<NpcRow> GetNpcs(MapEntry e)
        {
            var list = new List<NpcRow>();
            foreach (JsonObject n in GetValues(e, "NPCs"))
            {
                if (n["Location"] is not JsonObject loc) continue;
                list.Add(new NpcRow
                {
                    Node = n,
                    Index = n["Index"]?.GetValue<int>() ?? 0,
                    FileName = n["FileName"]?.GetValue<string>() ?? "",
                    Name = n["Name"]?.GetValue<string>() ?? "",
                    X = loc["X"]?.GetValue<int>() ?? 0,
                    Y = loc["Y"]?.GetValue<int>() ?? 0,
                    Rate = n["Rate"]?.GetValue<int>() ?? 0,
                    Image = n["Image"]?.GetValue<int>() ?? 0
                });
            }
            return list;
        }

        // ---------- 新增 ----------
        public void AddSafeZone(MapEntry e, int x, int y, int size, bool start)
        {
            var arr = GetValues(e, "SafeZones");
            var z = new JsonObject
            {
                ["$id"] = JsonValue.Create(NextId()),
                ["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) },
                ["Size"] = JsonValue.Create(size),
                ["StartPoint"] = JsonValue.Create(start),
                ["Info"] = new JsonObject { ["$ref"] = JsonValue.Create(e.Node["$id"]?.GetValue<string>() ?? "") }
            };
            arr.Add(z);
        }

        public void AddRespawn(MapEntry e, int monsterIndex, int x, int y, int count, int spread, int delay, int direction)
        {
            var arr = GetValues(e, "Respawns");
            var r = new JsonObject
            {
                ["$id"] = JsonValue.Create(NextId()),
                ["MonsterIndex"] = JsonValue.Create(monsterIndex),
                ["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) },
                ["Count"] = JsonValue.Create(count),
                ["Spread"] = JsonValue.Create(spread),
                ["Delay"] = JsonValue.Create(delay),
                ["RandomDelay"] = JsonValue.Create(0),
                ["Direction"] = JsonValue.Create(direction),
                ["RoutePath"] = JsonValue.Create(""),
                ["RespawnIndex"] = JsonValue.Create(NextId()),
                ["SaveRespawnTime"] = JsonValue.Create(false),
                ["RespawnTicks"] = JsonValue.Create(0)
            };
            arr.Add(r);
        }

        public void AddNpc(MapEntry e, string fileName, string name, int x, int y, int rate, int image)
        {
            var arr = GetValues(e, "NPCs");
            var n = new JsonObject
            {
                ["Index"] = JsonValue.Create(NextId()),
                ["FileName"] = JsonValue.Create(fileName),
                ["Name"] = JsonValue.Create(name),
                ["MapIndex"] = JsonValue.Create(e.Index),
                ["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) },
                ["Rate"] = JsonValue.Create(rate),
                ["Image"] = JsonValue.Create(image)
            };
            arr.Add(n);
        }

        // ---------- 删除 ----------
        public void RemoveFromArray(JsonArray arr, JsonObject item)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                if (ReferenceEquals(arr[i], item))
                {
                    arr.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
