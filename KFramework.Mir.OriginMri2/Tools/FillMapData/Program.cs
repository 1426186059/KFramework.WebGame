using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// 桥接导入：真库(463 英文地图) -> MapInfo_Fix(607 中文地图)。
/// 思路（按用户要求，英文猜中文 + 模糊搜）：
///   1. 把真库每张英文地图名翻译成候选中文名；
///   2. 与目标库的中文 Title 做模糊匹配（互含 / 关键片段重叠）；
///   3. 命中则把 安全区/怪物/传送门 一条条 MERGE 进目标地图（不覆盖整文件）。
/// 同时修正 $id 唯一、安全区 Info.$ref 指向本图、传送门 MapIndex 按桥接翻译。
/// 输出每一张真库地图的[英文名|候选中文|最佳匹配目标|分数]，便于人工补字典。
/// </summary>
class Program
{
    static int _nextId;
    // 英文/罗马化片段 -> 中文
    static readonly Dictionary<string, string> Dict = new(StringComparer.OrdinalIgnoreCase)
    {
        // 地名根
        {"Bichon","比奇"},{"Mongchon","盟重"},{"Woomyon","沃玛"},{"Oma","沃玛"},{"Zuma","祖玛"},
        {"Serpent","蛇"},{"Prajna","般若"},{"CastleGi-Ryoong","基龙"},{"Gi-Ryoong","基龙"},{"Past","废弃"},
        {"Treepath","林间"},{"CraftingVillage","工匠村"},{"S_Valley","山谷"},{"Valley","谷"},{"Gorge","峡谷"},
        {"Pass","关口"},{"Woods","森林"},{"Forest","森林"},{"Island","岛"},{"Province","省"},{"Field","野外"},
        {"Cave","洞"},{"Mine","矿"},{"Tomb","墓"},{"Temple","寺庙"},{"Mountain","山"},
        {"North","北"},{"South","南"},{"East","东"},{"West","西"},
        // 建筑/功能
        {"MeatStore","肉铺"},{"WeaponStore","武器店"},{"Library","书店"},{"AccessoryStore","饰品店"},
        {"DrapersStore","布店"},{"BeautySaloon","美容院"},{"ReagentStore","药剂店"},{"MedicineRoom","药房"},
        {"Drug_Store","药店"},{"Inn","客栈"},{"2FofInn","客栈二楼"},{"Tavern","酒馆"},{"Blacksmith","铁匠铺"},
        {"Warehouse","仓库"},{"GroceryStore","杂货店"},{"Prison","监狱"},{"Palace","宫殿"},{"PrivateHouse","民房"},
        {"Barracks","兵营"},{"Kitchen","厨房"},{"Academy","学院"},{"MageHouse","魔法屋"},{"Mill","磨坊"},
        {"BorderRestaurant","边界餐厅"},{"ContestGround","比武场"},{"Lobby","前厅"},{"Ancient","古"},{"ZumaLobby","祖玛阁"},
        {"ArchersHideout","弓手洞"},{"Rev.Tao_Office","道馆"},{"Laundry","洗衣房"},{"Item_Lab","实验室"},
        {"Armory","军械库"},{"Residence","住宅"},{"Hideout","洞"},{"Village","村"},{"Ground","场"},
        {"Restaurant","餐厅"},{"Store","店"},{"House","屋"},{"Room","房"},{"Office","室"},{"LobbyofZumaTemple","祖玛寺庙前厅"},
    };

    static readonly char[] Splitters = { ' ', '_', '.', '-', '(', ')' };

    static string Translate(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        // 先整名命中
        if (Dict.TryGetValue(name, out var whole)) return whole;
        // 按大小写/分隔符切词
        var tokens = Regex.Split(name, @"(?<=[a-z])(?=[A-Z])"); // camelCase 切分
        var sb = new System.Text.StringBuilder();
        foreach (var t in tokens)
        {
            var tk = t.Trim(Splitters);
            if (tk.Length == 0) continue;
            if (Dict.TryGetValue(tk, out var zh)) sb.Append(zh);
            else sb.Append(tk); // 未识别保留原文（多为数字/缩写）
        }
        return sb.ToString();
    }

    // 模糊匹配：候选中文 c 与目标标题 t 的互含得分
    static int Score(string c, string t)
    {
        if (string.IsNullOrEmpty(c) || string.IsNullOrEmpty(t)) return 0;
        if (t.Contains(c)) return c.Length * 2;
        if (c.Contains(t)) return t.Length * 2;
        // 关键片段重叠（取较长公共子串）
        int best = 0;
        for (int i = 0; i < c.Length; i++)
            for (int j = i + 1; j <= c.Length; j++)
            {
                var sub = c.Substring(i, j - i);
                if (sub.Length >= 2 && t.Contains(sub)) best = Math.Max(best, sub.Length);
            }
        return best;
    }

