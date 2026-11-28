using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using S = ServerPackets;

namespace Client.MirScenes
{
    public sealed partial class GameScene : MirScene
    {
        private void ChangeQuest(S.ChangeQuest p)
        {
            switch (p.QuestState)
            {
                case QuestState.Add:
                    if (!User.CurrentQuests.Any(q => q.Id == p.Quest.Id))
                    {
                        User.CurrentQuests.Add(p.Quest);
                    }
                    // 后台预载所有任务（待接取 + 进行中）的目标地点地图瓦片。
                    QuestMapTilePrefetcher.PreLoadAll();

                    foreach (ClientQuestProgress quest in User.CurrentQuests)
                    {
                        BindQuest(quest);
                    }
                    // TrackQuest 由服务器根据角色存档下发，客户端不再本地判断/保存。
                    if (p.TrackQuest)
                    {
                        GameScene.Scene.QuestTrackingDialog.AddQuest(p.Quest);
                    }

                    if (p.AutoGuide)
                    {
                        AutoGuideToQuestNPC(p.Quest.QuestInfo.StartNpcObjectID);
                    }

                    // 任务面板刷新：对齐原始传奇 TaskState —— 接取后若任务立刻完成(Finish)则保持面板
                    // 原地刷成“领取奖励”，若仍进行中(Accepted)则关闭面板回到 NPC 普通对话。
                    Scene.QuestListDialog.OnQuestStateChanged(p.Quest, p.QuestState);

                    break;
                case QuestState.Update:
                    for (int i = 0; i < User.CurrentQuests.Count; i++)
                    {
                        if (User.CurrentQuests[i].Id != p.Quest.Id)
                        {
                            continue;
                        }

                        User.CurrentQuests[i] = p.Quest;
                    }

                    foreach (ClientQuestProgress quest in User.CurrentQuests)
                    {
                        BindQuest(quest);
                    }

                    // 532 买物品任务完成后服务端只发 Update（不Remove），
                    // 必须通知任务面板原地刷新成“领取奖励”，否则面板停在“继续”无法领奖。
                    Scene.QuestListDialog.OnQuestStateChanged(p.Quest, p.QuestState);

                    break;
                case QuestState.Remove:

                    for (int i = User.CurrentQuests.Count - 1; i >= 0; i--)
                    {
                        if (User.CurrentQuests[i].Id != p.Quest.Id) continue;

                        User.CurrentQuests.RemoveAt(i);
                    }

                    for (int i = User.OfferedQuests.Count - 1; i >= 0; i--)
                    {
                        if (User.OfferedQuests[i].Id != p.Quest.Id) continue;

                        User.OfferedQuests.RemoveAt(i);
                    }

                    // 服务器已移除该任务，客户端不再回发跟踪取消包。
                    GameScene.Scene.QuestTrackingDialog.RemoveQuest(p.Quest);

                    // 领奖结单的特效不在这里播：完成信号走 QuestState.CompletedList（原 S.CompleteQuest），
                    // 那里对比新旧完成列表，只对本次新增完成的任务播特效。Remove 只是任务移除通知。

                    // 领奖后服务端只发 Remove，不会再发 Add/Offered 通知面板关闭；
                    // 必须显式通知任务面板，否则面板会停在已领奖任务上不关闭。
                    Scene.QuestListDialog.OnQuestStateChanged(p.Quest, p.QuestState);

                    break;

                case QuestState.Offered:
                {
                    // 服务端在 S.ChangeQuest 中已按发放 NPC 解析好 StartNpcName；
                    // BindQuest 会用本地 QuestInfoList 覆盖 QuestInfo，若本地条目缺 StartNpcName 则保留服务端下发的值。
                    string serverStartNpc = p.Quest.QuestInfo != null ? p.Quest.QuestInfo.StartNpcName : string.Empty;
                    BindQuest(p.Quest);
                    if (string.IsNullOrEmpty(p.Quest.QuestInfo?.StartNpcName) && !string.IsNullOrEmpty(serverStartNpc))
                        p.Quest.QuestInfo.StartNpcName = serverStartNpc;

                    if (!User.OfferedQuests.Any(q => q.Id == p.Quest.Id))
                    {
                        User.OfferedQuests.Add(p.Quest);
                    }
                    // 后台预载所有任务（待接取 + 进行中）的目标地点地图瓦片。
                    QuestMapTilePrefetcher.PreLoadAll();

                    GameScene.Scene.QuestTrackingDialog.DisplayQuests();
                    break;
                }

                // 完成列表由原 S.CompleteQuest 合并而来。对比新旧列表，只对本次新增完成的任务播特效。
                case QuestState.CompletedList:
                    if (!User.CompletedListInitialized)
                    {
                        // 登录首包是全量历史完成列表，不是"刚完成的任务"，只初始化不播特效。
                        User.CompletedListInitialized = true;
                    }
                    else
                    {
                        foreach (int finishedId in p.CompletedQuests)
                        {
                            if (User.CompletedQuests.Contains(finishedId)) continue;
                            TaskCompleteEffect?.Play();
                        }
                    }
                    User.CompletedQuests = p.CompletedQuests;
                    break;
            }

            GameScene.Scene.QuestTrackingDialog.DisplayQuests();

            if (Scene.QuestListDialog.Visible)
            {
                Scene.QuestListDialog.DisplayInfo();
            }

            if (Scene.QuestLogDialog.Visible)
            {
                Scene.QuestLogDialog.DisplayQuests();
            }
        }

