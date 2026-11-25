using Server.MirEnvir;
using Server.MirObjects;

namespace Server.MirDatabase
{
    // 运行期任务对象：外层持有静态配置 Info，所有解析结果/NPC 绑定都保存在本类，不写回 QuestInfo.json。
    public class QuestInfoRunItem
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public QuestInfoTable Info { get; set; }

        public QuestInfoRunItem()
        {
            Info = new QuestInfoTable();
            KillMessage = Info.KillMessage;
            ItemMessage = Info.ItemMessage;
            FlagMessage = Info.FlagMessage;
            GotoMessage = Info.GotoMessage;
        }

        public QuestInfoRunItem(QuestInfoTable info)
        {
            Info = info ?? new QuestInfoTable();
            ExpReward = Info.ExpReward;
            GoldReward = Info.GoldReward;
            CreditReward = Info.CreditReward;
            KillMessage = Info.KillMessage;
            ItemMessage = Info.ItemMessage;
            FlagMessage = Info.FlagMessage;
            GotoMessage = Info.GotoMessage;
        }

        // ===== 静态配置转发属性（数据仍在 Info 里）=====

        public int Index
        {
            get { return Info.Index; }
            set { Info.Index = value; }
        }

        public string source
        {
            get { return Info.source; }
            set { Info.source = value; }
        }

        public int originTaskId
        {
            get { return Info.originTaskId; }
            set { Info.originTaskId = value; }
        }

        public int fnpc
        {
            get { return Info.fnpc; }
            set { Info.fnpc = value; }
        }

        public int tnpc
        {
            get { return Info.tnpc; }
            set { Info.tnpc = value; }
        }

        public string fnpcName
        {
            get { return Info.fnpcName; }
            set { Info.fnpcName = value; }
        }

        public string tnpcName
        {
            get { return Info.tnpcName; }
            set { Info.tnpcName = value; }
        }

        public int next
        {
            get { return Info.next; }
            set { Info.next = value; }
        }

        public int requiredQuestId
        {
            get { return Info.requiredQuestId; }
            set { Info.requiredQuestId = value; }
        }

        public int state
        {
            get { return Info.state; }
            set { Info.state = value; }
        }

        public int taskgroupid
        {
            get { return Info.taskgroupid; }
            set { Info.taskgroupid = value; }
        }

        public string condition
        {
            get { return Info.condition; }
            set { Info.condition = value; }
        }

        public string conditionText
        {
            get { return Info.conditionText; }
            set { Info.conditionText = value; }
        }

        public string rewards
        {
            get { return Info.rewards; }
            set { Info.rewards = value; }
        }

        public string reward
        {
            get { return Info.reward; }
            set { Info.reward = value; }
        }

        public string tip3
        {
            get { return Info.tip3; }
            set { Info.tip3 = value; }
        }

        public string completeinfo
        {
            get { return Info.completeinfo; }
            set { Info.completeinfo = value; }
        }

        public int goalid
        {
            get { return Info.goalid; }
            set { Info.goalid = value; }
        }

        public int goaltype
        {
            get { return Info.goaltype; }
            set { Info.goaltype = value; }
        }

        public int goalcount
        {
            get { return Info.goalcount; }
            set { Info.goalcount = value; }
        }

        public int goalmapid
        {
            get { return Info.goalmapid; }
            set { Info.goalmapid = value; }
        }

        public int goalx
        {
            get { return Info.goalx; }
            set { Info.goalx = value; }
        }

        public int goaly
        {
            get { return Info.goaly; }
            set { Info.goaly = value; }
        }

        public string goalinfo
        {
            get { return Info.goalinfo; }
            set { Info.goalinfo = value; }
        }

        public string goalparams
        {
            get { return Info.goalparams; }
            set { Info.goalparams = value; }
        }

        public bool goalmultitarget
        {
            get { return Info.goalmultitarget; }
            set { Info.goalmultitarget = value; }
        }

        public string trackingGoalRich
        {
            get { return Info.trackingGoalRich; }
            set { Info.trackingGoalRich = value; }
        }

