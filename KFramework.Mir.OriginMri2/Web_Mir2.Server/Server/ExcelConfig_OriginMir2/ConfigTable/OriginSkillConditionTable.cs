using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 2config.skills_condition：技能等级条件表（技能id → 等级 → 条件记录）。condition 原始为字符串（如 "10100"）。
    public class OriginSkillConditionTable
    {
        [JsonPropertyName("skillId")] public int skillId { get; set; } // 技能 id
        [JsonPropertyName("level")] public int level { get; set; } // 技能等级
        [JsonPropertyName("effLevelID")] public int effLevelID { get; set; } // 生效等级 id
        [JsonPropertyName("condition")] public string condition { get; set; } = ""; // 学习条件（10000+等级）
        [JsonPropertyName("rate")] public int rate { get; set; } // 触发概率
        [JsonPropertyName("monHurtAddFix")] public int monHurtAddFix { get; set; } // 对怪伤害加成
        [JsonPropertyName("show")] public string show { get; set; } = ""; // 显示文本
        [JsonPropertyName("cdTime")] public int cdTime { get; set; } // 冷却时间
        [JsonPropertyName("mpCost")] public int mpCost { get; set; } // 魔法消耗
    }
}
