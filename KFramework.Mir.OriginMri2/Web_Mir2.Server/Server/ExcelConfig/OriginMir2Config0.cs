// 原始传奇配置分片 0config 的表 DTO（对应 0config10.25341.1.json）。
// 已建具体 DTO 的表：conditions / global；其余 100 张表由基类 ExtraTables 全量兜底。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    /// <summary>0config.conditions 单条条件（原始传奇 Logic.checkCondition 用）。</summary>
    public class OriginConditionTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 条件 id
        [JsonPropertyName("conditionType")] public int conditionType { get; set; } // 条件类型（1=等级≥ 2=等级≤ 9=开服天数 34=区间）
        [JsonPropertyName("conditionParam")] public string conditionParam { get; set; } = ""; // 条件参数（等级/天数等）
        [JsonPropertyName("txt")] public string txt { get; set; } = ""; // 条件显示文本
    }

    /// <summary>0config.global 单条全局配置（任务类型名 global[9201] 用）。</summary>
    public class OriginGlobalTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 全局配置 id（9201=任务类型前缀）
        // 9201 的值形如 "1#[主线]&amp;201#[转生]&amp;..."（type#[前缀]&amp;...）
        [JsonPropertyName("value")] public string value { get; set; } = "";
    }

    /// <summary>0config 分片根类（conditions/global 强类型，其余表走基类 ExtraTables）。
    /// key 用 string：原始配置里存在 "undefined" 等非数字 key，用 int 会让整表反序列化失败。</summary>
    public class OriginMir2Config0 : OriginConfigRoot
    {
        [JsonPropertyName("conditions")] public Dictionary<string, OriginConditionTable> conditions { get; set; } = new(); // 条件表（id → 条件）
        [JsonPropertyName("global")] public Dictionary<string, OriginGlobalTable> global { get; set; } = new(); // 全局配置表（id → 值）
    }
}