        public string trackingCompleteRich
        {
            get { return Info.trackingCompleteRich; }
            set { Info.trackingCompleteRich = value; }
        }

        public List<QuestOriginKill> originKillRefs
        {
            get { return Info.originKillRefs; }
            set { Info.originKillRefs = value; }
        }

        public List<QuestOriginSkillGoal> skillGoals
        {
            get { return Info.skillGoals; }
            set { Info.skillGoals = value; }
        }

        public QuestOriginAlternate alternate
        {
            get { return Info.alternate; }
            set { Info.alternate = value; }
        }

        public string Name
        {
            get { return Info.Name; }
            set { Info.Name = value; }
        }

        public string Group
        {
            get { return Info.Group; }
            set { Info.Group = value; }
        }

        public string FileName
        {
            get { return Info.FileName; }
            set { Info.FileName = value; }
        }

        public int RequiredMinLevel
        {
            get { return Info.RequiredMinLevel; }
            set { Info.RequiredMinLevel = value; }
        }

        public int RequiredMaxLevel
        {
            get { return Info.RequiredMaxLevel; }
            set { Info.RequiredMaxLevel = value; }
        }

        public int RequiredQuest
        {
            get { return Info.RequiredQuest; }
            set { Info.RequiredQuest = value; }
        }

        public RequiredClass RequiredClass
        {
            get { return Info.RequiredClass; }
            set { Info.RequiredClass = value; }
        }

        public QuestType Type
        {
            get { return Info.Type; }
            set { Info.Type = value; }
        }

        public int TimeLimitInSeconds
        {
            get { return Info.TimeLimitInSeconds; }
            set { Info.TimeLimitInSeconds = value; }
        }

        public List<OriginBoxRewardTable> OriginRewards
        {
            get { return Info.OriginRewards; }
            set { Info.OriginRewards = value; }
        }

        // ===== 运行期数据 =====

        public NPCInfoTable NpcInfo;
        public uint NpcIndex;
        public uint NpcObjectID;

        private uint _finishNpcIndex;

        public uint FinishNpcIndex
        {
            get { return _finishNpcIndex == 0 ? NpcIndex : _finishNpcIndex; }
            set { _finishNpcIndex = value; }
        }

        public uint FinishNpcObjectID;

        public NPCObject FinishNPC
        {
            get
            {
                return Envir.NPCs.FirstOrDefault(x => x.ObjectID == FinishNpcObjectID) ??
                       Envir.NPCs.FirstOrDefault(x => x.Info != null && x.Info.Index == FinishNpcIndex);
            }
        }

        // 这 4 条消息原本既在 JSON 里，也会由 LoadInfo 覆盖。运行值放这里，静态原值仍在 Info。
        public string GotoMessage { get; set; } = string.Empty;
        public string KillMessage { get; set; } = string.Empty;
        public string ItemMessage { get; set; } = string.Empty;
        public string FlagMessage { get; set; } = string.Empty;

        public List<string> Description { get; set; } = new List<string>();
        public List<string> TaskDescription { get; set; } = new List<string>();
        public List<string> ReturnDescription { get; set; } = new List<string>();
        public List<string> CompletionDescription { get; set; } = new List<string>();
        public List<string> PlayerTaskTalk { get; set; } = new List<string>();

        public List<QuestItemTask> CarryItems { get; set; } = new List<QuestItemTask>();
        public List<QuestKillTask> KillTasks { get; set; } = new List<QuestKillTask>();
        public List<QuestItemTask> ItemTasks { get; set; } = new List<QuestItemTask>();
        public List<QuestFlagTask> FlagTasks { get; set; } = new List<QuestFlagTask>();

        public uint GoldReward { get; set; }
        public uint ExpReward { get; set; }
        public uint CreditReward { get; set; }
        public List<QuestItemReward> FixedRewards { get; set; } = new List<QuestItemReward>();
        public List<QuestItemReward> SelectRewards { get; set; } = new List<QuestItemReward>();

        // 所有任务都来自原始传奇配置（source=_task），没有 .txt 任务脚本。
        // 运行期数据全部由 QuestInfo.json 在 LoadFromOrigin() 里重建。
        public void LoadInfo()
        {
            LoadFromOrigin();
        }

