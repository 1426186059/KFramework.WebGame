// 5config.effect：特效表（2336 条）。AnimExportService 用：effectId → model，给特效模型取名/归类。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginEffectTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 特效 id
        [JsonPropertyName("model")] public int model { get; set; } // 关联模型 id
        [JsonPropertyName("action")] public int action { get; set; } // 动作
        [JsonPropertyName("dir")] public int dir { get; set; } // 方向
        [JsonPropertyName("totaldir")] public int totaldir { get; set; } // 方向数
        [JsonPropertyName("intval")] public int intval { get; set; } // 帧间隔
        [JsonPropertyName("blendmode")] public string blendmode { get; set; } = ""; // 混合模式
    }
}
