using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 5config.goals：任务目标表，key = 目标 id（如 532000001）。mapId/x/y 原始为字符串，保持 string。
    public class OriginGoalTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 目标 id
        [JsonPropertyName("type")] public int type { get; set; } // 目标类型（335=击杀 516=学技能 等）
        [JsonPropertyName("count")] public int count { get; set; } // 目标数量
        [JsonPropertyName("parentType")] public int parentType { get; set; } // 父类型
        [JsonPropertyName("goInfo")] public string goInfo { get; set; } = ""; // 目标描述文本（| 分两段，{0}=数量）
        [JsonPropertyName("param")] public string param { get; set; } = ""; // 目标参数（怪物id/物品id 等，# 分隔）
        [JsonPropertyName("link")] public string link { get; set; } = ""; // 关联链接
        [JsonPropertyName("mapId")] public string mapId { get; set; } = ""; // 目标地图 id
        [JsonPropertyName("x")] public string x { get; set; } = ""; // 目标点 x
        [JsonPropertyName("y")] public string y { get; set; } = ""; // 目标点 y
        // 原始配置有 4 条显式 descType:null，用 int? 避免整表反序列化失败
        [JsonPropertyName("descType")] public int? descType { get; set; } // 描述类型（可为 null）
    }
}
