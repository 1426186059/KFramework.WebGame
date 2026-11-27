using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    public string OriginIcon = "", ConditionText = "", Tips1 = "", Show = "", TipAttributes = "";
    public int Cls, Career, IsShow, Condition, HandlerType, Aoe, MaxTarget, CanUp;
    public int TargetType, Action, HurtDelay, ReleaseDis, ReleaseDis2, ReleaseType;
    public int AreaDir, AreaDis, CommonCd, SkillParams, NotargetEff, Passive, IsAuto, OriginIndex;
    [JsonPropertyName("piaozi")]
    public int Piaozi;
    public int[] EffectIds = Array.Empty<int>();
    // 1=需技能书激活（skills_condition[id][1].cost 有值）；0=达等级自动学会。
    public int NeedBook;
}

public static class Program
{
    // 原始传奇 skillId 直接作为 Spell 键；只有 97/98/99 落进旧 1 字节枚举值域，
    // 加偏移避开服务器里残留的旧 Spell 常量（战斗逻辑重写后可去掉）。
    private const int LegacySpellMax = 255;
    private const int OriginIdOffset = 1000000;

    // 全部技能统一用原始传奇 skillId 作为 Spell 键（214 条），不再沿用旧引擎 Spell 枚举。
    // 技能效果由服务端按 5config.skill 的 handlerType + OriginId 数据驱动分派（见 HumanObject.OriginSkillDispatch）。
    // 仅 id<=255 的 3 条用 1000000+id 偏移避开旧枚举常量，其余直接用 skillId。
    private static readonly Dictionary<int, int> LegacySpellByOriginId = new();

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
            Console.WriteLine($"等级表   : {opt.Condition}");
            Console.WriteLine($"输出     : {opt.Output}{(opt.DryRun ? " (dry-run，不覆盖线上)" : "")}");

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(opt.Input));
            using JsonDocument condDoc = JsonDocument.Parse(File.ReadAllText(opt.Condition));
            string attrPath = Directory.GetFiles(Path.GetDirectoryName(opt.Input) ?? root, "0config*.json").First();
            using JsonDocument attrDoc = JsonDocument.Parse(File.ReadAllText(attrPath));
            JsonElement skills = doc.RootElement.GetProperty("skill");
            JsonElement effects = doc.RootElement.GetProperty("skillEffects");
            JsonElement buffs = doc.RootElement.GetProperty("buff");
            Dictionary<int, Dictionary<int, JsonElement>> conditions = BuildConditionIndex(condDoc.RootElement);
            Dictionary<int, string> attrNames = BuildAttributeNames(attrDoc.RootElement);

            var rows = new List<(int Id, JsonElement Raw)>();
            foreach (var kv in skills.EnumerateObject())
            {
                int id = kv.Value.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt32() : int.Parse(kv.Name);
                rows.Add((id, kv.Value));
            }
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));
            var skillIds = rows.Select(r => r.Id).ToHashSet();

            // skillEffects 键有两种形态：少数内功技能直接用 skillId（97/1001…），
            // 多数常规技能用 skillId*100+等级（如 1301101 = 治愈术 13011 Lv1）。
            Dictionary<int, List<int>> effectBySkill = BuildEffectIndex(effects, skillIds);

            var result = new List<MagicInfoOut>(rows.Count);
            int effectsAttached = 0, offsetCount = 0, legacyCount = 0, combatCount = 0;
            int lockedCount = 0, extraClampedCount = 0, mpClampedCount = 0;
            foreach (var (id, rec) in rows)
            {
                string name = Str(rec, "name");
                string icon = Str(rec, "icon");
                int cd = Int(rec, "cdTime");
                int releaseDis = Int(rec, "releaseDis");

                bool legacy = LegacySpellByOriginId.TryGetValue(id, out int legacySpell);
                int spell = legacy ? legacySpell : ToOriginSpell(id);
                if (legacy) legacyCount++;
                if (!legacy && id <= LegacySpellMax) offsetCount++;
                var dto = new MagicInfoOut
                {
                    Name = name,
                    Spell = spell,
                    Icon = 0,
                    DelayBase = (uint)Math.Max(0, cd)
                };
                if (releaseDis > 0 && releaseDis <= byte.MaxValue) dto.Range = (byte)releaseDis;

                // 只认权威 skillEffects（键 = skillId 或 skillId*100+等级）。
                // 绝不再按 model/icon 撞号兜底：数字巧合会把别的技能特效（如召唤神兽 759）错配给施毒术。
                int[] effectIds = effectBySkill.TryGetValue(id, out var list)
                    ? list.Distinct().ToArray() : Array.Empty<int>();
                if (effectIds.Length > 0) effectsAttached++;

                FillOriginFields(dto, rec, id, icon, effectIds);
                var combat = FillCombatFields(dto, conditions, id);
                dto.TipAttributes = BuildTipAttributes(rec, conditions, id, attrNames, buffs);
                if (combat.Matched)
                {
                    combatCount++;
                    if (combat.Locked) lockedCount++;
                    if (combat.ExtraClamped) extraClampedCount++;
                    if (combat.MpClamped) mpClampedCount++;
                }
                else
                {
                    // 没有等级记录的技能（如 10104~10108、90039）同样不可升级，
                    // 否则 GainSkillExp 会因 Need=0 立刻把技能刷到满级。
                    dto.Need1 = dto.Need2 = dto.Need3 = ushort.MaxValue;
                    lockedCount++;
                }
                if (string.IsNullOrEmpty(dto.Show))
                {
                    dto.Show = dto.Tips1;
                }
                result.Add(dto);
            }

            // skillEffects 全量快照（176 键 / 358 条，多数属于没有 skill 定义的怪物/系统技能）。
            // 注意：这是构建期参考资料，服务端与客户端运行时都不加载它——真正生效的特效已由
            // BuildEffectIndex 按 skillId 归集后写进 MagicInfo.json 的 EffectIds 字段并下发。
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
            if (check.Any(x => x.Need1 == 0 || x.Need2 == 0 || x.Need3 == 0))
                throw new InvalidOperationException("回读校验失败：熟练度门槛为 0，技能会被瞬间刷满级");

            Console.WriteLine();
            Console.WriteLine($"技能总数 : {result.Count}（回读校验通过，Spell 唯一值 {check.Count}）");
            Console.WriteLine($"分发键   : {legacyCount} 条沿用引擎 Spell 常量，{result.Count - legacyCount} 条用原始 skillId");
            Console.WriteLine($"特效归属 : {effectsAttached} 个技能挂上 skillEffects，共 {effectBySkill.Count} 个 skillid");
            Console.WriteLine($"偏移避让 : {offsetCount} 条 id<=255 用 1000000+id 避开旧枚举常量");
            Console.WriteLine($"等级数值 : {combatCount} 条命中 2config.skills_condition，{lockedCount} 条不可升级已锁门槛");
            Console.WriteLine($"数值填充 : MP {check.Count(x => x.BaseCost > 0)} 条 / 倍率 {check.Count(x => x.MultiplierBase != 1.0f)} 条 / " +
                              $"附加伤害 {check.Count(x => x.PowerBase > 0)} 条 / 等级门槛 {check.Count(x => x.Level1 > 0)} 条");
            Console.WriteLine($"宽度收敛 : extraHurt {extraClampedCount} 条、mpCost {mpClampedCount} 条按 ushort/byte 上限截断");
            Console.WriteLine("服务器 FillMagicInfoList 已删除；5config.skill + 2config.skills_condition 驱动数值，战斗代码无需改动。");
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
        dto.Piaozi = IntFlexible(rec, "piaozi");
        dto.EffectIds = effectIds;
    }

    // 2config.skills_condition[skillId][level]：原始传奇真正的按等级数值表（208 技能 / 747 条）。
    // JS 证据（main10.25341.1.min.js）：
    //   checkMP      -> cm.skills_condition[id][level].mpCost
    //   checkSkillCDs-> cm.skills_condition[id][level].cdTime
    //   checkUpLevel -> cm.skills_condition[id][level+1].condition / .cost
    //   checkTip     -> cm.skills_condition[id][level].skilled（技能点门槛）
    // rate(万分比伤害倍率) / extraHurt(附加固定伤害) 同表，5config.skill 里没有。
    private static Dictionary<int, Dictionary<int, JsonElement>> BuildConditionIndex(JsonElement root)
    {
        var map = new Dictionary<int, Dictionary<int, JsonElement>>();
        if (!root.TryGetProperty("skills_condition", out var table) || table.ValueKind != JsonValueKind.Object) return map;
        foreach (var sk in table.EnumerateObject())
        {
            if (!int.TryParse(sk.Name, out int id) || sk.Value.ValueKind != JsonValueKind.Object) continue;
            var levels = new Dictionary<int, JsonElement>();
            foreach (var lv in sk.Value.EnumerateObject())
                if (int.TryParse(lv.Name, out int n)) levels[n] = lv.Value.Clone();
            map[id] = levels;
        }
        return map;
    }

    // 0config.attribute_score：id -> 装备属性中文名（"免　伤" 等），用于技能属性提示。
    private static Dictionary<int, string> BuildAttributeNames(JsonElement root)
    {
        var map = new Dictionary<int, string>();
        if (!root.TryGetProperty("attribute_score", out var table) || table.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var kv in table.EnumerateObject())
        {
            if (int.TryParse(kv.Name, out int id))
            {
                map[id] = Str(kv.Value, "description");
            }
        }
        return map;
    }

    private static string AttrName(Dictionary<int, string> names, int id, string fallback)
    {
        return names.TryGetValue(id, out string? name) ? name.Replace("\u3000", string.Empty).Trim() : fallback;
    }

    // 按 buff 文本匹配原始装备属性名，优先用 0config.attribute_score 的中文名。
    private static string BuffAttrName(JsonElement buff, Dictionary<int, string> attrNames)
    {
        string txt = Str(buff, "txt");
        if (string.IsNullOrWhiteSpace(txt))
        {
            txt = Str(buff, "txt1");
        }

        if (txt.Contains("抵挡") || txt.Contains("伤害降低"))
        {
            return AttrName(attrNames, 16, "免伤");
        }
        if (txt.Contains("魔法防御"))
        {
            return AttrName(attrNames, 21, "魔防上限");
        }
        if (txt.Contains("物理防御"))
        {
            return AttrName(attrNames, 7, "防御上限");
        }
        if (txt.Contains("恢复生命"))
        {
            return AttrName(attrNames, 3, "生命恢复");
        }
        if (txt.Contains("伤害提高") || txt.Contains("伤害加深"))
        {
            return AttrName(attrNames, 192, "易伤");
        }
        if (txt.Contains("每秒造成"))
        {
            return AttrName(attrNames, 55, "增加技能伤害");
        }
        if (txt.Contains("无法移动"))
        {
            return "定身";
        }
        if (txt.Contains("不能进行任何操作"))
        {
            return "眩晕";
        }
        return string.Empty;
    }

    // 属性提示优先用 5config.buff.txt（原版 BuffTips 口径），没有 buff 才用 skills_condition 的数值列。
    private static string BuildTipAttributes(JsonElement rec, Dictionary<int, Dictionary<int, JsonElement>> conditions,
        int id, Dictionary<int, string> attrNames, JsonElement buffs)
    {
        if (!conditions.TryGetValue(id, out var levels) || !levels.TryGetValue(1, out var l1))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        string buffers = Str(l1, "buffers");
        foreach (string token in buffers.Split('#'))
        {
            if (!int.TryParse(token, out _) || buffs.ValueKind != JsonValueKind.Object
                || !buffs.TryGetProperty(token, out JsonElement buff))
            {
                continue;
            }

            string txt = Str(buff, "txt");
            if (string.IsNullOrWhiteSpace(txt))
            {
                txt = Str(buff, "txt1");
            }
            txt = txt.Replace("<br>", " ").Replace("<br/>", " ").Trim();
            if (string.IsNullOrEmpty(txt))
            {
                continue;
            }

            string attrName = BuffAttrName(buff, attrNames);
            string line = string.IsNullOrEmpty(attrName) ? txt : attrName + "：" + txt;
            if (!parts.Contains(line))
            {
                parts.Add(line);
            }
        }

        int extra = Num(l1, "extraHurt");
        int rate = Num(l1, "rate");
        int monHurtAdd = Num(l1, "monHurtAdd");
        int monHurtAddFix = Num(l1, "monHurtAddFix");
        int hurt = Num(l1, "hurt");

        if (extra > 0)
        {
            parts.Add(AttrName(attrNames, 15, "增加攻击伤害") + " +" + extra);
        }

        if (rate > 0 && extra > 0)
        {
            parts.Add("伤害倍率 " + (rate / 100f).ToString("0.##") + "%");
        }
        else if (rate > 0 && parts.Count == 0)
        {
            parts.Add("触发概率 " + (rate / 100f).ToString("0.##") + "%");
        }

        if (monHurtAdd > 0)
        {
            parts.Add(AttrName(attrNames, 30, "对怪增伤") + " +" + (monHurtAdd / 100f).ToString("0.##") + "%");
        }
        if (monHurtAddFix > 0)
        {
            parts.Add(AttrName(attrNames, 27, "对怪伤害") + " +" + monHurtAddFix);
        }
        if (hurt > 0)
        {
            parts.Add(AttrName(attrNames, 58, "对人伤害") + " +" + hurt);
        }

        return string.Join("；", parts);
    }

    // 把 Lv1~Lv3 的原始传奇数值映射进服务器现用的战斗字段，C# 战斗代码一行都不改。
    // 服务器公式：MagicCost=BaseCost+LevelCost*Level；GetMultiplier=MultiplierBase+Level*MultiplierBonus；
    //             GetDelay=DelayBase-Level*DelayReduction；GainSkillExp 用 LevelN/NeedN 卡升级。
    // 因此 LevelCost/MultiplierBonus/DelayReduction 取"Lv2-Lv1"的增量，恰好与 Level 从 0 起算对齐。
    private static (bool Matched, bool Locked, bool ExtraClamped, bool MpClamped) FillCombatFields(
        MagicInfoOut dto, Dictionary<int, Dictionary<int, JsonElement>> conditions, int id)
    {
        if (!conditions.TryGetValue(id, out var levels) || levels.Count == 0)
            return (false, false, false, false);
        JsonElement Lv(int n) => levels.TryGetValue(n, out var e) ? e : default;
        JsonElement l1 = Lv(1), l2 = Lv(2), l3 = Lv(3);

        // 原始传奇 SkillTips 显示的是 skills_condition[id][level].show，不是 skill.tips1。
        dto.Show = Str(l1, "show");

        // 5config.skill.canup=0 的 120 个技能在等级表里全部只有 1 条记录，即不可升级。
        bool canUp = l2.ValueKind == JsonValueKind.Object;

        int cd1 = Num(l1, "cdTime"), cd2 = Num(l2, "cdTime");
        if (cd1 > 0) dto.DelayBase = (uint)cd1;
        if (cd1 > cd2 && cd2 > 0) dto.DelayReduction = (uint)(cd1 - cd2);

        int mp1 = Num(l1, "mpCost"), mp2 = Num(l2, "mpCost");
        bool mpClamped = mp1 > byte.MaxValue || mp2 - mp1 > byte.MaxValue;
        if (mp1 > 0) dto.BaseCost = (byte)Math.Min(mp1, byte.MaxValue);
        if (mp2 > mp1) dto.LevelCost = (byte)Math.Min(mp2 - mp1, byte.MaxValue);

        dto.Level1 = LearnLevel(l1);
        dto.Level2 = canUp ? LearnLevel(l2) : dto.Level1;
        dto.Level3 = canUp ? LearnLevel(l3) : dto.Level1;

        // NeedBook：原版 skills_condition[id][1].cost 有值（如 "13031#1"）表示 Lv1 需消耗 1 级技能书激活；
        // 无 cost 的技能（普通攻击/倚天/神龙/内功）到 condition 等级自动学会。
        dto.NeedBook = string.IsNullOrEmpty(Str(l1, "cost")) ? 0 : 1;

        ushort n1 = Skilled(l1), n2 = Skilled(l2), n3 = Skilled(l3);
        // 内功类技能（1001/2001/3001/4001）canup=1 但整张等级表没有 skilled，
        // JS checkUpLevel 走的是 condition + cost(道具消耗) 通道，服务器没有对应机制。
        bool skilledUpgrade = canUp && n1 > 0 && n2 > 0 && n3 > 0;
        if (skilledUpgrade)
        {
            dto.Need1 = n1;
            dto.Need2 = n2;
            dto.Need3 = n3;
        }
        else
        {
            // 服务器 GainSkillExp 里 Experience>=Need 就升级；Need=0 会瞬间刷到满级，
            // 用一个永远攒不到的熟练度把不走熟练度通道的技能锁住。
            dto.Need1 = dto.Need2 = dto.Need3 = ushort.MaxValue;
        }

        int rate1 = Num(l1, "rate"), rate2 = Num(l2, "rate");
        if (rate1 > 0) dto.MultiplierBase = rate1 / 10000f;
        if (rate2 > rate1 && rate1 > 0) dto.MultiplierBonus = (rate2 - rate1) / 10000f;

        int extra = Num(l1, "extraHurt");
        bool extraClamped = extra > ushort.MaxValue;
        dto.PowerBase = (ushort)Math.Min(Math.Max(0, extra), ushort.MaxValue);

        return (true, !skilledUpgrade, extraClamped, mpClamped);
    }

    // condition 是 "10019"/"10100" 这类编码 = 10000 + 玩家等级；
    // 由 5config.skill.conditionText 佐证：10030 -> "30级可学"、10100 -> "100级可学"。
    private static byte LearnLevel(JsonElement e)
    {
        int c = Num(e, "condition");
        int lv = c >= 10000 ? c - 10000 : c;
        return (byte)Math.Clamp(lv, 0, byte.MaxValue);
    }

    // skilled 形如 "200"、"1000"、"5000&12012#1"，取 & 前的技能点门槛。
    // JS 证据：checkTip 里 g=Number(d.skilled.split("&")[0])，与 roleSkill.skillPoint 比较。
    private static ushort Skilled(JsonElement e)
        => (ushort)Math.Min(Math.Max(0, Num(e, "skilled")), ushort.MaxValue);

    // 分片里的数值既可能是 JSON 数字，也可能是数字字符串（可带 & 附加条件）。
    private static int Num(JsonElement e, string key)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return n;
        if (v.ValueKind != JsonValueKind.String) return 0;
        string s = v.GetString() ?? "";
        int amp = s.IndexOf('&');
        if (amp >= 0) s = s[..amp];
        return int.TryParse(s, out n) ? n : 0;
    }

    /// <summary>
    /// 权威索引：skillEffects 键 = skillId 或 skillId*100+等级（如 13011*100+1=1301101 治愈术Lv1）。
    /// 不对无条目的技能做任何数字撞号兜底——特效号与技能号/图标号是两套编号，撞号纯属巧合。
    /// </summary>
    private static Dictionary<int, List<int>> BuildEffectIndex(JsonElement effects, HashSet<int> skillIds)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var kv in effects.EnumerateObject())
        {
            if (!int.TryParse(kv.Name, out int key)) continue;
            int skillId = skillIds.Contains(key) ? key : key / 100;
            if (!skillIds.Contains(skillId)) continue;   // 怪物/系统技能的特效，不属于任何玩家技能
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
            if (ids.Count > 0)
            {
                if (!map.TryGetValue(skillId, out var list)) map[skillId] = list = new List<int>();
                list.AddRange(ids);   // 同技能多等级条目合并
            }
        }
        return map;
    }

    private static int Int(JsonElement e, string key, int def = 0)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
           && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : def;

    // 原始 5config.skill 的 piaozi 是字符串数字（如 "26"），Int() 只认 Number 会全部丢成 0。
    private static int IntFlexible(JsonElement e, string key, int def = 0)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out JsonElement v))
        {
            return def;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int numberValue))
        {
            return numberValue;
        }

        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int stringValue))
        {
            return stringValue;
        }

        return def;
    }

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
        string Input, string Condition, string Output, string Effects, bool NoEffects, bool DryRun);

    private static Options ParseArgs(string[] args, string root, string excel)
    {
        // 唯一权威源：资源提取工具下的原始传奇分片（中文原版），不再是 ExcelConfig 里的副本。
        string sourceDir = Path.Combine(root, "Tools", "原始传奇-资源提取", "原始传奇-资源参考");
        string input = Path.Combine(sourceDir, "5config10.25341.1.json");
        string condition = Path.Combine(sourceDir, "2config10.25341.1.json");
        string output = Path.Combine(excel, "MagicInfo.json");
        string effects = Path.Combine(excel, "SkillEffectInfo.json");
        bool noEffects = false, dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input": input = args[++i]; break;
                case "--condition": condition = args[++i]; break;
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
        return new Options(input, condition, output, effects, noEffects, dryRun);
    }
}
