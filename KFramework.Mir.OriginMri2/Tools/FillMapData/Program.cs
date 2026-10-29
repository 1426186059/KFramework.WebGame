using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

// 把 Crystal 真库（英文名地图）的 怪物/安全区/传送门/NPC，按中文名桥接进中文自定义图。
//   FillMapData            -> 处理地图内 怪物/安全区/传送门（写入 Maps/MapInfo_Fix.json）
//   FillMapData npc        -> 仅处理 NPC（写入 ExcelConfig/NPCInfo.json，按 MapIndex 关联）
// 全程仅使用 System.Text.Json（不使用 Newtonsoft）。输出为“干净 JSON”：无 $id/$ref/$values 信封。
// 说明：SafeZoneInfo.Info 由服务端加载后手动重新挂回（见 Envir.LoadDB），这里不再写 Info/$ref。
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

    // 与服务端/编辑器一致的写出选项（WriteIndented + 不转义中文）。
    static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // 读取文件，兼容旧信封格式({"$values":[...]})与新裸数组格式([...])，返回其中的“数组”。
    static JsonArray LoadArray(string path)
    {
        JsonNode node = JsonNode.Parse(File.ReadAllText(path));
        if (node is JsonArray arr) return arr;
        if (node is JsonObject obj && obj["$values"] is JsonArray vals) return vals;
        return new JsonArray();
    }

    // 递归把文档归一化为干净 JSON：删除 $id/$ref，并把 {"$values":[...]} 拆成数组。
    // 重建新树（不就地改），避免把已挂父节点的节点重复挂到新父节点抛出 "already has a parent"。
    static JsonNode NormalizeToClean(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj["$values"] is JsonArray vals) return NormalizeToClean(vals);
            var result = new JsonObject();
            foreach (var p in obj)
            {
                if (p.Key == "$id" || p.Key == "$ref") continue;
                result[p.Key] = NormalizeToClean(p.Value);
            }
            return result;
        }
        if (node is JsonArray arr)
        {
            var result = new JsonArray();
            foreach (var item in arr) result.Add(NormalizeToClean(item));
            return result;
        }
        return node.DeepClone();
    }

    static byte[] DecompressIfNeeded(byte[] raw)
    {
        if (raw == null || raw.Length < 2) return raw;
        if (raw[0] == 0x1f && raw[1] == 0x8b)
        {
            using var ms = new MemoryStream(raw);
            using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var o = new MemoryStream(); gz.CopyTo(o); return o.ToArray();
        }
        if (raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda))
        {
            using var ms = new MemoryStream(raw);
            using var zl = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var o = new MemoryStream(); zl.CopyTo(o); return o.ToArray();
        }
        return raw;
    }

    static void Main(string[] args)
    {
        bool npcOnly = args.Length > 0 && args[0] == "npc";
        string crystalDir = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\MirDBToJson\_realcheck";
        string versionRoot = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version";
        string targetMapPath = Path.Combine(versionRoot, "Maps", "MapInfo_Fix.json");
        string targetNpcPath = Path.Combine(versionRoot, "ExcelConfig", "NPCInfo.json");
        string logPath = Path.Combine(crystalDir, "bridge_log2.txt");

        // ---- 主流程读取（兼容旧信封 / 新裸数组）----
        var targetMaps = LoadArray(targetMapPath);
        var targetByIndex = new Dictionary<int, JsonObject>();
        foreach (JsonObject m in targetMaps.OfType<JsonObject>())
            targetByIndex[m["Index"]?.GetValue<int>() ?? -1] = m;

        var crystalDoc = JsonNode.Parse(File.ReadAllText(Path.Combine(crystalDir, "MapInfo.json")));
        var crystalMaps = crystalDoc is JsonArray ca ? ca : (JsonArray)crystalDoc["$values"];
        var crystalByIndex = new Dictionary<int, JsonObject>();
        var crystalByFile = new Dictionary<string, JsonObject>();
        foreach (JsonObject m in crystalMaps.OfType<JsonObject>())
        {
            crystalByIndex[m["Index"]?.GetValue<int>() ?? -1] = m;
            var fn = m["FileName"]?.GetValue<string>();
            if (fn != null) crystalByFile[fn] = m;
        }

        int? ResolveTarget(JsonObject cm)
        {
            string fn = cm["FileName"]?.GetValue<string>();
            if (fn != null && Override.TryGetValue(fn, out int t)) return t;
            string cn = Translate(cm["Title"]?.GetValue<string>());
            if (!string.IsNullOrEmpty(cn))
            {
                var cands = targetMaps.OfType<JsonObject>().Where(m => TitleMatch(m["Title"]?.GetValue<string>(), cn)).ToList();
                if (cands.Count == 1) return cands[0]["Index"]?.GetValue<int>();
            }
            return null;
        }

        var crystalIndexToTarget = new Dictionary<int, int>();
        foreach (JsonObject cm in crystalMaps.OfType<JsonObject>())
        {
            var t = ResolveTarget(cm);
            if (t.HasValue) crystalIndexToTarget[cm["Index"]?.GetValue<int>() ?? -1] = t.Value;
        }

        if (args.Length > 0 && args[0] == "npcclean")
        {
            var arr = LoadArray(targetNpcPath);
            var seen = new HashSet<string>();
            var toRemove = new List<JsonNode>();
            foreach (JsonObject o in arr.OfType<JsonObject>())
            {
                var loc = o["Location"] as JsonObject;
                var key = $"{o["MapIndex"]?.GetValue<int?>()}|{o["Name"]}|{loc?["X"]}|{loc?["Y"]}";
                if (!seen.Add(key)) toRemove.Add(o);
            }
            foreach (var r in toRemove) arr.Remove(r);
            File.WriteAllText(targetNpcPath, NormalizeToClean(arr).ToJsonString(WriteOpts));
            Console.WriteLine($"清理重复 NPC: 移除 {toRemove.Count} 条，剩余 {arr.Count}");
            return;
        }

        if (args.Length > 0 && args[0] == "verify")
        {
            foreach (var p in new[] { targetMapPath, targetNpcPath })
            {
                if (!File.Exists(p)) { Console.WriteLine($"跳过(不存在): {Path.GetFileName(p)}"); continue; }
                try
                {
                    var opts = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
                    var o = JsonSerializer.Deserialize<object>(File.ReadAllText(p), opts);
                    Console.WriteLine($"OK 可加载(无$冲突): {Path.GetFileName(p)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!! 加载失败: {Path.GetFileName(p)} -> {ex.Message}");
                }
            }
            return;
        }

        if (args.Length > 0 && args[0] == "mapcheck")
        {
            string datPath = Path.Combine(versionRoot, "Maps", "map10.25341.1.dat");
            var datIds = new HashSet<int>();
            if (!File.Exists(datPath)) Console.WriteLine("!! dat 不存在: " + datPath);
            else
            {
                byte[] data = DecompressIfNeeded(File.ReadAllBytes(datPath));
                if (data != null && data.Length >= 2)
                {
                    int count = (data[0] << 8) | data[1];
                    int pos = 2;
                    for (int i = 0; i < count && pos < data.Length; i++)
                    {
                        if (pos + 8 > data.Length) break;
                        int id = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
                        int len = (data[pos + 4] << 24) | (data[pos + 5] << 16) | (data[pos + 6] << 8) | data[pos + 7];
                        pos += 8;
                        if (len < 0 || len > data.Length - pos) break;
                        datIds.Add(id);
                        pos += len;
                    }
                }
            }
            var arr = LoadArray(targetMapPath);
            int total = 0, hit = 0, startMaps = 0; var missing = new List<int>();
            foreach (JsonObject m in arr.OfType<JsonObject>())
            {
                var fn = m["FileName"]?.GetValue<string>();
                if (!int.TryParse(fn, out int id)) continue;
                total++;
                if (datIds.Contains(id))
                {
                    hit++;
                    if (m["SafeZones"] is JsonArray sza)
                        foreach (JsonObject z in sza.OfType<JsonObject>()) if (z["StartPoint"]?.GetValue<bool>() == true) { startMaps++; break; }
                }
                else missing.Add(id);
            }
            Console.WriteLine($"地图总数={total}  dat内含={hit}  缺失={missing.Count}  含起始点(可启动)={startMaps}");
            if (missing.Count > 0) Console.WriteLine("缺失id(前40): " + string.Join(",", missing.Take(40)));
            return;
        }

        if (args.Length > 0 && args[0] == "editorsim")
        {
            // 模拟编辑器/本工具保存：用 System.Text.Json 写出并验证可重新加载（干净 JSON，无 $id/$ref）。
            var arr = LoadArray(targetMapPath);
            var cleaned = NormalizeToClean(arr);
            var outTxt = cleaned.ToJsonString(WriteOpts);
            try
            {
                JsonNode.Parse(outTxt);
                Console.WriteLine("模拟保存后：干净 JSON，无 $id/$ref/$values，可正常加载");
            }
            catch (Exception ex)
            {
                Console.WriteLine("!! 模拟保存失败: " + ex.Message);
            }
            return;
        }

        if (args.Length > 0 && args[0] == "clean")
        {
            // 仅把文件归一化为干净 JSON（剥离 $id/$ref、拆掉 $values 信封），不重新桥接/不丢数据。
            // 适用于把历史“引用信封”格式就地转换为无 $ 的干净格式。
            foreach (var p in new[] { targetMapPath, targetNpcPath })
            {
                if (!File.Exists(p)) { Console.WriteLine($"跳过(不存在): {Path.GetFileName(p)}"); continue; }
                var arr = LoadArray(p);
                File.WriteAllText(p, NormalizeToClean(arr).ToJsonString(WriteOpts));
                Console.WriteLine($"已归一化为干净 JSON（无 $id/$ref/$values）: {Path.GetFileName(p)}");
            }
            return;
        }

        var log = new List<string>();

        // ---- 地图内 怪物/安全区/传送门 ----
        if (!npcOnly)
        {
            string bak = targetMapPath + ".bak2";
            File.Copy(targetMapPath, bak, true);
            int filled = 0, respAdded = 0, szAdded = 0, mvAdded = 0;
            foreach (JsonObject cm in crystalMaps.OfType<JsonObject>())
            {
                var tIdx = ResolveTarget(cm);
                if (!tIdx.HasValue) continue;
                if (!targetByIndex.TryGetValue(tIdx.Value, out JsonObject tm)) continue;
                int ra = CopyArray(cm, tm, "Respawns");
                int sa = CopySafeZones(cm, tm);
                int ma = CopyMovements(cm, tm, crystalIndexToTarget);
                if (ra + sa + ma > 0)
                {
                    filled++; respAdded += ra; szAdded += sa; mvAdded += ma;
                    log.Add($"[FILL] {cm["FileName"]}({cm["Title"]}) -> {tIdx}({tm["Title"]})  怪{ra} 安{sa} 门{ma}");
                }
            }
            File.WriteAllText(targetMapPath, NormalizeToClean(targetMaps).ToJsonString(WriteOpts));
            log.Add($"# MapInfo 完成：填充地图 {filled} | 怪物+{respAdded} 安全区+{szAdded} 传送门+{mvAdded}");
            Console.WriteLine($"MapInfo 完成：填充地图 {filled} | 怪物+{respAdded} 安全区+{szAdded} 传送门+{mvAdded}");
        }

        // ---- NPC（写入 ExcelConfig/NPCInfo.json，按 MapIndex 关联）仅 npc 模式 ----
        if (npcOnly && File.Exists(targetNpcPath) && File.Exists(Path.Combine(crystalDir, "NPCInfo.json")))
        {
            string bak = targetNpcPath + ".bak2";
            File.Copy(targetNpcPath, bak, true);
            var nArr = LoadArray(targetNpcPath);
            int maxIdx = 0;
            foreach (JsonObject o in nArr.OfType<JsonObject>()) maxIdx = Math.Max(maxIdx, o["Index"]?.GetValue<int?>() ?? 0);

            var cNpc = JsonNode.Parse(File.ReadAllText(Path.Combine(crystalDir, "NPCInfo.json")));
            var cNpcArr = cNpc is JsonArray ca2 ? ca2 : (JsonArray)cNpc["$values"];
            int added = 0;
            foreach (JsonObject npc in cNpcArr.OfType<JsonObject>())
            {
                var mi = npc["MapIndex"]?.GetValue<int?>();
                if (!mi.HasValue || !crystalIndexToTarget.TryGetValue(mi.Value, out int ti)) continue;
                var c = (JsonObject)npc.DeepClone();
                c["MapIndex"] = JsonValue.Create(ti);
                maxIdx++;
                c["Index"] = JsonValue.Create(maxIdx);
                nArr.Add(c);
                added++;
            }
            File.WriteAllText(targetNpcPath, NormalizeToClean(nArr).ToJsonString(WriteOpts));
            log.Add($"# NPC 完成：新增 NPC {added} 条 -> {targetNpcPath}");
            Console.WriteLine($"NPC 完成：新增 {added} 条 -> {Path.GetFileName(targetNpcPath)}");
        }
        else if (npcOnly)
        {
            log.Add("[NPC] 跳过：目标或 crystal 的 NPCInfo.json 不存在");
            Console.WriteLine("[NPC] 跳过：NPCInfo.json 不存在");
        }

        File.WriteAllText(logPath, string.Join("\n", log));
        Console.WriteLine($"日志: {logPath}");
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

    static JsonArray GetOrCreateArray(JsonObject map, string field)
    {
        if (map[field] is JsonArray arr) return arr;
        if (map[field] is JsonObject jo && jo["$values"] is JsonArray a) return a; // 兼容旧信封
        var newArr = new JsonArray();
        map[field] = newArr;
        return newArr;
    }

    static int CopyArray(JsonObject src, JsonObject dst, string field)
    {
        if (!(src[field] is JsonObject sjo) || !(sjo["$values"] is JsonArray sa)) return 0;
        var da = GetOrCreateArray(dst, field);
        int n = 0;
        foreach (JsonObject item in sa.OfType<JsonObject>()) { da.Add((JsonObject)item.DeepClone()); n++; }
        return n;
    }

    static int CopySafeZones(JsonObject src, JsonObject dst)
    {
        if (!(src["SafeZones"] is JsonObject sjo) || !(sjo["$values"] is JsonArray sa)) return 0;
        var da = GetOrCreateArray(dst, "SafeZones");
        int n = 0;
        // 注意：不再写 Info/$ref，SafeZoneInfo.Info 由服务端加载后手动重新挂回。
        foreach (JsonObject item in sa.OfType<JsonObject>()) { da.Add((JsonObject)item.DeepClone()); n++; }
        return n;
    }

    static int CopyMovements(JsonObject src, JsonObject dst, Dictionary<int, int> idxMap)
    {
        if (!(src["Movements"] is JsonObject sjo) || !(sjo["$values"] is JsonArray sa)) return 0;
        var da = GetOrCreateArray(dst, "Movements");
        int n = 0;
        foreach (JsonObject item in sa.OfType<JsonObject>())
        {
            var mi = item["MapIndex"]?.GetValue<int?>();
            if (!mi.HasValue || !idxMap.TryGetValue(mi.Value, out int ti)) continue;
            var c = (JsonObject)item.DeepClone();
            c["MapIndex"] = JsonValue.Create(ti);
            da.Add(c); n++;
        }
        return n;
    }
}
