// 原始传奇配置分片 2config 的表 DTO（对应 2config10.25341.1.json）。
// 风格：属性名 = 原键名，[JsonPropertyName] 绑定，不做语义改名。
// Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    // ===================== 2config.skills_condition：技能等级条件表 =====================
    // 结构为 技能id -> 等级 -> 条件记录。condition 原始为字符串（如 "10100"）。
    public class OriginSkillConditionTable
    {
        [JsonPropertyName("skillId")] public int skillId { get; set; }
        [JsonPropertyName("level")] public int level { get; set; }
        [JsonPropertyName("effLevelID")] public int effLevelID { get; set; }
        [JsonPropertyName("condition")] public string condition { get; set; } = "";
        [JsonPropertyName("rate")] public int rate { get; set; }
        [JsonPropertyName("monHurtAddFix")] public int monHurtAddFix { get; set; }
        [JsonPropertyName("show")] public string show { get; set; } = "";
        [JsonPropertyName("cdTime")] public int cdTime { get; set; }
        [JsonPropertyName("mpCost")] public int mpCost { get; set; }
    }

    // ===================== 2config.box：奖励包表 =====================
    // key = 奖励包 id。items 形如 "4-1800-100;1405002-1-1-1;499-500-100"。
    // 分号分隔多个奖励项；原版 BoxUtil.getItemOrBox 对每项按 '-' 拆成：
    //   [0]=物品id（4=主角经验，1/499=金币），[1]=数量，[2]=权重/概率（原版展示逻辑未用），
    //   [3]=职业限制(>0 且不等于玩家职业则跳过)，[4]=性别限制(>0 且不等于玩家性别则跳过)，
    //   [5]/[6]=开服天数区间（原版 g[6] 同时兼作 condition id）。
    // 任务奖励包内不存在 '#' 随机组（已在原始配置中全量统计确认）。
    public class OriginBoxTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("isShow")] public int isShow { get; set; }
        [JsonPropertyName("items")] public string items { get; set; } = "";

        // 由 items 解析出的奖励项（加载期生成，不从 JSON 反序列化）。
        [JsonIgnore]
        public List<OriginBoxRewardTable> Rewards { get; set; } = new();

        public void ParseRewards()
        {
            Rewards.Clear();
            if (string.IsNullOrEmpty(items)) return;

            foreach (string seg in items.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                // '#' 随机组取首项作为代表（任务奖励包中不存在，兜底防未来配置引入）。
                var reward = OriginBoxRewardTable.Parse(seg);
                if (reward != null) Rewards.Add(reward);
            }
        }
    }

    /// <summary>
    /// 原始传奇 2config.box.items 单条奖励项的 1:1 DTO，字段对应原版按 '-' 拆分的位置。
    /// 构建期 Mir2QuestConverter 与服务器共用同一结构名，任务奖励用 List 承载，替代旧的平行数组。
    /// </summary>
    public class OriginBoxRewardTable
    {
        public int ItemId { get; set; }      // [0] 物品id：4=经验，1/499=金币，其余为真实物品 Index
        public int Count { get; set; }       // [1] 数量
        public int Weight { get; set; }      // [2] 权重/概率（原版展示逻辑未使用，保留不丢信息）
        public int Career { get; set; }      // [3] 职业限制：0=不限，1=战士 2=法师 3=道士
        public int Sex { get; set; }         // [4] 性别限制：0=不限
        public int OpenDayMin { get; set; }  // [5] 开服天数下限
        public int OpenDayMax { get; set; }  // [6] 开服天数上限（原版兼作 condition id）
        public string Raw { get; set; } = "";

        public static OriginBoxRewardTable Parse(string seg)
        {
            if (string.IsNullOrEmpty(seg)) return null;
            string[] g = seg.Split('#')[0].Split('-');
            if (g.Length < 2 || !int.TryParse(g[0], out int itemId) || !int.TryParse(g[1], out int count))
                return null;

            static int At(string[] parts, int index)
                => index < parts.Length && int.TryParse(parts[index], out int v) ? v : 0;

            return new OriginBoxRewardTable
            {
                ItemId = itemId,
                Count = count,
                Weight = At(g, 2),
                Career = At(g, 3),
                Sex = At(g, 4),
                OpenDayMin = At(g, 5),
                OpenDayMax = At(g, 6),
                Raw = seg
            };
        }
    }

    // ===================== 2config 顶层容器 =====================
    // 已建具体 DTO 的表：_task_tx / skills_condition / box；其余 84 张表由基类 ExtraTables 全量兜底。
    public class OriginMir2Config2 : OriginConfigRoot
    {
        [JsonPropertyName("_task_tx")] public Dictionary<string, OriginTaskTable> _task_tx { get; set; } = new();
        [JsonPropertyName("skills_condition")] public Dictionary<string, Dictionary<string, OriginSkillConditionTable>> skills_condition { get; set; } = new();
        [JsonPropertyName("box")] public Dictionary<string, OriginBoxTable> box { get; set; } = new();
    }
}
