// 3config.npc：NPC 表（840 条）。Mir2NPCConverter 用：导出 NPCInfo.json（名字/外观/对话/按钮/功能/商店/缩放）。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginNPCTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // NPC id
        [JsonPropertyName("name")] public string name { get; set; } = ""; // NPC 名
        [JsonPropertyName("title")] public string title { get; set; } = ""; // 对话框标题
        [JsonPropertyName("model")] public int model { get; set; } // 模型号
        [JsonPropertyName("scale")] public int scale { get; set; } // 显示缩放百分比（如 40/90）
        [JsonPropertyName("depiction")] public string depiction { get; set; } = ""; // 对话文本（& 分段）
        [JsonPropertyName("button")] public string button { get; set; } = ""; // 按钮文本
        [JsonPropertyName("function")] public string function { get; set; } = ""; // 功能串
        [JsonPropertyName("deliver")] public string deliver { get; set; } = ""; // 传送点
        [JsonPropertyName("storeId")] public int storeId { get; set; } // 商店 id（0=无商店）
        [JsonPropertyName("openCondition")] public string openCondition { get; set; } = ""; // 开启条件
        [JsonPropertyName("showcondition")] public string showcondition { get; set; } = ""; // 显示条件
        [JsonPropertyName("tips")] public string tips { get; set; } = ""; // 提示文本
    }
}
