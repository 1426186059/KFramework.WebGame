// 原始传奇配置分片 0config 的表 DTO（对应 0config10.25341.1.json）。
// 已建具体 DTO 的表：conditions / global；其余 100 张表由基类 ExtraTables 全量兜底。
// 单表 DTO 定义在 ConfigTable/ 子目录。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    /// <summary>0config 分片根类（conditions/global 强类型，其余表走基类 ExtraTables）。
    /// key 用 string：原始配置里存在 "undefined" 等非数字 key，用 int 会让整表反序列化失败。</summary>
    public class OriginMir2Config0 : OriginConfigRoot
    {
        [JsonPropertyName("conditions")] public Dictionary<string, OriginConditionTable> conditions { get; set; } = new(); // 条件表（id → 条件）
        [JsonPropertyName("global")] public Dictionary<string, OriginGlobalTable> global { get; set; } = new(); // 全局配置表（id → 值）
        [JsonPropertyName("map")] public Dictionary<string, OriginMapTable> map { get; set; } = new(); // 地图表（991）：任务目标地图名 / 地图像素尺寸（Mir2QuestConverter、MapInfoEditor）
        [JsonPropertyName("attribute_score")] public Dictionary<string, OriginAttributeScoreTable> attribute_score { get; set; } = new(); // 属性名表（150）：物品 col40 属性串解析（Mir2ItemConverter）
    }
}
