// 5config.soundnormal：地图背景音表（4 组）。MapInfoEditor 用：soundnormal["3"][mapId].file → 地图 BGM。结构 Group → mapId → 记录。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginSoundNormalTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 音效 id
        [JsonPropertyName("index")] public int index { get; set; } // 索引（通常是 mapId 或音效编号）
        [JsonPropertyName("type")] public int type { get; set; } // 类型
        [JsonPropertyName("mode")] public int mode { get; set; } // 播放模式
        [JsonPropertyName("file")] public int file { get; set; } // 音频文件号
        [JsonPropertyName("level")] public int level { get; set; } // 层级
    }
}
