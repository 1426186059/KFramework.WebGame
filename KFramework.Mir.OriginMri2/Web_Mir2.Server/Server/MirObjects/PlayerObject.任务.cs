// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using System.Drawing;
using C = ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using System.Text.RegularExpressions;
using Timer = Server.MirEnvir.Timer;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        #region Quests

        public void AcceptQuest(int index, bool navigate = false)
        {
            bool canAccept = true;

            if (CurrentQuests.Exists(e => e.Index == index)) return; //e.Info.NpcIndex == npcIndex && 

            QuestInfoRunItem info = Envir.QuestInfoList.FirstOrDefault(d => d.Index == index);
            if (info == null) return;

            EnsureQuestNPC(info);

            // 从"寻找NPC"追踪入口进入：传送至发放 NPC 旁并打开其任务面板，
            // 由玩家在面板内点击"领取"确认接取（不自动领取）。
            if (navigate)
            {
                NPCObject npcObj = GetQuestStartNPC(info);
                if (npcObj != null)
                {
                    bool inRange = Functions.InRange(npcObj.CurrentLocation, CurrentLocation, Globals.DataRange);
                    if (!inRange)
                    {
                        // NPC 脚下那格通常不可作传送落点。以 NPC 为中心逐环遍历周围格子，
                        // 挑第一个"可走格"（map.ValidPoint：Attribute==Walk）落入。
                        Map map = npcObj.CurrentMap;
                        Point center = npcObj.CurrentLocation;
                        Point dest = center;
                        if (map != null && !map.ValidPoint(center))
                        {
                            for (int r = 1; r <= 12; r++)
                            {
                                bool found = false;
                                for (int dx = -r; dx <= r && !found; dx++)
                                {
                                    for (int dy = -r; dy <= r && !found; dy++)
                                    {
                                        Point q = new Point(center.X + dx, center.Y + dy);
                                        if (map.ValidPoint(q)) { dest = q; found = true; }
                                    }
                                }
                                if (found) break;
                            }
                        }
                        if (map != null) Teleport(map, dest);
                    }

                    // 原始传奇 checknpcTaskDialog：任务处于待接(NotAccepted)状态 → 弹 NPC 任务面板。
                    if (!Dead)
                        CallNPC(npcObj.ObjectID, NPCScript.MainKey, openQuestPanel: true);
                }
                return;
            }

            // 站在 NPC 旁接取：用已链接的 NpcInfo 反查运行时 NPC（比按 ObjectID 比对更可靠）
            NPCObject npc = GetQuestStartNPC(info);
            if (npc == null || npc.CurrentMap != CurrentMap ||
                !Functions.InRange(npc.CurrentLocation, CurrentLocation, Globals.DataRange) ||
                !npc.VisibleLog[Info.Index] || !npc.Visible) return;

            if (!info.CanOffer(this))
            {
                canAccept = false;
            }

            if (CurrentQuests.Count >= Globals.MaxConcurrentQuests)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.MaximumQuestsTaken), ChatType.System);
                return;
            }

            if (CompletedQuests.Contains(index))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.QuestAlreadyCompleted), ChatType.System);
                return;
            }

            //check previous chained quests have been completed
            QuestInfoRunItem tempInfo = info;
            while (tempInfo != null && tempInfo.RequiredQuest != 0)
            {
                if (!CompletedQuests.Contains(tempInfo.RequiredQuest))
                {
                    canAccept = false;
                    break;
                }

                tempInfo = Envir.QuestInfoList.FirstOrDefault(d => d.Index == tempInfo.RequiredQuest);
            }

            if (!canAccept)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CouldNotAcceptQuest), ChatType.System);
                return;
            }

            if (info.CarryItems.Count > 0)
            {
                foreach (QuestItemTask carryItem in info.CarryItems)
                {
                    ushort count = carryItem.Count;

                    while (count > 0)
                    {
                        UserItem item = Envir.CreateFreshItem(carryItem.Item);

                        if (item.Info.StackSize > count)
                        {
                            item.Count = count;
                            count = 0;
                        }
                        else
                        {
                            count -= (ushort)item.Info.StackSize;
                            item.Count = (ushort)item.Info.StackSize;
                        }

                        if (!CanGainQuestItem(item))
                        {
                            RecalculateQuestBag();
                            return;
                        }

                        GainQuestItem(item);

                        Report.ItemChanged(item, item.Count, 2);
                    }
                }
            }

            QuestProgressInfo quest = new QuestProgressInfo(index);

            quest.Init(this);

            OfferedQuests.Remove(index);
            Enqueue(new S.ChangeQuest { Quest = new ClientQuestProgress { Id = index }, QuestState = QuestState.Remove });

            bool tracked = TrackQuest(index, true, false);
            SendUpdateQuest(quest, QuestState.Add, tracked);

            CallDefaultNPC(DefaultNPCType.OnAcceptQuest, index);
        }

        // 确保任务已解析出起始/交付 NPC 的运行时信息（NpcInfo 引用 + 世界唯一索引 ObjectID）。
        // 若任务写在某 NPC 脚本 [Quests] 段，ParseQuests 已写入；否则按配置索引(NpcIndex/FinishNpcIndex) 反查 NPCInfoTable，
        // 再从已生成的 NPC 实例中取运行时 ObjectID，供客户端按 ObjectID 匹配、以及服务端反查 NPC 实例使用。
        // 不依赖任务是否登记在脚本里，避免 NpcIndex=0 时"寻找NPC"整体失效（任务追踪栏只是回退显示任务名）。
        private void EnsureQuestNPC(QuestInfoRunItem info)
        {
            if (info == null) return;

            if (info.NpcInfo == null && info.NpcIndex != 0)
                info.NpcInfo = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)info.NpcIndex);

            if (info.NpcInfo != null)
            {
                var npc = Envir.NPCs.FirstOrDefault(n => n.Info == info.NpcInfo);
                if (npc != null) info.NpcObjectID = npc.ObjectID; // 运行时 ObjectID 单独存，NpcIndex 保持策划表 Index
            }

            if (info.FinishNpcIndex != 0)
            {
                var finishInfo = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)info.FinishNpcIndex);
                if (finishInfo != null)
                {
                    var fnpc = Envir.NPCs.FirstOrDefault(n => n.Info == finishInfo);
                    if (fnpc != null) info.FinishNpcObjectID = fnpc.ObjectID;
                }
            }
        }

        // 按已链接的发放 NPC 信息反查运行时 NPC 实例：优先 NpcObjectID，再按 NpcIndex 策划表 Index 反查。
        private NPCObject GetQuestStartNPC(QuestInfoRunItem info)
        {
            if (info == null) return null;

            // 1. 已链接的 NPCInfoTable 引用（ParseQuests 写入，最精确）
            if (info.NpcInfo != null)
            {
                foreach (var ob in Envir.NPCs)
                    if (ob is NPCObject npc && npc.Info == info.NpcInfo)
                        return npc;
            }

            if (info.NpcObjectID != 0)
            {
                NPCObject byId = NPCObject.Get(info.NpcObjectID);
                if (byId != null) return byId;
            }

            if (info.NpcIndex != 0)
            {
                foreach (var ob in Envir.NPCs)
                    if (ob is NPCObject npc && npc.Info != null && npc.Info.Index == (int)info.NpcIndex)
                        return npc;
            }

            // 4. 终极兜底：任务未绑定 NpcInfo/NpcIndex 时，按任务追踪栏"寻找"标签所用的名称反查 NPC 实例。
            //    标签名 = StartNpcName ?? 任务名，与 CreateClientQuestInfo / 客户端展示一致，故服务端凭 QuestIndex 即可定位。
            return null;
        }

        public void FinishQuest(int questIndex)
        {
            QuestProgressInfo quest = CurrentQuests.FirstOrDefault(e => e.Info.Index == questIndex);

            if (quest == null || !quest.Completed) return;

            NPCObject npc = null;

            for (int i = CurrentMap.NPCs.Count - 1; i >= 0; i--)
            {
                if (CurrentMap.NPCs[i].ObjectID != quest.Info.FinishNpcObjectID) continue;

                if (!Functions.InRange(CurrentMap.NPCs[i].CurrentLocation, CurrentLocation, Globals.DataRange)) break;
                npc = CurrentMap.NPCs[i];
                break;
            }
            if (npc == null || !npc.VisibleLog[Info.Index] || !npc.Visible) return;

            List<UserItem> rewardItems = BuildQuestRewardItems(quest.Info);

            if (!CanGainItems(rewardItems.ToArray()))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotHandInQuestBagFull), ChatType.System);
                return;
            }

            // 原始传奇只有主线任务是一次性的，完成后记入已完成列表；其它类型（除魔/悬赏/日常等）可重复，不记录。
            if (quest.Info.Type == QuestType.Main)
            {
                Info.CompletedQuests.Add(quest.Index);
                GetCompletedQuests();
            }

            SendUpdateQuest(quest, QuestState.Remove);

            // 领奖后清掉该任务的追踪记录，形成滚动窗口。
            // 否则 TrackedQuests 只增不减，很快达到 5 个上限；后续自动接取的任务虽然进了 CurrentQuests，
            // 但 TrackQuest 因上限返回 false，下发的 ChangeQuest 不带 TrackQuest，客户端追踪栏就不显示任何任务。
            TrackQuest(questIndex, false);

            if (quest.Info.CarryItems.Count > 0)
            {
                foreach (QuestItemTask carryItem in quest.Info.CarryItems)
                {
                    TakeQuestItem(carryItem.Item, carryItem.Count);
                }
            }

            foreach (QuestItemTask iTask in quest.Info.ItemTasks)
            {
                TakeQuestItem(iTask.Item, iTask.Count);
            }

            foreach (UserItem item in rewardItems)
            {
                GainItem(item);
            }

            RecalculateQuestBag();

            GainGold((uint)(quest.Info.GoldReward * Settings.DropRate));
            GainExp((uint)(quest.Info.ExpReward * Settings.ExpRate));
            GainCredit(quest.Info.CreditReward);

            CallDefaultNPC(DefaultNPCType.OnFinishQuest, questIndex);

            // 任务调度器：完成当前任务后推进下一环。对齐原始传奇——按 next 字段找下一环，
            // 若下一环接取NPC(StartNpcId)==当前任务交付NPC(FinishNpcId)（同一个NPC），直接接取；
            // 否则走"开放(寻找NPC)"流程，由玩家自行前往。
            QuestScheduler.AdvanceAfterComplete(quest.Info);
        }

        /// <summary>
        /// 原始传奇 goal type=532「买物品」引导任务（任务14 买技能书 / 任务24 买药品）：
        /// 只有"本次在目标 NPC 商店实际购买"才判定完成（页游同样是买的一瞬间完成），
        /// 登录/接取不再凭背包已有物品补判。
        /// </summary>
        /// <param name="fromPurchase">true=刚在 NPC 商店买完：置为"完成待交"并弹 NPC 任务面板，由玩家点完成才发奖推进；
        /// false=登录/接取调用，此时 boughtFromNPC=0，不会完成 532 目标。</param>
        public void CheckBuyGoalQuests(bool fromPurchase = false, uint boughtFromNPC = 0)
        {
            var buyQuests = CurrentQuests.Where(q => q.Info != null && q.Info.goaltype == 532 && !q.BuyGoalDone).ToList();
            if (buyQuests.Count == 0) return;

            foreach (QuestProgressInfo q in buyQuests)
            {
                if (!HasOriginBuyGoalItem(q.Info, boughtFromNPC)) continue;

                q.BuyGoalDone = true;
                if (!q.CheckCompleted()) continue;

                if (fromPurchase)
                {
                    // 买书场景：书已入包，任务进入"完成待交"；同步客户端让任务面板出现"领取奖励"按钮，
                    // 发奖/推进交给玩家点面板 Finish → FinishQuest 完成。
                    SendUpdateQuest(q, QuestState.Update);
                    TrackQuest(q.Info.Index, true);
                    continue;
                }

                SendUpdateQuest(q, QuestState.Remove);
                TrackQuest(q.Info.Index, false);
                GrantQuestCompletion(q.Info); // 发经验/金币/物品奖励
                if (!CompletedQuests.Contains(q.Info.Index)) CompletedQuests.Add(q.Info.Index);
                GetCompletedQuests();
                ReceiveOutputMessage($"任务完成：{q.Info.Name}", OutputMessageType.Quest);
                QuestScheduler.AdvanceAfterComplete(q.Info);
            }
        }

        /// <summary>
        /// 原始传奇 532「买物品」目标判定：与药品统一，只认本次在目标 NPC（tnpc）商店的实际购买。
        ///  - param=257 技能书：还要求买到的就是本职业 1 级技能书（书店也卖别的书）。
        ///  - param=255 药品（任务24 补给药品）：在夏家店老板商店买到任意药品即达成。
        /// 登录/接取调用时 boughtFromNPC=0，一律不补判完成。
        /// </summary>
        private bool HasOriginBuyGoalItem(QuestInfoRunItem info, uint boughtFromNPC = 0)
        {
            if (info == null || boughtFromNPC == 0 || info.tnpc <= 0) return false;

            // 必须是任务目标 NPC 本次交易（夏家店老板 / 书店老板），别处买的不算。
            if (!Envir.NPCs.Any(n => n != null && n.Info != null && n.Info.Index == info.tnpc
                                      && n.ObjectID == boughtFromNPC)) return false;

            string param = info.goalparams ?? string.Empty;
            if (!param.Split('&').Contains("257")) return true; // 255 药品：已确认在目标店购买

            int bookIndex = Class switch
            {
                MirClass.Warrior => 11011,
                MirClass.Wizard => 12011,
                MirClass.Taoist => 13011,
                _ => 0
            };
            if (bookIndex == 0) return false;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null && item.Info != null && item.Info.Index == bookIndex) return true;
            }
            return false;
        }

        /// <summary>
        /// 原始传奇"学习技能"支线（type=516，原版无 fnpc/tnpc）：学会本职业对应技能即自动领奖。
        /// 学会技能（技能书 / 升级自动学）、登录、升级时调用。
        /// </summary>
        public void CheckSkillGoalQuests()
        {
            var skillQuests = Envir.QuestInfoList.Where(q =>
                q.skillGoals.Count > 0).ToList();
            if (skillQuests.Count == 0) return;

            foreach (QuestInfoRunItem q in skillQuests)
            {
                if (Info.CompletedQuests.Contains(q.Index)) continue;
                if (q.RequiredMinLevel > Level) continue;
                if (!Envir.CheckOriginShowCondition(this, q.condition)) continue;

                // 三职业各一个目标，玩家只需学会自己职业那个（MirClass 0/1/2 → 1战/2法/3道）。
                var goal = q.skillGoals.FirstOrDefault(g => g.Career == (int)Class + 1);
                if (goal == null) continue;
                if (!HasLearnedSkillBook(goal.BookId)) continue;

                CompleteSkillGoalQuest(q);
            }
        }

        /// <summary>是否已学会技能书对应的技能（Level > 0 才算学会，0 是 LearnOriginSkills 的占位）。</summary>
        private bool HasLearnedSkillBook(int bookId)
        {
            SkillBookInfoTable book = SkillBookInfoTable.Find(bookId);
            if (book == null) return false;

            MagicInfoTable magic = null;
            for (int i = 0; i < Envir.MagicInfoList.Count; i++)
            {
                var info = Envir.MagicInfoList[i];
                if (info != null && info.OriginId == book.SkillId) { magic = info; break; }
            }
            if (magic == null) return false;

            UserMagic um = GetMagic(magic.Spell);
            return um != null && um.Level > 0;
        }

        /// <summary>无 NPC 的技能目标达成：直接接取→完成→发奖→推进下一环，跳过 NPC 交付校验。</summary>
        private void CompleteSkillGoalQuest(QuestInfoRunItem info)
        {
            if (!CanGainItems(BuildQuestRewardItems(info).ToArray())) return; // 背包不足则保持未完成，下次学会技能/登录再试

            var quest = new QuestProgressInfo(info.Index);
            quest.Init(this);
            quest.CheckCompleted(); // 无 Kill/Item/Flag 目标，立即置 Completed

            OfferedQuests.Remove(info.Index);
            Enqueue(new S.ChangeQuest { Quest = new ClientQuestProgress { Id = info.Index }, QuestState = QuestState.Remove });

            GrantQuestCompletion(info);

            if (!Info.CompletedQuests.Contains(info.Index))
            {
                Info.CompletedQuests.Add(info.Index);
                GetCompletedQuests();
            }
            TrackQuest(info.Index, false);
            CallDefaultNPC(DefaultNPCType.OnFinishQuest, info.Index);
            QuestScheduler.AdvanceAfterComplete(info);
        }

        /// <summary>
        /// 任务发奖核心（物品/金币/经验/功勋），由 NPC 交付 FinishQuest 与无 NPC 技能目标自动完成共用。
        /// 不含背包物品扣除、NPC 校验与下一环推进，那些留在各自调用方。
        /// </summary>
        private void GrantQuestCompletion(QuestInfoRunItem info)
        {
            List<UserItem> rewardItems = BuildQuestRewardItems(info);

            if (!CanGainItems(rewardItems.ToArray())) return;

            foreach (UserItem item in rewardItems) GainItem(item);
            RecalculateQuestBag();

            GainGold((uint)(info.GoldReward * Settings.DropRate));
            GainExp((uint)(info.ExpReward * Settings.ExpRate));
            GainCredit(info.CreditReward);
        }

        /// <summary>构建任务固定+可选奖励的物品列表（按职业过滤 box 分支奖励）。</summary>
        private List<UserItem> BuildQuestRewardItems(QuestInfoRunItem info)
        {
            List<UserItem> rewardItems = new List<UserItem>();

            foreach (var reward in info.FixedRewards)
            {
                // 原始传奇 box 里同一奖励按职业分三支（1战/2法/3道），只发玩家职业那一支。
                if (reward.Career > 0 && reward.Career != (int)Class + 1) continue;

                ushort count = reward.Count;
                while (count > 0)
                {
                    UserItem rewardItem = Envir.CreateFreshItem(reward.Item);
                    if (reward.Item.StackSize >= count)
                    {
                        rewardItem.Count = count;
                        count = 0;
                    }
                    else
                    {
                        rewardItem.Count = (ushort)reward.Item.StackSize;
                        count -= (ushort)reward.Item.StackSize;
                    }
                    rewardItems.Add(rewardItem);
                }
            }

            // 可选奖励全部发放（不再要求玩家选择）
            foreach (var selectReward in info.SelectRewards)
            {
                ushort count = selectReward.Count;
                while (count > 0)
                {
                    UserItem rewardItem = Envir.CreateFreshItem(selectReward.Item);
                    if (selectReward.Item.StackSize >= count)
                    {
                        rewardItem.Count = count;
                        count = 0;
                    }
                    else
                    {
                        rewardItem.Count = (ushort)selectReward.Item.StackSize;
                        count -= (ushort)selectReward.Item.StackSize;
                    }
                    rewardItems.Add(rewardItem);
                }
            }

            return rewardItems;
        }

        /// <summary>
        /// 自动接取指定任务（被任务调度器 / NPC 脚本调用）。
        /// 不要求玩家身处 NPC 附近——这是与手动 AcceptQuest 的关键区别，
        /// 目的是让任务"一个接一个"地推给玩家。
        /// </summary>
        public void AutoAcceptQuest(int index, bool showNPC = true)
        {
            if (index <= 0) return;
            if (CurrentQuests.Exists(e => e.Index == index)) return;

            QuestInfoRunItem info = Envir.QuestInfoList.FirstOrDefault(d => d.Index == index);
            if (info == null) return;
            if (CompletedQuests.Contains(index)) return;
            if (!info.CanOffer(this)) return;
            if (CurrentQuests.Count >= Globals.MaxConcurrentQuests) return;

            QuestProgressInfo quest = new QuestProgressInfo(index);
            quest.Init(this);

            bool tracked = TrackQuest(index, true, false);
            // 自动接取不触发自动寻路：玩家此刻通常就站在交付 NPC 面前（领完奖连续接下一环），
            // autoGuide 会让客户端跑 A*；目标 NPC 格子不可走时会穷举整张地图，WASM 单线程直接卡死。
            SendUpdateQuest(quest, QuestState.Add, tracked, false);

            // 532 买物品引导任务：若玩家背包已有对应技能书（先买后接），立即判定完成。
            CheckBuyGoalQuests();

            if (showNPC)
                CallDefaultNPC(DefaultNPCType.OnAcceptQuest, index);
        }

        /// <summary>
        /// 开放（未接取）任务：进入"已开放"状态并下发，左上角追踪栏显示"寻找 NPC"。
        /// 与 AutoAcceptQuest 的区别：不写入 CurrentQuests，需玩家前往 NPC（点击追踪条目传送）后才正式接取。
        /// </summary>
        public void OfferQuest(int index)
        {
            if (index <= 0) return;
            if (CurrentQuests.Exists(e => e.Index == index)) return;
            if (OfferedQuests.Contains(index)) return;
            if (CompletedQuests.Contains(index)) return;

            QuestInfoRunItem info = Envir.QuestInfoList.FirstOrDefault(d => d.Index == index);
            if (info == null) return;
            if (!info.CanOffer(this)) return;
            // 原始传奇 showCondition：等级/VIP/转生/开服天数等不满足就不在任务追踪栏显示。
            // GM 提到 100 级会开放大量高等级任务，但被 showCondition 拦掉的任务仍不能出现。
            if (!Envir.CheckOriginShowCondition(this, info.condition)) return;

            OfferedQuests.Add(index);

            EnsureQuestNPC(info); // 解析起始 NPC 的运行时世界索引，供客户端按 ObjectID 匹配任务面板

            // CreateClientQuestInfo() 已按 info.NpcInfo / NpcIndex 解析好起始 NPC 名（StartNpcName），此处无需重复覆盖。
            ClientQuestInfo cqi = info.CreateClientQuestInfo();

            Enqueue(new S.ChangeQuest
            {
                Quest = new ClientQuestProgress { Id = info.Index, QuestInfoTable = cqi },
                QuestState = QuestState.Offered
            });
        }

        public void CheckGroupQuestKill(MonsterInfoTable mInfo)
        {
            if (GroupMembers != null)
            {
                foreach (PlayerObject player in GroupMembers.
                    Where(player => player.CurrentMap == CurrentMap &&
                        Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) &&
                        !player.Dead))
                {
                    player.CheckNeedQuestKill(mInfo);
                }
            }
            else
                CheckNeedQuestKill(mInfo);
        }
        public override bool CheckGroupQuestItem(UserItem item, bool gainItem = true)
        {
            bool itemCollected = false;

            if (GroupMembers != null)
            {
                foreach (PlayerObject player in GroupMembers.
                    Where(player => player != null && player.Node != null && player.CurrentMap == CurrentMap &&
                        Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) &&
                        !player.Dead))
                {
                    if (player.CheckNeedQuestItem(item, gainItem))
                    {
                        itemCollected = true;
                        player.Report.ItemChanged(item, item.Count, 2, "CheckGroupQuestItem (WinQuestItem)");
                    }
                }
            }
            else
            {
                if (CheckNeedQuestItem(item, gainItem))
                {
                    itemCollected = true;
                    Report.ItemChanged(item, item.Count, 2, "CheckGroupQuestItem (WinQuestItem)");
                }
            }

            return itemCollected;
        }

        public bool CheckNeedQuestItem(UserItem item, bool gainItem = true)
        {
            foreach (QuestProgressInfo quest in CurrentQuests.
                Where(e => e.ItemTaskCount.Count > 0).
                Where(e => e.NeedItem(item.Info)).
                Where(e => CanGainQuestItem(item)))
            {
                if (gainItem)
                {
                    GainQuestItem(item);
                    quest.ProcessItem(Info.QuestInventory);

                    Enqueue(new S.SendOutputMessage { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouFound), item.FriendlyName), Type = OutputMessageType.Quest });

                    SendUpdateQuest(quest, QuestState.Update);

                    Report.ItemChanged(item, item.Count, 2, "CheckNeedQuestItem (WinQuestItem)");
                }
                return true;
            }

            return false;
        }
        public bool CheckNeedQuestFlag(int flagNumber)
        {
            foreach (QuestProgressInfo quest in CurrentQuests.
                Where(e => e.FlagTaskSet.Count > 0).
                Where(e => e.NeedFlag(flagNumber)))
            {
                quest.ProcessFlag(Info.Flags);

                //Enqueue(new S.SendOutputMessage { Message = string.Format("Location visited."), Type = OutputMessageType.Quest });

                SendUpdateQuest(quest, QuestState.Update);
                return true;
            }

            return false;
        }
        public void CheckNeedQuestKill(MonsterInfoTable mInfo)
        {
            foreach (QuestProgressInfo quest in CurrentQuests.
                    Where(e => e.KillTaskCount.Count > 0).
                    Where(quest => quest.NeedKill(mInfo)))
            {
                quest.ProcessKill(mInfo);

                Enqueue(new S.SendOutputMessage { Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouKilled), mInfo.GameName), Type = OutputMessageType.Quest });

                SendUpdateQuest(quest, QuestState.Update);
            }
        }

        public void RecalculateQuestBag()
        {
            for (int i = Info.QuestInventory.Length - 1; i >= 0; i--)
            {
                UserItem itm = Info.QuestInventory[i];

                if (itm == null) continue;

                bool itemRequired = false;
                bool isCarryItem = false;

                foreach (QuestProgressInfo quest in CurrentQuests)
                {
                    foreach (QuestItemTask carryItem in quest.Info.CarryItems)
                    {
                        if (carryItem.Item == itm.Info)
                        {
                            isCarryItem = true;
                            break;
                        }
                    }

                    foreach (QuestItemTask task in quest.Info.ItemTasks)
                    {
                        if (task.Item == itm.Info)
                        {
                            itemRequired = true;
                            break;
                        }
                    }
                }

                if (!itemRequired && !isCarryItem)
                {
                    Info.QuestInventory[i] = null;
                    Enqueue(new S.DeleteQuestItem { UniqueID = itm.UniqueID, Count = itm.Count });
                }
            }
        }

        public void SendUpdateQuest(QuestProgressInfo quest, QuestState state, bool trackQuest = false, bool autoGuide = false)
        {
            quest.CheckCompleted();

            switch (state)
            {
                case QuestState.Add:
                    if (!CurrentQuests.Contains(quest))
                    {
                        CurrentQuests.Add(quest);
                    }
                    break;
                case QuestState.Remove:
                    if (CurrentQuests.Contains(quest))
                    {
                        CurrentQuests.Remove(quest);
                    }
                    break;
            }

            Enqueue(new S.ChangeQuest
            {
                Quest = quest.CreateClientQuestProgress(),
                QuestState = state,
                TrackQuest = trackQuest,
                AutoGuide = autoGuide
            });
        }

        public void GetCompletedQuests()
        {
            Enqueue(new S.ChangeQuest
            {
                QuestState = QuestState.CompletedList,
                CompletedQuests = CompletedQuests
            });
        }

        #endregion
    }
}
