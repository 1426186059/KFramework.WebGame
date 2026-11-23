namespace Server.ExcelConfig
{
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
        public string Raw { get; set; } = ""; // 原始 items 片段

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
}
