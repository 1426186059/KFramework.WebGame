using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        public int X, Y, Size, SizeY;
        public float Rotation;
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
        public string FileName = "", Name = "", NameUI = "";
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
        // 文件名带版本号（如 map10.25487.1.dat），会随资源更新漂移，故不再写死：在 Maps 目录动态找第一个 .dat，
        // 找不到再回退到历史默认名，避免“文件不存在 → _datBytes 为 null → 全部地图预览不显示”。
        public string DatPath
        {
            get
            {
                var mapsDir = Path.Combine(VersionRoot, "Maps");
                var files = Directory.Exists(mapsDir)
                    ? Directory.GetFiles(mapsDir, "*.dat")
                    : Array.Empty<string>();
                return files.Length > 0 ? files[0] : Path.Combine(mapsDir, "map10.25341.1.dat");
            }
        }
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

        // 兼容信封格式({"$values":[...]})与裸数组 [...]（MapInfo_Fix / NPCInfo / MonsterInfo 均可能是任一种）。
        private static JsonArray LoadArray(JsonNode root)
        {
            if (root is JsonArray arr) return arr;
            if (root is JsonObject obj && obj["$values"] is JsonArray vals) return vals;
            return new JsonArray();
        }

        public void Load()
        {
            Root = JsonNode.Parse(File.ReadAllText(MapInfoPath, Encoding.UTF8));
            // 兼容旧信封格式({"$values":[...]})与新格式(裸数组 [...])
            MapsArray = Root is JsonArray rootArr ? rootArr : (Root["$values"] as JsonArray);

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
                                if (v is not JsonObject vo) continue;
                                int w = vo["width"]?.GetValue<int>() ?? 0;
                                int h = vo["height"]?.GetValue<int>() ?? 0;
                                string nm = vo["name"]?.GetValue<string>() ?? "";
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
                if (LoadArray(mj) is JsonArray mv)
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
                if (LoadArray(RootNpc) is JsonArray nv)
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
            // 写入前归一化：剥掉 $id/$ref、拆掉 $values 信封，输出干净 JSON（不再有 $ 元数据）。
            JsonNode clean = NormalizeNode((JsonNode)Root.DeepClone());
            File.WriteAllText(MapInfoPath, clean.ToJsonString(opts), Encoding.UTF8);
        }

        /// <summary>递归：删除 $id/$ref，并把 {"$values":[...]} 拆成数组（与服务端归一化逻辑一致）。</summary>
        private static JsonNode NormalizeNode(JsonNode node)
        {
            // 重建新树（不就地改），避免把已挂父节点的节点重复挂到新父节点抛出 "already has a parent"。
            if (node is JsonObject obj)
            {
                if (obj["$values"] is JsonArray vals) return NormalizeNode(vals);
                var result = new JsonObject();
                foreach (var p in obj)
                {
                    if (p.Key == "$id" || p.Key == "$ref") continue;
                    result[p.Key] = NormalizeNode(p.Value);
                }
                return result;
            }
            if (node is JsonArray arr)
            {
                var result = new JsonArray();
                foreach (var item in arr) result.Add(NormalizeNode(item));
                return result;
            }
            return node.DeepClone();
        }

        /// <summary>保存全局 NPC 表（NPCInfo.json）。同样归一化为干净 JSON（兼容旧信封，去掉 $id/$ref/$values）。</summary>
        public void SaveNpc()
        {
            if (RootNpc == null) return;
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            JsonNode clean = NormalizeNode((JsonNode)RootNpc.DeepClone());
            File.WriteAllText(NpcInfoPath, clean.ToJsonString(opts), Encoding.UTF8);
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

        // ---------- 数组获取 / 新建（直接数组，不再套 $values 信封） ----------
        public JsonArray GetValues(MapEntry e, string field)
        {
            if (e.Node[field] is JsonArray arr) return arr;
            if (e.Node[field] is JsonObject env && env["$values"] is JsonArray a) return a;
            var newArr = new JsonArray();
            e.Node[field] = newArr;
            return newArr;
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
                    SizeY = z["SizeY"]?.GetValue<int>() ?? 0,
                    Rotation = z["Rotation"]?.GetValue<float>() ?? 0f,
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
                    NameUI = n["NameUI"]?.GetValue<string>() ?? "",
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
        public void AddSafeZone(MapEntry e, int x, int y, int size, bool start, int sizeY = 0, float rotation = 0f)
        {
            var arr = GetValues(e, "SafeZones");
            var z = new JsonObject
            {
                ["Location"] = new JsonObject { ["X"] = JsonValue.Create(x), ["Y"] = JsonValue.Create(y) },
                ["Size"] = JsonValue.Create(size),
                ["SizeY"] = JsonValue.Create(sizeY),
                ["Rotation"] = JsonValue.Create(rotation),
                ["StartPoint"] = JsonValue.Create(start)
            };
            arr.Add(z);
        }

        public void AddRespawn(MapEntry e, int monsterIndex, int x, int y, int count, int spread, int delay, int direction)
        {
            var arr = GetValues(e, "Respawns");
            var r = new JsonObject
            {
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

        public bool UpdateNpc(int npcIndex, string fileName, string name, int x, int y, int rate, int image, string nameUI)
        {
            if (!NpcByIndex.TryGetValue(npcIndex, out var n)) return false;
            n["FileName"] = JsonValue.Create(fileName);
            n["Name"] = JsonValue.Create(name);
            if (string.IsNullOrEmpty(nameUI)) n.Remove("NameUI");
            else n["NameUI"] = JsonValue.Create(nameUI);
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

        // ---------- 全局修复：从原始 .dat 重新生成所有地图的安全区 ----------
        // 复用解码网格（CellFlags bit1=安全区），把安全格聚成连通分量，对每簇求最小朝向矩形（旋转卡壳），
        // 写回 SafeZones（Location 中心 + Size/SizeY 半轴 + Rotation）。与引擎 InRotatedRange 同款旋转公式，
        // 保证生成矩形完整包含该簇所有安全格。仅处理能成功解码（dat 有块且有配置宽高）的地图；其余保持原样。
        public object FixAllSafeZones()
        {
            string backup = MapInfoPath + ".bak";
            try { if (File.Exists(MapInfoPath)) File.Copy(MapInfoPath, backup, true); }
            catch { backup = ""; }

            int mapsProcessed = 0, skippedNoData = 0, mapsChanged = 0, zonesCreated = 0;
            var details = new List<object>();

            foreach (var e in Maps)
            {
                var data = DecodeMap(e.Index);
                if (data == null) { skippedNoData++; continue; }
                // 仅在能用配置宽高精确解码时才重算（否则网格为估算，坐标不可信，保持原 SafeZones 不动）
                ConfigDims.TryGetValue(e.Index, out var dim);
                if (dim.w <= 0 || dim.h <= 0 || !string.IsNullOrEmpty(data.Warn)) { skippedNoData++; continue; }

                var safe = new List<(int c, int r)>();
                for (int i = 0; i < data.Cols * data.Rows; i++)
                    if ((data.CellFlags[i] & 2) != 0)
                        safe.Add((i % data.Cols, i / data.Cols));

                mapsProcessed++;

                var oldZones = GetSafeZones(e);
                bool hadStart = oldZones.Any(z => z.Start);
                var oldStartCenters = oldZones.Where(z => z.Start).Select(z => (z.X, z.Y)).ToList();

                var arr = GetValues(e, "SafeZones");
                bool hadZones = arr.Count > 0;
                arr.Clear();

                if (safe.Count == 0)
                {
                    if (hadZones) mapsChanged++;
                    continue;
                }

                var clusters = ClusterSafeCells(safe);

                // 若原图标记过 StartPoint，挑离旧 StartPoint 中心最近的簇继承该标记（保持“城镇/出生点”语义）
                int startClusterIdx = -1;
                if (hadStart && clusters.Count > 0)
                {
                    double best = double.MaxValue;
                    for (int ci = 0; ci < clusters.Count; ci++)
                    {
                        var rect = MinAreaRect(clusters[ci]);
                        foreach (var sc in oldStartCenters)
                        {
                            double d = (rect.cx - sc.X) * (rect.cx - sc.X) + (rect.cy - sc.Y) * (rect.cy - sc.Y);
                            if (d < best) { best = d; startClusterIdx = ci; }
                        }
                    }
                }

                for (int ci = 0; ci < clusters.Count; ci++)
                {
                    var comp = clusters[ci];
                    var rect = MinAreaRect(comp);
                    double rot = rect.rot;
                    int cx = (int)Math.Round(rect.cx);
                    int cy = (int)Math.Round(rect.cy);
                    // 用整数中心重算半轴，确保整数坐标下仍完整包含该簇（ceil 防止边界差 0.5 漏格）
                    double ux = Math.Cos(rot * Math.PI / 180.0);
                    double uy = Math.Sin(rot * Math.PI / 180.0);
                    double vx = -uy, vy = ux;
                    double maxU = 0, maxV = 0;
                    foreach (var p in comp)
                    {
                        double pu = (p.c - cx) * ux + (p.r - cy) * uy;
                        double pv = (p.c - cx) * vx + (p.r - cy) * vy;
                        if (Math.Abs(pu) > maxU) maxU = Math.Abs(pu);
                        if (Math.Abs(pv) > maxV) maxV = Math.Abs(pv);
                    }
                    int hx = (int)Math.Ceiling(maxU);
                    int hy = (int)Math.Ceiling(maxV);
                    bool start = (ci == startClusterIdx);
                    AddSafeZone(e, cx, cy, hx, start, hy, (float)rot);
                    zonesCreated++;
                }
                mapsChanged++;
                details.Add(new { index = e.Index, title = e.Title, safeCells = safe.Count, clusters = clusters.Count });
            }

            Save();
            return new { ok = true, mapsProcessed, skippedNoData, mapsChanged, zonesCreated, backup, details };
        }

        // 8 邻接连通分量聚类
        private static List<List<(int c, int r)>> ClusterSafeCells(List<(int c, int r)> cells)
        {
            var set = new HashSet<(int, int)>(cells);
            var seen = new HashSet<(int, int)>();
            var result = new List<List<(int c, int r)>>();
            foreach (var p in cells)
            {
                if (!seen.Add(p)) continue;
                var comp = new List<(int c, int r)>();
                var stack = new Stack<(int c, int r)>();
                stack.Push(p);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    comp.Add(cur);
                    for (int dc = -1; dc <= 1; dc++)
                        for (int dr = -1; dr <= 1; dr++)
                        {
                            if (dc == 0 && dr == 0) continue;
                            var q = (cur.c + dc, cur.r + dr);
                            if (set.Contains(q) && seen.Add(q)) stack.Push(q);
                        }
                }
                result.Add(comp);
            }
            return result;
        }

        // 最小朝向矩形（旋转卡壳 on 凸包）：返回 (中心cx,cy, 半宽hx, 半高hy, 旋转角rot°)
        private static (double cx, double cy, double hx, double hy, double rot) MinAreaRect(List<(int c, int r)> pts)
        {
            if (pts.Count == 0) return (0, 0, 0, 0, 0);
            if (pts.Count <= 2)
            {
                int minc = int.MaxValue, maxc = int.MinValue, minr = int.MaxValue, maxr = int.MinValue;
                foreach (var p in pts) { if (p.c < minc) minc = p.c; if (p.c > maxc) maxc = p.c; if (p.r < minr) minr = p.r; if (p.r > maxr) maxr = p.r; }
                double hx = (maxc - minc) / 2.0, hy = (maxr - minr) / 2.0;
                return ((minc + maxc) / 2.0, (minr + maxr) / 2.0, hx, hy, 0.0);
            }
            var hull = ConvexHull(pts);
            double bestArea = double.MaxValue;
            double bux = 1, buy = 0, bcx = 0, bcy = 0, bhx = 0, bhy = 0;
            int n = hull.Count;
            for (int i = 0; i < n; i++)
            {
                var p1 = hull[i]; var p2 = hull[(i + 1) % n];
                double dx = p2.c - p1.c, dy = p2.r - p1.r;
                double L = Math.Sqrt(dx * dx + dy * dy);
                if (L < 1e-9) continue;
                double ux = dx / L, uy = dy / L;
                double vx = -uy, vy = ux;
                double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
                foreach (var p in pts)
                {
                    double pu = p.c * ux + p.r * uy;
                    double pv = p.c * vx + p.r * vy;
                    if (pu < minU) minU = pu; if (pu > maxU) maxU = pu;
                    if (pv < minV) minV = pv; if (pv > maxV) maxV = pv;
                }
                double area = (maxU - minU) * (maxV - minV);
                if (area < bestArea - 1e-9)
                {
                    bestArea = area; bux = ux; buy = uy;
                    bcx = (minU + maxU) / 2; bcy = (minV + maxV) / 2;
                    bhx = (maxU - minU) / 2; bhy = (maxV - minV) / 2;
                }
            }
            double cx = bcx * bux + bcy * (-buy);
            double cy = bcx * buy + bcy * bux;
            double rot = Math.Atan2(buy, bux) * 180.0 / Math.PI;
            // 矩形与旋转角 ±180° 等价（点对称重合），规范化到 (-90°,90] 避免出现 135° 这类等价但难读的角
            if (rot > 90) rot -= 180;
            else if (rot <= -90) rot += 180;
            return (cx, cy, bhx, bhy, rot);
        }

        // 单调链凸包
        private static List<(int c, int r)> ConvexHull(List<(int c, int r)> pts)
        {
            var ps = pts.Distinct().ToList();
            ps.Sort((a, b) => a.c != b.c ? a.c.CompareTo(b.c) : a.r.CompareTo(b.r));
            if (ps.Count <= 2) return ps;
            double Cross((int c, int r) o, (int c, int r) a, (int c, int r) b)
                => (a.c - o.c) * (double)(b.r - o.r) - (a.r - o.r) * (double)(b.c - o.c);
            var lower = new List<(int c, int r)>();
            foreach (var p in ps) { while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0) lower.RemoveAt(lower.Count - 1); lower.Add(p); }
            var upper = new List<(int c, int r)>();
            for (int i = ps.Count - 1; i >= 0; i--) { var p = ps[i]; while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0) upper.RemoveAt(upper.Count - 1); upper.Add(p); }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }
    }
}
