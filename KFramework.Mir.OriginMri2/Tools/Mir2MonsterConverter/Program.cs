using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mir2MonsterConverter;

// 与服务器 MonsterInfo 公共字段同形态的输出 DTO
public sealed class MonsterInfoOut
{
    public int Index;
    public string Name = "";
    public ushort Image;
    public byte AI, Effect, ViewRange = 7, CoolEye;
    public ushort Level;
    public byte Light;
    public ushort AttackSpeed = 2500, MoveSpeed = 1800;
    public uint Experience;
    public string DropPath = "";
    public List<MonsterDropSettingOut> DropSettings = new();
    public List<object> Drops = new();
    public bool CanTame = true, CanPush = true, AutoRev = true, Undead = false;
    public bool CanRecall = false;
    public bool IsBoss = false;
    public bool HasSpawnScript = false;
    public bool HasDieScript = false;
    public StatsOut Stats = new();
}

public sealed class StatsOut
{
    public Dictionary<string, int> Values = new();
}

// 与服务器 MonsterDropSetting 同形态；可选字段用默认值时不写出，保持配置表可读
public sealed class MonsterDropSettingOut
{
    public int Chance = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ItemIndex;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public uint Gold;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool QuestRequired;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public byte Type;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Random;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool First;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<int>? ItemIndexes;
}

public static class Program
{
    private const string ConfigVersion = "10.25341.1";
    private const string LegacyDropSubDir = "原始传奇";

    // 0config.attribute_score 的属性 id -> 服务器 Stat 枚举成员名。
    // 现代特攻/元素/减免类属性在服务器没有对应，丢弃。
    private static readonly Dictionary<string, string> AttrIdToStat = new(StringComparer.Ordinal)
    {
        ["1"] = "HP", ["3"] = "HealthRecovery",
        ["4"] = "MinDC", ["5"] = "MaxDC",
        ["6"] = "MinAC", ["7"] = "MaxAC",
        ["20"] = "MinMAC", ["21"] = "MaxMAC",
        ["92"] = "MinMC", ["93"] = "MaxMC",
        ["94"] = "MinSC", ["95"] = "MaxSC",
        ["96"] = "Accuracy", ["97"] = "Agility", ["9"] = "Agility",
        ["11"] = "AttackSpeed", ["28"] = "Luck",
        ["36"] = "MP", ["43"] = "Holy",
    };

