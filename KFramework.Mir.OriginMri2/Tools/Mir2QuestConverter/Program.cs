using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mir2QuestConverter;

// 与服务器 QuestInfo 公共字段同形态的输出 DTO。
// 任务正文不再放 Envir/Quests/*.txt，而是内嵌到 OriginSettings。
public sealed class QuestInfoOut
{
    public int Index;
    public string Name = "", Group = "", FileName = "";
    public string GotoMessage = "", KillMessage = "", ItemMessage = "", FlagMessage = "";
    public List<string> Description = new();
    public List<string> TaskDescription = new();
    public List<string> ReturnDescription = new();
    public List<string> CompletionDescription = new();
    public List<string> PlayerTaskTalk = new();
    public int RequiredMinLevel = 1, RequiredMaxLevel = 255, RequiredQuest = 0;
    public byte RequiredClass = 31;
    public byte Type = 0;
    public int TimeLimitInSeconds = 0;
    public List<object> CarryItems = new();
    public List<object> KillTasks = new();
    public List<object> ItemTasks = new();
    public List<object> FlagTasks = new();
    public uint GoldReward, ExpReward, CreditReward;
    public List<object> FixedRewards = new();
    public List<object> SelectRewards = new();
    public QuestOriginSettingsOut OriginSettings = new();
}

public sealed class QuestOriginSettingsOut
{
    public int OriginTaskId;
    public string Source = "";

    public int StartNpcId, FinishNpcId;
    public string StartNpcName = "", FinishNpcName = "";
    public int NextQuestId, RequiredQuestId;

    public int TaskGroupId, OriginalType;
    public string Condition = "", ConditionText = "";

    public string Rewards = "", RewardText = "", Tip3 = "", CompleteInfo = "";

    public int GoalId, GoalType, GoalCount, GoalMapId, GoalX, GoalY;
    public string GoalInfo = "", GoalParams = "";
    public bool GoalMultiTarget;

    // 按 Index 精确引用，避免按名字查找重名物品/怪物。
    public List<QuestOriginRewardOut> FixedRewards = new();
    public List<QuestOriginKillOut> KillTasks = new();

    // 2config._task_tx 是同一套 id 的另一条任务线文本，按 id 合并为备选文本，不另占 Index。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public QuestOriginAlternateOut? Alternate;

    // 原始行全部字段（已语义化的除外），保证零丢失。
    public Dictionary<string, string> Raw = new();
}

public sealed class QuestOriginRewardOut
{
    public int ItemIndex;
    public uint Count;
    public string ItemName = "";
    public bool ItemResolved;
}

public sealed class QuestOriginKillOut
{
    public int MonsterIndex;
    public int Count;
    public string MonsterName = "";
    public bool MonsterResolved;
}

public sealed class QuestOriginAlternateOut
{
    public string Name = "", Tip3 = "", RewardText = "", CompleteInfo = "", Rewards = "";
    public int StartNpcId, FinishNpcId;
}

public static class Program
{
    private const string ConfigVersion = "10.25341.1";

    private const int PseudoItemExp = 4;    // 主角经验
    private const int PseudoItemGold = 499; // 金币

    private static readonly HashSet<string> SemanticKeys = new(StringComparer.Ordinal)
    {
        "id", "name", "next", "condition", "rewards", "reward", "fnpc", "tnpc",
        "tip3", "completeinfo", "goals", "type", "taskgroupid"
    };

    private sealed record ConditionRow(int Type, string Param, string Txt);
    private sealed record GoalRow(int Type, string Param, int Count, string Info, int MapId, int X, int Y);

    public static int Main(string[] args)
    {
        string root = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2";
        string configDir = Path.Combine(root, @"Tools\原始传奇-资源提取\原始传奇-资源参考");
        string outputDir = Path.Combine(root, @"Tools\原始传奇-资源提取\migration-preview");
        string excelDir = Path.Combine(root, @"Web_Mir2.Server.Version\ExcelConfig");

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config-dir" && i + 1 < args.Length) configDir = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) outputDir = args[++i];
            else if (args[i] == "--excel-config" && i + 1 < args.Length) excelDir = args[++i];
        }

