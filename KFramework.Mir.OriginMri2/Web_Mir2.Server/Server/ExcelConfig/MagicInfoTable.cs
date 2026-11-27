using System;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 静态配置 DTO：只保存 MagicInfo.json 里的原始列，不保存任何运行期状态。
    // 运行期对象（等级/熟练度/存档/发包/战斗计算）在 MagicInfoRunItem（MagicInfoRunItem.cs）。
    public class MagicInfoTable
    {
        // 战斗数值字段：字段名沿用现有引擎公式（GetDamage/GetPower/GetDelay/MagicCost），
        // 值由 Tools/Mir2SkillConverter 从《原始传奇》2config.skills_condition 按等级映射，
        // MagicCost=BaseCost+LevelCost*Level、GetMultiplier=MultiplierBase+Level*MultiplierBonus，
        // 因此 LevelCost/MultiplierBonus/DelayReduction 存 Lv2-Lv1 增量；战斗代码无需改动。
        public string Name { get; set; }
        public Spell Spell { get; set; }
        public byte BaseCost { get; set; }
        public byte LevelCost { get; set; }
        public byte Icon { get; set; }
        public byte Level1 { get; set; }
        public byte Level2 { get; set; }
        public byte Level3 { get; set; }
        public ushort Need1 { get; set; }
        public ushort Need2 { get; set; }
        public ushort Need3 { get; set; }
        public uint DelayBase { get; set; } = 1800;
        public uint DelayReduction { get; set; }
        public ushort PowerBase { get; set; }
        public ushort PowerBonus { get; set; }
        public ushort MPowerBase { get; set; }
        public ushort MPowerBonus { get; set; }
        public float MultiplierBase { get; set; } = 1.0f;
        public float MultiplierBonus { get; set; }
        public byte Range { get; set; } = 9;

        // 《原始传奇》5config.skill 的字段直接平铺在本表，不再内嵌原始记录副本。
        // 重复语义由转换器统一派生：Range=releaseDis、Spell=skill.id（38 条沿用引擎效果分发键）。
        // DelayBase 取 2config.skills_condition 的 cdTime（JS checkSkillCDs 的口径）。
        // 未纳入的全量原始列用 OriginId 回查权威分片即可，不在运行时表里存第二份。
        public int OriginId { get; set; }
        public string OriginIcon { get; set; } = "";
        public string ConditionText { get; set; } = "";
        public string Tips1 { get; set; } = "";
        // 原始传奇 SkillTips 的正文：skills_condition[id][level].show（Lv1 口径）。
        public string Show { get; set; } = "";
        // 增强提示属性行：0config.attribute_score 属性名 + 5config.buff/skills_condition 数值。
        public string TipAttributes { get; set; } = "";
        public int Cls { get; set; }
        public int Career { get; set; }
        public int IsShow { get; set; }
        public int Condition { get; set; }
        public int HandlerType { get; set; }
        public int Aoe { get; set; }
        public int MaxTarget { get; set; }
        public int CanUp { get; set; }
        public int TargetType { get; set; }
        public int Action { get; set; }
        public int HurtDelay { get; set; }
        public int ReleaseDis { get; set; }
        public int ReleaseDis2 { get; set; }
        public int ReleaseType { get; set; }
        public int AreaDir { get; set; }
        public int AreaDis { get; set; }
        public int CommonCd { get; set; }
        public int SkillParams { get; set; }
        public int NotargetEff { get; set; }
        public int Passive { get; set; }
        public int IsAuto { get; set; }
        public int OriginIndex { get; set; }
        [JsonPropertyName("piaozi")]
        public int Piaozi { get; set; }
        public int[] EffectIds { get; set; } = Array.Empty<int>();
        // 1=需技能书激活（config cost），0=达等级自动学会。
        public int NeedBook { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
