// partial 文件：Envir 任务域方法（原始任务表加载 / 技能学习 / 任务查询 / 跨天清理）。
using Server.Library.MirDatabase;
using Server.MirDatabase;
using Server.MirObjects;
using Shared.Helpers;
using System.IO;
using System.Text.Json;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        /// <summary>
        /// 原始传奇技能移植：玩家可见技能（IsShow=1）按职业（Cls：1战/2法/3道）在学习等级自动学会。
        /// Condition = 10000 + 学习等级（如 10007 = 7 级）。普通攻击(Condition=10001,Spell=10101)不纳入，
        /// 普攻走引擎 Spell.None=0，不需要作为技能学习。
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
                if (info == null || info.IsShow != 1 || info.Cls != cls) continue;
                // condition 10001 = 普通攻击等 1 级无门槛技能，跳过（普攻不占技能栏）。
                if (info.Condition <= 10001) continue;
                result.Add(info);
            }
            return result;
        }

        /// <summary>技能学习等级：Condition - 10000（如 10007 → 7），钳制到 [1,255]。</summary>
        public static int GetLearnLevel(MagicInfoTable info)
        {
            if (info == null) return 0;
            int level = info.Condition >= 10000 ? info.Condition - 10000 : info.Condition;
            return Math.Max(1, Math.Min(byte.MaxValue, level));
        }
        public QuestInfoTable GetQuestInfo(int index)
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