        string c0 = Path.Combine(configDir, $"0config{ConfigVersion}.json");
        string c2 = Path.Combine(configDir, $"2config{ConfigVersion}.json");
        string c4 = Path.Combine(configDir, $"4config{ConfigVersion}.json");
        string c5 = Path.Combine(configDir, $"5config{ConfigVersion}.json");
        string itemPath = Path.Combine(excelDir, "ItemInfo.json");
        string monsterPath = Path.Combine(excelDir, "MonsterInfo.json");
        string npcPreviewPath = Path.Combine(outputDir, "NPCInfo.json");
        foreach (var path in new[] { c0, c2, c4, c5, itemPath, monsterPath, npcPreviewPath })
        {
            if (!File.Exists(path)) { Console.Error.WriteLine($"找不到输入文件: {path}"); return 1; }
        }

        using var doc0 = Parse(c0);
        using var doc2 = Parse(c2);
        using var doc4 = Parse(c4);
        using var doc5 = Parse(c5);
        using var docItem = Parse(itemPath);
        using var docMonster = Parse(monsterPath);
        using var docNpc = Parse(npcPreviewPath);

        var conditions = new Dictionary<int, ConditionRow>();
        foreach (var e in GetTable(doc0, "conditions").EnumerateObject())
        {
            if (!int.TryParse(e.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
            conditions[id] = new ConditionRow(
                TryInt(e.Value, "conditionType", out int ct) ? ct : 0,
                GetStr(e.Value, "conditionParam"),
                Clean(GetStr(e.Value, "txt")));
        }

        var mapNames = new Dictionary<int, string>();
        foreach (var e in GetTable(doc0, "map").EnumerateObject())
        {
            if (!int.TryParse(e.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
            mapNames[id] = Clean(GetStr(e.Value, "name"));
        }

        var goals = new Dictionary<int, GoalRow>();
        foreach (var e in GetTable(doc5, "goals").EnumerateObject())
        {
            if (!int.TryParse(e.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
            goals[id] = new GoalRow(
                TryInt(e.Value, "type", out int gt) ? gt : 0,
                GetStr(e.Value, "param"),
                TryInt(e.Value, "count", out int gc) ? gc : 1,
                Clean(GetStr(e.Value, "goInfo")),
                TryInt(e.Value, "mapId", out int gm) ? gm : 0,
                TryInt(e.Value, "x", out int gx) ? gx : 0,
                TryInt(e.Value, "y", out int gy) ? gy : 0);
        }

        var itemNames = ReadIndexNames(docItem.RootElement);
        var monsterNames = ReadIndexNames(docMonster.RootElement);
        var npcNames = ReadIndexNames(docNpc.RootElement);
        var npcMapIndex = new Dictionary<int, int>();
        foreach (var e in docNpc.RootElement.EnumerateArray())
        {
            if (e.TryGetProperty("Index", out var pi) && pi.TryGetInt32(out int idx) &&
                e.TryGetProperty("MapIndex", out var pm) && pm.TryGetInt32(out int mapIdx))
                npcMapIndex[idx] = mapIdx;
        }

        var taskTable = GetTable(doc4, "_task");
        var altTable = GetTable(doc2, "_task_tx");
        var tasks = new List<(int Id, JsonElement Data)>();
        foreach (var e in taskTable.EnumerateObject())
        {
            if (int.TryParse(e.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                tasks.Add((id, e.Value));
        }
        tasks.Sort((a, b) => a.Id.CompareTo(b.Id));

        var alternates = new Dictionary<int, JsonElement>();
        foreach (var e in altTable.EnumerateObject())
        {
            if (int.TryParse(e.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                alternates[id] = e.Value;
        }

        // next 是「下一个任务 id」，反推服务器 RequiredQuest（需要先完成的前置任务）。
        var prevByNext = new Dictionary<int, int>();
        foreach (var (id, o) in tasks)
        {
            if (!TryInt(o, "next", out int next) || next <= 0) continue;
            if (!prevByNext.ContainsKey(next)) prevByNext[next] = id;
        }

        Directory.CreateDirectory(outputDir);
        var list = new List<QuestInfoOut>();
        int noName = 0, withAlternate = 0, withGoal = 0, withSingleKill = 0, withMultiKill = 0;
        int withTip3 = 0, withRewardText = 0, withCompleteInfo = 0, withNpcRefs = 0;
        int rewardItemRefs = 0, rewardItemHit = 0, killRefs = 0, killHit = 0;
        int npcRefs = 0, npcRefsHit = 0, condRefs = 0, condRefsHit = 0, highLevelGate = 0;
        uint totalGold = 0, totalExp = 0;

        foreach (var (id, o) in tasks)
        {
            string name = Clean(GetStr(o, "name"));
            if (string.IsNullOrEmpty(name)) { name = "任务" + id.ToString(CultureInfo.InvariantCulture); noName++; }

            int startNpc = TryInt(o, "fnpc", out int fn) ? fn : 0;
            int finishNpc = TryInt(o, "tnpc", out int tn) ? tn : 0;
            if (startNpc != 0 || finishNpc != 0) withNpcRefs++;
            foreach (int nid in new[] { startNpc, finishNpc })
            {
                if (nid == 0) continue;
                npcRefs++;
                if (npcNames.ContainsKey(nid)) npcRefsHit++;
            }

            string tip3 = Clean(GetStr(o, "tip3"));
            string completeInfo = Clean(GetStr(o, "completeinfo"));
            string rewardText = Clean(GetStr(o, "reward"));
            string rewards = GetStr(o, "rewards");
            if (tip3.Length > 0) withTip3++;
            if (rewardText.Length > 0) withRewardText++;
            if (completeInfo.Length > 0) withCompleteInfo++;

            string[] rewardParts = rewardText.Split('#', 2);
            string rewardSummary = rewardParts[0].Trim();
            string playerTalk = rewardParts.Length > 1 ? rewardParts[1].Trim() : "";

            // condition 是 0config.conditions 的 id 串（& 分隔）：1=等级下限，2=等级上限（低于 N 级）。
            string condRaw = GetStr(o, "condition");
            var condTexts = new List<string>();
            int minLevel = 1, maxLevel = 255;
            foreach (var seg in condRaw.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(seg.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cid)) continue;
                condRefs++;
                if (!conditions.TryGetValue(cid, out var cond)) continue;
                condRefsHit++;
                if (cond.Txt.Length > 0) condTexts.Add(cond.Txt);
                if (cond.Type == 1 && int.TryParse(cond.Param, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lv))
                    minLevel = Math.Max(minLevel, lv);
                if (cond.Type == 2 && int.TryParse(cond.Param, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lvMax))
                    maxLevel = Math.Min(maxLevel, lvMax - 1);
            }
            if (minLevel >= 999) highLevelGate++;

            int groupId = TryInt(o, "taskgroupid", out int tg) ? tg : 0;
            int originalType = TryInt(o, "type", out int ot) ? ot : 0;
            int nextId = TryInt(o, "next", out int nx) ? nx : 0;
            int requiredQuest = prevByNext.TryGetValue(id, out int prev) ? prev : 0;

            // goals 是 5config.goals 的 id：type 335 为击杀怪物，param 为怪物 Index（可多候选）。
            int goalId = TryInt(o, "goals", out int gid) ? gid : 0;
            GoalRow? goal = goalId > 0 && goals.TryGetValue(goalId, out var gr) ? gr : null;
            if (goal != null) withGoal++;
            string goalInfo = goal != null
                ? goal.Info.Replace("{0}", Math.Max(1, goal.Count).ToString(CultureInfo.InvariantCulture))
                : "";
            int goalMapId = goal?.MapId ?? 0;
            string group = "";
            if (goalMapId > 0 && mapNames.TryGetValue(goalMapId, out string? goalMapName)) group = goalMapName ?? "";
            if (string.IsNullOrEmpty(group) && startNpc > 0 && npcMapIndex.TryGetValue(startNpc, out int nm) &&
                mapNames.TryGetValue(nm, out string? npcMapName)) group = npcMapName ?? "";
            if (string.IsNullOrEmpty(group)) group = "其他";

            string gotoMessage = completeInfo.Length > 0 ? completeInfo : goalInfo;
            var taskDescription = Lines(goalInfo.Length > 0 ? goalInfo : completeInfo);

            var settings = new QuestOriginSettingsOut
            {
                OriginTaskId = id,
                Source = "_task",
                StartNpcId = startNpc,
                FinishNpcId = finishNpc,
                StartNpcName = startNpc > 0 ? npcNames.GetValueOrDefault(startNpc) ?? "" : "",
                FinishNpcName = finishNpc > 0 ? npcNames.GetValueOrDefault(finishNpc) ?? "" : "",
                NextQuestId = nextId,
                RequiredQuestId = requiredQuest,
                TaskGroupId = groupId,
                OriginalType = originalType,
                Condition = condRaw,
                ConditionText = string.Join("；", condTexts),
                Rewards = rewards,
                RewardText = rewardText,
                Tip3 = tip3,
                CompleteInfo = completeInfo,
                GoalId = goalId,
                GoalType = goal?.Type ?? 0,
                GoalCount = goal?.Count ?? 0,
                GoalMapId = goalMapId,
                GoalX = goal?.X ?? 0,
                GoalY = goal?.Y ?? 0,
                GoalInfo = goalInfo,
                GoalParams = goal?.Param ?? "",
                Raw = BuildRaw(o)
            };

            // rewards: "itemId#数量&itemId#数量..."；4=经验、499=金币是伪道具，直接落到数值字段。
            uint gold = 0, exp = 0;
            foreach (var seg in rewards.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = seg.Split('#');
                if (kv.Length < 2 || !int.TryParse(kv[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) continue;
                uint count = uint.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint c) ? c : 1;
                if (itemId == PseudoItemGold) { gold += count; continue; }
                if (itemId == PseudoItemExp) { exp += count; continue; }
                rewardItemRefs++;
                bool hit = itemNames.ContainsKey(itemId);
                if (hit) rewardItemHit++;
                settings.FixedRewards.Add(new QuestOriginRewardOut
                {
                    ItemIndex = itemId,
                    Count = count,
                    ItemName = itemNames.GetValueOrDefault(itemId) ?? "",
                    ItemResolved = hit
                });
            }
            totalGold += gold;
            totalExp += exp;

            if (goal != null && goal.Type == 335)
            {
                string[] ps = goal.Param.Split('#', StringSplitOptions.RemoveEmptyEntries);
                if (ps.Length == 1 && int.TryParse(ps[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int monsterId))
                {
                    killRefs++;
                    bool hit = monsterNames.ContainsKey(monsterId);
                    if (hit) killHit++;
                    settings.KillTasks.Add(new QuestOriginKillOut
                    {
                        MonsterIndex = monsterId,
                        Count = Math.Max(1, goal.Count),
                        MonsterName = monsterNames.GetValueOrDefault(monsterId) ?? "",
                        MonsterResolved = hit
                    });
                    withSingleKill++;
                }
                else if (ps.Length > 1)
                {
                    settings.GoalMultiTarget = true;
                    withMultiKill++;
                }
            }

            if (alternates.TryGetValue(id, out var ao))
            {
                withAlternate++;
                settings.Alternate = new QuestOriginAlternateOut
                {
                    Name = Clean(GetStr(ao, "name")),
                    Tip3 = Clean(GetStr(ao, "tip3")),
                    RewardText = Clean(GetStr(ao, "reward")),
                    CompleteInfo = Clean(GetStr(ao, "completeinfo")),
                    Rewards = GetStr(ao, "rewards"),
                    StartNpcId = TryInt(ao, "fnpc", out int afn) ? afn : 0,
                    FinishNpcId = TryInt(ao, "tnpc", out int atn) ? atn : 0
                };
            }

            list.Add(new QuestInfoOut
            {
                Index = id,
                Name = name,
                Group = group,
                FileName = "", // 内嵌配置，不生成 Envir/Quests/*.txt
                GotoMessage = gotoMessage,
                Description = Lines(tip3),
                TaskDescription = taskDescription,
                ReturnDescription = Lines(completeInfo),
                CompletionDescription = Lines(rewardSummary),
                PlayerTaskTalk = Lines(playerTalk),
                RequiredMinLevel = minLevel,
                RequiredMaxLevel = maxLevel,
                RequiredQuest = requiredQuest,
                Type = (byte)(groupId >= 1 && groupId <= 6 ? 3 : 0),
                GoldReward = gold,
                ExpReward = exp,
                OriginSettings = settings
            });
        }

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string questOut = Path.Combine(outputDir, "QuestInfo.json");
        File.WriteAllText(questOut, JsonSerializer.Serialize(list, opts), new UTF8Encoding(false));

        string reportPath = Path.Combine(outputDir, "quest_report.txt");
        using (var w = new StreamWriter(reportPath, false, new UTF8Encoding(false)))
        {
            w.WriteLine("原始传奇 → 本服务器：Quest 表迁移预览报告");
            w.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();
            w.WriteLine("【输入】");
            w.WriteLine($"  主任务线   : {c4}  (_task {tasks.Count} 条)");
            w.WriteLine($"  备选任务线 : {c2}  (_task_tx {alternates.Count} 条)");
            w.WriteLine($"  条件/地图  : {c0}  (conditions {conditions.Count} / map {mapNames.Count})");
            w.WriteLine($"  任务目标   : {c5}  (goals {goals.Count})");
            w.WriteLine($"  物品表     : {itemPath}  ({itemNames.Count} 条)");
            w.WriteLine($"  怪物表     : {monsterPath}  ({monsterNames.Count} 条)");
            w.WriteLine($"  NPC 预览表 : {npcPreviewPath}  ({npcNames.Count} 条)");
            w.WriteLine();
            w.WriteLine("【输出（旁路，未替换任何线上文件）】");
            w.WriteLine($"  {questOut}");
            w.WriteLine($"  {reportPath}");
            w.WriteLine("  输出目录在工具侧，不写入 ExcelConfig 上线配置目录。");
            w.WriteLine();
            w.WriteLine("【一、字段映射】");
            w.WriteLine("  Index       <- _task.id（_task_tx 与 _task 共用同一套 id，不另占 Index）");
            w.WriteLine("  Name/Group  <- _task.name / 目标地图名（goals.mapId → 0config.map.name，兜底 NPC 所在地图）");
            w.WriteLine("  FileName    <- 留空：不再生成 Envir/Quests/*.txt 脚本");
            w.WriteLine("  Description <- tip3（接任务时 NPC 对话）");
            w.WriteLine("  TaskDescription / GotoMessage <- goals.goInfo（{0} 换成 count）/ completeinfo");
            w.WriteLine("  CompletionDescription <- reward 中「#」前的奖励描述；PlayerTaskTalk <- 「#」后的玩家回话");
            w.WriteLine("  RequiredMinLevel/MaxLevel <- condition 引用 0config.conditions（1=等级下限，2=低于N级→N-1）");
            w.WriteLine("  RequiredQuest <- 反查 _task.next（谁指向本任务，谁就是前置任务）");
            w.WriteLine("  Type         <- taskgroupid 1..6 记 Story(3)，其余 General(0)；不用 Daily/Repeatable 避免每日重置");
            w.WriteLine("  GoldReward / ExpReward <- rewards 里伪道具 499=金币 / 4=主角经验");
            w.WriteLine("  OriginSettings <- 内嵌：「原始字段 Raw + 条件/目标/奖励/NPC/任务链 + 另一条任务线 Alternate」");
            w.WriteLine("      FixedRewards / KillTasks 用 ItemIndex / MonsterIndex 精确引用，服务器启动按 Index 解析，");
            w.WriteLine("      避开按名字查找（换表后旧名字大量失配）的问题。");
            w.WriteLine();
            w.WriteLine("【二、配套服务器改动】");
            w.WriteLine("  1. QuestInfo.cs 新增 QuestOriginSettings / QuestOriginReward / QuestOriginKill / QuestOriginAlternate");
            w.WriteLine("     与 QuestInfo.OriginSettings 字段；旧表无此字段→null，继续读 Envir/Quests/*.txt，行为不变。");
            w.WriteLine("  2. QuestInfo.LoadInfo()：OriginSettings 非 null 时改走 LoadFromOrigin()，不读 txt、不刷 File Not Found。");
            w.WriteLine("     LoadFromOrigin 按 Index 解析奖励物品与击杀怪物，填充 FixedRewards / KillTasks。");
            w.WriteLine("  3. NPCObject.LoadScript()：内嵌配置 NPC 直接按 StartNpcId/FinishNpcId 把任务绑定到该 NPC 实例，");
            w.WriteLine("     代替原来由 [Quests] 脚本段做的绑定（需 NPC 预览表一起上线才生效）。");
            w.WriteLine("  4. QuestInfo.CreateClientQuestInfo()：NpcIndex 未绑定时按 OriginSettings 的 NPC id 兜底显示名字。");
            w.WriteLine();
            w.WriteLine("【三、引用对齐】");
            w.WriteLine($"  _task_tx 按 id 合并为 Alternate: {withAlternate}/{tasks.Count}（仅 _task 独有的任务 {tasks.Count - withAlternate} 个）");
            w.WriteLine($"  condition 引用命中: {condRefsHit}/{condRefs}");
            w.WriteLine($"  goals 命中: {withGoal}/{tasks.Count}；单怪物目标 {withSingleKill}，多怪物候选 {withMultiKill}");
            w.WriteLine($"  奖励物品引用命中: {rewardItemHit}/{rewardItemRefs}（伪道具金币/经验已单独计入数值）");
            w.WriteLine($"  击杀怪物引用命中: {killHit}/{killRefs}");
            w.WriteLine($"  fnpc/tnpc 命中 NPC 预览表: {npcRefsHit}/{npcRefs}（有 NPC 引用的任务 {withNpcRefs}）");
            w.WriteLine();
            w.WriteLine("【四、本批未迁移 / 降级】");
            w.WriteLine("  - 只生成预览表，未替换 ExcelConfig/QuestInfo.json，也未改 Envir/Quests/*.txt。");
            w.WriteLine("  - 多怪物候选目标（param 含 #）只保留 GoalParams 原文，未拆成多个 KillTask：服务器");
            w.WriteLine("    QuestKillTask 是「每个都要完成」的语义，直接拆会改变目标数量，需后续确认。");
            w.WriteLine("  - goals 里非 335 类型（升级/副本/收集等）只保留 GoalInfo 文本，未映射成服务器任务类型。");
            w.WriteLine("  - condition=10999 的「需要等级999」按原样落到 RequiredMinLevel，服务器按等级门槛不会接取；");
            w.WriteLine($"    共 {highLevelGate} 个，多为活动/精英任务，原版由其他系统触发，需后续接活动逻辑。");
            w.WriteLine("  - 任务自动寻路/自动战斗/传送(chuansong/overchuan/autopathing/...) 只保留在 Raw，未实现。");
            w.WriteLine("  - 换 Index 会波及玩家任务存档、NPC 脚本 [Quests]、客户端协议；上线前需整批核对（含 NPC/地图）。");
            w.WriteLine();
            w.WriteLine("【五、使用方式（确认后再动线上）】");
            w.WriteLine("  1. 重新编译服务器（QuestOriginSettings + LoadInfo 分流 + NPC 任务绑定）。");
            w.WriteLine("  2. 把 migration-preview/QuestInfo.json 拷到 ExcelConfig/QuestInfo.json，旧表先备份到 migration-preview。");
            w.WriteLine("  3. 与 migration-preview/NPCInfo.json 一起上线，任务才会挂到 NPC 上；旧的 Envir/Quests/*.txt 不用动。");
            w.WriteLine();
            w.WriteLine("【统计】");
            w.WriteLine($"  任务 {list.Count}，无名兜底 {noName}，有前置任务 {list.Count(q => q.RequiredQuest > 0)}");
            w.WriteLine($"  文本覆盖：tip3 {withTip3} / reward {withRewardText} / completeinfo {withCompleteInfo}");
            w.WriteLine($"  奖励合计：金币 {totalGold} / 经验 {totalExp}");
        }

        Console.WriteLine($"任务            : {list.Count}（_task_tx 合并为 Alternate {withAlternate}）");
        Console.WriteLine($"引用命中        : condition {condRefsHit}/{condRefs} / 奖励物品 {rewardItemHit}/{rewardItemRefs} / 击杀怪物 {killHit}/{killRefs} / NPC {npcRefsHit}/{npcRefs}");
        Console.WriteLine($"目标            : goals {withGoal} / 单怪物 {withSingleKill} / 多怪物候选 {withMultiKill}");
        Console.WriteLine($"文本            : tip3 {withTip3} / reward {withRewardText} / completeinfo {withCompleteInfo}");
        Console.WriteLine($"输出            : {questOut}");
        Console.WriteLine($"报告            : {reportPath}");
        return 0;
    }

    static JsonDocument Parse(string path)
        => JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));

    static Dictionary<int, string> ReadIndexNames(JsonElement root)
    {
        var map = new Dictionary<int, string>();
        foreach (var e in root.EnumerateArray())
        {
            if (!e.TryGetProperty("Index", out var pi) || !pi.TryGetInt32(out int idx)) continue;
            map[idx] = GetStr(e, "Name");
        }
        return map;
    }

    static Dictionary<string, string> BuildRaw(JsonElement o)
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        if (o.ValueKind != JsonValueKind.Object) return raw;
        foreach (var p in o.EnumerateObject())
        {
            if (SemanticKeys.Contains(p.Name)) continue;
            raw[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? "",
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => p.Value.GetRawText()
            };
        }
        return raw;
    }

    static List<string> Lines(string s)
    {
        var list = new List<string>();
        foreach (var line in s.Split('\n'))
        {
            string t = line.Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
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

    static bool TryInt(JsonElement e, string key, out int value)
    {
        value = 0;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var p)) return false;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int n)) { value = n; return true; }
        if (p.ValueKind == JsonValueKind.String &&
            int.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s)) { value = s; return true; }
        return false;
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s, @"^\s*\d+&", ""); // 文本样式前缀，如 "4&"
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        s = s.Replace('|', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }
}
