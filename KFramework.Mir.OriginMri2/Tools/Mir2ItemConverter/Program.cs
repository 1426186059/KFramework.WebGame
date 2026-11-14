using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mir2ItemConverter;

// 与目标服务器 ItemInfo 反序列化产物同形态的输出 DTO（字段名须与 ItemInfo 公共字段完全一致）
public class ItemInfoOut
{
    public int Index;
    public string Name = "";
    public int Type;
    public int Grade;
    public int RequiredType;
    public int RequiredClass;
    public int RequiredGender;
    public int Set;
    public int Shape;
    public int Weight;
    public int Light;
    public int RequiredAmount;
    public int Image;
    public int Durability;
    public long Price;
    public int StackSize = 1;
    public bool StartItem;
    public int Effect;
    public bool NeedIdentify;
    public bool ShowGroupPickup;
    public bool GlobalDropNotify;
    public bool ClassBased;
    public bool LevelBased;
    public bool CanMine;
    public bool CanFastRun;
    public bool CanAwakening;
    public int Bind;
    public int Unique;
    public int RandomStatsId;
    public string ToolTip = "";
    public int Slots;
    public StatsOut Stats = new();
}

public class StatsOut
{
    public Dictionary<string, int> Values = new();
}

public static class Program
{
    // 原始 item 表中 Name(col2) 为这些值时，表示该物品废弃/未使用，直接丢弃
    private static readonly HashSet<string> UnusedNames = new(StringComparer.Ordinal)
    {
        "没用", "无用", "Unused", "unused", "弃用", "未用"
    };

    // col5 Category(中文类别) -> ItemType 枚举值
    private static readonly Dictionary<string, int> CategoryMap = new()
    {
        { "武器", 1 }, { "兵器", 1 },
        { "衣服", 2 }, { "盔甲", 2 }, { "防具", 2 }, { "铠甲", 2 }, { "战衣", 2 },
        { "头盔", 4 },
        { "项链", 5 },
        { "手镯", 6 },
        { "戒指", 7 }, { "特戒", 7 },
        { "护身符", 8 }, { "护符", 8 }, { "符", 8 },
        { "腰带", 9 },
        { "鞋子", 10 }, { "靴", 10 }, { "靴子", 10 },
        { "宝石", 18 }, { "宝石类", 18 },
        { "药品", 13 }, { "药", 13 }, { "药水", 13 }, { "消耗品", 13 },
        { "材料", 16 }, { "素材", 16 },
        { "书", 20 }, { "技能书", 20 }, { "书籍", 20 },
        { "卷轴", 17 },
        { "坐骑", 19 }, { "马", 19 },
        { "食物", 27 },
        { "时装", 38 }, { "装饰", 38 },
        { "宠物", 36 },
        { "变身", 37 },
        { "矿石", 14 }, { "矿", 14 },
        { "肉", 15 },
        { "其他", 0 }, { "杂项", 0 }, { "杂物", 0 },
    };

    // col5 有 1373 种取值，大量是复合类别（洪荒武器 / 御龙头盔 / 新幻武 / 男衣服 …）。
    // 精确表命中不了时按关键词兜底，否则 Type 会大面积落到 0(Nothing)，
    // 客户端 UseItem() 与服务器 CanEquipItem() 会判定"不是装备"而拒绝穿戴。
    // 顺序即优先级：部位词必须排在泛化词之前。
    private static readonly (string Keyword, int Type)[] CategoryKeywords = new[]
    {
        ("护身符", 8), ("护符", 8),
        ("头盔", 4), ("项链", 5), ("手镯", 6), ("护手", 6),
        ("戒指", 7), ("特戒", 7), ("腰带", 9), ("靴", 10), ("鞋", 10),
        ("武器", 1), ("幻武", 1), ("神兵", 1), ("魔兵", 1), ("兵器", 1),
        ("衣服", 2), ("战衣", 2), ("盔甲", 2), ("铠甲", 2), ("防具", 2),
        ("宝石", 18), ("药水", 13), ("药品", 13), ("卷轴", 17),
        ("技能书", 20), ("书", 20), ("材料", 16), ("时装", 38),
        ("坐骑", 19), ("矿石", 14), ("食物", 27),
    };

