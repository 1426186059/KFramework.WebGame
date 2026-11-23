// 原始传奇配置分片 4config 的表 DTO（对应 4config10.25341.1.json）。
// 已建具体 DTO 的表：npcstore / _task；其余 73 张表由基类 ExtraTables 全量兜底。
// 任务表 DTO OriginTaskTable 同时被 2config._task_tx 复用，故定义在本文件。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    // ===================== 4config._task / 2config._task_tx：任务表 DTO =====================
    // 两表字段集合一致，共用同一 DTO；任务权威表在 4config._task，故放在本文件。key = 任务 id。
    public class OriginTaskTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("name")] public string name { get; set; } = "";
        [JsonPropertyName("next")] public int next { get; set; }
        [JsonPropertyName("condition")] public string condition { get; set; } = "";
        [JsonPropertyName("goals")] public string goals { get; set; } = "";
        [JsonPropertyName("rewards")] public string rewards { get; set; } = "";
        [JsonPropertyName("mutireward")] public string mutireward { get; set; } = "";
        [JsonPropertyName("reward")] public string reward { get; set; } = "";
        [JsonPropertyName("fnpc")] public int fnpc { get; set; }
        [JsonPropertyName("tnpc")] public int tnpc { get; set; }
        [JsonPropertyName("type")] public int type { get; set; }
        [JsonPropertyName("tip3")] public string tip3 { get; set; } = "";
        [JsonPropertyName("completeinfo")] public string completeinfo { get; set; } = "";
        [JsonPropertyName("taskgroupid")] public int taskgroupid { get; set; }
        [JsonPropertyName("state")] public int state { get; set; }
        [JsonPropertyName("limit")] public int limit { get; set; }
        [JsonPropertyName("star")] public int star { get; set; }
        [JsonPropertyName("mode")] public int mode { get; set; }
        [JsonPropertyName("chuansong")] public int chuansong { get; set; }
        [JsonPropertyName("overchuan")] public int overchuan { get; set; }
        [JsonPropertyName("autopathing")] public int autopathing { get; set; }
        [JsonPropertyName("autofight")] public int autofight { get; set; }
        [JsonPropertyName("autofinsh")] public int autofinsh { get; set; }
        [JsonPropertyName("nofly")] public int nofly { get; set; }
        [JsonPropertyName("showboss")] public string showboss { get; set; } = "";
        [JsonPropertyName("tradebox")] public int tradebox { get; set; }
        // item 字段样本全为 null，保留原始 JsonElement 不丢失信息。
        [JsonPropertyName("item")] public JsonElement item { get; set; }
    }

    /// <summary>4config.npcstore 单条商品记录。</summary>
    public class OriginStoreItemTable
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        [JsonPropertyName("storeId")]
        public int storeId { get; set; }

        /// <summary>"物品id#数量"，如 "8151#1" / "1404000#1"。</summary>
        [JsonPropertyName("item")]
        public string item { get; set; } = "";

        /// <summary>"货币物品id#价格"，如 "499#100"（金币）/ "41#1000"。</summary>
        [JsonPropertyName("cost")]
        public string cost { get; set; } = "";

        [JsonPropertyName("showCondition")]
        public string showCondition { get; set; } = "";
    }

    /// <summary>4config 分片根类（npcstore/_task 强类型，其余表走基类 ExtraTables）。</summary>
    public class OriginMir2Config4 : OriginConfigRoot
    {
        [JsonPropertyName("npcstore")]
        public Dictionary<int, Dictionary<int, OriginStoreItemTable>> npcstore { get; set; } = new();

        /// <summary>4config._task 任务权威表（key = 任务 id），单条 DTO 见 OriginTaskTable。</summary>
        [JsonPropertyName("_task")]
        public Dictionary<string, OriginTaskTable> _task { get; set; } = new();
    }
}
