// partial 文件：Envir 任务域业务方法（技能学习 / 任务查询 / 跨天清理）。
// 配置表一律在 Envir.配置表.cs 的 LoadExcelConfig 里加载，这里只读内存中的 QuestInfoList/MagicInfoList。
using Server.Library.MirDatabase;
using Server.MirDatabase;
using Server.MirObjects;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        /// <summary>
        /// 原始传奇技能移植：玩家可见技能（IsShow=1）按职业（Cls：1战/2法/3道）在学习等级自动学会。
        /// Cls=23 是通用技能（普通攻击 10101 等），对所有职业开放；Condition=10001（1级可学）同样纳入，
        /// 保证每个角色创建时即自动激活普通攻击技能。
        /// Condition = 10000 + 学习等级（如 10007 = 7 级）。
        /// </summary>
        public List<MagicInfoTable> GetLearnableSkills(MirClass playerClass)
        {
            int cls = playerClass switch
            {
                MirClass.Warrior => 1,
                MirClass.Wizard => 2,
                MirClass.Taoist => 3,
                _ => -1,
            };
            var result = new List<MagicInfoTable>();
            if (cls < 0) return result;

            for (int i = 0; i < MagicInfoList.Count; i++)
            {
                MagicInfoTable info = MagicInfoList[i];
                if (info == null || info.IsShow != 1) continue;
                // Cls=23 = 全职业通用（普通攻击），其余按职业匹配。
                if (info.Cls != 23 && info.Cls != cls) continue;
                result.Add(info);
            }
            return result;
        }

        /// <summary>
        /// 技能学习等级：只有 Condition=10000+N 是等级条件。
        /// 倚天四式的 Condition=260004~260007 是装备触发条件，不能当作 255 级自动学会。
        /// </summary>
        public static int GetLearnLevel(MagicInfoTable info)
        {
            if (info == null)
            {
                return int.MaxValue;
            }

            if (info.Condition < 10001 || info.Condition > 10255)
            {
                return int.MaxValue;
            }

            return info.Condition - 10000;
        }
        public QuestInfoRunItem GetQuestInfo(int index)
        {
            return QuestInfoList.FirstOrDefault(info => info.Index == index);
        }
        private void ClearDailyQuests(CharacterInfo info)
        {
            foreach (var quest in QuestInfoList)
            {
                // 原始传奇主线为一次性任务，不参与跨天重置；其余类型按日常任务处理。
                if (quest.Type == QuestType.Main || quest.Type == QuestType.None) continue;

                for (var i = 0; i < info.CompletedQuests.Count; i++)
                {
                    if (info.CompletedQuests[i] != quest.Index) continue;

                    info.CompletedQuests.RemoveAt(i);
                }
            }

            info.Player?.GetCompletedQuests();
        }
    }
}