    private static int ResolveType(string category)
    {
        if (string.IsNullOrEmpty(category)) return 0;
        if (CategoryMap.TryGetValue(category, out int exact)) return exact;

        foreach (var (keyword, type) in CategoryKeywords)
        {
            if (category.Contains(keyword, StringComparison.Ordinal)) return type;
        }
        return 0;
    }

    public static int Main(string[] args)
    {
        string input = @"C:\Users\Administrator\Downloads\Mir2Config\Json\item.json";
        string output = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Tools\原始传奇-资源提取\migration-preview\ItemInfo.fromOriginal.json";
        // 属性来源已由客户端 JS 核实为 col40，默认开启；--no-stats 可回到空属性。
        bool withStats = true;
        string? attrsPath = null;
        int career = 1; // col40 按 & 分 3 段对应 战/法/道，静态表取其中一段

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--input" && i + 1 < args.Length) input = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) output = args[++i];
            else if (args[i] == "--with-stats") withStats = true;
            else if (args[i] == "--no-stats") withStats = false;
            else if (args[i] == "--career" && i + 1 < args.Length && int.TryParse(args[++i], out int cr)) career = Math.Clamp(cr, 1, 3);
            else if (args[i] == "--dump-attrs" && i + 1 < args.Length) attrsPath = args[++i];
        }

