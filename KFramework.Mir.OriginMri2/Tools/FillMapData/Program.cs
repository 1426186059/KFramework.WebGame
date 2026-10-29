using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 把 Crystal 真库（英文名地图）的 怪物/安全区/传送门/NPC，按中文名桥接进中文自定义图。
//   FillMapData            -> 处理地图内 怪物/安全区/传送门（写入 Maps/MapInfo_Fix.json）
//   FillMapData npc        -> 仅处理 NPC（写入 ExcelConfig/NPCInfo.json，按 MapIndex 关联）
// 策略：覆盖表（英文名 -> 目标 Index，手工核对）优先；未命中用 翻译+子串 唯一匹配；多义/无匹配跳过。
class Program
{
    static readonly Dictionary<string, int> Override = new()
    {
        { "1", 5426 }, { "11", 5426 },
        { "D021", 5416 }, { "D022", 5417 }, { "D023", 5418 }, { "D024", 5355 },
        { "D021A", 5416 }, { "D022A", 5417 }, { "D023A", 5418 }, { "D024A", 5355 },
        { "D2006", 5355 },
        { "D001", 5109 }, { "D002", 5110 }, { "D003", 5109 },
        { "D011", 5006 }, { "D012", 5007 },
        { "D501", 5026 }, { "D502", 5027 }, { "D503", 5028 }, { "D504", 5029 }, { "D505", 5030 },
        { "D501A", 5026 }, { "D502A", 5027 }, { "D503A", 5028 },
        { "D511", 5025 }, { "D512", 5025 }, { "D513", 5025 }, { "D514", 5025 }, { "D515", 5025 },
        { "D5061", 5031 }, { "D5062", 5032 }, { "D5063", 5033 }, { "D5064", 5034 }, { "D5065", 5035 },
        { "D5066", 5036 }, { "D5067", 5037 }, { "D5068", 5038 }, { "D5069", 5039 },
        { "D5071", 5379 }, { "D5072", 5379 }, { "D5073", 5379 }, { "D5074", 5379 },
        { "0157", 5379 }, { "0157A", 5031 },
        { "D10062", 5136 },
        { "D2031", 5011 }, { "D2032", 5012 }, { "D2033", 5014 }, { "D2034", 5022 },
        { "SG001", 5011 }, { "SG002", 5012 }, { "SG003", 5014 }, { "SG004", 5022 },
        { "D401", 5016 }, { "D402", 5016 }, { "D403", 5016 }, { "D404", 5016 }, { "D405", 5020 }, { "D406", 5016 },
        { "D411", 5016 }, { "D412", 5016 }, { "D413", 5016 }, { "D414", 5016 }, { "D415", 5016 }, { "D416", 5016 },
        { "D421", 5016 }, { "D422", 5016 }, { "D4221", 5016 },
        { "6", 5016 },
        { "D710", 5050 }, { "D710A", 5050 },
        { "D711", 5051 }, { "D712", 5052 }, { "D713", 5053 }, { "D714", 5054 }, { "D715", 5055 }, { "D716", 5056 }, { "D717", 5383 },
        { "DM001", 5227 }, { "DM001A", 5227 }, { "DM002", 5004 },
        { "D71601", 5058 }, { "D71602", 5059 }, { "D71603", 5060 }, { "D71604", 5061 }, { "D71605", 5062 },
        { "D71606", 5063 }, { "D71607", 5064 }, { "D71608", 5065 }, { "D71609", 5066 }, { "D71610", 5067 },
        { "D71611", 5068 }, { "D71612", 5069 }, { "D71613", 5070 }, { "D71614", 5071 }, { "D71615", 5072 },
        { "D71616", 5073 }, { "D71617", 5074 }, { "D71618", 5075 }, { "D71619", 5076 }, { "D71620", 5077 },
        { "D71621", 5078 }, { "D71622", 5079 }, { "D71623", 5080 }, { "D71624", 5081 }, { "D71625", 5082 },
        { "D71650", 5058 }, { "D71651", 5059 }, { "D71652", 5060 }, { "D71653", 5061 },
        { "D701", 5311 },
        { "D2081", 5280 }, { "D2082", 5281 },
        { "HELL201", 5287 }, { "HELL202", 5288 }, { "HELL203", 5289 }, { "HELL204", 5287 }, { "HELL205", 5288 }, { "HELL206", 5289 },
        { "SnowCave_1F", 5287 }, { "SnowCave_2F", 5288 }, { "SnowCave_BOSS", 5289 },
        { "HF1", 5232 }, { "HF2", 5232 }, { "HF3", 5232 }, { "HKR", 5232 },
        { "2", 5427 }, { "L001", 5427 }, { "L002", 5427 }, { "L003", 5427 }, { "L004", 5427 },
        { "SNAKE", 5427 }, { "SnakePre", 5427 }, { "SnakePre2", 5427 },
        { "Q004", 5124 },
        { "E001", 5111 }, { "E002", 5112 }, { "E401", 5113 }, { "E402", 5114 }, { "E403", 5115 }, { "E404", 5116 },
        { "E601", 5117 }, { "E602", 5118 }, { "E603", 5119 }, { "E604", 5115 }, { "E605", 5115 },
        { "E701", 5111 }, { "E702", 5111 }, { "E703", 5111 },
        { "EBEE01", 5444 }, { "EBEE02", 5444 }, { "EBEE03", 5444 }, { "EBEEBOSS", 5445 },
        { "EM001", 5339 }, { "EM002", 5339 }, { "EM003", 5339 },
        { "Fox01", 5283 }, { "Fox02", 5284 }, { "Fox03", 5285 },
        { "12", 5125 },
        { "LABY01", 5125 }, { "LABY02", 5125 }, { "LABY03", 5125 }, { "LABY04", 5125 }, { "LABYBUY01", 5125 }, { "LABYBUY02", 5125 }, { "LABYROOM", 5125 },
        { "DogYoMineLobby", 5279 }, { "DogYoHyun", 5279 }, { "DogYoArena2", 5309 },
        { "DY_Pharmacy", 5370 }, { "DY_Weapon", 5369 }, { "DY_Wearhouse", 5312 }, { "DY_Craft1", 5413 }, { "DY_Head1", 5413 },
        { "DY_MedStorage", 5370 }, { "DY_Smithy", 5369 },
        { "WhiteVillage", 5384 },
        { "whitevil_acc", 5387 }, { "whitevil_armor", 5387 }, { "whitevil_book", 5388 }, { "whitevil_empty", 5384 },
        { "whitevil_empty1", 5384 }, { "whitevil_etc", 5391 }, { "whitevil_etc1", 5389 }, { "whitevil_etc2", 5390 },
        { "whitevil_fish", 5384 }, { "whitevil_weapon", 5385 },
        { "B341", 5372 }, { "B342", 5369 }, { "B343", 5370 }, { "B344", 5397 }, { "B345", 5374 }, { "B346", 5399 }, { "B347", 5312 },
        { "B351", 5369 }, { "B352", 5397 }, { "B353", 5374 }, { "B354", 5386 }, { "B355", 5384 }, { "B356", 5384 }, { "B357", 5346 },
        { "0", 103 }, { "3", 154 },
        { "0100", 5334 }, { "0101", 200 }, { "0102", 5323 }, { "0103", 5369 }, { "0104", 5047 }, { "01041", 5047 },
        { "0105", 5397 }, { "0106", 5374 }, { "0107", 5338 }, { "0108", 5370 }, { "0109", 5370 },
        { "0115", 5047 }, { "0116", 200 }, { "0117", 5370 }, { "0120", 5083 }, { "0121", 5346 }, { "0122", 5001 },
        { "0123", 5342 }, { "0124", 5342 }, { "0125", 5342 }, { "0126", 5342 }, { "0127", 5342 }, { "0128", 5342 },
        { "0129", 5342 }, { "0130", 5342 }, { "0131", 5342 },
        { "0132", 5047 }, { "0136", 5350 }, { "0137", 5339 }, { "0138", 5342 }, { "0139", 5339 },
        { "0140", 5312 }, { "0141", 5376 }, { "0142", 5342 }, { "0143", 5342 }, { "0144", 5342 }, { "0145", 5342 },
        { "0146", 5333 }, { "0147", 5352 }, { "0148", 200 }, { "0149", 5374 },
        { "0151", 5083 }, { "0152", 5312 }, { "0153", 5370 }, { "0154", 5370 }, { "0155", 5370 }, { "0156", 5399 },
    };

