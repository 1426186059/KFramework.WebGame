using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 5config.textstyle：富文本样式表（任务追踪文字颜色来源）。
    // 原版 Logic.getCol(id) = cm.textstyle[id].textColor；任务追踪 info0=31(白) / info1=32(绿+下划线) / 标题=28/29(金)。
    public class OriginTextStyleTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 样式 id（31=白 32=绿+下划线）
        [JsonPropertyName("stroke")] public int stroke { get; set; } // 描边宽度
        [JsonPropertyName("bold")] public int bold { get; set; } // 是否粗体
        [JsonPropertyName("size")] public int size { get; set; } // 字号
        [JsonPropertyName("underline")] public int underline { get; set; } // 是否下划线
        [JsonPropertyName("textColor")] public string textColor { get; set; } = ""; // 文字颜色（无 # 前缀）
        [JsonPropertyName("strokeColor")] public string strokeColor { get; set; } = ""; // 描边颜色
    }
}