        /// <summary>
        /// 自动接龙引导：寻路走到任务关联的发放 NPC。
        /// ClientQuestInfo.StartNpcObjectID 即 NPC 运行时 ObjectID，可直接在 MapControl.Objects 中按 ObjectID 取到该 NPC 再走过去。
        /// 该 NPC 必须在当前地图才有效；跨图时客户端没有这个 NPC 对象，自动寻路自然 no-op，
        /// 由任务追踪栏继续给出指引（跨图自动移动需另行接传送逻辑）。
        /// </summary>
        private void AutoGuideToQuestNPC(uint npcObjectID)
        {
            if (npcObjectID == 0) return;
            if (MapObject.User == null) return;

            var map = Scene.MapControl;
            if (map.M2CellInfo == null) return;

            if (!MapControl.Objects.TryGetValue(npcObjectID, out MapObject ob) || !(ob is NPCObject npc))
                return;

            Point target = npc.CurrentLocation;
            Point from = MapObject.User.CurrentLocation;
            if (target.X < 0 || target.Y < 0 || target.X >= map.Width || target.Y >= map.Height)
                return;

            var path = map.PathFinder.FindPath(from, target, 20);
            if (path == null || path.Count == 0) return;

            map.CurrentPath = path;
            map.AutoPath = true;
            map.ShowClickEffect(target);
        }

