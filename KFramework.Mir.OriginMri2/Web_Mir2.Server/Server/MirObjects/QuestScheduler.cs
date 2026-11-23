using System.Collections.Generic;
using System.Linq;
using Server.Library;
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
        /// 某任务完成后推进下一环，对齐原始传奇的任务链配置：
        ///  - 优先按当前任务的 OriginSettings.NextQuestId（= 原版 next 字段）找下一环；
        ///  - 自动接取以原版 _task.state 为准：state==2（TaskState.Accepted）直接 AutoAcceptQuest，
        ///    state==1（NotAccepted）或缺失只 OfferQuest（追踪栏显示"寻找NPC"，玩家去 fnpc 手动接）。
        ///    不再用"同 NPC"猜测（原版存在同 NPC 但不自动接、不同 NPC 却自动接的任务）。
        /// 没有 next 配置时，退化为从 Index+1 往后找第一个可接任务，同样按 state 判断。
        /// </summary>
        public void AdvanceAfterComplete(QuestInfoTable completed)
        {
            var list = Envir.Main.QuestInfoList;
            if (list.Count == 0 || completed == null) return;

            QuestInfoTable next = null;
            if (completed.next > 0)
                next = list.FirstOrDefault(d => d.Index == completed.next);

            if (next != null)
            {
                if (next.CanOffer(_player))
                    AdvanceTo(next);
                return;
            }

            // 退化：无 next 配置时从 Index+1 往后找第一个可接任务
            int maxIndex = list.Max(q => q.Index);
            int classSkipRun = 0;
            int cand = completed.Index + 1;
            while (cand <= maxIndex)
            {
                QuestInfoTable q = list.FirstOrDefault(d => d.Index == cand);
                if (q == null || _player.CompletedQuests.Contains(cand))
                {
                    classSkipRun = 0;
                    cand++;
                    continue;
                }
                if (q.CanOffer(_player))
                {
                    AdvanceTo(q);
                    return;
                }
                if (!IsClassEligible(q))
                {
                    classSkipRun++;
                    if (classSkipRun > MaxClassSkips) return;
                    cand++;
                    continue;
                }
                return;
            }
        }

        /// <summary>
        /// 按原版 _task.state 推进：2=Accepted 自动接取，其余=NotAccepted 只开放。
        /// 非原版任务（OriginSettings==null）保持旧的 Index+1 接龙行为：不作自动接取，仅开放。
        /// </summary>
        private void AdvanceTo(QuestInfoTable quest)
        {
            // 原始传奇无 NPC 的技能学习任务不走"寻找NPC"（会显示 ???），交由 CheckSkillGoalQuests 按学会技能自动完成。
            if (quest.skillGoals.Count > 0) return;

            bool auto = quest.state == 2;
            // 任务链自动推进下一环时不弹默认NPC面板：玩家可能正停在书店老板等对话界面操作，
            // AutoAcceptQuest 里的 CallDefaultNPC(OnAcceptQuest) 会 S.NPCUpdate 把面板切到任务NPC，
            // 打断当前操作。下一次玩家主动点NPC时仍是正常面板。
            if (auto) _player.AutoAcceptQuest(quest.Index, showNPC: false);
            else _player.OfferQuest(quest.Index);
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
                // 无 NPC 的技能学习目标任务不派发"寻找NPC"（否则追踪栏显示 ???），交给 CheckSkillGoalQuests。
                if (q.skillGoals.Count > 0) continue;
                if (q.CanOffer(_player)) _player.OfferQuest(q.Index);
            }
        }

        /// <summary>
        /// 该任务对当前玩家是否“职业符合”。用位掩码判断，与 RequiredClass 的值（1/2/4/8/16/31）一致。
        /// RequiredClass == 0 视为无职业限制。
        /// </summary>
        private bool IsClassEligible(QuestInfoTable q)
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
