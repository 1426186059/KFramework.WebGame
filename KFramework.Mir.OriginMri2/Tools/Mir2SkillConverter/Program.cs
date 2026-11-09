using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Mir2SkillConverter;

// 输出 DTO：字段名/类型必须与 Web_Mir2.Server/Server/MirDatabase/MagicInfo.cs 的公共字段完全一致，
// 服务器的 JsonConfigOptions 大小写敏感、按字段名反序列化、无数字字符串与枚举名转换器。
public class MagicInfoOut
{
    public string Name = "";
    public int Spell;
    public byte BaseCost, LevelCost, Icon;
    public byte Level1, Level2, Level3;
    public ushort Need1, Need2, Need3;
    public uint DelayBase = 1800, DelayReduction;
    public ushort PowerBase, PowerBonus;
    public ushort MPowerBase, MPowerBonus;
    public float MultiplierBase = 1.0f, MultiplierBonus;
    public byte Range = 9;
    public int OriginId;
    public string OriginIcon = "", ConditionText = "", Tips1 = "";
    public int Cls, Career, IsShow, Condition, HandlerType, Aoe, MaxTarget, CanUp;
    public int TargetType, Action, HurtDelay, ReleaseDis, ReleaseDis2, ReleaseType;
    public int AreaDir, AreaDis, CommonCd, SkillParams, NotargetEff, Passive, IsAuto, OriginIndex, Piaozi;
    public int[] EffectIds = Array.Empty<int>();
}

public static class Program
{
    // 原始传奇 skillId 直接作为 Spell 键；只有 97/98/99 落进旧 1 字节枚举值域，
    // 加偏移避开服务器里残留的旧 Spell 常量（战斗逻辑重写后可去掉）。
    private const int LegacySpellMax = 255;
    private const int OriginIdOffset = 1000000;

    // 配置表统一无 BOM：File.ReadAllText 会自动吞 BOM，但 python/node 等工具会直接报错。
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            string root = RepoRoot();
            string excel = Path.Combine(root, "Web_Mir2.Server.Version", "ExcelConfig");
            var opt = ParseArgs(args, root, excel);

