// 2config.modelInfo：模型动作表（4725 条）。客户端动画/AnimExportService 用：modelId → action → {totaldir/standx/standy/intval}。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginModelInfoTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 模型 id
        [JsonPropertyName("action")] public int action { get; set; } // 动作号（1=站 2=走 4=攻 10=被击 12=死）
        [JsonPropertyName("intval")] public int intval { get; set; } // 每帧间隔（毫秒）
        [JsonPropertyName("totaldir")] public int totaldir { get; set; } // 方向数（1/5/8）
        [JsonPropertyName("standx")] public double standx { get; set; } // 枢轴 x（原版有小数，如 533.7）
        [JsonPropertyName("standy")] public double standy { get; set; } // 枢轴 y（原版有小数）
    }
}
