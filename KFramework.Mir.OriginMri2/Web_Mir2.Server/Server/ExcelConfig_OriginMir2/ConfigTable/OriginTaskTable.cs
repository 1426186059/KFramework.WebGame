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
        [JsonPropertyName("goals")] public string goals { get; set; } = ""; // 目标 id：单值或 | 分隔多个，指向 5config.goals（cm.goals）
        [JsonPropertyName("rewards")] public string rewards { get; set; } = ""; // 奖励包串（boxId#份数，& 分隔）
        [JsonPropertyName("mutireward")] public string mutireward { get; set; } = ""; // 多重领取参数（# 分隔，如 6#40#2；JS 用于显示剩余次数）
        [JsonPropertyName("reward")] public string reward { get; set; } = ""; // 奖励描述文本（# 分两段：说明/按钮文字）
        [JsonPropertyName("fnpc")] public int fnpc { get; set; } // 接取 NPC 的 mapnpcid（cm.mapnpc[fnpc]）
        [JsonPropertyName("tnpc")] public int tnpc { get; set; } // 交付 NPC 的 mapnpcid（cm.mapnpc[tnpc]）
        [JsonPropertyName("type")] public int type { get; set; } // 任务类型/分组（1=主线 201=转生试炼等，TaskType）
        [JsonPropertyName("tip3")] public string tip3 { get; set; } = ""; // 任务描述文本（任务面板正文）
        [JsonPropertyName("completeinfo")] public string completeinfo { get; set; } = ""; // 完成/追踪文本（| 分两段：动作词/目标名）
        [JsonPropertyName("taskgroupid")] public int taskgroupid { get; set; } // 任务组 id
        [JsonPropertyName("state")] public int state { get; set; } // 配置初始状态（1=待接取 2=进行中；运行时任务状态另由服务端维护）
        [JsonPropertyName("limit")] public int limit { get; set; } // 每日完成次数上限（样本恒为 1）
        [JsonPropertyName("star")] public int star { get; set; } // 星级/转生阶（201 转生试炼 star=reinLevel+1）
        [JsonPropertyName("mode")] public int mode { get; set; } // 模式（有值样本恒为 1）
        [JsonPropertyName("chuansong")] public int chuansong { get; set; } // 传送方式开关（>1 时任务进行中走传送检查）
        [JsonPropertyName("overchuan")] public int overchuan { get; set; } // 越级传送开关（1=已完成时弹跨图提示）
        [JsonPropertyName("autopathing")] public int autopathing { get; set; } // 自动寻路开关（有值样本恒为 1）
        [JsonPropertyName("autofight")] public int autofight { get; set; } // 自动战斗开关（有值样本恒为 1）
        [JsonPropertyName("autofinsh")] public int autofinsh { get; set; } // 自动完成开关（有值样本恒为 0）
        [JsonPropertyName("nofly")] public int nofly { get; set; } // 禁止飞行开关（1=禁飞，需走到目标）
        [JsonPropertyName("showboss")] public string showboss { get; set; } = ""; // 展示推荐条目（# 分隔，指向 cm.taskRecommend）
        [JsonPropertyName("tradebox")] public int tradebox { get; set; } // 交易宝箱 id（仅 1 条有值）
        [JsonPropertyName("item")] public string? item { get; set; } // 原版空壳占位字段（464 条中仅 11 条为 null，JS 从不读取）
    }
}
