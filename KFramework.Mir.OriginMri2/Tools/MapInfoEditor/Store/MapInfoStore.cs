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

    /// <summary>NPC 视图（来自全局 NPCInfo.json，按 MapIndex 关联）。</summary>
    public class NpcRow
    {
        public JsonObject Node;
        public int Index, X, Y, Rate, Image;
        public string FileName = "", Name = "";
    }

    /// <summary>传送点视图（Movements，属于 MapInfo_Fix）。</summary>
    public class MovementRow
    {
        public JsonObject Node;
        public int DestMapIndex, SourceX, SourceY, DestX, DestY;
        public bool NeedHole, NeedMove;
    }

    public class MapInfoStore
    {
        public const string VersionRoot = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version";
        public string MapInfoPath = Path.Combine(VersionRoot, "Maps", "MapInfo_Fix.json");
        public Dictionary<int, string> NameById = new();
        // dat 与 MapInfo_Fix 同目录（Maps/），编辑器按 id 解码网格。切勿指向 ExcelConfig（那里没有 dat）。
        public string DatPath = Path.Combine(VersionRoot, "Maps", "map10.25341.1.dat");
        // NPC / 怪物 等“全局配置表”走本地 ExcelConfig（已与 Crystal/Config 同步拷贝）：
        // 服务器 Web 模式下载的即此类文件，编辑这份即可让游戏读到。
        public string MonsterInfoPath = Path.Combine(VersionRoot, "ExcelConfig", "MonsterInfo.json");
        public string NpcInfoPath = Path.Combine(VersionRoot, "ExcelConfig", "NPCInfo.json");

        public JsonNode Root;
        public JsonArray MapsArray;
        public List<MapEntry> Maps = new List<MapEntry>();
        public Dictionary<int, MapEntry> ByIndex = new Dictionary<int, MapEntry>();

        // 全局 NPC 表（NPCInfo.json）
        public JsonNode RootNpc;
        public JsonArray NpcArray;
        public Dictionary<int, JsonObject> NpcByIndex = new Dictionary<int, JsonObject>();

        public Dictionary<int, (int w, int h)> ConfigDims = new Dictionary<int, (int, int)>();
        public Dictionary<int, (string Name, bool IsBoss)> Monsters = new Dictionary<int, (string, bool)>();

        private byte[] _datBytes;

        public void Load()
        {
            Root = JsonNode.Parse(File.ReadAllText(MapInfoPath, Encoding.UTF8));
            MapsArray = (JsonArray)Root["$values"];

            // 直接从配置表(*config*.json)读取 id -> (宽, 高, 名)，替代已废弃的 MapIndex.json。
            // 既用于反推网格，也用于解析地图中文名（MapInfo_Fix 里很多是 "???"）。
            ConfigDims.Clear();
            NameById.Clear();
            string excelCfg = Path.Combine(VersionRoot, "ExcelConfig");
            if (Directory.Exists(excelCfg))
            {
                foreach (var cfg in Directory.GetFiles(excelCfg, "*config*.json"))
                {
                    try
                    {
                        var cj = JsonNode.Parse(File.ReadAllText(cfg, Encoding.UTF8));
                        if (cj["map"] is JsonObject mapObj)
                        {
                            foreach (var kv in mapObj)
                            {
                                if (!int.TryParse(kv.Key, out int id)) continue;
                                var v = kv.Value;
                                int w = v["width"]?.GetValue<int>() ?? 0;
                                int h = v["height"]?.GetValue<int>() ?? 0;
                                string nm = v["name"]?.GetValue<string>() ?? "";
                                if (w > 0 && h > 0 && !ConfigDims.ContainsKey(id)) ConfigDims[id] = (w, h);
                                if (!string.IsNullOrEmpty(nm) && !NameById.ContainsKey(id)) NameById[id] = nm;
                            }
                        }
                    }
                    catch { }
                }
            }

            Maps.Clear();
            ByIndex.Clear();
            foreach (var n in MapsArray)
            {
                if (n is not JsonObject obj) continue;
                int idx = obj["Index"]?.GetValue<int>() ?? 0;
                string rawTitle = obj["Title"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle == "???")
                    rawTitle = NameById.TryGetValue(idx, out var nn) ? nn : rawTitle;
                var e = new MapEntry
                {
                    Node = obj,
                    Index = idx,
                    FileName = obj["FileName"]?.GetValue<string>() ?? "",
                    Title = rawTitle
                };
                Maps.Add(e);
                if (!ByIndex.ContainsKey(idx)) ByIndex[idx] = e;
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

            // NPCInfo.json: 全局 NPC 表（按 MapIndex 关联地图）。服务器 Web 模式下载的即此文件。
            NpcByIndex.Clear();
            if (File.Exists(NpcInfoPath))
            {
                RootNpc = JsonNode.Parse(File.ReadAllText(NpcInfoPath, Encoding.UTF8));
                if (RootNpc["$values"] is JsonArray nv)
                {
                    NpcArray = nv;
                    foreach (var n in nv)
                    {
                        if (n is not JsonObject no) continue;
                        int idx = no["Index"]?.GetValue<int>() ?? -1;
                        if (idx >= 0 && !NpcByIndex.ContainsKey(idx)) NpcByIndex[idx] = no;
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

        /// <summary>保存全局 NPC 表（NPCInfo.json）。</summary>
        public void SaveNpc()
        {
            if (RootNpc == null) return;
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(NpcInfoPath, RootNpc.ToJsonString(opts), Encoding.UTF8);
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

        private int MaxId(JsonNode n)
        {
            int m = 0;
            if (n is JsonObject o)
            {
                if (o.TryGetPropertyValue("$id", out var idn) && idn is JsonValue jv && jv.TryGetValue<int>(out int v)) m = v;
                foreach (var kv in o) m = Math.Max(m, MaxId(kv.Value));
            }
            else if (n is JsonArray a)
            {
                foreach (var item in a) m = Math.Max(m, MaxId(item));
            }
            return m;
        }

        private int _maxIdCache = -1;
        public int NextId()
        {
            if (_maxIdCache < 0) _maxIdCache = Root != null ? MaxId(Root) : 0;
            _maxIdCache++;
            return _maxIdCache;
        }

        private int _maxNpcId = -1;
        public int NextNpcId()
        {
            if (_maxNpcId < 0) _maxNpcId = RootNpc != null ? MaxId(RootNpc) : 0;
            _maxNpcId++;
            return _maxNpcId;
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

        /// <summary>从全局 NPC 表按 MapIndex 取某张图的 NPC。</summary>
        public List<NpcRow> GetNpcs(int mapIndex)
        {
            var list = new List<NpcRow>();
            if (NpcArray == null) return list;
            foreach (JsonObject n in NpcArray)
            {
                if (n["MapIndex"]?.GetValue<int>() != mapIndex) continue;
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

        public List<MovementRow> GetMovements(MapEntry e)
        {
            var list = new List<MovementRow>();
            foreach (JsonObject mv in GetValues(e, "Movements"))
            {
                if (mv["Source"] is not JsonObject src) continue;
                if (mv["Destination"] is not JsonObject dst) continue;
                list.Add(new MovementRow
                {
                    Node = mv,
                    DestMapIndex = mv["MapIndex"]?.GetValue<int>() ?? 0,
                    SourceX = src["X"]?.GetValue<int>() ?? 0,
                    SourceY = src["Y"]?.GetValue<int>() ?? 0,
                    DestX = dst["X"]?.GetValue<int>() ?? 0,
                    DestY = dst["Y"]?.GetValue<int>() ?? 0,
                    NeedHole = mv["NeedHole"]?.GetValue<bool>() ?? false,
                    NeedMove = mv["NeedMove"]?.GetValue<bool>() ?? false
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

        public void AddMovement(MapEntry e, int destMapIndex, int sourceX, int sourceY, int destX, int destY, bool needHole, bool needMove)
        {
            var arr = GetValues(e, "Movements");
            var mv = new JsonObject
            {
                ["$id"] = JsonValue.Create(NextId()),
                ["MapIndex"] = JsonValue.Create(destMapIndex),
                ["Source"] = new JsonObject { ["X"] = JsonValue.Create(sourceX), ["Y"] = JsonValue.Create(sourceY) },
                ["Destination"] = new JsonObject { ["X"] = JsonValue.Create(destX), ["Y"] = JsonValue.Create(destY) },
                ["NeedHole"] = JsonValue.Create(needHole),
                ["NeedMove"] = JsonValue.Create(needMove),
                ["ConquestIndex"] = JsonValue.Create(0),
                ["ShowOnBigMap"] = JsonValue.Create(false),
                ["Icon"] = JsonValue.Create(0)
            };
            arr.Add(mv);
        }

        /// <summary>向全局 NPC 表新增一个 NPC（克隆现有条目为模板，保证字段完整；重置嵌套 $id 避免重复）。</summary>
        public int AddNpcGlobal(int mapIndex, string fileName, string name, int x, int y, int rate, int image)
        {
            if (NpcArray == null)
            {
                RootNpc = new JsonObject { ["$id"] = JsonValue.Create(NextNpcId()), ["$values"] = new JsonArray() };
                NpcArray = (JsonArray)RootNpc["$values"];
            }
            JsonObject template = NpcArray.Count > 0 ? (JsonObject)NpcArray[0] : null;
            int newIndex = 1;
            foreach (var kv in NpcByIndex) newIndex = Math.Max(newIndex, kv.Key + 1);
            JsonObject n = template != null ? (JsonObject)template.DeepClone() : new JsonObject();
            n["$id"] = JsonValue.Create(NextNpcId());
            n["Index"] = JsonValue.Create(newIndex);
            n["FileName"] = JsonValue.Create(fileName);
            n["Name"] = JsonValue.Create(name);
            n["MapIndex"] = JsonValue.Create(mapIndex);
            n["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) };
            n["Rate"] = JsonValue.Create(rate);
            n["Image"] = JsonValue.Create(image);
            // 重置含 $id 的嵌套对象，避免重复 $id 破坏引用一致性
            n["CollectQuestIndexes"] = new JsonObject { ["$id"] = JsonValue.Create(NextNpcId()), ["$values"] = new JsonArray() };
            n["FinishQuestIndexes"] = new JsonObject { ["$id"] = JsonValue.Create(NextNpcId()), ["$values"] = new JsonArray() };
            NpcArray.Add(n);
            NpcByIndex[newIndex] = n;
            return newIndex;
        }

        public bool UpdateNpc(int npcIndex, string fileName, string name, int x, int y, int rate, int image)
        {
            if (!NpcByIndex.TryGetValue(npcIndex, out var n)) return false;
            n["FileName"] = JsonValue.Create(fileName);
            n["Name"] = JsonValue.Create(name);
            n["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) };
            n["Rate"] = JsonValue.Create(rate);
            n["Image"] = JsonValue.Create(image);
            return true;
        }

        public bool DeleteNpc(int npcIndex)
        {
            if (NpcArray == null) return false;
            for (int i = 0; i < NpcArray.Count; i++)
            {
                if (NpcArray[i] is JsonObject o && o["Index"]?.GetValue<int>() == npcIndex)
                {
                    NpcArray.RemoveAt(i);
                    NpcByIndex.Remove(npcIndex);
                    return true;
                }
            }
            return false;
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
