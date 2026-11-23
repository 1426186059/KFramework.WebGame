using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 4config._task / 2config._task_tx：任务表 DTO（两表字段集合一致，共用）。key = 任务 id。
    public class OriginTaskTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 任务 id
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 任务名
        [JsonPropertyName("next")] public int next { get; set; } // 下一环任务 id（0=终点）
        [JsonPropertyName("condition")] public string condition { get; set; } = ""; // 接取条件 id 串（& 分隔，指向 0config.conditions）
        [JsonPropertyName("goals")] public string goals { get; set; } = ""; // 任务目标 id 串（# 分隔，指向 5config.goals）
        [JsonPropertyName("rewards")] public string rewards { get; set; } = ""; // 奖励包串（boxId#份数，& 分隔）
        [JsonPropertyName("mutireward")] public string mutireward { get; set; } = ""; // 多奖励（原版备用）
        [JsonPropertyName("reward")] public string reward { get; set; } = ""; // 奖励描述文本
        [JsonPropertyName("fnpc")] public int fnpc { get; set; } // 发放/接取 NPC id
        [JsonPropertyName("tnpc")] public int tnpc { get; set; } // 交付 NPC id
        [JsonPropertyName("type")] public int type { get; set; } // 任务类型
        [JsonPropertyName("tip3")] public string tip3 { get; set; } = ""; // 接任务时 NPC 对话文本
        [JsonPropertyName("completeinfo")] public string completeinfo { get; set; } = ""; // 完成待交文本（| 分两段）
        [JsonPropertyName("taskgroupid")] public int taskgroupid { get; set; } // 任务组 id
        [JsonPropertyName("state")] public int state { get; set; } // 起始状态（2=自动接取 1=手动接取）
        [JsonPropertyName("limit")] public int limit { get; set; } // 限制（原版备用）
        [JsonPropertyName("star")] public int star { get; set; } // 星级
        [JsonPropertyName("mode")] public int mode { get; set; } // 模式
        [JsonPropertyName("chuansong")] public int chuansong { get; set; } // 传送点 id
        [JsonPropertyName("overchuan")] public int overchuan { get; set; } // 越级传送点 id
        [JsonPropertyName("autopathing")] public int autopathing { get; set; } // 自动寻路开关
        [JsonPropertyName("autofight")] public int autofight { get; set; } // 自动战斗开关
        [JsonPropertyName("autofinsh")] public int autofinsh { get; set; } // 自动完成开关
        [JsonPropertyName("nofly")] public int nofly { get; set; } // 禁止飞行开关
        [JsonPropertyName("showboss")] public string showboss { get; set; } = ""; // 显示 Boss
        [JsonPropertyName("tradebox")] public int tradebox { get; set; } // 交易宝箱
        // item 字段样本全为 null，保留原始 JsonElement 不丢失信息
        [JsonPropertyName("item")] public JsonElement item { get; set; } // 原版备用字段（样本全为 null）
    }
}
