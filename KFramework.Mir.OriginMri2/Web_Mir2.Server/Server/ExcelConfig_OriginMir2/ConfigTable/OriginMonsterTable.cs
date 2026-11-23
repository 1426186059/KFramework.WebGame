// 4config.monster：怪物表（4848 条）。Mir2MonsterConverter 用：导出 MonsterInfo.json（名字/模型/等级/属性/掉落）。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMonsterTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 怪物 id
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 怪物名
        [JsonPropertyName("model")] public int model { get; set; } // 模型号
        [JsonPropertyName("level")] public int level { get; set; } // 等级
        [JsonPropertyName("type")] public int type { get; set; } // 类型
        [JsonPropertyName("color")] public string color { get; set; } = ""; // 名字颜色
        [JsonPropertyName("scale")] public int scale { get; set; } // 显示缩放
        [JsonPropertyName("attribute")] public string attribute { get; set; } = ""; // 属性串（id#值|...）
        [JsonPropertyName("bossDrop")] public string bossDrop { get; set; } = ""; // Boss 掉落包 id
        [JsonPropertyName("gather")] public string gather { get; set; } = ""; // 采集产物
    }
}
