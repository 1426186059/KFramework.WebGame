using System;
using System.Collections.Generic;

namespace Server.ExcelConfig
{
    /// <summary>
    /// 原始传奇技能书映射（Tools 构建期从 1config.item 导出，ExcelConfig/SkillBookInfo.json）：
    /// Index=物品 id；SkillId=原始技能 id（对应 MagicInfoTable.OriginId）；
    /// TargetLevel=原版目标等级（1=1级书激活，4/5/6/7=升级书）；
    /// NeedLevel/Career/SubType=使用门槛（col24/col52/col21）。
    /// </summary>
    public class SkillBookInfoTable
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SkillId { get; set; }
        public int TargetLevel { get; set; }
        public int NeedLevel { get; set; }
        public int Career { get; set; }
        public int SubType { get; set; }

        private static Dictionary<int, SkillBookInfoTable> _byIndex;

        /// <summary>按物品 id 查表。</summary>
        public static SkillBookInfoTable Find(int itemIndex)
        {
            if (_byIndex != null) return _byIndex.TryGetValue(itemIndex, out var info) ? info : null;
            return null;
        }

        public static void BuildIndex(List<SkillBookInfoTable> list)
        {
            _byIndex = new Dictionary<int, SkillBookInfoTable>();
            if (list == null) return;
            foreach (var info in list)
                if (info != null && info.Index > 0 && !_byIndex.ContainsKey(info.Index))
                    _byIndex[info.Index] = info;
        }
    }
}
