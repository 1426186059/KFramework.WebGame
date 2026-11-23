// 原始传奇配置分片 5config 的表 DTO（对应 5config10.25341.1.json）。
// 风格：属性名 = 原键名，[JsonPropertyName] 绑定，不做语义改名。
// Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
// 单表 DTO 定义在 ConfigTable/ 子目录。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // ===================== 5config 顶层容器 =====================
    // 已建具体 DTO 的表：goals / mapnpc / skill / textstyle；其余 70 张表由基类 ExtraTables 全量兜底。
    public class OriginMir2Config5 : OriginConfigRoot
    {
        [JsonPropertyName("goals")] public Dictionary<string, OriginGoalTable> goals { get; set; } = new(); // 任务目标表（id → 目标）
        [JsonPropertyName("mapnpc")] public Dictionary<string, OriginMapNpcTable> mapnpc { get; set; } = new(); // NPC 摆放表（id → 摆放点）
        [JsonPropertyName("skill")] public Dictionary<string, OriginSkillTable> skill { get; set; } = new(); // 技能表（id → 技能）
        [JsonPropertyName("textstyle")] public Dictionary<string, OriginTextStyleTable> textstyle { get; set; } = new(); // 富文本样式表（id → 样式）
        [JsonPropertyName("soundnormal")] public Dictionary<string, Dictionary<string, OriginSoundNormalTable>> soundnormal { get; set; } = new(); // 地图背景音（4组：group → mapId → 音频记录），MapInfoEditor 用
        [JsonPropertyName("skillEffects")] public Dictionary<string, List<OriginSkillEffectTable>> skillEffects { get; set; } = new(); // 技能特效归属（key=技能id或技能id*100+等级 → 特效记录数组），Mir2SkillConverter/动画导出用
        [JsonPropertyName("effect")] public Dictionary<string, OriginEffectTable> effect { get; set; } = new(); // 特效表（2336）：effectId → model，动画导出取名用
        [JsonPropertyName("title")] public Dictionary<string, OriginTitleTable> title { get; set; } = new(); // 称号表（2503）：称号 model 特效取名用
    }
}