    static int idc = 900000;

    // 把文档内所有 $id 重新分配为唯一序号，并同步修正所有 $ref（保留数据，仅修复引用一致性）。
    // 关键：按“对象本体”逐一分配唯一新 id（即使旧 id 已重复也不会再撞），旧 id→新 id 取首次出现用于 $ref。
    static void NormalizeIds(JObject doc)
    {
        int counter = 1;
        var newIdOf = new Dictionary<JObject, string>(); // 引用相等：每个有 $id 的对象得到唯一新 id
        var oldToNew = new Dictionary<string, string>();  // 旧 id -> 新 id（首次出现，供 $ref 修正）
        void Assign(JToken t)
        {
            if (t is JObject o)
            {
                var id = o["$id"]?.ToString();
                if (id != null)
                {
                    string nid = (counter++).ToString();
                    newIdOf[o] = nid;
                    if (!oldToNew.ContainsKey(id)) oldToNew[id] = nid;
                }
                foreach (var p in o.Properties()) if (p.Name != "$id") Assign(p.Value);
            }
            else if (t is JArray a) { foreach (var e in a) Assign(e); }
        }
        Assign(doc);
        void Replace(JToken t)
        {
            if (t is JObject o)
            {
                if (newIdOf.TryGetValue(o, out var nid)) o["$id"] = nid;
                if (o["$ref"] != null) { var r = o["$ref"].ToString(); if (oldToNew.TryGetValue(r, out var nn)) o["$ref"] = nn; }
                foreach (var p in o.Properties()) if (p.Name != "$id" && p.Name != "$ref") Replace(p.Value);
            }
            else if (t is JArray a) { foreach (var e in a) Replace(e); }
        }
        Replace(doc);
    }

