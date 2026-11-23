// 原始传奇配置分片 3config 的根类（对应 3config10.25341.1.json）。
// 顶层 27 张表，已显式加载 npc 表（840 条），其余 26 张表由基类 ExtraTables 原样兜底。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMir2Config3 : OriginConfigRoot
    {
        // NPC 表（840）：name/model/depiction/button/function/storeId/scale... Mir2NPCConverter 的生成源。
        [JsonPropertyName("npc")] public Dictionary<string, OriginNPCTable> npc { get; set; } = new();
    }
}
