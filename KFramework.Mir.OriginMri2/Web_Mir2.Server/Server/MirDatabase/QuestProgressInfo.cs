using Server.MirObjects;
using Server.MirEnvir;
using System.Text.RegularExpressions;

namespace Server.MirDatabase
{
    public class QuestProgressInfo
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public PlayerObject Owner;

        public int Index;

        public QuestInfoTable Info;

        public DateTime StartDateTime = DateTime.MinValue;
        public DateTime EndDateTime = DateTime.MaxValue;

        public List<QuestKillTaskProgress> KillTaskCount = new List<QuestKillTaskProgress>();
        public List<QuestItemTaskProgress> ItemTaskCount = new List<QuestItemTaskProgress>();
        public List<QuestFlagTaskProgress> FlagTaskSet = new List<QuestFlagTaskProgress>();

        // 原始传奇 goal type=532「购买物品」引导任务（如任务14买一本技能书）的完成标记。
        // 该目标没有 Kill/Item/Flag 进度，若不加这道闸，CheckCompleted 会因零目标立即完成、
        // 任务被秒跳过而不显示。买书后由 PlayerObject.CheckBuyGoalQuests 置位，书留在玩家背包。
        public bool BuyGoalDone;

        public List<string> TaskList = new List<string>();

        // 任务追踪栏“点击传送”目标（与 TaskList 一一对应：怪物/NPC + 地图坐标）。
        public List<QuestTaskTarget> Targets = new List<QuestTaskTarget>();

        public bool IsOrphan { get; private set; }
        
        public bool Taken
        {
            get { return StartDateTime > DateTime.MinValue; }
        }

        public bool Completed
        {
            get { return EndDateTime < DateTime.MaxValue; }
        }

        public bool New
        {
            get { return StartDateTime > Envir.Now.AddDays(-1); }
        }

        public QuestProgressInfo(int index)
        {
            Index = index;

            Info = Envir.QuestInfoList.FirstOrDefault(e => e.Index == index);

            if (Info == null)
            {
                IsOrphan = true;
                return;
            }
            
            foreach (var kill in Info.KillTasks)
            {
                KillTaskCount.Add(new QuestKillTaskProgress
                {
                    MonsterID = kill.Monster.Index,
                    Info = kill
                });
            }

            foreach (var item in Info.ItemTasks)
            {
                ItemTaskCount.Add(new QuestItemTaskProgress
                {
                    ItemID = item.Item.Index,
                    Info = item
                });
            }

            foreach (var flag in Info.FlagTasks)
            {
                FlagTaskSet.Add(new QuestFlagTaskProgress
                {
                    Number = flag.Number,
                    Info = flag
                });
            }
        }

