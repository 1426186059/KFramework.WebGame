// 0config.attribute_score：属性名表（150 条）。Mir2ItemConverter（DumpAttributeScore）用：id → 属性显示名，供物品 col40 属性串解析。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginAttributeScoreTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 属性 id（item.col40 的 key）
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 属性英文名（hp/atk/...）
        [JsonPropertyName("description")] public string description { get; set; } = ""; // 属性中文名
        [JsonPropertyName("tips")] public string tips { get; set; } = ""; // 属性提示文本
        [JsonPropertyName("type")] public int type { get; set; } // 类型
        [JsonPropertyName("show")] public int show { get; set; } // 是否显示
    }
}
