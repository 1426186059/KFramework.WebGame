using Server.MirObjects;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    // 原始传奇任务内嵌配置：非 null 表示任务内容直接来自配置表，不走 Envir/Quests/*.txt。
    public class QuestOriginSettings
    {
        public int OriginTaskId { get; set; }
        public string Source { get; set; } = "";

        public int StartNpcId { get; set; }
        public int FinishNpcId { get; set; }
        public string StartNpcName { get; set; } = "";
        public string FinishNpcName { get; set; } = "";
        public int NextQuestId { get; set; }
        public int RequiredQuestId { get; set; }

        // 原版 _task.state：2=TaskState.Accepted（上一环领完奖后直接自动接取），
        // 1=NotAccepted（需要玩家去 fnpc 对话手动接取）。自动接取完全以此为准，不再按同 NPC 猜测。
        public int State { get; set; }

        public int TaskGroupId { get; set; }
        public string Condition { get; set; } = "";
        public string ConditionText { get; set; } = "";

        public string Rewards { get; set; } = "";
        public string RewardText { get; set; } = "";
        public string Tip3 { get; set; } = "";
        public string CompleteInfo { get; set; } = "";

        public int GoalId { get; set; }
        public int GoalType { get; set; }
        public int GoalCount { get; set; }
        public int GoalMapId { get; set; }
        public int GoalX { get; set; }
        public int GoalY { get; set; }
        public string GoalInfo { get; set; } = "";
        public string GoalParams { get; set; } = "";
        public bool GoalMultiTarget { get; set; }

        // 按 Index 精确引用，服务器启动时解析成 ItemInfoTable / MonsterInfoTable，避开按名字查找重名的问题。
        public List<QuestOriginReward> FixedRewards { get; set; } = new List<QuestOriginReward>();
        public List<QuestOriginKill> KillTasks { get; set; } = new List<QuestOriginKill>();

        // 原始传奇 type=516 学习技能目标：原版三职业各一个（# 分隔），玩家只需学会自己职业那个。
        public List<QuestOriginSkillGoal> SkillGoals { get; set; } = new List<QuestOriginSkillGoal>();

        // 2config._task_tx 是同一套 id 的另一条任务线文本，按 id 合并为备选文本。
        public QuestOriginAlternate Alternate { get; set; }
    }

    public class QuestOriginReward
    {
        public int ItemIndex { get; set; }
        public uint Count { get; set; }
        public string ItemName { get; set; } = "";
        public bool ItemResolved { get; set; }
    }

    public class QuestOriginKill
    {
        public int MonsterIndex { get; set; }
        public int Count { get; set; }
        public string MonsterName { get; set; } = "";
        public bool MonsterResolved { get; set; }
    }

    public class QuestOriginSkillGoal
    {
        public int Career { get; set; }        // 1=战士 2=法师 3=道士
        public int BookId { get; set; }        // 技能书物品 Index
        public string SkillName { get; set; } = "";
        public int GoalId { get; set; }
    }

    public class QuestOriginAlternate
    {
        public string Name { get; set; } = "";
        public string Tip3 { get; set; } = "";
        public string RewardText { get; set; } = "";
        public string CompleteInfo { get; set; } = "";
        public string Rewards { get; set; } = "";
        public int StartNpcId { get; set; }
        public int FinishNpcId { get; set; }
    }

    public class QuestInfoTable
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        protected static Envir EditEnvir
        {
            get { return Envir.Edit; }
        }

        protected static MessageQueue MessageQueue
        {
            get { return MessageQueue.Instance; }
        }

        public int Index { get; set; }

        // 旧表没有该字段（反序列化为 null），继续走 FileName 指向的 Envir/Quests/*.txt 脚本。
        public QuestOriginSettings OriginSettings { get; set; }

        // 发放（起始）NPC 索引。
        // 注意：此字段由发放 NPC 在加载其脚本 [Quests] 段时自动回填
        // （见 NPCScript.ParseQuests：正数任务索引 -> info.NpcIndex = LoadedObjectID），
        // 对应 NPCInfoTable 中声明的接取任务索引。
        // 因此不要在 QuestInfo.json 里手动维护本字段，它会在运行时被自动填充。
        // 该字段仅存在于运行期，不参与 JSON 序列化（避免写入无意义的 0 或被误改）。
        [JsonIgnore]
        public uint NpcIndex;
        public NPCInfoTable NpcInfo;

        private uint _finishNpcIndex;

        public uint FinishNpcIndex
        {
            get { return _finishNpcIndex == 0 ? NpcIndex : _finishNpcIndex; }
            set { _finishNpcIndex = value; }
        }

        public NPCObject FinishNPC
        {
            get
            {
                return Envir.NPCs.Single(x => x.ObjectID == FinishNpcIndex);
            }
        }

        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string GotoMessage { get; set; } = string.Empty;
        public string KillMessage { get; set; } = string.Empty;
        public string ItemMessage { get; set; } = string.Empty;
        public string FlagMessage { get; set; } = string.Empty;

        public List<string> Description { get; set; } = new List<string>();
        public List<string> TaskDescription { get; set; } = new List<string>();
        public List<string> ReturnDescription { get; set; } = new List<string>();
        public List<string> CompletionDescription { get; set; } = new List<string>();
        // 玩家任务台词：NPC 说完（Description / @NPC_TASK_TALK）后，玩家自动回复的话语（@PLAYER_TASK_TALK），形成一问一答。
        public List<string> PlayerTaskTalk { get; set; } = new List<string>();

        public int RequiredMinLevel { get; set; }
        public int RequiredMaxLevel { get; set; }
        public int RequiredQuest { get; set; }
        public RequiredClass RequiredClass { get; set; } = RequiredClass.None;

        public QuestType Type { get; set; }

        public int TimeLimitInSeconds { get; set; } = 0;

        public List<QuestItemTask> CarryItems { get; set; } = new List<QuestItemTask>();

        public List<QuestKillTask> KillTasks { get; set; } = new List<QuestKillTask>();
        public List<QuestItemTask> ItemTasks { get; set; } = new List<QuestItemTask>();
        public List<QuestFlagTask> FlagTasks { get; set; } = new List<QuestFlagTask>();
        //TODO: ZoneTasks
        //TODO: EscortTasks

        public uint GoldReward { get; set; }
        public uint ExpReward { get; set; }
        public uint CreditReward { get; set; }
        public List<QuestItemReward> FixedRewards { get; set; } = new List<QuestItemReward>();
        public List<QuestItemReward> SelectRewards { get; set; } = new List<QuestItemReward>();

        // 原始传奇奖励包（2config.box）由构建期展开成三组平行数组，加载时按索引解析成 FixedRewards。
        // 职业值：0=不限，1=战士，2=法师，3=道士（与 MirClass + 1 对齐）。
        public List<int> RewardItemIndices { get; set; } = new List<int>();
        public List<int> RewardItemCounts { get; set; } = new List<int>();
        public List<int> RewardCareers { get; set; } = new List<int>();

        private Regex _regexMessage = new Regex("\"([^\"]*)\"");


        public QuestInfoTable() { }

        public QuestInfoTable(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            Name = reader.ReadString();
            Group = reader.ReadString();
            FileName = reader.ReadString();
            RequiredMinLevel = reader.ReadInt32();

            RequiredMaxLevel = reader.ReadInt32();
            if (RequiredMaxLevel == 0) RequiredMaxLevel = ushort.MaxValue;

            RequiredQuest = reader.ReadInt32();
            RequiredClass = (RequiredClass)reader.ReadByte();
            Type = (QuestType)reader.ReadByte();
            GotoMessage = reader.ReadString();
            KillMessage = reader.ReadString();
            ItemMessage = reader.ReadString();
            FlagMessage = reader.ReadString();

            if (Envir.LoadVersion > 90)
            {
                TimeLimitInSeconds = reader.ReadInt32();
            }

            LoadInfo();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(Name);
            writer.Write(Group);
            writer.Write(FileName);
            writer.Write(RequiredMinLevel);
            writer.Write(RequiredMaxLevel);
            writer.Write(RequiredQuest);
            writer.Write((byte)RequiredClass);
            writer.Write((byte)Type);
            writer.Write(GotoMessage);
            writer.Write(KillMessage);
            writer.Write(ItemMessage);
            writer.Write(FlagMessage);
            writer.Write(TimeLimitInSeconds);
        }

        public void LoadInfo(bool clear = false)
        {
            if (clear) ClearInfo();

            // 内嵌配置的任务没有脚本文件，直接从配置表按 Index 解析引用，避免刷 File Not Found。
            if (OriginSettings != null)
            {
                LoadFromOrigin();
                return;
            }

            if (!Directory.Exists(Settings.QuestPath)) return;

            string fileName = Path.Combine(Settings.QuestPath, FileName + ".txt");

            if (File.Exists(fileName))
            {
                List<string> lines = File.ReadAllLines(fileName).ToList();

                ParseFile(lines);
            }
            else
                MessageQueue.Enqueue(string.Format("File Not Found: {0}, Quest: {1}", fileName, Name));
        }

        public void ClearInfo()
        {
            Description.Clear();
            PlayerTaskTalk.Clear();
            KillTasks = new List<QuestKillTask>();
            ItemTasks = new List<QuestItemTask>();
            FlagTasks = new List<QuestFlagTask>();
            FixedRewards = new List<QuestItemReward>();
            SelectRewards = new List<QuestItemReward>();
            ExpReward = 0;
            GoldReward = 0;
            CreditReward = 0;
        }

        // 把内嵌配置展开成运行时对象：文本段落 + 按 Index 解析的物品奖励/击杀目标。
        public void LoadFromOrigin()
        {
            var o = OriginSettings;
            if (o == null) return;

            // Type 即原始传奇任务类型（TaskType / taskgroupid），由 JSON 顶层字段反序列化，无需在此二次设置。
            Description.Clear();
            TaskDescription.Clear();
            ReturnDescription.Clear();
            CompletionDescription.Clear();
            PlayerTaskTalk.Clear();
            FixedRewards.Clear();
            KillTasks.Clear();

            // 交任务 NPC：原始传奇内嵌任务用 FinishNpcId 指定交付 NPC（如任务24=夏家店老板10091），
            // 与发放 NPC（StartNpcId，如比奇老兵10201）往往不同。不设置的话 FinishNpcIndex 会
            // 回退到 NpcIndex（发放NPC），任务追踪栏点击传送就指向错误地图（比奇老兵的854而非夏家店老板的103）。
            if (o.FinishNpcId > 0)
                FinishNpcIndex = (uint)o.FinishNpcId;

            AddLines(Description, o.Tip3);
            AddLines(TaskDescription, o.GoalInfo.Length > 0 ? o.GoalInfo : o.CompleteInfo);
            AddLines(ReturnDescription, o.CompleteInfo);

            string[] rewardParts = o.RewardText.Split('#', 2);
            AddLines(CompletionDescription, rewardParts[0]);
            if (rewardParts.Length > 1) AddLines(PlayerTaskTalk, rewardParts[1]);

            GotoMessage = o.CompleteInfo.Length > 0 ? o.CompleteInfo : o.GoalInfo;

            // 真实奖励由构建期从原始传奇 2config.box 展开（经验/金币/功勋已写入顶层字段，
            // 物品写入三组平行数组），这里按 Index 解析成运行时物品对象，不再从 reward 文本猜解。
            for (int i = 0; i < RewardItemIndices.Count; i++)
            {
                ItemInfoTable item = Envir.GetItemInfo(RewardItemIndices[i]);
                if (item == null) continue;

                int rawCount = i < RewardItemCounts.Count ? RewardItemCounts[i] : 1;
                ushort count = (ushort)(rawCount > 65535 ? 65535 : (rawCount < 1 ? 1 : rawCount));
                int career = i < RewardCareers.Count ? RewardCareers[i] : 0;

                FixedRewards.Add(new QuestItemReward { Item = item, Count = count, Career = career });
            }

            if (o.KillTasks != null)
            {
                foreach (var k in o.KillTasks)
                {
                    MonsterInfoTable monster = Envir.GetMonsterInfo(k.MonsterIndex);
                    if (monster == null) continue;

                    KillTasks.Add(new QuestKillTask { Monster = monster, Count = k.Count, Message = "" });
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

        public void ParseFile(List<string> lines)
        {
            const string
                npcTaskTalkKey = "[@NPC_TASK_TALK]",
                descriptionLegacyKey = "[@DESCRIPTION]",
                descriptionTaskKey = "[@TASKDESCRIPTION]",
                descriptionReturnKey = "[@RETURNDESCRIPTION]",
                descriptionCompletionKey = "[@COMPLETION]",
                carryItemsKey = "[@CARRYITEMS]",
                killTasksKey = "[@KILLTASKS]",
                itemTasksKey = "[@ITEMTASKS]",
                flagTasksKey = "[@FLAGTASKS]",
                fixedRewardsKey = "[@FIXEDREWARDS]",
                selectRewardsKey = "[@SELECTREWARDS]",
                expRewardKey = "[@EXPREWARD]",
                goldRewardKey = "[@GOLDREWARD]",
                creditRewardKey = "[@CREDITREWARD]",
                playerTaskTalkKey = "[@PLAYER_TASK_TALK]";

            List<string> headers = new List<string>
            {
                npcTaskTalkKey, descriptionLegacyKey, descriptionTaskKey, descriptionCompletionKey,
                carryItemsKey, killTasksKey, itemTasksKey, flagTasksKey,
                fixedRewardsKey, selectRewardsKey, expRewardKey, goldRewardKey, creditRewardKey, descriptionReturnKey,
                playerTaskTalkKey
            };

            int currentHeader = 0;

            while (currentHeader < headers.Count)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].ToUpper();

                    if (line != headers[currentHeader].ToUpper()) continue;

                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        string innerLine = lines[j];

                        if (innerLine.StartsWith("[") || innerLine.StartsWith("//")) break;
                        if (string.IsNullOrEmpty(lines[j])) continue;

                        switch (line)
                        {
                            case npcTaskTalkKey:
                            case descriptionLegacyKey:
                                Description.Add(innerLine);
                                break;
                            case descriptionTaskKey:
                                TaskDescription.Add(innerLine);
                                break;
                            case descriptionReturnKey:
                                ReturnDescription.Add(innerLine);
                                break;
                            case playerTaskTalkKey:
                                PlayerTaskTalk.Add(innerLine);
                                break;
                            case descriptionCompletionKey:
                                CompletionDescription.Add(innerLine);
                                break;
                            case carryItemsKey:
                                QuestItemTask t = ParseItem(innerLine);
                                if (t != null) CarryItems.Add(t);
                                break;
                            case killTasksKey:
                                QuestKillTask t1 = ParseKill(innerLine);
                                if (t1 != null) KillTasks.Add(t1);
                                break;
                            case itemTasksKey:
                                QuestItemTask t2 = ParseItem(innerLine);
                                if (t2 != null) ItemTasks.Add(t2);
                                break;
                            case flagTasksKey:
                                QuestFlagTask t3 = ParseFlag(innerLine);
                                if (t3 != null) FlagTasks.Add(t3);
                                break;
                            case fixedRewardsKey:
                                {
                                    ParseReward(FixedRewards, innerLine);
                                    break;
                                }
                            case selectRewardsKey:
                                {
                                    ParseReward(SelectRewards, innerLine);
                                    break;
                                }
                            case expRewardKey:
                                uint.TryParse(innerLine, out uint expReward);
                                ExpReward = expReward;
                                break;
                            case goldRewardKey:
                                uint.TryParse(innerLine, out uint goldReward);
                                GoldReward = goldReward;
                                break;
                            case creditRewardKey:
                                uint.TryParse(innerLine, out uint creditReward);
                                CreditReward = creditReward;
                                break;
                        }
                    }
                }

                currentHeader++;
            }
        }

        public void ParseReward(List<QuestItemReward> list, string line)
        {
            if (line.Length < 1) return;

            string[] split = line.Split(' ');
            ushort count = 1;

            if (split.Length > 1) ushort.TryParse(split[1], out count);

            ItemInfoTable mInfo = Envir.GetItemInfo(split[0]);

            if (mInfo == null)
            {
                mInfo = Envir.GetItemInfo(split[0] + "(M)");
                if (mInfo != null) list.Add(new QuestItemReward() { Item = mInfo, Count = count });

                mInfo = Envir.GetItemInfo(split[0] + "(F)");
                if (mInfo != null) list.Add(new QuestItemReward() { Item = mInfo, Count = count });
            }
            else
            {
                list.Add(new QuestItemReward() { Item = mInfo, Count = count });
            }
        }

        public QuestKillTask ParseKill(string line)
        {
            if (line.Length < 1) return null;

            string[] split = line.Split(' ');
            int count = 1;
            string message = "";

            MonsterInfoTable mInfo = Envir.GetMonsterInfo(split[0]);
            if (split.Length > 1) int.TryParse(split[1], out count);

            var match = _regexMessage.Match(line);
            if (match.Success)
            {
                message = match.Groups[1].Captures[0].Value;
            }

            return mInfo == null ? null : new QuestKillTask() { Monster = mInfo, Count = count, Message = message };
        }

        public QuestItemTask ParseItem(string line)
        {
            if (line.Length < 1) return null;

            string[] split = line.Split(' ');
            ushort count = 1;
            string message = "";

            ItemInfoTable mInfo = Envir.GetItemInfo(split[0]);
            if (split.Length > 1) ushort.TryParse(split[1], out count);

            var match = _regexMessage.Match(line);
            if (match.Success)
            {
                message = match.Groups[1].Captures[0].Value;
            }
            //if (mInfo.StackSize <= 1)
            //{
            //    //recursively add item if cant stack???
            //}

            return mInfo == null ? null : new QuestItemTask { Item = mInfo, Count = count, Message = message };
        }

        public QuestFlagTask ParseFlag(string line)
        {
            if (line.Length < 1) return null;

            string[] split = line.Split(' ');

            int number = -1;
            string message = "";

            int.TryParse(split[0], out number);

            if (number < 0 || number > Globals.FlagIndexCount - 1000) return null;

            var match = _regexMessage.Match(line);
            if (match.Success)
            {
                message = match.Groups[1].Captures[0].Value;
            }

            return new QuestFlagTask { Number = number, Message = message };
        }

        /// <summary>判断该任务是否应当开放给玩家（等级/职业/前置任务），即“是否可提供任务”，不是玩家点击接取时的校验。</summary>
        public bool CanOffer(PlayerObject player)
        {
            if (RequiredMinLevel > player.Level || RequiredMaxLevel < player.Level)
                return false;

            if (RequiredQuest > 0 && !player.CompletedQuests.Contains(RequiredQuest))
                return false;

            switch (player.Class)
            {
                case MirClass.Warrior:
                    if (!RequiredClass.HasFlag(RequiredClass.Warrior))
                        return false;
                    break;
                case MirClass.Wizard:
                    if (!RequiredClass.HasFlag(RequiredClass.Wizard))
                        return false;
                    break;
                case MirClass.Taoist:
                    if (!RequiredClass.HasFlag(RequiredClass.Taoist))
                        return false;
                    break;
                case MirClass.Assassin:
                    if (!RequiredClass.HasFlag(RequiredClass.Assassin))
                        return false;
                    break;
                case MirClass.Archer:
                    if (!RequiredClass.HasFlag(RequiredClass.Archer))
                        return false;
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

            // 发放 NPC：优先用已链接的 info.NpcInfo（NPCScript.ParseQuests 在 NPC 生成时填入），兜底按 NpcIndex 配置索引反查。
            var startNpc = NpcInfo ?? Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)NpcIndex);
            var finishNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == (int)FinishNpcIndex);

            // 内嵌配置任务还没被 NPC 脚本绑定时，按 OriginSettings 里的 NPC id 兜底显示名字。
            if (startNpc == null && OriginSettings != null)
                startNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == OriginSettings.StartNpcId);
            if (finishNpc == null && OriginSettings != null)
                finishNpc = Envir.Main.NPCInfoList.FirstOrDefault(n => n.Index == OriginSettings.FinishNpcId);

            // 客户端按“运行时世界唯一索引(ObjectID)”匹配 NPC（NPCObject.Quests / GetAvailableQuests 用 NPCIndex == ObjectID），
            // 故把配置索引解析成对应 NPC 实例的 ObjectID 下发；取不到运行时实例则回退配置索引（仍能显示名字）。
            uint startNpcObjectID = NpcIndex;
            if (startNpc != null)
            {
                var npcObj = Envir.Main.NPCs.FirstOrDefault(n => n.Info == startNpc);
                if (npcObj != null) startNpcObjectID = npcObj.ObjectID;
            }

            uint finishNpcObjectID = FinishNpcIndex;
            if (finishNpc != null)
            {
                var npcObj = Envir.Main.NPCs.FirstOrDefault(n => n.Info == finishNpc);
                if (npcObj != null) finishNpcObjectID = npcObj.ObjectID;
            }

            return new ClientQuestInfo
            {
                Index = Index,
                NPCIndex = startNpcObjectID,
                FinishNPCIndex = finishNpcObjectID,
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

        public static void FromText(string text)
        {
            string[] data = text.Split(new[] { ',' });

            if (data.Length < 10) return;
            QuestInfoTable info;
            bool isNew = false;
            if (!int.TryParse(data[0], out var index))
            {
                index = -1;
            }
            if (index == -1 || (info = EditEnvir.QuestInfoList.FirstOrDefault(it => it.Index == index)) == null)
            {
                info = new QuestInfoTable() { Index = ++EditEnvir.QuestIndex };
                isNew = true;

            }
            info.Name = data[1];
            info.Group = data[2];

            byte temp;

            byte.TryParse(data[3], out temp);

            info.Type = (QuestType)temp;

            info.FileName = data[4];
            info.GotoMessage = data[5];
            info.KillMessage = data[6];
            info.ItemMessage = data[7];
            info.FlagMessage = data[8];

            int.TryParse(data[9], out int requiredMinLevel);
            int.TryParse(data[10], out int requiredMaxLevel);
            int.TryParse(data[11], out int requiredQuest);
            info.RequiredMinLevel = requiredMinLevel;
            info.RequiredMaxLevel = requiredMaxLevel;
            info.RequiredQuest = requiredQuest;

            byte.TryParse(data[12], out temp);

            info.RequiredClass = (RequiredClass)temp;

            if (isNew) EditEnvir.QuestInfoList.Add(info);
        }

        public string ToText()
        {
            return string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                Index, Name, Group, (byte)Type, FileName, GotoMessage, KillMessage, ItemMessage, FlagMessage, RequiredMinLevel, RequiredMaxLevel, RequiredQuest, (byte)RequiredClass);
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
