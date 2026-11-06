using System.Collections.Generic;
using System.Linq;
using Server.Library;
using Server.MirDatabase;
using Server.MirEnvir;

namespace Server.MirObjects
{
    /// <summary>
    /// 每个玩家服务器内部持有的任务调度器。
    /// 负责在“完成任务”与“升级”时，按条件自动派发下一个任务，取代原先写死在任务上的接龙机制。
    /// </summary>
    public class QuestScheduler
    {
        // 职业数量：战/法/道/刺客/弓。连续“职业不符”的跳过次数上限，防止一直往后跳。
        private const int MaxClassSkips = 5;

        private readonly PlayerObject _player;

        public QuestScheduler(PlayerObject player)
        {
            _player = player;
        }

        /// <summary>
        /// 某任务完成后，从“已完成任务的 Index +1”开始往后寻找下一个可接任务。
        /// 规则：
        ///  - 空洞（已被删除、不存在的 Index）：自然越过，并重置连续跳过计数；
        ///  - 仅“职业不满足”可以跳过，且连续跳过超过 MaxClassSkips 即停止（不一直跳）；
        ///  - 等级 / 前置任务不满足：立即停止（不跳过）。
        /// 找到第一个满足条件的任务即自动接取并结束。
        /// </summary>
        public void AdvanceAfterComplete(int completedIndex)
        {
            var list = Envir.Main.QuestInfoList;
            if (list.Count == 0) return;
            int maxIndex = list.Max(q => q.Index);

            int classSkipRun = 0;
            int cand = completedIndex + 1;
            while (cand <= maxIndex)
            {
                QuestInfo q = list.FirstOrDefault(d => d.Index == cand);
                if (q == null)                       // 空洞：越过并重置计数
                {
                    classSkipRun = 0;
                    cand++;
                    continue;
                }
                if (_player.CompletedQuests.Contains(cand))   // 已完成：越过并重置计数
                {
                    classSkipRun = 0;
                    cand++;
                    continue;
                }
                if (q.CanAccept(_player))            // 满足条件：开放（寻找NPC）并结束
                {
                    _player.OfferQuest(cand);
                    return;
                }
                if (!IsClassEligible(q))              // 仅职业不符可跳过
                {
                    classSkipRun++;
                    if (classSkipRun > MaxClassSkips) return;
                    cand++;
                    continue;
                }
                return;                              // 等级/前置不符：停止
            }
        }

        /// <summary>
        /// 升级 / 登录时调用：接取因等级达成而新开放的任务（等级任务）。
        /// 只处理 RequiredMinLevel > 0 的任务，避免与主线顺序链重复派发。
        /// </summary>
        public void CheckLevelUp(int newLevel)
        {
            foreach (var q in Envir.Main.QuestInfoList)
            {
                if (q.RequiredMinLevel <= 0) continue;                       // 只处理等级任务
                if (q.RequiredMinLevel > newLevel) continue;                  // 等级未到
                if (q.RequiredMaxLevel > 0 && q.RequiredMaxLevel < newLevel) continue;
                if (_player.CompletedQuests.Contains(q.Index)) continue;
                if (_player.CurrentQuests.Exists(e => e.Index == q.Index)) continue;
                if (q.CanAccept(_player)) _player.OfferQuest(q.Index);
            }
        }

        /// <summary>
        /// 该任务对当前玩家是否“职业符合”。用位掩码判断，与 RequiredClass 的值（1/2/4/8/16/31）一致。
        /// RequiredClass == 0 视为无职业限制。
        /// </summary>
        private bool IsClassEligible(QuestInfo q)
        {
            int rc = (int)q.RequiredClass;
            if (rc == 0) return true;
            switch (_player.Class)
            {
                case MirClass.Warrior: return (rc & 1) != 0;
                case MirClass.Wizard: return (rc & 2) != 0;
                case MirClass.Taoist: return (rc & 4) != 0;
                case MirClass.Assassin: return (rc & 8) != 0;
                case MirClass.Archer: return (rc & 16) != 0;
            }
            return true;
        }
    }
}