    static void Main(string[] args)
    {
        bool npcOnly = args.Length > 0 && args[0] == "npc";
        string crystalDir = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\MirDBToJson\_realcheck";
        string versionRoot = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version";
        string targetMapPath = Path.Combine(versionRoot, "Maps", "MapInfo_Fix.json");
        string targetNpcPath = Path.Combine(versionRoot, "ExcelConfig", "NPCInfo.json");
        string logPath = Path.Combine(crystalDir, "bridge_log2.txt");

        // 注意：绝不能用 PreserveReferencesHandling.Objects —— 它会给无 $id 的对象从 "1" 重新编号，
        // 和原文件已有 $id 冲突。这里把 $id/$ref 当普通属性原样保留，仅新容器用唯一 id。
        var set = new JsonSerializerSettings
        {
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        var targetDoc = JObject.Parse(File.ReadAllText(targetMapPath));
        NormalizeIds(targetDoc); // 防御：先规范化基底的 $id/$ref，避免任何历史污染导致冲突
        var targetMaps = (JArray)targetDoc["$values"];
        var targetByIndex = new Dictionary<int, JObject>();
        foreach (JObject m in targetMaps) targetByIndex[(int)m["Index"]] = m;

        var crystalDoc = JObject.Parse(File.ReadAllText(Path.Combine(crystalDir, "MapInfo.json")));
        var crystalMaps = (JArray)crystalDoc["$values"];
        var crystalByIndex = new Dictionary<int, JObject>();
        var crystalByFile = new Dictionary<string, JObject>();
        foreach (JObject m in crystalMaps) { crystalByIndex[(int)m["Index"]] = m; var fn = (string)m["FileName"]; if (fn != null) crystalByFile[fn] = m; }

        int? ResolveTarget(JObject cm)
        {
            string fn = (string)cm["FileName"];
            if (fn != null && Override.TryGetValue(fn, out int t)) return t;
            string cn = Translate((string)cm["Title"]);
            if (!string.IsNullOrEmpty(cn))
            {
                var cands = targetMaps.OfType<JObject>().Where(m => TitleMatch((string)m["Title"], cn)).ToList();
                if (cands.Count == 1) return (int)cands[0]["Index"];
            }
            return null;
        }

        var crystalIndexToTarget = new Dictionary<int, int>();
        foreach (JObject cm in crystalMaps) { var t = ResolveTarget(cm); if (t.HasValue) crystalIndexToTarget[(int)cm["Index"]] = t.Value; }

        if (args.Length > 0 && args[0] == "npcclean")
        {
            var nDoc = JObject.Parse(File.ReadAllText(targetNpcPath));
            var nArr = (JArray)nDoc["$values"];
            var seen = new HashSet<string>();
            var toRemove = new List<JObject>();
            foreach (JObject o in nArr)
            {
                var loc = o["Location"] as JObject;
                var key = $"{(int?)o["MapIndex"]}|{o["Name"]}|{loc?["X"]}|{loc?["Y"]}";
                if (!seen.Add(key)) toRemove.Add(o);
            }
            foreach (var r in toRemove) nArr.Remove(r);
            File.WriteAllText(targetNpcPath, JsonConvert.SerializeObject(nDoc, set));
            Console.WriteLine($"清理重复 NPC: 移除 {toRemove.Count} 条，剩余 {nArr.Count}");
            return;
        }

        if (args.Length > 0 && args[0] == "verify")
        {
            foreach (var p in new[] { targetMapPath, targetNpcPath })
            {
                if (!File.Exists(p)) { Console.WriteLine($"跳过(不存在): {Path.GetFileName(p)}"); continue; }
                try
                {
                    var opts = new System.Text.Json.JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve };
                    var o = System.Text.Json.JsonSerializer.Deserialize<object>(File.ReadAllText(p), opts);
                    Console.WriteLine($"OK 可加载(无$id冲突): {Path.GetFileName(p)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!! 加载失败: {Path.GetFileName(p)} -> {ex.Message}");
                }
            }
            return;
        }

        if (args.Length > 0 && args[0] == "normalize")
        {
            string normPath = args.Length > 1 ? args[1] : targetMapPath;
            var doc = JObject.Parse(File.ReadAllText(normPath));
            int before = CountIds(doc);
            NormalizeIds(doc);
            File.WriteAllText(normPath, JsonConvert.SerializeObject(doc, set));
            Console.WriteLine($"normalize 完成：重排 {before} 个 $id 为唯一序号并修正 $ref，已写回 {Path.GetFileName(normPath)}");
            return;
        }

        static int CountIds(JToken t)
        {
            int n = 0;
            if (t is JObject o) { if (o["$id"] != null) n++; foreach (var p in o.Properties()) if (p.Name != "$id") n += CountIds(p.Value); }
            else if (t is JArray a) { foreach (var e in a) n += CountIds(e); }
            return n;
        }

        if (args.Length > 0 && args[0] == "scanids")
        {
            string scanPath = args.Length > 1 ? args[1] : targetMapPath;
            var doc = JObject.Parse(File.ReadAllText(scanPath));
            var ids = new Dictionary<string, int>();
            int dups = 0;
            void Walk(JToken t)
            {
                if (t is JObject o)
                {
                    var id = o["$id"]?.ToString();
                    if (id != null) { if (!ids.TryAdd(id, 1)) { ids[id]++; dups++; Console.WriteLine($"重复 $id: {id} (第{ids[id]}次) 路径≈{o.Path}"); } }
                    foreach (var p in o.Properties()) if (p.Name != "$id") Walk(p.Value);
                }
                else if (t is JArray a) { foreach (var e in a) Walk(e); }
            }
            Walk(doc);
            Console.WriteLine(dups == 0 ? "未检测到重复 $id，磁盘文件干净" : $"共发现 {dups} 处重复 $id");
            return;
        }

        if (args.Length > 0 && args[0] == "editorsim")
        {
            // 模拟 MapInfoEditor.Save()：JsonNode.Parse + ToJsonString(WriteIndented, UnsafeRelaxedJsonEscaping)
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(targetMapPath));
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var outTxt = node.ToJsonString(opts);
            var doc = JObject.Parse(outTxt);
            var ids = new Dictionary<string, int>();
            int dups = 0;
            void Walk(JToken t)
            {
                if (t is JObject o)
                {
                    var id = o["$id"]?.ToString();
                    if (id != null) { if (!ids.TryAdd(id, 1)) { ids[id]++; dups++; } }
                    foreach (var p in o.Properties()) if (p.Name != "$id") Walk(p.Value);
                }
                else if (t is JArray a) { foreach (var e in a) Walk(e); }
            }
            Walk(doc);
            Console.WriteLine(dups == 0 ? "模拟编辑器保存后：无重复 $id（编辑器保存安全）" : $"模拟编辑器保存后：发现 {dups} 处重复 $id（编辑器会破坏！）");
            return;
        }

