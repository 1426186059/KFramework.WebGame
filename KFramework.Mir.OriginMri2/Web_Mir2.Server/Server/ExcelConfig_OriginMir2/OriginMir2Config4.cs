// 原始传奇配置分片 4config 的表 DTO（对应 4config10.25341.1.json）。
// 已建具体 DTO 的表：npcstore / _task；其余 73 张表由基类 ExtraTables 全量兜底。
// 单表 DTO 定义在 ConfigTable/ 子目录。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    /// <summary>4config 分片根类（npcstore/_task 强类型，其余表走基类 ExtraTables）。</summary>
    public class OriginMir2Config4 : OriginConfigRoot
    {
        [JsonPropertyName("npcstore")] public Dictionary<int, Dictionary<int, OriginStoreItemTable>> npcstore { get; set; } = new(); // 商店表（商店id → 商品记录）
        // 4config._task 任务权威表（key = 任务 id），单条 DTO 见 OriginTaskTable
        [JsonPropertyName("_task")] public Dictionary<string, OriginTaskTable> _task { get; set; } = new();
        // 怪物表（4848）：name/model/level/attribute/bossDrop/gather，Mir2MonsterConverter 导出 MonsterInfo.json 的源表。
        [JsonPropertyName("monster")] public Dictionary<string, OriginMonsterTable> monster { get; set; } = new();
    }
}
