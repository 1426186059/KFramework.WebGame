using Server.MirObjects;
using Server.MirEnvir;

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

        public QuestInfo Info;

        public DateTime StartDateTime = DateTime.MinValue;
        public DateTime EndDateTime = DateTime.MaxValue;

        public List<QuestKillTaskProgress> KillTaskCount = new List<QuestKillTaskProgress>();
        public List<QuestItemTaskProgress> ItemTaskCount = new List<QuestItemTaskProgress>();
        public List<QuestFlagTaskProgress> FlagTaskSet = new List<QuestFlagTaskProgress>();

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

                if (Info.TimeLimitInSeconds > 0)
                {
                    Owner.ExpireTimer($"Quest-{Index}");
                }
            }
// updatetask again to show gototask
            UpdateTasks();
            return true;
        }

        #region Need Requirement

        public bool NeedItem(ItemInfo iInfo)
        {
            return ItemTaskCount.Where((task, i) => task.Info.Item == iInfo && !task.Complete).Any();
        }

        public bool NeedKill(MonsterInfo mInfo)
        {
            return KillTaskCount.Where((task, i) => mInfo.Name.StartsWith(task.Info.Monster.Name, StringComparison.OrdinalIgnoreCase) && !task.Complete).Any();
        }

        public bool NeedFlag(int flagNumber)
        {
            return FlagTaskSet.Where((task, i) => task.Number == flagNumber && !task.Complete).Any();
        }

        #endregion

        #region Process Quest Task

        public void ProcessKill(MonsterInfo mInfo)
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

            UpdateKillTasks();
            UpdateItemTasks();
            UpdateFlagTasks();
            UpdateGotoTask();
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

        private QuestTaskTarget MakeMonsterTarget(MonsterInfo monster)
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
                    Y = spawn.Info.Location.Y
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
                        Y = spawn.Info.Location.Y
                    };
                }
            }

            return null;
        }

        private QuestTaskTarget MakeNPCTarget()
        {
            NPCInfo npc = Info.NpcInfo;
            // 兜底解析：部分任务可能尚未绑定 NpcInfo，尝试用索引定位 NPC
            if (npc == null && Info.NpcIndex != 0)
                npc = Envir.GetNPCInfo((int)Info.NpcIndex);
            if (npc == null && Info.FinishNpcIndex != 0)
                npc = Envir.GetNPCInfo((int)Info.FinishNpcIndex);
            if (npc == null) return null;

            return new QuestTaskTarget
            {
                Type = QuestTargetType.NPC,
                Name = npc.Name,
                MapIndex = (uint)npc.MapIndex,
                X = npc.Location.X,
                Y = npc.Location.Y
            };
        }

        private QuestTaskTarget MakeFinishNPCTarget()
        {
            // 交任务（返回）目标：使用 FinishNpcIndex（运行时 NPC ObjectID）定位真正的交任务 NPC，
            // 而不是发放/起始 NPC（NpcInfo）。
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
            // 没有击杀怪可参考、或该怪物无刷点信息时，退回交任务 NPC。
            if (KillTaskCount.Count > 0 && KillTaskCount[0].Info?.Monster != null)
            {
                var t = MakeMonsterTarget(KillTaskCount[0].Info.Monster);
                if (t != null) return t;
            }
            return MakeNPCTarget();
        }

        #endregion

        #endregion

        #region Optional Functions

        public void SetTimer()
        {
            if (Owner == null)
            {
                return;
            }

            if (Info.TimeLimitInSeconds > 0)
            {
                var secondsSinceStarted = (int)(Envir.Now - StartDateTime).TotalSeconds;

                var remainingSeconds = Info.TimeLimitInSeconds - secondsSinceStarted;

                if (remainingSeconds > 0)
                {
                    Owner.SetTimer($"Quest-{Index}", remainingSeconds, 1);
                }

                DelayedAction action = new DelayedAction(DelayedType.Quest, Envir.Time + (remainingSeconds * 1000), this, QuestAction.TimeExpired, true);
                Owner.ActionList.Add(action);
            }
        }

        public void RemoveTimer()
        {
            if (Owner == null)
            {
                return;
            }

            if (Info.TimeLimitInSeconds > 0)
            {
                Owner.ExpireTimer($"Quest-{Index}");
            }
        }

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