        var log = new List<string>();

        // ---- 地图内 怪物/安全区/传送门 ----
        if (!npcOnly)
        {
            string bak = targetMapPath + ".bak2";
            File.Copy(targetMapPath, bak, true);
            int filled = 0, respAdded = 0, szAdded = 0, mvAdded = 0;
            foreach (JObject cm in crystalMaps)
            {
                var tIdx = ResolveTarget(cm);
                if (!tIdx.HasValue) continue;
                if (!targetByIndex.TryGetValue(tIdx.Value, out JObject tm)) continue;
                int ra = CopyArray(cm, tm, "Respawns");
                int sa = CopySafeZones(cm, tm);
                int ma = CopyMovements(cm, tm, crystalIndexToTarget);
                if (ra + sa + ma > 0)
                {
                    filled++; respAdded += ra; szAdded += sa; mvAdded += ma;
                    log.Add($"[FILL] {cm["FileName"]}({cm["Title"]}) -> {tIdx}({tm["Title"]})  怪{ra} 安{sa} 门{ma}");
                }
            }
            File.WriteAllText(targetMapPath, JsonConvert.SerializeObject(targetDoc, set));
            log.Add($"# MapInfo 完成：填充地图 {filled} | 怪物+{respAdded} 安全区+{szAdded} 传送门+{mvAdded}");
            Console.WriteLine($"MapInfo 完成：填充地图 {filled} | 怪物+{respAdded} 安全区+{szAdded} 传送门+{mvAdded}");
        }

