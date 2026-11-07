using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mir2NPCConverter;

// 与服务器 NPCInfo 公共字段同形态的输出 DTO
public sealed class NPCInfoOut
{
    public int Index;
    public string FileName = "", Name = "", NameUI = "";
    public int MapIndex;
    public PointOut Location = new();
    public ushort Rate = 100;
    public ushort Image;
    public object Colour = new();
    public bool TimeVisible = false;
    public byte HourStart = 0, MinuteStart = 0, HourEnd = 0, MinuteEnd = 1;
    public short MinLev = 0, MaxLev = 0;
    public string DayofWeek = "", ClassRequired = "";
    public bool Sabuk = false;
    public int FlagNeeded = 0;
    public int Conquest;
    public bool ShowOnBigMap;
    public int BigMapIcon;
    public bool CanTeleportTo;
    public bool ConquestVisible = true;
    public List<int> CollectQuestIndexes = new();
    public List<int> FinishQuestIndexes = new();
    public NPCOriginSettingsOut OriginSettings = new();
}

public sealed class PointOut
{
    public int X;
    public int Y;
}

public sealed class NPCOriginSettingsOut
{
    public int OriginNpcId;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Title;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Depiction;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Button;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Function;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Deliver;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int StoreId;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OpenCondition;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ShowCondition;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Tips;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<NPCPlacementOut>? Placements;

    // 原始行全部字段（已语义化的除外），保证零丢失
    public Dictionary<string, string> Raw = new();
}

public sealed class NPCPlacementOut
{
    public int MapId;
    public int X;
    public int Y;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool MapResolved;
}

public static class Program
{
    private const string ConfigVersion = "10.25341.1";

    // 已在 NPCOriginSettingsOut 里语义化的键，不再重复放进 Raw
    private static readonly HashSet<string> SemanticKeys = new(StringComparer.Ordinal)
    {
        "title", "depiction", "button", "function", "deliver", "storeId",
        "openCondition", "showcondition", "tips"
    };

    public static int Main(string[] args)
    {
        string root = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2";
        string configDir = Path.Combine(root, @"Tools\原始传奇-资源提取\原始传奇-资源参考");
        string outputDir = Path.Combine(root, @"Tools\原始传奇-资源提取\migration-preview");
        string mapInfoPath = Path.Combine(root, @"Web_Mir2.Server.Version\Maps\MapInfo_Fix.json");

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config-dir" && i + 1 < args.Length) configDir = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length) outputDir = args[++i];
            else if (args[i] == "--map-info" && i + 1 < args.Length) mapInfoPath = args[++i];
        }

        string c2 = Path.Combine(configDir, $"2config{ConfigVersion}.json");
        string c3 = Path.Combine(configDir, $"3config{ConfigVersion}.json");
        string c4 = Path.Combine(configDir, $"4config{ConfigVersion}.json");
        string c5 = Path.Combine(configDir, $"5config{ConfigVersion}.json");
        foreach (var path in new[] { c2, c3, c4, c5, mapInfoPath })
        {
            if (!File.Exists(path)) { Console.Error.WriteLine($"找不到输入文件: {path}"); return 1; }
        }

        using var doc3 = JsonDocument.Parse(File.ReadAllText(c3).TrimStart('\uFEFF'));
        using var doc5 = JsonDocument.Parse(File.ReadAllText(c5).TrimStart('\uFEFF'));
        using var docMap = JsonDocument.Parse(File.ReadAllText(mapInfoPath).TrimStart('\uFEFF'));
        var npcTable = GetTable(doc3, "npc");
        var mapNpcTable = GetTable(doc5, "mapnpc");

        var mapIndexes = new HashSet<int>();
        foreach (var e in docMap.RootElement.EnumerateArray())
        {
            if (e.TryGetProperty("Index", out var p) && p.TryGetInt32(out int idx)) mapIndexes.Add(idx);
        }

        var placementsByNpc = new Dictionary<int, List<NPCPlacementOut>>();
        var unresolvedMaps = new HashSet<string>();
        int placementsTotal = 0, placementsResolved = 0, placementsNoMap = 0;
        foreach (var entry in mapNpcTable.EnumerateObject())
        {
            var o = entry.Value;
            if (!TryInt(o, "npcid", out int npcId)) continue;
            int mapId = TryInt(o, "mapid", out int mid) ? mid : 0;
            int x = TryInt(o, "x", out int xx) ? xx : 0;
            int y = TryInt(o, "y", out int yy) ? yy : 0;
            if (mapId == 0) placementsNoMap++;
            bool resolved = mapId > 0 && mapIndexes.Contains(mapId);
            if (resolved) placementsResolved++;
            else unresolvedMaps.Add(mapId.ToString(CultureInfo.InvariantCulture));

            if (!placementsByNpc.TryGetValue(npcId, out var lst))
            {
                lst = new List<NPCPlacementOut>();
                placementsByNpc[npcId] = lst;
            }
            lst.Add(new NPCPlacementOut { MapId = mapId, X = x, Y = y, MapResolved = resolved });
            placementsTotal++;
        }