        public void LoadFromOrigin()
        {
            if (string.IsNullOrEmpty(source)) return;

            Description.Clear();
            TaskDescription.Clear();
            ReturnDescription.Clear();
            CompletionDescription.Clear();
            PlayerTaskTalk.Clear();
            FixedRewards.Clear();
            KillTasks.Clear();

            if (tnpc > 0)
                FinishNpcIndex = (uint)tnpc;

            AddLines(Description, tip3);
            AddLines(TaskDescription, goalinfo.Length > 0 ? goalinfo : completeinfo);
            AddLines(ReturnDescription, completeinfo);

            string[] rewardParts = reward.Split('#', 2);
            AddLines(CompletionDescription, rewardParts[0]);
            if (rewardParts.Length > 1) AddLines(PlayerTaskTalk, rewardParts[1]);

            GotoMessage = completeinfo.Length > 0 ? completeinfo : goalinfo;

            foreach (var originReward in OriginRewards)
            {
                if (originReward == null || originReward.ItemId == 4 || originReward.ItemId == 1 || originReward.ItemId == 499) continue;

                ItemInfoTable item = Envir.GetItemInfo(originReward.ItemId);
                if (item == null) continue;

                ushort count = (ushort)(originReward.Count > 65535 ? 65535 : (originReward.Count < 1 ? 1 : originReward.Count));
                FixedRewards.Add(new QuestItemReward { Item = item, Count = count, Career = originReward.Career });
            }

            if (originKillRefs != null)
            {
                foreach (var k in originKillRefs)
                {
                    MonsterInfoTable monster = Envir.GetMonsterInfo(k.MonsterIndex);
                    if (monster == null) continue;

                    var task = new QuestKillTask { Monster = monster, Count = k.Count, Message = "" };

                    if (goalmultitarget && !string.IsNullOrEmpty(goalparams))
                    {
                        foreach (string part in goalparams.Split('#', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (int.TryParse(part, out int id) && id > 0) task.AltMonsterIds.Add(id);
                        }
                    }

                    KillTasks.Add(task);
                }
            }
        }

        private static void AddLines(List<string> target, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (var line in text.Split('\n'))
            {
                string t = line.Trim();
                if (t.Length > 0) target.Add(t);
            }
        }

        /// <summary>判断该任务是否应当开放给玩家，即“是否可提供任务”。</summary>
        public bool CanOffer(PlayerObject player)
        {
            if (RequiredMinLevel > player.Level || RequiredMaxLevel < player.Level) return false;
            if (RequiredQuest > 0 && !player.CompletedQuests.Contains(RequiredQuest)) return false;

            switch (player.Class)
            {
                case MirClass.Warrior:
                    if (!RequiredClass.HasFlag(RequiredClass.Warrior)) return false;
                    break;
                case MirClass.Wizard:
                    if (!RequiredClass.HasFlag(RequiredClass.Wizard)) return false;
                    break;
                case MirClass.Taoist:
                    if (!RequiredClass.HasFlag(RequiredClass.Taoist)) return false;
                    break;
                case MirClass.Assassin:
                    if (!RequiredClass.HasFlag(RequiredClass.Assassin)) return false;
                    break;
                case MirClass.Archer:
                    if (!RequiredClass.HasFlag(RequiredClass.Archer)) return false;
                    break;
            }

            return true;
        }

        public ClientQuestInfo CreateClientQuestInfo(PlayerObject viewer = null)
        {
            var description = LinkFormatter.ReplacePlaceholders(Description, (type, index) => EnsureLinkInfo(viewer, type, index)) ?? new List<string>();
            var taskDescription = LinkFormatter.ReplacePlaceholders(TaskDescription, (type, index) => EnsureLinkInfo(viewer, type, index)) ?? new List<string>();
            var returnDescription = LinkFormatter.ReplacePlaceholders(ReturnDescription, (type, index) => EnsureLinkInfo(viewer, type, index)) ?? new List<string>();
            var completionDescription = LinkFormatter.ReplacePlaceholders(CompletionDescription, (type, index) => EnsureLinkInfo(viewer, type, index)) ?? new List<string>();

            var startNpc = NpcInfo ?? Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)NpcIndex);
            var finishNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)FinishNpcIndex);

            if (startNpc == null && !string.IsNullOrEmpty(source))
                startNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == fnpc);
            if (finishNpc == null && !string.IsNullOrEmpty(source))
                finishNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == tnpc);

            uint startNpcObjectID = NpcObjectID;
            if (startNpc != null)
            {
                var npcObj = Envir.Main.NPCs.FirstOrDefault(n => n.Info == startNpc && viewer?.CurrentMap != null && n.CurrentMap == viewer.CurrentMap)
                             ?? Envir.Main.NPCs.FirstOrDefault(n => n.ObjectID == NpcObjectID)
                             ?? Envir.Main.NPCs.FirstOrDefault(n => n.Info == startNpc);
                if (npcObj != null) startNpcObjectID = npcObj.ObjectID;
            }

            uint finishNpcObjectID = FinishNpcObjectID;
            if (finishNpc != null)
            {
                var npcObj = Envir.Main.NPCs.FirstOrDefault(n => n.Info == finishNpc && viewer?.CurrentMap != null && n.CurrentMap == viewer.CurrentMap)
                             ?? Envir.Main.NPCs.FirstOrDefault(n => n.ObjectID == FinishNpcObjectID)
                             ?? Envir.Main.NPCs.FirstOrDefault(n => n.Info == finishNpc);
                if (npcObj != null) finishNpcObjectID = npcObj.ObjectID;
            }

            return new ClientQuestInfo
            {
                Index = Index,
                StartNpcObjectID = startNpcObjectID,
                FinishNpcObjectID = finishNpcObjectID,
                StartNpcIndex = startNpc != null ? startNpc.Index : (int)NpcIndex,
                FinishNpcIndex = finishNpc != null ? finishNpc.Index : (int)FinishNpcIndex,
                Name = Name,
                Group = Group,
                Description = description,
                TaskDescription = taskDescription,
                ReturnDescription = returnDescription,
                CompletionDescription = completionDescription,
                PlayerTaskTalk = PlayerTaskTalk,
                MinLevelNeeded = RequiredMinLevel,
                MaxLevelNeeded = RequiredMaxLevel,
                ClassNeeded = RequiredClass,
                QuestNeeded = RequiredQuest,
                Type = Type,
                TimeLimitInSeconds = TimeLimitInSeconds,
                RewardGold = GoldReward,
                RewardExp = ExpReward,
                RewardCredit = CreditReward,
                RewardsFixedItem = viewer == null ? FixedRewards : FixedRewards.Where(r => r.Career == 0 || r.Career == (int)viewer.Class + 1).ToList(),
                RewardsSelectItem = SelectRewards,
                StartNpcName = startNpc != null ? startNpc.Name : "???NPC???"
            };
        }

        private void EnsureLinkInfo(PlayerObject viewer, string linkType, int index)
        {
            if (viewer?.Connection == null) return;

            switch (linkType)
            {
                case "ITEM":
                    var itemInfo = Envir.GetItemInfo(index);
                    if (itemInfo != null)
                        viewer.Connection.CheckItemInfo(itemInfo);
                    break;
                case "MONSTER":
                    viewer.Connection.CheckMonsterInfo(index);
                    break;
                case "NPC":
                    viewer.Connection.CheckNPCInfo(index);
                    break;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}:   {1}", Index, Name);
        }
    }

    public class QuestKillTask
    {
        public MonsterInfoTable Monster { get; set; }
        public int Count { get; set; }
        public string Message { get; set; }
        public List<int> AltMonsterIds { get; set; } = new List<int>();
    }

    public class QuestItemTask
    {
        public ItemInfoTable Item { get; set; }
        public ushort Count { get; set; }
        public string Message { get; set; }
    }

    public class QuestFlagTask
    {
        public int Number { get; set; }
        public string Message { get; set; }
    }
}
