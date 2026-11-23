// 原始传奇配置分片 "4config" 的整文件 DTO（仿客户端 OriginMir2Config0/2/5 写法）。
// 顶层仅取用 npcstore（商店 id → 商品记录），其余顶层表反序列化时自动忽略。
// 服务端用它把原始传奇商店（NPCInfo.OriginSettings.StoreId）转成可下发的商品列表。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
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

    /// <summary>整个 4config 分片 JSON 的 DTO。npcstore 键为数字字符串，自动转 int 字典键。</summary>
    public class OriginMir2Config4
    {
        [JsonPropertyName("npcstore")]
        public Dictionary<int, Dictionary<int, OriginStoreItemTable>> npcstore { get; set; } = new();

        /// <summary>4config._task 任务权威表（key = 任务 id），单条 DTO 见 OriginTaskTable。</summary>
        [JsonPropertyName("_task")]
        public Dictionary<string, OriginTaskTable> _task { get; set; } = new();
    }

    /// <summary>0config.conditions 单条条件（原始传奇 Logic.checkCondition 用）。</summary>
    public class OriginConditionTable
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        [JsonPropertyName("conditionType")]
        public int conditionType { get; set; }

        [JsonPropertyName("conditionParam")]
        public string conditionParam { get; set; } = "";

        [JsonPropertyName("txt")]
        public string txt { get; set; } = "";
    }

    /// <summary>0config.global 单条全局配置（任务类型名 global[9201] 用）。</summary>
    public class OriginGlobalTable
    {
        [JsonPropertyName("id")]
        public int id { get; set; }

        /// <summary>9201 的值形如 "1#[主线]&amp;201#[转生]&amp;..."（type#[前缀]&amp;...）。</summary>
        [JsonPropertyName("value")]
        public string value { get; set; } = "";
    }

    /// <summary>0config 分片 DTO，仅取 conditions 条件表 + global 全局表（任务类型前缀）。
    /// key 用 string：原始配置里存在 "undefined" 等非数字 key，用 int 会让整表反序列化失败。</summary>
    public class OriginMir2Config0
    {
        [JsonPropertyName("conditions")]
        public Dictionary<string, OriginConditionTable> conditions { get; set; } = new();

        [JsonPropertyName("global")]
        public Dictionary<string, OriginGlobalTable> global { get; set; } = new();
    }
}