        // ---- NPC（写入 ExcelConfig/NPCInfo.json，按 MapIndex 关联）仅 npc 模式 ----
        if (npcOnly && File.Exists(targetNpcPath) && File.Exists(Path.Combine(crystalDir, "NPCInfo.json")))
        {
            string bak = targetNpcPath + ".bak2";
            File.Copy(targetNpcPath, bak, true);
            var nDoc = JObject.Parse(File.ReadAllText(targetNpcPath));
            var nArr = nDoc["$values"] is JArray a ? a : (nDoc["$values"] = new JArray()) as JArray;
            if (nDoc["$values"] is JArray) nArr = (JArray)nDoc["$values"];
            int maxIdx = 0;
            foreach (JObject o in nArr) maxIdx = Math.Max(maxIdx, (int?)o["Index"] ?? 0);

            var cNpc = JObject.Parse(File.ReadAllText(Path.Combine(crystalDir, "NPCInfo.json")));
            var cNpcArr = (JArray)cNpc["$values"];
            int added = 0;
            foreach (JObject npc in cNpcArr)
            {
                var mi = (int?)npc["MapIndex"];
                if (!mi.HasValue || !crystalIndexToTarget.TryGetValue(mi.Value, out int ti)) continue;
                var c = (JObject)npc.DeepClone();
                c.Remove("$id");
                c["MapIndex"] = ti;
                maxIdx++;
                c["Index"] = maxIdx;
                // 递归清掉嵌套 $id，交给 Newtonsoft 重排
                ClearNestedIds(c);
                nArr.Add(c);
                added++;
            }
            File.WriteAllText(targetNpcPath, JsonConvert.SerializeObject(nDoc, set));
            log.Add($"# NPC 完成：新增 NPC {added} 条 -> {targetNpcPath}");
            Console.WriteLine($"NPC 完成：新增 {added} 条 -> {Path.GetFileName(targetNpcPath)}");
        }
        else
        {
            log.Add("[NPC] 跳过：目标或 crystal 的 NPCInfo.json 不存在");
            Console.WriteLine("[NPC] 跳过：NPCInfo.json 不存在");
        }