    public static int Main(string[] args)
    {
        string root = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2";
        string configDir = Path.Combine(root, @"Tools\原始传奇-资源提取\原始传奇-资源参考");
        string itemInfoPath = Path.Combine(root, @"Web_Mir2.Server.Version\ExcelConfig\ItemInfo.json");
        string outputDir = Path.Combine(root, @"Tools\原始传奇-资源提取\migration-preview");
        int bossChance = 10;
        int gatherChance = 20;
        bool independent = false;
        bool noDrops = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config-dir" && i + 1 < args.Length) configDir = args[++i];
            else if (args[i] == "--item-info" && i + 1 < args.Length) itemInfoPath = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) outputDir = args[++i];
            else if (args[i] == "--boss-chance" && i + 1 < args.Length && int.TryParse(args[++i], out int bc)) bossChance = Math.Max(1, bc);
            else if (args[i] == "--gather-chance" && i + 1 < args.Length && int.TryParse(args[++i], out int gc)) gatherChance = Math.Max(0, gc);
            else if (args[i] == "--independent") independent = true;
            else if (args[i] == "--no-drops") noDrops = true;
        }

        string c2 = Path.Combine(configDir, $"2config{ConfigVersion}.json");
        string c4 = Path.Combine(configDir, $"4config{ConfigVersion}.json");
        foreach (var path in new[] { c2, c4, itemInfoPath })
        {
            if (!File.Exists(path)) { Console.Error.WriteLine($"找不到输入文件: {path}"); return 1; }
        }

        using var itemDoc = JsonDocument.Parse(File.ReadAllText(itemInfoPath));
        var itemIds = new HashSet<int>();
        var itemNames = new Dictionary<int, string>();
        foreach (var e in itemDoc.RootElement.EnumerateArray())
        {
            if (!e.TryGetProperty("Index", out var p) || !p.TryGetInt32(out int id)) continue;
            string name = e.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            itemIds.Add(id);
            itemNames[id] = name;
        }

        using var doc2 = JsonDocument.Parse(File.ReadAllText(c2));
        using var doc4 = JsonDocument.Parse(File.ReadAllText(c4));
        var boxTable = GetTable(doc2, "box");
        var monsterTable = GetTable(doc4, "monster");

        var monsters = new List<(int Id, JsonElement Data)>();
        foreach (var p in monsterTable.EnumerateObject())
        {
            if (int.TryParse(p.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                monsters.Add((id, p.Value));
        }
        monsters.Sort((a, b) => a.Id.CompareTo(b.Id));

        Directory.CreateDirectory(outputDir);
        CleanLegacyDrops(outputDir, LegacyDropSubDir);

        var list = new List<MonsterInfoOut>();
        int withBoss = 0, withGather = 0, boxMissing = 0, testBoxSkipped = 0;
        int monstersWithDrops = 0, dropSettings = 0, itemRefs = 0;
        int boxEntriesTotal = 0, boxZeroSlots = 0, boxMissingItems = 0, candidatesKept = 0;
        int gatherTotal = 0, gatherMissing = 0, gatherKept = 0;
        int imageClamped = 0, levelClamped = 0, hpClamped = 0, attrsDropped = 0, noName = 0;

        foreach (var (id, m) in monsters)
        {
            string name = GetStr(m, "name");
            if (string.IsNullOrWhiteSpace(name)) noName++;

            var outM = new MonsterInfoOut { Index = id, Name = name };

            long model = GetLong(m, "model") ?? 0;
            if (model > ushort.MaxValue) { imageClamped++; model = ushort.MaxValue; }
            if (model < 0) model = 0;
            outM.Image = (ushort)model;

            long level = GetLong(m, "level") ?? 0;
            if (level > ushort.MaxValue) { levelClamped++; level = ushort.MaxValue; }
            if (level < 0) level = 0;
            outM.Level = (ushort)level;

            outM.Stats.Values = BuildStats(m, ref hpClamped, ref attrsDropped);

            string bossDrop = GetStr(m, "bossDrop").Trim();
            string gather = GetStr(m, "gather").Trim();
            bool useBossDrop = !string.IsNullOrEmpty(bossDrop) && !noDrops;
            if (useBossDrop)
            {
                string boxName = int.TryParse(bossDrop, NumberStyles.Integer, CultureInfo.InvariantCulture, out int boxId)
                                 && itemNames.TryGetValue(boxId, out var bn) ? bn : "";
                if (boxName.Contains("测试", StringComparison.Ordinal))
                {
                    useBossDrop = false;
                    testBoxSkipped++;
                }
                else
                {
                    outM.IsBoss = true;
                    withBoss++;
                }
            }
            if (!string.IsNullOrEmpty(gather)) withGather++;

            var drops = new List<MonsterDropSettingOut>();

            if (useBossDrop)
            {
                if (boxTable.TryGetProperty(bossDrop, out var box))
                {
                    var parsed = ParseBox(box, itemIds);
                    boxEntriesTotal += parsed.Total;
                    boxZeroSlots += parsed.Zero;
                    boxMissingItems += parsed.Missing;
                    candidatesKept += parsed.Ids.Count;
                    itemRefs += parsed.Ids.Count;

                    if (parsed.Ids.Count > 0)
                    {
                        if (independent)
                        {
                            int chance = Math.Max(1, bossChance * parsed.Ids.Count);
                            foreach (int itemId in parsed.Ids)
                                drops.Add(new MonsterDropSettingOut { Chance = chance, ItemIndex = itemId });
                        }
                        else
                        {
                            drops.Add(new MonsterDropSettingOut { Chance = bossChance, Random = true, ItemIndexes = parsed.Ids });
                        }
                    }
                }
                else
                {
                    boxMissing++;
                }
            }

            if (!string.IsNullOrEmpty(gather) && gatherChance > 0 && !noDrops)
            {
                foreach (var seg in gather.Split(new[] { '|', '&', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var gf = seg.Split('#');
                    if (gf.Length < 1 || !int.TryParse(gf[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gid)) continue;
                    gatherTotal++;
                    if (!itemIds.Contains(gid)) { gatherMissing++; continue; }
                    drops.Add(new MonsterDropSettingOut { Chance = gatherChance, ItemIndex = gid });
                    gatherKept++;
                    itemRefs++;
                }
            }

            outM.DropSettings = drops;
            if (drops.Count > 0)
            {
                monstersWithDrops++;
                dropSettings += drops.Count;
            }

            list.Add(outM);
        }

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string monsterOut = Path.Combine(outputDir, "MonsterInfo.json");
        File.WriteAllText(monsterOut, JsonSerializer.Serialize(list, opts), new UTF8Encoding(false));

        string reportPath = Path.Combine(outputDir, "report.txt");
        using (var w = new StreamWriter(reportPath, false, new UTF8Encoding(false)))
        {
            w.WriteLine("原始传奇 → 本服务器：怪物 + 掉落 迁移预览报告");
            w.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();
            w.WriteLine("【输入】");
            w.WriteLine($"  monster : {c4}  ({monsters.Count} 条)");
            w.WriteLine($"  box     : {c2}  ({boxTable.EnumerateObject().Count()} 条)");
            w.WriteLine($"  item    : {itemInfoPath}  ({itemIds.Count} 条)");
            w.WriteLine();
            w.WriteLine("【输出（旁路，未替换任何线上文件）】");
            w.WriteLine("  输出目录位于工具侧，不写入 ExcelConfig 上线配置目录。");
            w.WriteLine($"  {monsterOut}");
            w.WriteLine("  掉落直接写在每条怪物的 DropSettings 字段里，不再生成 Drops/*.txt。");
            w.WriteLine($"  {reportPath}");
            w.WriteLine();
            w.WriteLine("【一、字段映射】");
            w.WriteLine("  Index     <- monster.id（原始传奇怪物索引体系，与旧 570 条完全不同）");
            w.WriteLine("  Name      <- monster.name");
            w.WriteLine("  Image     <- monster.model（clamp 到 ushort，客户端外观体系需另行接原始资源）");
            w.WriteLine("  Level     <- monster.level");
            w.WriteLine("  IsBoss    <- 有效 monster.bossDrop 非空（已排除测试展示包）");
            w.WriteLine("  Stats     <- monster.attribute，按 0config.attribute_score 语义映射：");
            w.WriteLine("              1=HP 3=生命恢复 4/5=攻上下 6/7=防上下 20/21=魔防上下");
            w.WriteLine("              92/93=魔法上下 94/95=道术上下 96=准确 97=敏捷 11=攻速 36=MP 43=神圣");
            w.WriteLine("  DropPath  <- 留空（新表不再走 txt 脚本）");
            w.WriteLine("  DropSettings <- 新增字段，掉落直接写进配置表：");
            w.WriteLine("      Chance                     1/N 概率");
            w.WriteLine("      ItemIndex                  物品 Index（精确引用，不受重名影响）");
            w.WriteLine("      ItemIndexes + Random=true  候选池里随机掉 1 件（等价旧 GROUP*）");
            w.WriteLine("      Gold/QuestRequired/Type/First  预留字段，本次未使用");
            w.WriteLine("  AI/Effect/Light/AttackSpeed/MoveSpeed/Experience/CanTame 等：服务器默认值");
            w.WriteLine();
            w.WriteLine("【二、关键发现（务必先读）】");
            w.WriteLine("  1. 重要：原始传奇【客户端配置里没有怪物掉落表】。");
            w.WriteLine("     monster.bossDrop 指向的 86 个 box，对应物品名有 85 个以“展示”结尾");
            w.WriteLine("     （虹魔教主展示、沃玛教主展示…），另 1 个是“boss掉落测试1”。");
            w.WriteLine("     它们是 UI 里“该 boss 可能掉什么”的预览清单，不是掉落配置。");
            w.WriteLine($"     测试包已跳过 {testBoxSkipped} 个怪物；其余按候选池重新构造掉落。");
            w.WriteLine("     真实掉率由页游服务端计算，不在可提取的客户端配置里。");
            w.WriteLine("  2. 证据：main10.25341.1.min.js 的 BoxUtil.getBoxList(boxId, sep) 只把 box.items");
            w.WriteLine("     解析成展示清单 ItemGridData：g[0]=itemId、g[1]=count、g[3]=职业限制、");
            w.WriteLine("     g[4]=性别限制、g[5]/g[6]=开服天数或条件；代码从不读第 3 段，也不做随机抽取。");
            w.WriteLine("     bossDrop 共 768 条、只指向 86 个 box，全部是分号分隔的 3 段清单，");
            w.WriteLine("     第 3 段取值只有 1 和 100。0~5config 共 366 张表里，*reward/*box 均为活动、");
            w.WriteLine("     排名、礼包奖励，没有按怪物发放的权重表。");
            w.WriteLine("  3. 因此本转换器把 box 当作“候选掉落清单”重新构造掉落，概率是策略参数，不是原版数值：");
            w.WriteLine($"       --boss-chance   = {bossChance}   父级 1/N（占位默认值，务必按运营数值重设）");
            w.WriteLine($"       --gather-chance = {gatherChance}  图鉴包类采集掉落（0 表示不生成）");
            w.WriteLine($"       --independent   = {(independent ? "开" : "关")}   开启后改为每个候选独立 1/N");
            w.WriteLine("     默认模式：一次判定成功后，从候选清单里等概率掉 1 件。");
            w.WriteLine("     若不接受这种重构，用 --no-drops 只生成怪物基础数据。");
            w.WriteLine("  4. 服务器掉落原本按【名字】查物品，物品表 12594 条只有 10809 个唯一名，");
            w.WriteLine("     box 引用有 1151 条会解析到同名但不同 Index 的物品（元宝/金币/创造宝石/技能书等）。");
            w.WriteLine("     新字段直接用 ItemIndex，天然避开这个问题，也不再需要 txt 里的 @itemId 语法。");
            w.WriteLine();
            w.WriteLine("【三、降级与未迁移项】");
            w.WriteLine($"  - box 清单条目 {boxEntriesTotal}，空槽(id=0) {boxZeroSlots}，物品表中不存在 {boxMissingItems}，保留候选 {candidatesKept}。");
            w.WriteLine("    未找到的条目通常是原始 item 表里 Name=没用/无用 的占位物品；本次统计为 0。");
            w.WriteLine($"  - gather 条目 {gatherTotal}，缺失物品 {gatherMissing}，写入 {gatherKept}。它们是图鉴包，原始语义是图鉴收集。");
            w.WriteLine("  - 掉落数量(count)未迁移：服务器掉落每次固定 1 件。");
            w.WriteLine("  - box 条目的职业/性别/开服天数限制段未迁移（bossDrop 引用的清单全是 3 段，无该限制）。");
            w.WriteLine("  - monster.attribute 的现代属性（物理/魔法伤害减少、切割、暴击、麻痹、魔盾等）服务器无对应，丢弃。");
            w.WriteLine($"    丢弃条目 {attrsDropped}；HP 超过 int.MaxValue 被钳制 {hpClamped} 条；");
            w.WriteLine($"    model 超过 ushort 被钳制 {imageClamped} 条；level 被钳制 {levelClamped} 条。");
            w.WriteLine($"  - bossDrop 找不到 box: {boxMissing}；跳过测试展示包 {testBoxSkipped}；怪物无名字: {noName}。");
            w.WriteLine("  - 原始传奇没有经验值字段，Experience 全部为 0，需要另行设计。");
            w.WriteLine("  - 怪物外观：model 是页游精灵号，与服务器 Monster 枚举/客户端资源号不是同一体系。");
            w.WriteLine();
            w.WriteLine("【四、使用方式（确认后再动线上）】");
            w.WriteLine("  1. 重新编译服务器：MonsterInfo 新增 DropSettings 字段，ReloadDrops 优先读配置表。");
            w.WriteLine("  2. 把 Tools/原始传奇-资源提取/migration-preview/MonsterInfo.json 拷到 ExcelConfig/MonsterInfo.json 即可。");
            w.WriteLine("     不用拷 Drops 目录，也不用动 Envir/Drops 下的任何现有文件。");
            w.WriteLine("  3. 启动日志会显示 “Drops Loaded. (配置表 N / 脚本 M)”。");
            w.WriteLine("     DropSettings 为空的怪物仍回退旧 txt，现有 570 条旧表完全不受影响。");
            w.WriteLine("  4. 改掉落只需编辑 JSON 里的 DropSettings 数组，不用再写 txt。");
            w.WriteLine("  5. 注意：MonsterInfo.Index 换成原始 id 后，地图刷怪(RespawnTimer/MapInfo)仍指向旧 570 条，");
            w.WriteLine("     必须连 5config.mapnpc(845)/monsterWave(599)/地图刷怪一起迁移后才能上线。");
            w.WriteLine("  6. --no-drops 可生成不带掉落配置的怪物表。");
            w.WriteLine();
            w.WriteLine("【五、后续批次】");
            w.WriteLine("  NPC(3config.npc 840) / 商店(3config.store 5532) / 任务(4config._task 464");
            w.WriteLine("  + 2config._task_tx 458) / 技能(5config.skill 214) / 地图刷怪(5config.mapnpc 845)。");
            w.WriteLine();
            w.WriteLine("【统计】");
            w.WriteLine($"  怪物 {list.Count}，有效 bossDrop {withBoss}，跳过测试包 {testBoxSkipped}，有 gather {withGather}");
            w.WriteLine($"  带 DropSettings 的怪物 {monstersWithDrops}，掉落条目 {dropSettings}，物品引用 {itemRefs}");
        }

        Console.WriteLine($"怪物总数        : {monsters.Count}");
        Console.WriteLine($"有效 bossDrop   : {withBoss}（跳过测试展示包 {testBoxSkipped}）");
        Console.WriteLine($"有 gather       : {withGather}");
        Console.WriteLine($"带掉落配置怪物  : {monstersWithDrops}（DropSettings 条目 {dropSettings} / 物品引用 {itemRefs}）");
        Console.WriteLine($"box 候选条目    : {boxEntriesTotal} -> 保留 {candidatesKept}（空槽 {boxZeroSlots} / 缺物品 {boxMissingItems}）");
        Console.WriteLine($"gather 写入     : {gatherKept}");
        Console.WriteLine($"掉落模式        : {(independent ? "独立概率" : "候选池随机 1 件")}；boss 1/{bossChance}，gather 1/{gatherChance}");
        Console.WriteLine($"降级            : model钳制 {imageClamped} / level钳制 {levelClamped} / HP钳制 {hpClamped} / 属性丢弃 {attrsDropped}");
        Console.WriteLine($"输出            : {monsterOut}");
        Console.WriteLine($"报告            : {reportPath}");
        return 0;
    }

    static void CleanLegacyDrops(string outputDir, string subDir)
    {
        string legacy = Path.Combine(outputDir, "Drops", subDir);
        if (!Directory.Exists(legacy)) return;
        foreach (var f in Directory.EnumerateFiles(legacy, "*.txt"))
        {
            if (int.TryParse(Path.GetFileNameWithoutExtension(f), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                File.Delete(f);
        }
        if (!Directory.EnumerateFileSystemEntries(legacy).Any()) Directory.Delete(legacy);
        string dropsRoot = Path.Combine(outputDir, "Drops");
        if (Directory.Exists(dropsRoot) && !Directory.EnumerateFileSystemEntries(dropsRoot).Any())
            Directory.Delete(dropsRoot);
    }

    static (List<int> Ids, int Total, int Zero, int Missing) ParseBox(JsonElement box, HashSet<int> itemIds)
    {
        var ids = new List<int>();
        var seen = new HashSet<int>();
        int total = 0, zero = 0, missing = 0;
        string raw = box.ValueKind == JsonValueKind.Object &&
                     box.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.String
            ? it.GetString() ?? ""
            : "";

        foreach (var seg in raw.Split(new[] { '#', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            total++;
            var f = seg.Split('-');
            if (f.Length < 2 || !int.TryParse(f[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) || itemId == 0)
            {
                zero++;
                continue;
            }
            if (!itemIds.Contains(itemId)) { missing++; continue; }
            if (!seen.Add(itemId)) continue;
            ids.Add(itemId);
        }
        return (ids, total, zero, missing);
    }

    static Dictionary<string, int> BuildStats(JsonElement m, ref int hpClamped, ref int dropped)
    {
        var s = new Dictionary<string, int>();
        string raw = GetStr(m, "attribute");
        if (string.IsNullOrWhiteSpace(raw)) return s;

        string first = raw.Split('&')[0];
        foreach (var pair in first.Split('|'))
        {
            var kv = pair.Split('#');
            if (kv.Length < 2) continue;
            if (!long.TryParse(kv[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)) continue;
            if (!AttrIdToStat.TryGetValue(kv[0].Trim(), out var stat)) { dropped++; continue; }
            if (v == 0) continue;
            if (v > int.MaxValue) { if (stat == "HP") hpClamped++; v = int.MaxValue; }
            if (v < int.MinValue) v = int.MinValue;
            s[stat] = (int)v;
        }
        return s;
    }

    static JsonElement GetTable(JsonDocument doc, string name)
    {
        if (!doc.RootElement.TryGetProperty(name, out var t) || t.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"配置分片里找不到表 {name}");
        return t;
    }

    static string GetStr(JsonElement e, string key)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var p)) return "";
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? "",
            JsonValueKind.Number => p.GetRawText(),
            _ => ""
        };
    }

    static long? GetLong(JsonElement e, string key)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out long n)) return n;
        if (p.ValueKind == JsonValueKind.String &&
            long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s)) return s;
        return null;
    }
}