        private void PlayerUpdate(S.PlayerUpdate p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out var ob) && ob is PlayerObject player)
                player.Update(p);
        }

        private void PlayerInspect(S.PlayerInspect p)
        {
            InspectDialog.Items = p.Equipment;

            InspectDialog.Name = p.Name;
            InspectDialog.GuildName = p.GuildName;
            InspectDialog.GuildRank = p.GuildRank;
            InspectDialog.Class = p.Class;
            InspectDialog.Gender = p.Gender;
            InspectDialog.Hair = p.Hair;
            InspectDialog.Level = p.Level;
            InspectDialog.LoverName = p.LoverName;
            InspectDialog.AllowObserve = p.AllowObserve;

            InspectDialog.RefreshInferface(p.IsHero);
            InspectDialog.Show();
        }
        private void LogOutSuccess(S.LogOutSuccess p)
        {
            User = null;
            // 先 Dispose 旧场景（停掉地图 BGM），再创建新场景（播选人 BGM）；顺序反了会把新场景的音乐停掉。
            Dispose();
            ActiveScene = new SelectScene(p.Characters);
        }
        private void LogOutFailed(S.LogOutFailed p)
        {
            Enabled = true;
        }

        private void ReturnToLogin(S.ReturnToLogin p)
        {
            User = null;
            // 先 Dispose 旧场景（停掉地图 BGM），再创建新场景；顺序反了会把新场景的音乐停掉。
            Dispose();
            ActiveScene = new LoginScene();
            MirMessageBox.Show(LanguageMgr.Get("ObservedPlayerLoggedOff"));
        }

        private void ChangeAMode(S.ChangeAMode p)
        {
            AMode = p.Mode;

            switch (p.Mode)
            {
                case AttackMode.Peace:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_Peace"), ChatType.System);
                    break;
                case AttackMode.Group:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_Group"), ChatType.System);
                    break;
                case AttackMode.Guild:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_Guild"), ChatType.System);
                    break;
                case AttackMode.EnemyGuild:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_EnemyGuild"), ChatType.System);
                    break;
                case AttackMode.RedBrown:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_RedBrown"), ChatType.System);
                    break;
                case AttackMode.All:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("AttackMode_All"), ChatType.System);
                    break;
            }
        }
        private void ChangePMode(S.ChangePMode p)
        {
            PMode = p.Mode;
            switch (p.Mode)
            {
                case PetMode.Both:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PetMode_Both"), ChatType.System);
                    break;
                case PetMode.MoveOnly:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PetMode_MoveOnly"), ChatType.System);
                    break;
                case PetMode.AttackOnly:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PetMode_AttackOnly"), ChatType.System);
                    break;
                case PetMode.None:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PetMode_None"), ChatType.System);
                    break;
                case PetMode.FocusMasterTarget:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PetMode_FocusMasterTarget"), ChatType.System);
                    break;
            }
        }

        private void ObjectItem(S.ObjectItem p)
        {
            ItemObject ob = new ItemObject(p.ObjectID);
            ob.Load(p);
            /*
            string[] Warnings = new string[] {"HeroNecklace","AdamantineNecklace","8TrigramWheel","HangMaWheel","BaekTaGlove","SpiritReformer","BokMaWheel","BoundlessRing","ThunderRing","TaeGukRing","OmaSpiritRing","NobleRing"};
            if (Warnings.Contains(p.Name))
            {
                ChatDialog.ReceiveChat(string.Format("{0} at {1}", p.Name, p.Location), ChatType.System);
            }
            */
        }
        private void ObjectGold(S.ObjectGold p)
        {
            ItemObject ob = new ItemObject(p.ObjectID);
            ob.Load(p);
        }
        private void GainedItem(S.GainedItem p)
        {
            Bind(p.Item);
            AddItem(p.Item);
            User.RefreshStats();

            // “获得”亮绿，其余白色（引擎 MirLabel 支持 <color=...> 富文本标签）；固定中文绕过远程语言包
            OutputMessage($"<color=lime>获得</color> {p.Item.FriendlyName} x{p.Item.Count}");

            TryShowQuickAction(p.Item);
        }

        /// <summary>
        /// 原始传奇 NoticeUseItemView / NoticeWearEquipView：
        /// 获得技能书 → 快捷使用；获得战力更高的装备 → 快捷装备。仅这两种情况弹窗。
        /// </summary>
        private void TryShowQuickAction(UserItem item)
        {
            if (item == null || item.Info == null || QuickActionDialog == null) return;

            ulong uid = item.UniqueID;
            Action use = () =>
            {
                MirItemCell cell = InventoryDialog.GetCell(uid) ?? BeltDialog.GetCell(uid);
                if (cell != null) cell.UseItem();
            };

            if (item.Info.Type == ItemType.Book)
            {
                QuickActionDialog.ShowUse(item, use);
                return;
            }

            if (!QuickActionDialog.IsEquipment(item.Info.Type)) return;

            int power = QuickActionDialog.PowerOf(item);
            if (power <= 0) return;

            // 与当前已穿戴的同类型装备比较，只有战力更高才提示。
            int best = 0;
            if (User != null && User.Equipment != null)
            {
                foreach (UserItem equipped in User.Equipment)
                {
                    if (equipped == null || equipped.Info == null || equipped.Info.Type != item.Info.Type) continue;
                    int ep = QuickActionDialog.PowerOf(equipped);
                    if (ep > best) best = ep;
                }
            }

            if (power > best)
                QuickActionDialog.ShowEquip(item, power - best, use);
        }
    }
}