        File.WriteAllText(logPath, string.Join("\n", log));
        Console.WriteLine($"日志: {logPath}");
    }

    static void ClearNestedIds(JObject o)
    {
        foreach (var p in o.Properties().ToList())
        {
            if (p.Name == "$id") { p.Remove(); continue; }
            if (p.Value is JObject jo) ClearNestedIds(jo);
            else if (p.Value is JArray ja)
            {
                foreach (var e in ja) if (e is JObject eo) ClearNestedIds(eo);
            }
        }
    }

    static bool TitleMatch(string title, string cn)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(cn) || cn.Length < 2) return false;
        return title.Contains(cn) || cn.Contains(title);
    }

    static string Translate(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        var s = title;
        s = s.Replace("_1F", "一层").Replace("_2F", "二层").Replace("_3F", "三层").Replace("_4F", "四层")
             .Replace("_5F", "五层").Replace("_6F", "六层").Replace("_7F", "七层").Replace("_8F", "八层").Replace("_9F", "九层")
             .Replace("_10F", "十层").Replace("_11F", "十一层").Replace("_12F", "十二层")
             .Replace("B1", "地下一层").Replace("B2", "地下二层");
        s = s.Replace("Woomyon", "沃玛").Replace("Wooma", "沃玛").Replace("Oma", "沃玛").Replace("Zuma", "祖玛")
             .Replace("Red", "赤").Replace("Blood", "赤").Replace("Serpent", "蛇").Replace("Snake", "蛇").Replace("Viper", "蛇")
             .Replace("Mongchon", "盟重").Replace("Prajna", "般若").Replace("BlackDragon", "黑龙").Replace("HwanMa", "幻")
             .Replace("Castle", "城").Replace("WhiteDragon", "白龙").Replace("Ancient", "古")
             .Replace("NaturalCave", "自然洞穴").Replace("Cave", "洞").Replace("Temple", "寺庙").Replace("Valley", "谷")
             .Replace("Gorge", "峡谷").Replace("Pass", "通道").Replace("Mine", "矿").Replace("MineralMine", "矿区")
             .Replace("Store", "店").Replace("House", "屋").Replace("Palace", "宫殿").Replace("Tavern", "酒馆")
             .Replace("Inn", "旅店").Replace("Kitchen", "厨房").Replace("Meat", "肉").Replace("Prison", "监狱")
             .Replace("Barracks", "兵营").Replace("Warehouse", "仓库").Replace("Library", "图书馆").Replace("Academy", "学院")
             .Replace("Beauty", "美容").Replace("Reagent", "药").Replace("Medicine", "药").Replace("Accessory", "首饰")
             .Replace("Draper", "服装").Replace("Grocery", "杂货").Replace("Weapon", "武器").Replace("Book", "书店")
             .Replace("Boss", "教主").Replace("Room", "房").Replace("Hall", "厅").Replace("Lobby", "大厅")
             .Replace("Entrance", "入口").Replace("ConnectionPath", "连接通道").Replace("ConnectionCave", "连接通道")
             .Replace("DeadMine", "废矿").Replace("Swamp", "沼泽").Replace("Hell", "地狱").Replace("Ice", "冰")
             .Replace("Snow", "雪").Replace("Dark", "暗").Replace("Forest", "森林").Replace("Island", "岛")
             .Replace("Village", "村").Replace("Town", "镇").Replace("Province", "省").Replace("Woods", "森林")
             .Replace("Path", "道").Replace("Maze", "迷宫").Replace("Tomb", "墓").Replace("Ruin", "废墟")
             .Replace("Crypt", "墓").Replace("Cavern", "洞窟");
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) if (c >= 0x4e00 && c <= 0x9fff) sb.Append(c);
        return sb.ToString();
    }

    static JArray GetOrCreateValues(JObject map, string field)
    {
        var token = map[field];
        if (token is JObject jo && jo["$values"] is JArray arr) return arr;
        var newArr = new JArray();
        map[field] = new JObject { ["$id"] = "n" + (idc++), ["$values"] = newArr };
        return newArr;
    }

    static int CopyArray(JObject src, JObject dst, string field)
    {
        if (!(src[field] is JObject sjo) || !(sjo["$values"] is JArray sa)) return 0;
        var da = GetOrCreateValues(dst, field);
        int n = 0;
        foreach (JObject item in sa) { var c = (JObject)item.DeepClone(); ClearNestedIds(c); da.Add(c); n++; }
        return n;
    }

    static int CopySafeZones(JObject src, JObject dst)
    {
        if (!(src["SafeZones"] is JObject sjo) || !(sjo["$values"] is JArray sa)) return 0;
        var da = GetOrCreateValues(dst, "SafeZones");
        int n = 0;
        foreach (JObject item in sa)
        {
            var c = (JObject)item.DeepClone();
            ClearNestedIds(c);
            c["Info"] = new JObject { ["$ref"] = (string)dst["$id"] };
            da.Add(c); n++;
        }
        return n;
    }

    static int CopyMovements(JObject src, JObject dst, Dictionary<int, int> idxMap)
    {
        if (!(src["Movements"] is JObject sjo) || !(sjo["$values"] is JArray sa)) return 0;
        var da = GetOrCreateValues(dst, "Movements");
        int n = 0;
        foreach (JObject item in sa)
        {
            var mi = (int?)item["MapIndex"];
            if (!mi.HasValue || !idxMap.TryGetValue(mi.Value, out int ti)) continue;
            var c = (JObject)item.DeepClone();
            ClearNestedIds(c);
            c["MapIndex"] = ti;
            da.Add(c); n++;
        }
        return n;
    }
}
