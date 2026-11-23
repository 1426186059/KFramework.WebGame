// 5config.title：称号表（2503 条）。AnimExportService 用：称号 model 特效取名。
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginTitleTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 称号 id
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 称号名
        [JsonPropertyName("model")] public JsonElement model { get; set; } // 模型号（数字/字符串混用）
        [JsonPropertyName("isShow")] public int isShow { get; set; } // 是否展示
        [JsonPropertyName("type")] public int type { get; set; } // 类型
        [JsonPropertyName("attribute")] public string attribute { get; set; } = ""; // 属性串
    }
}