    static void Main()
    {
        string targetPath = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version\Maps\MapInfo_Fix.json";
        string tempPath = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\MirDBToJson\_originbin\MapInfo.json";

        var target = JsonNode.Parse(File.ReadAllText(targetPath, Encoding.UTF8));
        var temp = JsonNode.Parse(File.ReadAllText(tempPath, Encoding.UTF8));
        _nextId = MaxId(target) + 1;

        var realByFile = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var realIndexToFile = new Dictionary<int, string>();
        foreach (JsonObject m in (JsonArray)temp["$values"])
        {
            string fn = m["FileName"]?.GetValue<string>() ?? "";
            int idx = m["Index"]?.GetValue<int>() ?? -1;
            if (!string.IsNullOrEmpty(fn) && !realByFile.ContainsKey(fn)) realByFile[fn] = m;
            if (idx >= 0 && !realIndexToFile.ContainsKey(idx)) realIndexToFile[idx] = fn;
        }

        // 目标：Title -> 地图节点（取第一个）
        var targetByTitle = new Dictionary<string, JsonObject>();
        foreach (JsonObject m in (JsonArray)target["$values"])
        {
            string ti = (m["Title"]?.GetValue<string>() ?? "").Trim();
            if (!string.IsNullOrEmpty(ti) && !targetByTitle.ContainsKey(ti)) targetByTitle[ti] = m;
        }

        int filled = 0, sz = 0, rp = 0, mv = 0;
        var log = new List<string>();
        foreach (var kv in realByFile)
        {
            var src = kv.Value;
            string enName = kv.Key;
            string cn = Translate(enName);
            if (string.IsNullOrEmpty(cn)) cn = Translate(src["Title"]?.GetValue<string>() ?? "");

            // 找最佳目标
            JsonObject bestTarget = null; int bestScore = 0; string bestTitle = "";
            foreach (var tt in targetByTitle)
            {
                int s = Score(cn, tt.Key);
                if (s > bestScore) { bestScore = s; bestTarget = tt.Value; bestTitle = tt.Key; }
            }

            if (bestTarget != null && bestScore >= 2)
            {
                string mapId = bestTarget["$id"]?.GetValue<string>() ?? "";
                if (string.IsNullOrEmpty(mapId)) { mapId = (_nextId++).ToString(); bestTarget["$id"] = JsonValue.Create(mapId); }
                foreach (var field in new[] { "SafeZones", "Respawns", "Movements" })
                {
                    if (src[field] is JsonObject srcField)
                    {
                        var clone = srcField.DeepClone();
                        Reassign(clone, mapId);
                        if (field == "Movements") RemapMovementDest(clone, realIndexToFile, targetByTitle);
                        bestTarget[field] = clone;
                        int c = ((JsonArray)clone["$values"]).Count;
                        if (field == "SafeZones") sz += c; else if (field == "Respawns") rp += c; else if (field == "Movements") mv += c;
                    }
                }
                filled++;
                log.Add($"[MATCH {bestScore}] {enName} -> {bestTitle}");
            }
            else
            {
                log.Add($"[NOMATCH] {enName} | 候选中文:{cn}");
            }
        }

        var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(targetPath, target.ToJsonString(opts), Encoding.UTF8);

        File.WriteAllLines(@"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\MirDBToJson\bridge_log.txt", log, Encoding.UTF8);
        Console.WriteLine($"真库地图={realByFile.Count}  桥接命中并填充={filled}");
        Console.WriteLine($"新增: 安全区={sz} 刷怪={rp} 传送门={mv}");
        Console.WriteLine("未命中清单见 bridge_log.txt (NOMATCH 行)");
    }

    static int RemapMovementDest(JsonNode node, Dictionary<int, string> realIndexToFile, Dictionary<string, JsonObject> targetByTitle)
    {
        int remapped = 0;
        if (node is JsonObject o)
        {
            if (o.ContainsKey("MapIndex") && o["MapIndex"] is JsonValue mv && mv.TryGetValue<int>(out int realIdx))
            {
                if (realIndexToFile.TryGetValue(realIdx, out var fn) && targetByTitle.TryGetValue(fn, out var destMap))
                {
                    int ti = destMap["Index"]?.GetValue<int>() ?? -1;
                    if (ti >= 0 && ti != realIdx) { o["MapIndex"] = JsonValue.Create(ti); remapped++; }
                }
            }
            foreach (var kv in o) remapped += RemapMovementDest(kv.Value, realIndexToFile, targetByTitle);
        }
        else if (node is JsonArray a) foreach (var item in a) remapped += RemapMovementDest(item, realIndexToFile, targetByTitle);
        return remapped;
    }

    static void Reassign(JsonNode node, string mapId)
    {
        if (node is JsonObject o)
        {
            if (o.ContainsKey("$id")) o["$id"] = JsonValue.Create((_nextId++).ToString());
            if (o.ContainsKey("Info") && o["Info"] is JsonObject info && info.ContainsKey("$ref")) info["$ref"] = JsonValue.Create(mapId);
            foreach (var kv in o) Reassign(kv.Value, mapId);
        }
        else if (node is JsonArray a) foreach (var item in a) Reassign(item, mapId);
    }

    static int MaxId(JsonNode n)
    {
        int m = 0;
        if (n is JsonObject o)
        {
            if (o.TryGetPropertyValue("$id", out var idn) && idn is JsonValue jv)
            {
                if (jv.TryGetValue<int>(out int v)) m = v;
                else if (jv.TryGetValue<string>(out string s) && int.TryParse(s, out v)) m = v;
            }
            foreach (var kv in o) m = Math.Max(m, MaxId(kv.Value));
        }
        else if (n is JsonArray a) foreach (var item in a) m = Math.Max(m, MaxId(item));
        return m;
    }
}
