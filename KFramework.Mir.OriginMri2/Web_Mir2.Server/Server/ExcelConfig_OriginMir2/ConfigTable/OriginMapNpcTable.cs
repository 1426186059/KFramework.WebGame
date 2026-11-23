using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 5config.mapnpc：NPC 摆放表，key = 摆放记录 id。
    public class OriginMapNpcTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 摆放记录 id
        [JsonPropertyName("npcid")] public int npcid { get; set; } // NPC id
        [JsonPropertyName("mapid")] public int mapid { get; set; } // 地图 id
        [JsonPropertyName("x")] public int x { get; set; } // 坐标 x
        [JsonPropertyName("y")] public int y { get; set; } // 坐标 y
    }
}
