using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    /// <summary>0config.conditions 单条条件（原始传奇 Logic.checkCondition 用）。</summary>
    public class OriginConditionTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 条件 id
        [JsonPropertyName("conditionType")] public int conditionType { get; set; } // 条件类型（1=等级≥ 2=等级≤ 9=开服天数 34=区间）
        [JsonPropertyName("conditionParam")] public string conditionParam { get; set; } = ""; // 条件参数（等级/天数等）
        [JsonPropertyName("txt")] public string txt { get; set; } = ""; // 条件显示文本
    }
}