        public QuestProgressInfo(BinaryReader reader, int version, int customVersion)
        {
            Index = reader.ReadInt32();
            Info = Envir.QuestInfoList.FirstOrDefault(e => e.Index == Index);

            StartDateTime = DateTime.FromBinary(reader.ReadInt64());
            EndDateTime = DateTime.FromBinary(reader.ReadInt64());

            if (Info == null)
            {
                IsOrphan = true;

                if (version < 90)
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++) reader.ReadInt32(); // Kill counts

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++) reader.ReadInt32(); // Item counts

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++) reader.ReadBoolean(); // Flag states
                }
                else
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        reader.ReadInt32(); // MonsterID
                        reader.ReadInt32(); // Count
                    }

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        reader.ReadInt32(); // ItemID
                        reader.ReadInt32(); // Count
                    }

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        reader.ReadInt32();  // Flag Number
                        reader.ReadBoolean(); // State
                    }
                }

                return; // Skip rest of constructor if quest is missing
            }
            
            if (version < 90)
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var killCount = reader.ReadInt32();

                    if (Info.KillTasks.Count > i)
                    {
                        var progress = new QuestKillTaskProgress
                        {
                            MonsterID = Info.KillTasks[i].Monster.Index,
                            Count = killCount,
                            Info = Info.KillTasks[i]
                        };
                        KillTaskCount.Add(progress);
                    }
                }

                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var itemCount = reader.ReadInt32();
                    if (Info.ItemTasks.Count > i)
                    {
                        var progress = new QuestItemTaskProgress
                        {
                            ItemID = Info.ItemTasks[i].Item.Index,
                            Count = itemCount,
                            Info = Info.ItemTasks[i]
                        };
                        ItemTaskCount.Add(progress);
                    }
                }

                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var flagState = reader.ReadBoolean();
                    if (Info.FlagTasks.Count > i)
                    {
                        var progress = new QuestFlagTaskProgress
                        {
                            Number = Info.FlagTasks[i].Number,
                            State = flagState,
                            Info = Info.FlagTasks[i]
                        };
                        FlagTaskSet.Add(progress);
                    }
                }
            }
            else
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var progress = new QuestKillTaskProgress
                    {
                        MonsterID = reader.ReadInt32(),
                        Count = reader.ReadInt32()
                    };

                    foreach (var task in Info.KillTasks)
                    {
                        if (task.Monster.Index != progress.MonsterID) continue;

                        progress.Info = task;
                        KillTaskCount.Add(progress);
                        break;
                    }
                }

                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var progress = new QuestItemTaskProgress
                    {
                        ItemID = reader.ReadInt32(),
                        Count = reader.ReadInt32()
                    };

                    foreach (var task in Info.ItemTasks)
                    {
                        if (task.Item.Index != progress.ItemID) continue;

                        progress.Info = task;
                        ItemTaskCount.Add(progress);
                        break;
                    }
                }

                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var progress = new QuestFlagTaskProgress
                    {
                        Number = reader.ReadInt32(),
                        State = reader.ReadBoolean()
                    };

                    foreach (var task in Info.FlagTasks)
                    {
                        if (task.Number != progress.Number) continue;

                        progress.Info = task;
                        FlagTaskSet.Add(progress);
                        break;
                    }
                }

                //Add any new tasks which may have been added
                foreach (var kill in Info.KillTasks)
                {
                    if (KillTaskCount.Any(x => x.MonsterID == kill.Monster.Index)) continue;

                    KillTaskCount.Add(new QuestKillTaskProgress
                    {
                        MonsterID = kill.Monster.Index,
                        Info = kill
                    });
                }

                foreach (var item in Info.ItemTasks)
                {
                    if (ItemTaskCount.Any(x => x.ItemID == item.Item.Index)) continue;

                    ItemTaskCount.Add(new QuestItemTaskProgress
                    {
                        ItemID = item.Item.Index,
                        Info = item
                    });
                }

                foreach (var flag in Info.FlagTasks)
                {
                    if (FlagTaskSet.Any(x => x.Number == flag.Number)) continue;

                    FlagTaskSet.Add(new QuestFlagTaskProgress
                    {
                        Number = flag.Number,
                        Info = flag
                    });
                }
            }
        }

        public void Init(PlayerObject player)
        {
            Owner = player;

            if (StartDateTime == DateTime.MinValue)
            {
                StartDateTime = Envir.Now;
            }
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);

            writer.Write(StartDateTime.ToBinary());
            writer.Write(EndDateTime.ToBinary());

            writer.Write(KillTaskCount.Count);
            for (int i = 0; i < KillTaskCount.Count; i++)
            {
                writer.Write(KillTaskCount[i].MonsterID);
                writer.Write(KillTaskCount[i].Count);
            }

            writer.Write(ItemTaskCount.Count);
            for (int i = 0; i < ItemTaskCount.Count; i++)
            {
                writer.Write(ItemTaskCount[i].ItemID);
                writer.Write(ItemTaskCount[i].Count);
            }

            writer.Write(FlagTaskSet.Count);
            for (int i = 0; i < FlagTaskSet.Count; i++)
            {
                writer.Write(FlagTaskSet[i].Number);
                writer.Write(FlagTaskSet[i].State);
            }
        }


        public bool CheckCompleted()
        {
            UpdateTasks();

            bool canComplete = true;

            // 原始传奇 goal type=532 买物品引导任务：必须先买到东西才完成，不能因零目标秒完成。
            if (Info != null && Info.OriginSettings != null && Info.OriginSettings.GoalType == 532 && !BuyGoalDone)
                canComplete = false;

            for (int j = 0; j < KillTaskCount.Count; j++)
            {
                if (KillTaskCount[j].Complete) continue;

                canComplete = false;
            }

            for (int j = 0; j < ItemTaskCount.Count; j++)
            {
                if (ItemTaskCount[j].Complete) continue;

                canComplete = false;
            }

            for (int j = 0; j < FlagTaskSet.Count; j++)
            {
                if (FlagTaskSet[j].Complete) continue;

                canComplete = false;
            }

            if (!canComplete) return false;

            if (!Completed)
            {
                EndDateTime = Envir.Now;
            }
// updatetask again to show gototask
            UpdateTasks();
            return true;
        }

        #region Need Requirement

        public bool NeedItem(ItemInfoTable iInfo)
        {
            return ItemTaskCount.Where((task, i) => task.Info.Item == iInfo && !task.Complete).Any();
        }

        public bool NeedKill(MonsterInfoTable mInfo)
        {
            return KillTaskCount.Where((task, i) => mInfo.Name.StartsWith(task.Info.Monster.Name, StringComparison.OrdinalIgnoreCase) && !task.Complete).Any();
        }

        public bool NeedFlag(int flagNumber)
        {
            return FlagTaskSet.Where((task, i) => task.Number == flagNumber && !task.Complete).Any();
        }

        #endregion

        #region Process Quest Task

        public void ProcessKill(MonsterInfoTable mInfo)
        {
            if (Info.KillTasks.Count < 1) return;

            for (int i = 0; i < KillTaskCount.Count; i++)
            {
                if (!mInfo.Name.StartsWith(KillTaskCount[i].Info.Monster.Name, StringComparison.OrdinalIgnoreCase)) continue;
                KillTaskCount[i].Count++;

                return;
            }
        }

        public void ProcessItem(UserItem[] inventory)
        {
            for (int i = 0; i < ItemTaskCount.Count; i++)
            {
                var count = inventory.Where(item => item != null).
                    Where(item => item.Info.Name == ItemTaskCount[i].Info.Item.Name).
                    Aggregate<UserItem, int>(0, (current, item) => current + item.Count);

                ItemTaskCount[i].Count = count;
            }
        }

        public void ProcessFlag(bool[] Flags)
        {
            for (int i = 0; i < FlagTaskSet.Count; i++)
            {
                for (int j = 0; j < Flags.Length - 1000; j++)
                {
                    if (FlagTaskSet[i].Number != j || !Flags[j]) continue;

                    FlagTaskSet[i].State = Flags[j];
                    break;
                }
            }
        }

        #endregion

        #region Update Task Messages

        public void UpdateTasks()
        {
            TaskList = new List<string>();
            Targets = new List<QuestTaskTarget>();

            // 原始传奇任务：追踪文字直接读权威原表（4config._task / 5config.goals），
            // 忠实复刻原版 MobileTaskViewItem.dataChanged 的两段式 goInfo/completeinfo。
            if (Info != null && Info.OriginSettings != null && Envir.OriginTasks.Count > 0)
            {
                UpdateOriginQuestTasks();
                return;
            }

            UpdateKillTasks();
            UpdateItemTasks();
            UpdateFlagTasks();
            UpdateGotoTask();
        }

        /// <summary>
        /// 原始传奇任务追踪文字：完全按原版 dataChanged 逻辑读权威表。
        /// 进行中(Accepted)：遍历目标，取 5config.goals[goalId].goInfo，按 "|" 拆成
        ///   info0(前缀，白) + info1(目标，绿)，info1 里的 {0} 替换为 "当前/总数"；
        ///   goal.type=342(物品)时 info1 显示物品名（cm.item[param]）。
        /// 完成(Finish)或无进度：取 4config._task[id].completeinfo 按 "|" 拆成 info0 + info1。
        /// 客户端 QuestTrackingItem.Bind 会把 info1 染绿，这里只负责拼出两段文字。
        /// </summary>
        private void UpdateOriginQuestTasks()
        {
            var os = Info.OriginSettings;

            // 原表取不到时回退到落表的清洗字段（保证老版本/缺分片也能显示）
            OriginTaskTable orgTask = null;
            if (os.OriginTaskId > 0) Envir.OriginTasks.TryGetValue(os.OriginTaskId, out orgTask);
            string completeInfo = orgTask != null && !string.IsNullOrEmpty(orgTask.completeinfo)
                ? orgTask.completeinfo : os.CompleteInfo;

            // 完成待交：显示 completeinfo（"找到  |书店老板" → "找到 书店老板"）
            if (Completed)
            {
                if (!string.IsNullOrEmpty(completeInfo))
                {
                    TaskList.Add(StripOriginSeparator(completeInfo));
                    Targets.Add(MakeFinishNPCTarget());
                }
                return;
            }

            // 进行中：优先按权威 goals 表渲染目标行
            OriginGoalTable orgGoal = null;
            if (os.GoalId > 0) Envir.OriginGoals.TryGetValue(os.GoalId, out orgGoal);

            if (orgGoal != null && !string.IsNullOrEmpty(orgGoal.goInfo))
            {
                BuildOriginGoalLine(orgGoal);
                return;
            }

            // 回退：用落表的 GoalInfo（转换器已把 | 清成空格、{0} 换成总数）
            if (!string.IsNullOrEmpty(os.GoalInfo))
            {
                TaskList.Add(StripOriginSeparator(os.GoalInfo));
                Targets.Add(MakeNPCTarget());
            }
        }

        /// <summary>按原版 dataChanged 的 Accepted 分支拼一行目标文字，并挂对应传送目标。</summary>
        private void BuildOriginGoalLine(OriginGoalTable goal)
        {
            string[] parts = goal.goInfo.Split('|');
            string info0 = parts.Length > 0 ? parts[0] : string.Empty;
            string info1 = parts.Length > 1 ? parts[1] : string.Empty;

            // 原版：type=342 物品目标显示物品名（cm.item[parseInt(param)] 的 [3]）；其余用 {0} 替换进度。
            if (goal.type == 342 && int.TryParse(goal.param, out int itemId) && itemId > 0)
            {
                var item = Envir.GetItemInfo(itemId);
                info1 = item != null ? item.Name : info1;
            }
            else
            {
                int current = GetOriginGoalProgress();
                int total = Math.Max(1, goal.count);
                info1 = Regex.Replace(info1, @"\{\d+\}", current + "/" + total);
            }

            string text = (info0 + info1).Trim();
            if (string.IsNullOrEmpty(text)) text = StripOriginSeparator(Info.OriginSettings.GoalInfo);

            TaskList.Add(text);

            // 目标类型：击杀类挂怪物坐标，其余挂交付 NPC。
            QuestTaskTarget monsterTarget = null;
            if (KillTaskCount.Count > 0 && KillTaskCount[0].Info != null && KillTaskCount[0].Info.Monster != null)
                monsterTarget = MakeMonsterTarget(KillTaskCount[0].Info.Monster);
            Targets.Add(monsterTarget ?? MakeNPCTarget());
        }

        /// <summary>当前目标的进度值：击杀优先，其次收集，均无进度(如买书)时按完成标记取 0/1。</summary>
        private int GetOriginGoalProgress()
        {
            if (KillTaskCount.Count > 0) return KillTaskCount[0].Count;
            if (ItemTaskCount.Count > 0) return ItemTaskCount[0].Count;
            var os = Info.OriginSettings;
            if (os != null && os.GoalType == 532) return BuyGoalDone ? 1 : 0;
            return 0;
        }

        /// <summary>去掉原版两段分隔符 "|"（两段文字在客户端拼成一行显示）。</summary>
        private static string StripOriginSeparator(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("|", string.Empty).Trim();
        }

        public void UpdateKillTasks()
        {
            if(Info.KillMessage.Length > 0 && Info.KillTasks.Count > 0) 
            {
                bool allComplete = true;
                for (int i = 0; i < KillTaskCount.Count; i++)
                {
                    if (KillTaskCount[i].Complete) continue;

                    allComplete = false;
                }

                TaskList.Add(string.Format("{0} {1}", Info.KillMessage, allComplete ? GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TaskCompleted) : ""));
                Targets.Add(KillTaskCount.Count > 0 ? MakeMonsterTarget(KillTaskCount[0].Info.Monster) ?? MakeNPCTarget() : MakeNPCTarget());
                return;
            }

            for (int i = 0; i < KillTaskCount.Count; i++)
            {
                if (KillTaskCount[i].Complete) continue;

                if (string.IsNullOrEmpty(Info.KillTasks[i].Message))
                {
                    TaskList.Add(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.KillCountProgress), KillTaskCount[i].Info.Monster.GameName, KillTaskCount[i].Count,
                        KillTaskCount[i].Info.Count, ""));
                    Targets.Add(MakeMonsterTarget(KillTaskCount[i].Info.Monster) ?? MakeNPCTarget());
                }
                else
                {
                    TaskList.Add(string.Format(Info.KillTasks[i].Message, KillTaskCount[i].Count, KillTaskCount[i].Info.Count));
                    Targets.Add(MakeMonsterTarget(KillTaskCount[i].Info.Monster) ?? MakeNPCTarget());
                }
            }
        }

        public void UpdateItemTasks()
        {
            if (Info.ItemMessage.Length > 0 && Info.ItemTasks.Count > 0)
            {
                bool allComplete = true;
                for (int i = 0; i < ItemTaskCount.Count; i++)
                {
                    if (ItemTaskCount[i].Complete) continue;

                    allComplete = false;
                }
                
                TaskList.Add(Info.ItemMessage);
                Targets.Add(MakeItemTarget());
                return;
            }

            for (int i = 0; i < ItemTaskCount.Count; i++)
            {
                if (ItemTaskCount[i].Complete) continue;

                if (string.IsNullOrEmpty(Info.ItemTasks[i].Message))
                {
                    TaskList.Add(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CollectProgress), Info.ItemTasks[i].Item.FriendlyName, ItemTaskCount[i].Count,
                        Info.ItemTasks[i].Count, ""));
                    Targets.Add(MakeItemTarget());
                }
                else
                {
                    TaskList.Add(string.Format(Info.ItemTasks[i].Message, ItemTaskCount[i].Count, Info.ItemTasks[i].Count));
                    Targets.Add(MakeItemTarget());
                }
            }
        }

        public void UpdateFlagTasks()
        {
            if (Info.FlagMessage.Length > 0)
            {
                bool allComplete = true;
                for (int i = 0; i < FlagTaskSet.Count; i++)
                {
                    if (FlagTaskSet[i].State) continue;

                    allComplete = false;
                }
                
                TaskList.Add(Info.FlagMessage);
                Targets.Add(MakeNPCTarget());
                return;
            }

            for (int i = 0; i < FlagTaskSet.Count; i++)
            {
                if (FlagTaskSet[i].Complete) continue;

                if (string.IsNullOrEmpty(Info.FlagTasks[i].Message))
                {
                    TaskList.Add(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ActivateFlag), Info.FlagTasks[i].Number, ""));
                    Targets.Add(MakeNPCTarget());
                }
                else
                {
                    TaskList.Add(Info.FlagTasks[i].Message);
                    Targets.Add(MakeNPCTarget());
                }
            }
        }

        public void UpdateGotoTask()
        {
            if (Info.GotoMessage.Length <= 0 || !Completed) return;
            
            TaskList.Add(Info.GotoMessage);
            Targets.Add(MakeFinishNPCTarget());
        }

        #region Quest Target Helpers

        private QuestTaskTarget MakeMonsterTarget(MonsterInfoTable monster)
        {
            if (monster == null) return null;

            // 1) 已持久化的刷点（开启 SaveRespawnTime 的怪物）
            for (int i = 0; i < Envir.SavedSpawns.Count; i++)
            {
                MapRespawn spawn = Envir.SavedSpawns[i];
                if (spawn.Info == null || spawn.Map == null) continue;
                if (spawn.Info.MonsterIndex != monster.Index) continue;

                return new QuestTaskTarget
                {
                    Type = QuestTargetType.Monster,
                    Name = monster.GameName,
                    MapIndex = (uint)spawn.Map.Info.Index,
                    X = spawn.Info.Location.X,
                    Y = spawn.Info.Location.Y,
                    MonsterIndex = (uint)monster.Index
                };
            }

            // 2) 兜底：扫描所有已加载地图的实时刷怪点，覆盖未开启 SaveRespawnTime 的怪物（如稻草人）
            for (int m = 0; m < Envir.MapList.Count; m++)
            {
                Map map = Envir.MapList[m];
                if (map == null || map.Info == null) continue;

                for (int i = 0; i < map.Respawns.Count; i++)
                {
                    MapRespawn spawn = map.Respawns[i];
                    if (spawn?.Info == null || spawn.Map == null) continue;
                    if (spawn.Info.MonsterIndex != monster.Index) continue;

                    return new QuestTaskTarget
                    {
                        Type = QuestTargetType.Monster,
                        Name = monster.GameName,
                        MapIndex = (uint)map.Info.Index,
                        X = spawn.Info.Location.X,
                        Y = spawn.Info.Location.Y,
                        MonsterIndex = (uint)monster.Index
                    };
                }
            }

            return null;
        }

        private QuestTaskTarget MakeNPCTarget()
        {
            NPCInfoTable npc = Info.NpcInfo;
            // 兜底：只用 OriginSettings 里的配置索引（StartNpcId/FinishNpcId）查 NPCInfo。
            // NpcIndex/FinishNpcIndex 存的是运行时 ObjectID，绝不能当配置索引查——会误命中
            // NPCInfo(854) 这类 MapIndex=854@(63,71) 的占位坏 NPC，导致任务追踪传送到不存在的地图。
            if (npc == null && Info.OriginSettings != null && Info.OriginSettings.StartNpcId > 0)
                npc = Envir.GetNPCInfo(Info.OriginSettings.StartNpcId);
            if (npc == null && Info.OriginSettings != null && Info.OriginSettings.FinishNpcId > 0)
                npc = Envir.GetNPCInfo(Info.OriginSettings.FinishNpcId);
            if (npc == null)
            {
                // 仍未找到：返回“不可导航”的占位目标，避免 Targets 出现 null 导致序列化崩溃
                return new QuestTaskTarget
                {
                    Type = QuestTargetType.NPC,
                    Name = string.Empty,
                    MapIndex = 0,
                    X = 0,
                    Y = 0
                };
            }

            return new QuestTaskTarget
            {
                Type = QuestTargetType.NPC,
                Name = npc.Name,
                MapIndex = (uint)npc.MapIndex,
                X = npc.Location.X,
                Y = npc.Location.Y
            };
        }

        /// <summary>
        /// 按 NPC 配置 Index 找第一个运行中的实例，取其真实地图/坐标。
        /// 同一个 NPC（如夏家店老板 10091）可能在多个地图各有一个实例（103 比奇省、154 盟重、
        /// 甚至室内衣服店），配置里的 MapIndex/Location 只是初始摆放，不能当传送目标写死。
        /// 绝不能拿 ObjectID 当 NPCInfo 配置索引查——两套编号体系，会误命中 MapIndex=854 的坏 NPC。
        /// </summary>
        private QuestTaskTarget MakeNPCTargetByConfigIndex(int npcConfigIndex)
        {
            if (npcConfigIndex <= 0) return null;

            for (int i = 0; i < Envir.NPCs.Count; i++)
            {
                var npc = Envir.NPCs[i];
                if (npc == null || npc.Info == null || npc.Info.Index != npcConfigIndex) continue;
                if (npc.CurrentMap == null || npc.CurrentMap.Info == null) continue;

                return new QuestTaskTarget
                {
                    Type = QuestTargetType.NPC,
                    Name = npc.Name,
                    MapIndex = (uint)npc.CurrentMap.Info.Index,
                    X = npc.CurrentLocation.X,
                    Y = npc.CurrentLocation.Y
                };
            }

            // 实例尚未生成：退回 NPCInfo 配置摆放（第一个 Placement）
            NPCInfoTable npcInfo = Envir.GetNPCInfo(npcConfigIndex);
            if (npcInfo != null)
                return new QuestTaskTarget
                {
                    Type = QuestTargetType.NPC,
                    Name = npcInfo.Name,
                    MapIndex = (uint)npcInfo.MapIndex,
                    X = npcInfo.Location.X,
                    Y = npcInfo.Location.Y
                };
            return null;
        }

        private QuestTaskTarget MakeFinishNPCTarget()
        {
            // 交任务（返回）目标：优先按 NPC 配置索引找运行时实例（可能在任意地图、有多个分身），
            // 取真实 CurrentMap/CurrentLocation，而不是配置写死的 MapIndex/Location。
            if (Info.OriginSettings != null)
            {
                var t = MakeNPCTargetByConfigIndex(Info.OriginSettings.FinishNpcId);
                if (t != null) return t;
            }
            // 兜底：旧数据没有 OriginSettings 时用 FinishNpcIndex 按 ObjectID 找运行时实例。
            if (Info.FinishNpcIndex != 0)
            {
                var npc = Envir.NPCs.FirstOrDefault(x => x.ObjectID == Info.FinishNpcIndex);
                if (npc != null && npc.CurrentMap != null)
                    return new QuestTaskTarget
                    {
                        Type = QuestTargetType.NPC,
                        Name = npc.Name,
                        MapIndex = (uint)npc.CurrentMap.Info.Index,
                        X = npc.CurrentLocation.X,
                        Y = npc.CurrentLocation.Y
                    };
            }
            return MakeNPCTarget();
        }

        private QuestTaskTarget MakeItemTarget()
        {
            // 收集任务：优先传送到“掉落该物品的怪物”（通常是本任务的击杀目标，例如杀稻草人掉姜茶），
            // 没有击杀怪可参考、或该怪物无刷点信息时，尝试反查“掉落本任务物品”的怪物（如收割鹿得肉），
            // 仍找不到再退回交任务 NPC。
            if (KillTaskCount.Count > 0 && KillTaskCount[0].Info?.Monster != null)
            {
                var t = MakeMonsterTarget(KillTaskCount[0].Info.Monster);
                if (t != null) return t;
            }

            var dropTarget = MakeDropSourceTarget();
            if (dropTarget != null) return dropTarget;

            return MakeNPCTarget();
        }

        private QuestTaskTarget MakeDropSourceTarget()
        {
            // 反查“掉落本任务所需物品”的怪物，并传送到其刷点（例如收集鹿肉 → 鹿）。
            // 覆盖纯收集类任务（无击杀任务，如收割鹿取肉）以及击杀目标无刷点时的退化场景。
            if (ItemTaskCount == null || ItemTaskCount.Count == 0) return null;

            foreach (var it in ItemTaskCount)
            {
                if (it?.Info?.Item == null) continue;
                int itemIndex = it.Info.Item.Index;

                for (int m = 0; m < Envir.MonsterInfoList.Count; m++)
                {
                    MonsterInfoTable monster = Envir.MonsterInfoList[m];
                    if (monster?.Drops == null) continue;

                    bool dropsItem = false;
                    for (int d = 0; d < monster.Drops.Count; d++)
                    {
                        DropInfo drop = monster.Drops[d];
                        if (drop?.Item != null && drop.Item.Index == itemIndex)
                        {
                            dropsItem = true;
                            break;
                        }
                    }
                    if (!dropsItem) continue;

                    QuestTaskTarget t = MakeMonsterTarget(monster);
                    if (t != null) return t;
                }
            }
            return null;
        }

        #endregion

        #endregion

        public ClientQuestProgress CreateClientQuestProgress()
        {
            int done = 0;
            for (int i = 0; i < KillTaskCount.Count; i++) if (KillTaskCount[i].Complete) done++;
            for (int i = 0; i < ItemTaskCount.Count; i++) if (ItemTaskCount[i].Complete) done++;
            for (int i = 0; i < FlagTaskSet.Count; i++) if (FlagTaskSet[i].Complete) done++;

            return new ClientQuestProgress
            {
                Id = Index,
                TaskList = TaskList,
                Targets = new List<QuestTaskTarget>(Targets),
                Taken = Taken,
                Completed = Completed,
                New = New,
                TaskCompletedCount = done
            };
        }
    }

    public class QuestKillTaskProgress
    {
        public int MonsterID { get; set; }
        public int Count { get; set; }
        public QuestKillTask Info { get; set; }

        public bool Complete { get { return Info != null && Count >= Info.Count; } }
    }

    public class QuestItemTaskProgress
    {
        public int ItemID { get; set; }
        public int Count { get; set; }
        public QuestItemTask Info { get; set; }

        public bool Complete { get { return Info != null && Count >= Info.Count; } }
    }

    public class QuestFlagTaskProgress
    {
        public int Number { get; set; }
        public bool State { get; set; }
        public QuestFlagTask Info { get; set; }

        public bool Complete { get { return Info != null && State == true; } }
    }
}