            Console.WriteLine($"权威源   : {opt.Input}");
            Console.WriteLine($"输出     : {opt.Output}{(opt.DryRun ? " (dry-run，不覆盖线上)" : "")}");

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(opt.Input));
            JsonElement skills = doc.RootElement.GetProperty("skill");
            JsonElement effects = doc.RootElement.GetProperty("skillEffects");
            Dictionary<int, List<int>> effectBySkill = BuildEffectIndex(effects);

            var rows = new List<(int Id, JsonElement Raw)>();
            foreach (var kv in skills.EnumerateObject())
            {
                int id = kv.Value.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt32() : int.Parse(kv.Name);
                rows.Add((id, kv.Value));
            }
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));

            var result = new List<MagicInfoOut>(rows.Count);
            int effectsAttached = 0, offsetCount = 0;
            foreach (var (id, rec) in rows)
            {
                string name = Str(rec, "name");
                string icon = Str(rec, "icon");
                int cd = Int(rec, "cdTime");
                int releaseDis = Int(rec, "releaseDis");

                if (id <= LegacySpellMax) offsetCount++;
                var dto = new MagicInfoOut
                {
                    Name = name,
                    Spell = ToOriginSpell(id),
                    Icon = 0,
                    DelayBase = (uint)Math.Max(0, cd)
                };
                if (releaseDis > 0 && releaseDis <= byte.MaxValue) dto.Range = (byte)releaseDis;

                int[] effectIds = effectBySkill.TryGetValue(id, out var list)
                    ? list.Distinct().ToArray() : Array.Empty<int>();
                if (effectIds.Length > 0) effectsAttached++;

                FillOriginFields(dto, rec, id, icon, effectIds);
                result.Add(dto);
            }

            // skillEffects 全量另存（176 键 / 358 条，多数属于没有 skill 定义的怪物/系统技能）
            if (!opt.NoEffects)
            {
                var effectOut = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var kv in effects.EnumerateObject()) effectOut[kv.Name] = kv.Value.Clone();
                File.WriteAllText(opt.Effects, JsonSerializer.Serialize(effectOut, JsonOptions), Utf8NoBom);
                Console.WriteLine($"特效表   : {opt.Effects} ({effectOut.Count} 键)");
            }

            string json = JsonSerializer.Serialize(result, JsonOptions);

            if (!opt.DryRun)
            {
                string backupDir = Path.Combine(root, ".ai", "backups");
                Directory.CreateDirectory(backupDir);
                string backup = Path.Combine(backupDir,
                    $"MagicInfo.json.{DateTime.Now:yyyyMMdd-HHmmss}.pre-skill-swap_bak");
                if (File.Exists(opt.Output)) File.Copy(opt.Output, backup, false);
                Console.WriteLine($"备份     : {backup}");
            }

            File.WriteAllText(opt.Output, json, Utf8NoBom);

            // 回读验证：字段名、类型、条数、扁平字段完整性、键唯一性
            List<MagicInfoOut>? check = JsonSerializer.Deserialize<List<MagicInfoOut>>(
                File.ReadAllText(opt.Output), JsonOptions);
            if (check == null || check.Count != result.Count)
                throw new InvalidOperationException("回读校验失败：条数不一致");
            if (check.Any(x => x.OriginId == 0))
                throw new InvalidOperationException("回读校验失败：OriginId 缺失");
            if (check.Select(x => x.OriginId).Distinct().Count() != check.Count)
                throw new InvalidOperationException("回读校验失败：OriginId 重复");
            if (check.Select(x => x.Spell).Distinct().Count() != check.Count)
                throw new InvalidOperationException("回读校验失败：Spell 键重复");

            Console.WriteLine();
            Console.WriteLine($"技能总数 : {result.Count}（回读校验通过，Spell 唯一值 {check.Count}，全部为原始传奇 skillId）");
            Console.WriteLine($"特效归属 : {effectsAttached} 个技能挂上 skillEffects，共 {effectBySkill.Count} 个 skillid");
            Console.WriteLine($"偏移避让 : {offsetCount} 条 id<=255 用 1000000+id 避开旧枚举常量");
            Console.WriteLine("旧 Mir2 技能兜底与名字映射已全部移除；服务器 FillMagicInfoList 也已删除。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("转换失败: " + ex);
            return 1;
        }
    }

    private static int ToOriginSpell(int id) => id <= LegacySpellMax ? OriginIdOffset + id : id;

    // 5config.skill 的列直接落到主表字段；不在运行时表里复制整份原始字典。
    private static void FillOriginFields(MagicInfoOut dto, JsonElement rec, int id, string icon, int[] effectIds)
    {
        dto.OriginId = id;
        dto.OriginIcon = icon;
        dto.ConditionText = Str(rec, "conditionText");
        dto.Tips1 = Str(rec, "tips1");
        dto.Cls = Int(rec, "cls");
        dto.Career = Int(rec, "career");
        dto.IsShow = Int(rec, "isshow");
        dto.Condition = Int(rec, "condition");
        dto.HandlerType = Int(rec, "handlerType");
        dto.Aoe = Int(rec, "aoe");
        dto.MaxTarget = Int(rec, "maxtarget");
        dto.CanUp = Int(rec, "canup");
        dto.TargetType = Int(rec, "targetType");
        dto.Action = Int(rec, "action");
        dto.HurtDelay = Int(rec, "hurtDelay");
        dto.ReleaseDis = Int(rec, "releaseDis");
        dto.ReleaseDis2 = Int(rec, "releaseDis2");
        dto.ReleaseType = Int(rec, "releaseType");
        dto.AreaDir = Int(rec, "areaDir");
        dto.AreaDis = Int(rec, "areaDis");
        dto.CommonCd = Int(rec, "commonCd");
        dto.SkillParams = Int(rec, "params");
        dto.NotargetEff = Int(rec, "notargeteff");
        dto.Passive = Int(rec, "passive");
        dto.IsAuto = Int(rec, "isAuto");
        dto.OriginIndex = Int(rec, "index");
        dto.Piaozi = Int(rec, "piaozi");
        dto.EffectIds = effectIds;
    }

    private static Dictionary<int, List<int>> BuildEffectIndex(JsonElement effects)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var kv in effects.EnumerateObject())
        {
            if (!int.TryParse(kv.Name, out int skillId)) continue;
            var ids = new List<int>();
            IEnumerable<JsonElement> items = kv.Value.ValueKind == JsonValueKind.Array
                ? kv.Value.EnumerateArray().ToArray()
                : new[] { kv.Value };
            foreach (var e in items)
            {
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("effect", out var eff)
                    && eff.ValueKind == JsonValueKind.Number && eff.TryGetInt32(out int effId))
                    ids.Add(effId);
            }
            if (ids.Count > 0) map[skillId] = ids;
        }
        return map;
    }

    private static int Int(JsonElement e, string key, int def = 0)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
           && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : def;

    private static string Str(JsonElement e, string key, string def = "")
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? def : v.ToString()) : def;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Web_Mir2.Server.Version", "ExcelConfig")))
            dir = dir.Parent;
        if (dir == null) throw new InvalidOperationException("找不到工作区根目录");
        return dir.FullName;
    }

    private sealed record Options(
        string Input, string Output, string Effects, bool NoEffects, bool DryRun);

    private static Options ParseArgs(string[] args, string root, string excel)
    {
        // 唯一权威源：资源提取工具下的原始传奇分片（中文原版），不再是 ExcelConfig 里的副本。
        string input = Path.Combine(root, "Tools", "原始传奇-资源提取", "原始传奇-资源参考", "5config10.25341.1.json");
        string output = Path.Combine(excel, "MagicInfo.json");
        string effects = Path.Combine(excel, "SkillEffectInfo.json");
        bool noEffects = false, dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": input = args[++i]; break;
                case "--output": output = args[++i]; break;
                case "--effects": effects = args[++i]; break;
                case "--no-effects": noEffects = true; break;
                case "--dry-run": dryRun = true; break;
                default: throw new ArgumentException("未知参数: " + args[i]);
            }
        }

        if (dryRun)
        {
            string ai = Path.Combine(root, ".ai");
            Directory.CreateDirectory(ai);
            output = Path.Combine(ai, "MagicInfo.converted.preview.json");
            effects = Path.Combine(ai, "SkillEffectInfo.preview.json");
        }
        return new Options(input, output, effects, noEffects, dryRun);
    }
}
