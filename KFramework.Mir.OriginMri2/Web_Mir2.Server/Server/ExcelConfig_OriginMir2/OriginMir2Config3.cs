// 原始传奇配置分片 3config 的根类（对应 3config10.25341.1.json）。
// 顶层 27 张表，其中 npc 是 Mir2NPCConverter 的生成源（导出 NPCInfo.json），
// 其余 26 张表由基类 ExtraTables 原样兜底；需要排查时给本类加具体 DTO 属性即可。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMir2Config3 : OriginConfigRoot
    {
        // NPC 表（840）：name/model/depiction/button/function/storeId/scale...
        // Mir2NPCConverter 用它导出 NPCInfo.json；运行时不直接读，仅加载进来方便对比排查。
        [JsonPropertyName("npc")] public Dictionary<string, OriginNPCTable> npc { get; set; } = new();
    }
}
