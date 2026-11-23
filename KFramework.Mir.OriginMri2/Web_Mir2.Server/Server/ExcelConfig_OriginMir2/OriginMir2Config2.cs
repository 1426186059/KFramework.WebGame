// 原始传奇配置分片 2config 的表 DTO（对应 2config10.25341.1.json）。
// 风格：属性名 = 原键名，[JsonPropertyName] 绑定，不做语义改名。
// Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
// 单表 DTO 定义在 ConfigTable/ 子目录。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // ===================== 2config 顶层容器 =====================
    // 已建具体 DTO 的表：_task_tx / skills_condition / box；其余 84 张表由基类 ExtraTables 全量兜底。
    public class OriginMir2Config2 : OriginConfigRoot
    {
        [JsonPropertyName("_task_tx")] public Dictionary<string, OriginTaskTable> _task_tx { get; set; } = new(); // 任务文本线（id → 任务）
        [JsonPropertyName("skills_condition")] public Dictionary<string, Dictionary<string, OriginSkillConditionTable>> skills_condition { get; set; } = new(); // 技能等级条件（技能id → 等级 → 条件）
        [JsonPropertyName("box")] public Dictionary<string, OriginBoxTable> box { get; set; } = new(); // 奖励包表（id → 包）
        // 模型动作表（4725）：modelId → action("1"/"2"/...) → {intval/totaldir/standx/standy}，动画导出与帧间隔来源。
        [JsonPropertyName("modelInfo")] public Dictionary<string, Dictionary<string, OriginModelInfoTable>> modelInfo { get; set; } = new();
    }
}
