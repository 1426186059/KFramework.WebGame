// 5config.skillEffects：技能特效归属表（176 条）。Mir2SkillConverter / AnimExportService 用：key=技能id 或 技能id*100+等级，值是特效记录数组。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginSkillEffectTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 记录 id
        [JsonPropertyName("skillid")] public int skillid { get; set; } // 技能 id
        [JsonPropertyName("effect")] public int effect { get; set; } // 特效模型 id
        [JsonPropertyName("frame")] public int frame { get; set; } // 帧
        [JsonPropertyName("delaytime")] public int delaytime { get; set; } // 延迟
        [JsonPropertyName("type")] public int type { get; set; } // 类型
        [JsonPropertyName("pos")] public int pos { get; set; } // 位置
    }
}
