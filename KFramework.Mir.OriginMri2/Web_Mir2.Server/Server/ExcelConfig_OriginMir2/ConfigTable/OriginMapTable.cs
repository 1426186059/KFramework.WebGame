// 0config.map：地图表（991 条）。Mir2QuestConverter（任务目标地图名）、MapInfoEditor / OriginalMapSource（地图名与像素尺寸）用。
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMapTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 地图 id
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 地图名
        [JsonPropertyName("img")] public JsonElement img { get; set; } // 地图图号（数字/字符串混用，保留原始）
        [JsonPropertyName("data")] public JsonElement data { get; set; } // 地图数据号
        [JsonPropertyName("width")] public int width { get; set; } // 像素宽
        [JsonPropertyName("height")] public int height { get; set; } // 像素高
        [JsonPropertyName("cls")] public int cls { get; set; } // 地图分类
        [JsonPropertyName("condition")] public string condition { get; set; } = ""; // 进入条件 id 串
        [JsonPropertyName("showText")] public string showText { get; set; } = ""; // 进入条件说明
        [JsonPropertyName("safe")] public int safe { get; set; } // 是否安全区
        [JsonPropertyName("homeXY")] public string homeXY { get; set; } = ""; // 回城点（地图id#x#y）
        [JsonPropertyName("deathDrop")] public string deathDrop { get; set; } = ""; // 死亡掉落配置
    }
}