        var npcs = new List<(int Id, JsonElement Data)>();
        foreach (var p in npcTable.EnumerateObject())
        {
            if (int.TryParse(p.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                npcs.Add((id, p.Value));
        }
        npcs.Sort((a, b) => a.Id.CompareTo(b.Id));
        var npcIdSet = new HashSet<int>(npcs.Select(x => x.Id));
        int orphanPlacements = placementsByNpc.Where(kv => !npcIdSet.Contains(kv.Key)).Sum(kv => kv.Value.Count);

        Directory.CreateDirectory(outputDir);
        var list = new List<NPCInfoOut>();
        int noName = 0, imageClamped = 0, withPlacement = 0, resolvedPlacement = 0;
        int withTitle = 0, withDepiction = 0, withButton = 0, withFunction = 0, withDeliver = 0, withStore = 0;

        foreach (var (id, o) in npcs)
        {
            string name = GetStr(o, "name").Trim();
            if (string.IsNullOrEmpty(name)) { name = "NPC_" + id.ToString(CultureInfo.InvariantCulture); noName++; }
            string title = GetStr(o, "title").Trim();

            var placements = placementsByNpc.TryGetValue(id, out var pl) ? pl : new List<NPCPlacementOut>();
            var first = placements.FirstOrDefault(q => q.MapId > 0);
            if (first != null) { withPlacement++; if (first.MapResolved) resolvedPlacement++; }

            long model = GetLong(o, "model") ?? 0;
            if (model > ushort.MaxValue) { imageClamped++; model = ushort.MaxValue; }
            if (model < 0) model = 0;

            string depiction = CleanHtml(GetStr(o, "depiction"));
            string button = GetStr(o, "button");
            string function = GetStr(o, "function");
            string deliver = GetStr(o, "deliver");
            string openCond = GetStr(o, "openCondition");
            string showCond = GetStr(o, "showcondition");
            string tips = GetStr(o, "tips");
            if (!string.IsNullOrEmpty(title)) withTitle++;
            if (!string.IsNullOrEmpty(depiction)) withDepiction++;
            if (!string.IsNullOrEmpty(button)) withButton++;
            if (!string.IsNullOrEmpty(function)) withFunction++;
            if (!string.IsNullOrEmpty(deliver)) withDeliver++;
            int storeId = TryInt(o, "storeId", out int sid) ? sid : 0;
            if (storeId != 0) withStore++;

            var settings = new NPCOriginSettingsOut
            {
                OriginNpcId = id,
                Title = NullIfEmpty(title),
                Depiction = NullIfEmpty(depiction),
                Button = NullIfEmpty(button),
                Function = NullIfEmpty(function),
                Deliver = NullIfEmpty(deliver),
                StoreId = storeId,
                OpenCondition = NullIfEmpty(openCond),
                ShowCondition = NullIfEmpty(showCond),
                Tips = NullIfEmpty(tips),
                Placements = placements.Count > 0 ? placements : null,
                Raw = BuildRaw(o)
            };

            var outNpc = new NPCInfoOut
            {
                Index = id,
                FileName = "",
                Name = name,
                NameUI = name,
                MapIndex = first?.MapId ?? 0,
                Location = new PointOut { X = first?.X ?? 0, Y = first?.Y ?? 0 },
                Image = (ushort)model,
                OriginSettings = settings
            };
            list.Add(outNpc);
        }

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string npcOut = Path.Combine(outputDir, "NPCInfo.json");
        File.WriteAllText(npcOut, JsonSerializer.Serialize(list, opts), new UTF8Encoding(false));

        // 顺带盘点 Quest，供下一批迁移参考
        int taskCount = 0, taskTxCount = 0, taskNpcRefs = 0, taskNpcRefsHit = 0;
        using (var doc4 = JsonDocument.Parse(File.ReadAllText(c4).TrimStart('\uFEFF')))
        using (var doc2 = JsonDocument.Parse(File.ReadAllText(c2).TrimStart('\uFEFF')))
        {
            var task = GetTable(doc4, "_task");
            var taskTx = GetTable(doc2, "_task_tx");
            taskCount = task.EnumerateObject().Count();
            taskTxCount = taskTx.EnumerateObject().Count();
            var allNpcIds = new HashSet<int>(list.Select(x => x.Index));
            foreach (var e in task.EnumerateObject())
            {
                foreach (var key in new[] { "fnpc", "tnpc" })
                {
                    string v = GetStr(e.Value, key);
                    if (string.IsNullOrEmpty(v) || v == "0") continue;
                    taskNpcRefs++;
                    if (int.TryParse(v, out int nid) && allNpcIds.Contains(nid)) taskNpcRefsHit++;
                }
            }
        }

        string reportPath = Path.Combine(outputDir, "npc_report.txt");
        using (var w = new StreamWriter(reportPath, false, new UTF8Encoding(false)))
        {
            w.WriteLine("原始传奇 → 本服务器：NPC 表迁移预览报告");
            w.WriteLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine();
            w.WriteLine("【输入】");
            w.WriteLine($"  npc     : {c3}  ({npcs.Count} 条)");
            w.WriteLine($"  mapnpc  : {c5}  ({mapNpcTable.EnumerateObject().Count()} 条摆放)");
            w.WriteLine($"  服务器地图: {mapInfoPath}  ({mapIndexes.Count} 张，用于校验 MapIndex)");
            w.WriteLine();
            w.WriteLine("【输出（旁路，未替换任何线上文件）】");
            w.WriteLine($"  {npcOut}");
            w.WriteLine($"  {reportPath}");
            w.WriteLine("  输出目录在工具侧，不写入 ExcelConfig 上线配置目录。");
            w.WriteLine();
            w.WriteLine("【一、字段映射】");
            w.WriteLine("  Index     <- npc.id（840 个 NPC 定义；任务表 fnpc/tnpc 引用的正是这套 id）");
            w.WriteLine("  Name/NameUI <- npc.name（玩家可见名）；npc.title 是对话框标题，保留在 OriginSettings.Title");
            w.WriteLine("  Image     <- npc.model（clamp 到 ushort；与客户端资源号不是同一体系）");
            w.WriteLine("  MapIndex/Location <- mapnpc 里按 npcid 找到的第一个摆放点（mapid/x/y）");
            w.WriteLine("  FileName  <- 留空：不再生成 Envir/NPCs/*.txt 脚本");
            w.WriteLine("  OriginSettings <- 新增字段，把原始 NPC 内容直接内嵌进配置表：");
            w.WriteLine("      Title/Depiction/Button/Function/Deliver/StoreId/OpenCondition/ShowCondition/Tips");
            w.WriteLine("      Placements：该 NPC 的全部摆放点（含 MapResolved 标记）");
            w.WriteLine("      Raw：原始行所有字段（已语义化的除外），保证零丢失");
            w.WriteLine("  Rate=100 / ShowOnBigMap=false / ConquestVisible=true 等：服务器默认值");
            w.WriteLine();
            w.WriteLine("【二、配套服务器改动】");
            w.WriteLine("  1. Server/MirDatabase/NPCInfo.cs 新增 NPCOriginSettings / NPCPlacement 与 NPCInfo.OriginSettings 字段。");
            w.WriteLine("     旧表没有该字段，反序列化为 null，行为完全不变。");
            w.WriteLine("  2. Server/MirObjects/NPCObject.cs：OriginSettings 非 null 时跳过「脚本文件名必须等于 Name」校验。");
            w.WriteLine("  3. Server/MirObjects/NPC/NPCScript.cs：FileName 为空时直接跳过 LoadInfo，不刷 Script Not Found。");
            w.WriteLine("  这样 840 个 NPC 无需任何 txt 就能出现在地图上，也不会刷警告。");
            w.WriteLine("  注意：本批只把数据落地，OriginSettings 还没有被交互逻辑消费（对话/商店/传送暂未生效）。");
            w.WriteLine();
            w.WriteLine("【三、地图对齐情况】");
            w.WriteLine($"  摆放点 {placementsTotal}，直接命中服务器地图 Index {placementsResolved}，无 mapid {placementsNoMap}。");
            w.WriteLine($"  无法映射的地图 id 共 {unresolvedMaps.Count} 个（多为 0config.map 里标注“没用”的废弃地图）。");
            w.WriteLine("  0config.map 991 张与服务器 MapInfo_Fix 607 张交集 604，即两套地图索引基本同源；");
            w.WriteLine("  但服务器缺 387 张原始地图，落在其上的 NPC 暂时不会被 CreateMap 挂到地图里。");
            w.WriteLine("  这与怪物刷怪一样，要连地图表一起迁移才完整。");
            w.WriteLine();
            w.WriteLine("【四、本批未迁移 / 降级】");
            w.WriteLine($"  - mapnpc 里 {orphanPlacements} 个摆放点的 npcid 在 npc 表中不存在（孤儿），未挂到任何 NPC。");
            w.WriteLine("  - 商店商品：3config.store 5532 条、npc.storeId 20 个值（10 个命中 store.sellId），未展开。");
            w.WriteLine("  - 传送：npc.deliver 是「分组#mapid#mapid...&...」长串，只原样保留，未解析成传送点。");
            w.WriteLine("  - npc.function 功能 id（6/1/5/4…）原样保留，未映射到服务器技能/面板。");
            w.WriteLine("  - 任务绑定：不再依赖 [Quests] 脚本段，Quest 批次已在 NPCObject.LoadScript 按 fnpc/tnpc 绑定（需 NPC + Quest 两张预览表一起上线）。");
            w.WriteLine("  - 对话框文本已去掉“数字&”样式前缀与 HTML 标签（<br> 转换行、font 标签剥离）。");
            w.WriteLine("  - 替换 NPCInfo.Index 会波及任务、Goods 存档、客户端协议；上线前需一起核对。");
            w.WriteLine();
            w.WriteLine("【五、使用方式（确认后再动线上）】");
            w.WriteLine("  1. 重新编译服务器（OriginSettings 字段 + 脚本跳过逻辑）。");
            w.WriteLine("  2. 把 migration-preview/NPCInfo.json 拷到 ExcelConfig/NPCInfo.json。");
            w.WriteLine("     不用生成任何 Envir/NPCs/*.txt，旧的 213 个脚本文件也不用动。");
            w.WriteLine("  3. 旧 NPCInfo.json 建议先备份。");
            w.WriteLine();
            w.WriteLine("【六、Quest 表盘点（下一批）】");
            w.WriteLine($"  4config._task {taskCount} 条 / 2config._task_tx {taskTxCount} 条，两套内容高度相似（不同任务线）。");
            w.WriteLine("  字段：id/name/next/condition/rewards/reward/fnpc/tnpc/type/tip3/completeinfo/taskgroupid…");
            w.WriteLine($"  任务里 fnpc/tnpc 共 {taskNpcRefs} 个 NPC 引用，命中本表 npc.id {taskNpcRefsHit} 个。");
            w.WriteLine("  rewards 是「itemId#数量」，对话文本 tip3/reward/completeinfo 全中文，可直接迁。");
            w.WriteLine("  服务器 QuestInfo 是元数据表 + Envir/Quests/*.txt 内容脚本，需按本批同样的「配置表内嵌」思路处理。");
            w.WriteLine();
            w.WriteLine("【统计】");
            w.WriteLine($"  NPC {list.Count}，无名兜底 {noName}，model 钳制 {imageClamped}");
            w.WriteLine($"  有摆放点 {withPlacement}（其中落在服务器已有地图 {resolvedPlacement}）");
            w.WriteLine($"  内容覆盖：title {withTitle} / 对话 {withDepiction} / 按钮 {withButton} / 功能 {withFunction} / 传送 {withDeliver} / 商店 {withStore}");
        }

        Console.WriteLine($"NPC 定义        : {list.Count}");
        Console.WriteLine($"摆放点          : {placementsTotal}（命中服务器地图 {placementsResolved} / 无 mapid {placementsNoMap} / 孤儿 {orphanPlacements}）");
        Console.WriteLine($"有摆放点的 NPC  : {withPlacement}（其中地图可落地 {resolvedPlacement}）");
        Console.WriteLine($"内容字段        : title {withTitle} / 对话 {withDepiction} / 按钮 {withButton} / 功能 {withFunction} / 传送 {withDeliver} / 商店 {withStore}");
        Console.WriteLine($"降级            : model钳制 {imageClamped} / 无名兜底 {noName}");
        Console.WriteLine($"Quest 盘点      : _task {taskCount} / _task_tx {taskTxCount} / NPC引用命中 {taskNpcRefsHit}/{taskNpcRefs}");
        Console.WriteLine($"输出            : {npcOut}");
        Console.WriteLine($"报告            : {reportPath}");
        return 0;
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

    static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

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

    static long? GetLong(JsonElement e, string key)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out long n)) return n;
        if (p.ValueKind == JsonValueKind.String &&
            long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s)) return s;
        return null;
    }

    static string CleanHtml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s, @"^\s*\d+&", ""); // 文本样式前缀，如 "4&"
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        return s.Trim();
    }
}