        if (attrsPath != null) return DumpAttributeScore(attrsPath);

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"找不到输入文件: {input}");
            return 1;
        }

        var list = new List<ItemInfoOut>();
        var seen = new HashSet<int>();
        var catCount = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0, skippedUnused = 0, skippedNoIndex = 0, skippedDup = 0, errors = 0;
        int unknownType = 0, iconOverUshort = 0, effectClamped = 0;
        int attrItems = 0, attrSplitItems = 0, attrsDropped = 0;
        int minIdx = int.MaxValue, maxIdx = int.MinValue;

        using var doc = JsonDocument.Parse(File.ReadAllText(input));
        // 既支持"抽取出来的 item.json"（顶层直接是条目），也支持本地 0~5config 分片
        // （顶层是 {"item": {...}}），这样不用再依赖外部下载目录。
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("item", out var itemNode) &&
            itemNode.ValueKind == JsonValueKind.Object)
            root = itemNode;

        foreach (var prop in root.EnumerateObject())
        {
            total++;
            var item = prop.Value;
            try
            {
                if (!TryGetInt(item, "0", out int index) || index <= 0)
                {
                    skippedNoIndex++;
                    continue;
                }

                string name = GetStr(item, "2").Trim();
                // 过滤废弃条目：Name 为“没用/无用”等
                if (string.IsNullOrEmpty(name) || UnusedNames.Contains(name))
                {
                    skippedUnused++;
                    continue;
                }

                if (!seen.Add(index))
                {
                    skippedDup++;
                    continue;
                }

                var outItem = new ItemInfoOut { Index = index, Name = name };

                // Type <- Category(col5)
                string cat = GetStr(item, "5").Trim();
                if (!string.IsNullOrEmpty(cat))
                {
                    catCount.TryGetValue(cat, out int c);
                    catCount[cat] = c + 1;
                }
                outItem.Type = ResolveType(cat);
                if (outItem.Type == 0) unknownType++;

                // RequiredClass <- Career(col52)
                if (TryGetInt(item, "52", out int careerLimit))
                    outItem.RequiredClass = careerLimit == 0 ? 31 : (careerLimit & 0x1F);
                else
                    outItem.RequiredClass = 31;

                // RequiredGender <- Sex(col56)
                if (TryGetInt(item, "56", out int sex))
                    outItem.RequiredGender = sex switch { 1 => 1, 2 => 2, _ => 3 };
                else
                    outItem.RequiredGender = 3;

                // Image <- IconId(col19)  —— 服务器 ItemInfo.Image 已扩为 uint，保留原始大图标 ID（不再截断）
                if (TryGetInt(item, "19", out int icon))
                {
                    if (icon > ushort.MaxValue) iconOverUshort++;
                    outItem.Image = icon < 0 ? 0 : icon;
                }

                // Effect <- EffectId(col18)  —— byte 上限保护
                if (TryGetInt(item, "18", out int eff))
                {
                    if (eff > byte.MaxValue) { effectClamped++; outItem.Effect = byte.MaxValue; }
                    else outItem.Effect = eff;
                }

                // Price <- col22 ("price#cur")
                outItem.Price = ParsePrice(GetStr(item, "22"));

                // ToolTip <- Desc(col42) 去 HTML
                outItem.ToolTip = CleanHtml(GetStr(item, "42"));

                // Stats <- col40 属性串（客户端 JS: getTipsAttr(o[40],...,o[52],...)）
                if (withStats)
                {
                    outItem.Stats.Values = BuildStats(item, career, out bool split, out int dropped);
                    if (outItem.Stats.Values.Count > 0) attrItems++;
                    if (split) attrSplitItems++;
                    attrsDropped += dropped;
                }

                if (index < minIdx) minIdx = index;
                if (index > maxIdx) maxIdx = index;

                list.Add(outItem);
            }
            catch (Exception ex)
            {
                errors++;
                Console.Error.WriteLine($"条目 {prop.Name} 解析失败: {ex.Message}");
            }
        }

        // 保留中文原文：默认 encoder 会把所有非 ASCII 转成 \uXXXX，人眼不可读。
        // UnsafeRelaxedJsonEscaping 仍会正确转义 JSON 结构字符（引号/反斜杠/控制符）。
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        File.WriteAllText(output, JsonSerializer.Serialize(list, opts));

        Console.WriteLine($"输入条目总数      : {total}");
        Console.WriteLine($"跳过-废弃(没用等) : {skippedUnused}");
        Console.WriteLine($"跳过-无Index      : {skippedNoIndex}");
        Console.WriteLine($"跳过-重复Index    : {skippedDup}");
        Console.WriteLine($"解析错误          : {errors}");
        Console.WriteLine($"输出条目数        : {list.Count}");
        Console.WriteLine($"未识别类别(Type=0): {unknownType}");
        Console.WriteLine($"Icon>65535(已保留uint): {iconOverUshort}");
        Console.WriteLine($"Effect>255被截断  : {effectClamped}");
        Console.WriteLine($"带属性条目(col40) : {attrItems}");
        Console.WriteLine($"  其中职业分3段   : {attrSplitItems} (取第 {career} 段: {(career == 1 ? "战" : career == 2 ? "法" : "道")})");
        Console.WriteLine($"丢弃的现代属性   : {attrsDropped}");
        Console.WriteLine($"Index 范围        : {minIdx} .. {maxIdx}");
        Console.WriteLine($"输出文件          : {Path.GetFullPath(output)}");
        Console.WriteLine("类别分布(Top20):");
        foreach (var kv in catCount.OrderByDescending(x => x.Value).Take(20))
            Console.WriteLine($"  {kv.Key}: {kv.Value}");

        // 全量类别分布落盘，便于人工补全映射
        var reportPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".", "category_report.txt");
        using (var w = new StreamWriter(reportPath, false, System.Text.Encoding.UTF8))
        {
            w.WriteLine($"总类别数: {catCount.Count}；未识别(Type=0)条目: {unknownType}");
            foreach (var kv in catCount.OrderByDescending(x => x.Value))
            {
                int resolved = ResolveType(kv.Key);
                w.WriteLine($"{kv.Key}\t{kv.Value}\t{(resolved > 0 ? resolved.ToString() : "??")}");
            }
        }
        Console.WriteLine($"全量类别分布      : {reportPath}");

        return 0;
    }

    // attribute_score 的属性 id -> 服务器 Stat 枚举成员名（只保留经典属性）。
    // 依据 main10.25341.1.min.js：CommonUtils.getTipsAttr(o[40], ..., o[52], ...) 读 col40 属性串，
    //   getAttrMap 按 '|'/'#' 拆成 id->值；isSpecialAttr/getMaxAttrId 硬编码
    //   4/6/20/92/94 = 下限，5/7/21/93/95 = 上限；cm.attribute_score[id] 提供显示名。
    // 其余 id（63~/85~/96~ 等现代特攻/元素/PK 属性）在服务器 Stat 枚举里无对应，丢弃。
    private static readonly Dictionary<string, string> AttrIdToStat = new(StringComparer.Ordinal)
    {
        ["1"] = "HP", ["36"] = "MP",
        ["4"] = "MinDC", ["5"] = "MaxDC",
        ["6"] = "MinAC", ["7"] = "MaxAC",
        ["20"] = "MinMAC", ["21"] = "MaxMAC",
        ["92"] = "MinMC", ["93"] = "MaxMC",
        ["94"] = "MinSC", ["95"] = "MaxSC",
        ["8"] = "Accuracy", ["9"] = "Agility",
        ["28"] = "Luck", ["11"] = "AttackSpeed", ["43"] = "Holy",
    };

    static Dictionary<string, int> BuildStats(JsonElement item, int career, out bool careerSplit, out int dropped)
    {
        var s = new Dictionary<string, int>();
        careerSplit = false;
        dropped = 0;

        // col40 = "id#值|id#值..."，可按 & 分 3 段对应 战/法/道（JS: getAttributeBycareer）
        string raw = GetRaw(item, "40");
        if (string.IsNullOrWhiteSpace(raw)) return s;

        string seg = raw;
        var careerSegs = raw.Split('&');
        if (careerSegs.Length >= 3)
        {
            seg = careerSegs[career - 1];
            careerSplit = true;
        }

        foreach (var pair in seg.Split('|'))
        {
            var kv = pair.Split('#');
            if (kv.Length < 2) continue;
            if (!AttrIdToStat.TryGetValue(kv[0].Trim(), out string? stat)) { dropped++; continue; }
            if (!long.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) || v == 0) continue;
            s[stat] = (int)Math.Clamp(v, int.MinValue, int.MaxValue);
        }
        return s;
    }

    static string GetRaw(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var p)) return "";
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? "",
            JsonValueKind.Number => p.GetRawText(),
            _ => ""
        };
    }

    static int DumpAttributeScore(string path)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"找不到: {path}"); return 1; }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("attribute_score", out var asNode) || asNode.ValueKind != JsonValueKind.Object)
        { Console.Error.WriteLine("未找到 attribute_score"); return 1; }
        Console.WriteLine("id\tname\tdescription");
        foreach (var p in asNode.EnumerateObject())
        {
            string name = "", desc = "";
            if (p.Value.ValueKind == JsonValueKind.Object)
            {
                if (p.Value.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String) name = n.GetString() ?? "";
                if (p.Value.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String) desc = d.GetString() ?? "";
            }
            Console.WriteLine($"{p.Name}\t{name}\t{desc}");
        }
        return 0;
    }

    static int? GetIntN(JsonElement e, string key)
        => TryGetInt(e, key, out int v) ? v : (int?)null;

    static bool TryGetInt(JsonElement e, string key, out int value)
    {
        value = 0;
        if (e.TryGetProperty(key, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) { value = i; return true; }
            if (p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si)) { value = si; return true; }
            }
        }
        return false;
    }

    static string GetStr(JsonElement e, string key)
    {
        if (e.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String) return p.GetString() ?? "";
        return "";
    }

    static long ParsePrice(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var part = s.Split('#')[0].Trim();
        if (ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return (long)Math.Min(v, (ulong)uint.MaxValue);
        return 0;
    }

    static string CleanHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        return s.Trim();
    }
}
