using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 2config.box：奖励包表，key = 奖励包 id。
    // items 形如 "4-1800-100;1405002-1-1-1;499-500-100"，按 ';' 拆项，每项按 '-' 拆位：
    //   [0]=物品id（4=经验、1/499=金币）、[1]=数量、[2]=权重、[3]=职业、[4]=性别、[5]/[6]=开服天数区间。
    public class OriginBoxTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 奖励包 id
        [JsonPropertyName("isShow")] public int isShow { get; set; } // 是否展示（1=展示）
        [JsonPropertyName("items")] public string items { get; set; } = ""; // 奖励项串（; 分隔多项，每项按 - 拆位）

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
}
