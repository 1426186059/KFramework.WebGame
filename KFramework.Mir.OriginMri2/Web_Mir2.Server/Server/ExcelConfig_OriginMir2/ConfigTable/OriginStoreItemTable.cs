using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    /// <summary>4config.npcstore 单条商品记录。</summary>
    public class OriginStoreItemTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 商品记录 id
        [JsonPropertyName("storeId")] public int storeId { get; set; } // 所属商店 id
        // "物品id#数量"，如 "8151#1" / "1404000#1"
        [JsonPropertyName("item")] public string item { get; set; } = "";
        // "货币物品id#价格"，如 "499#100"（金币）/ "41#1000"
        [JsonPropertyName("cost")] public string cost { get; set; } = "";
        [JsonPropertyName("showCondition")] public string showCondition { get; set; } = ""; // 显示条件 id 串（指向 0config.conditions）
    }
}
