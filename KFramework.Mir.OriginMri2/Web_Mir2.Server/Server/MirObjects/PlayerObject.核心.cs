// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using System.Drawing;
using S = ServerPackets;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        /// <summary>接取任务时自动记录到跟踪栏，服务器权威并随角色存档持久化；客户端没有勾选/取消入口。</summary>
        private bool TrackQuest(int questIndex, bool track, bool requireActive = true)
        {
            if (questIndex <= 0) return false;
            // 接取任务时 SendUpdateQuest 还没把 quest 加进 CurrentQuests，此时不做在线任务校验。
            if (track && requireActive && !CurrentQuests.Any(q => q.Info != null && q.Info.Index == questIndex)) return false;

            if (track)
            {
                if (TrackedQuests.Contains(questIndex)) return true;
                // 上限时淘汰最早追踪的一条（旧存档可能已累积满），保证新接取的任务总能进追踪栏。
                if (TrackedQuests.Count >= 5)
                    TrackedQuests.RemoveAt(0);

                TrackedQuests.Add(questIndex);
            }
            else
            {
                TrackedQuests.Remove(questIndex);
            }

            return track ? TrackedQuests.Contains(questIndex) : !TrackedQuests.Contains(questIndex);
        }

        // 已开放（未接取）的任务索引：左上角追踪栏显示"寻找 NPC"，玩家前往后才正式接取。
        public List<int> OfferedQuests = new List<int>();

        private QuestScheduler _questScheduler;
        public QuestScheduler QuestScheduler => _questScheduler ?? (_questScheduler = new QuestScheduler(this));

        public PlayerObject() { }

        public PlayerObject(CharacterInfo info, MirConnection connection) : base(info, connection) { }
        protected override void Load(CharacterInfo info, MirConnection connection)
        {
            if (info.Player != null)
            {
                throw new InvalidOperationException("Player.Info not Null.");
            }

            info.Player = this;
            info.Mount = new MountInfo(this);

            Connection = connection;
            Info = info;
            Account = Connection.Account;

            Stats = new Stats();

            Report = new Reporting(this);

            if (Account.AdminAccount)
            {
                IsGM = true;
                MessageQueue.Enqueue(string.Format("{0} is now a GM", Name));
            }

            if (Level == 0) NewCharacter();

            if (Info.GuildIndex != -1)
            {
                MyGuild = Envir.GetGuild(Info.GuildIndex);
            }

            if (info.CurrentHeroIndex > 0)
                CurrentHero = Envir.GetHeroInfo(info.CurrentHeroIndex);

            RefreshStats();

            if (HP == 0)
            {
                SetHP(Stats[Stat.HP]);
                SetMP(Stats[Stat.MP]);

                CurrentLocation = BindLocation;
                CurrentMapIndex = BindMapIndex;

                if (Info.PKPoints >= 200)
                {
                    Map temp = Envir.GetMapByNameAndInstance(Settings.PKTownMapName, 1);
                    Point tempLocation = new Point(Settings.PKTownPositionX, Settings.PKTownPositionY);

                    if (temp != null && temp.ValidPoint(tempLocation))
                    {
                        CurrentMapIndex = temp.Info.Index;
                        CurrentLocation = tempLocation;
                    }
                }
            }

            Info.LastLoginDate = Envir.Now;

            // 原始传奇技能：登录时补齐占位/自动学会（需书技能保持 0 级待书）。
            LearnOriginSkills(false);
            CheckSkillGoalQuests(); // 已学会对应技能的"学习技能"支线在登录时补发奖励
            CheckBuyGoalQuests();   // 532 买物品引导任务：背包已有书时补判完成
        }

        /// <summary>
        /// 原始传奇技能初始化（原版"未学也可见"：所有技能都进 Magics，Level=0 为未学占位）。
        /// NeedBook=1 的技能（config 有 cost）必须用 1 级技能书激活；
        /// NeedBook=0 的技能（普通攻击/倚天/神龙/内功等）达到 condition 学习等级自动置 1。
        /// notify=true 时升级新学会的自动技能单独发包（登录包会统一下发全部）。
        /// </summary>
        public void LearnOriginSkills(bool notify)
        {
            if (Info == null) return;
            var learnable = Envir.GetLearnableSkills(Info.Class);

            foreach (MagicInfoTable info in learnable)
            {
                MagicInfoRunItem magic = null;
                for (int i = 0; i < Info.Magics.Count; i++)
                {
                    if (Info.Magics[i] != null && Info.Magics[i].Spell == info.Spell)
                    {
                        magic = Info.Magics[i];
                        break;
                    }
                }

                // 达学习等级 + 无需技能书 → 自动学会，MaxLevel 按配置可升级级数，Level 从 0 起靠熟练度升。
                bool autoLearn = info.NeedBook == 0 && Envir.GetLearnLevel(info) <= Level;
                byte learnMaxLevel = info.Need3 > 0 ? (byte)3 : (info.Need2 > 0 ? (byte)2 : (byte)1);

                if (magic == null)
                {
                    magic = new MagicInfoRunItem(info.Spell);
                    if (magic.Info == null) continue;
                    magic.MaxLevel = autoLearn ? learnMaxLevel : (byte)0;
                    Info.Magics.Add(magic);
                    if (autoLearn && notify) SendMagicInfo(magic);
                }
                else if (autoLearn && !magic.IsLearned())
                {
                    magic.MaxLevel = learnMaxLevel;
                    if (notify) SendMagicInfo(magic);
                }
            }
        }

        public void StopGame(byte reason)
        {
            if (Node == null) return;

            if (CurrentMap != null && CurrentMap.ValidPoint(CurrentLocation))
            {
                CurrentMapIndex = CurrentMap.Info.Index;

                if (!CurrentMap.Info.RequiredGroup)
                {
                    LastValidMap = CurrentMap;
                    LastValidLocation = CurrentLocation;
                }
            }

            for (int i = Pets.Count - 1; i >= 0; i--)
            {
                MonsterObject pet = Pets[i];

                if (pet.Race == ObjectType.Creature)
                {
                    //dont save Creatures they will miss alot of AI-Info when they get spawned on login
                    UnSummonIntelligentCreature(((IntelligentCreatureObject)pet).PetType, false);

                    Pets.RemoveAt(i);
                    continue;
                }

                pet.Master = null;

                if (!pet.Dead)
                {
                    switch (Settings.PetSave)
                    {
                        case true when Settings.PetSave is true:

                            switch (Class)
                            {
                                case (MirClass.Assassin):

                                    if (Info.Name != Settings.AssassinCloneName)
                                    {
                                        Info.Pets.Add(new PetInfo(pet));
                                    }

                                    break;
                                default:

                                    Info.Pets.Add(new PetInfo(pet));

                                    break;
                            }

                            break;
                        case false when Settings.PetSave is false:

                            switch (Class)
                            {
                                case (MirClass.Wizard):

                                    if (pet.Name == Settings.CloneName)
                                    {
                                        Info.Pets.Add(new PetInfo(pet));
                                    }
                                    else
                                    {
                                        Info.Pets.Add(new PetInfo(pet)
                                        {
                                            TameTime = pet.TameTime - Envir.Time
                                        });
                                    }

                                    break;

                                case (MirClass.Taoist):
                                    if (pet.Name == Settings.SkeletonName || pet.Name == Settings.AngelName || pet.Name == Settings.ShinsuName)
                                        Info.Pets.Add(new PetInfo(pet));

                                    break;
                            }

                            break;
                    }

                    Envir.MonsterCount--;
                    pet.CurrentMap.MonsterCount--;

                    pet.CurrentMap.RemoveObject(pet);
                    pet.Despawn();

                    Pets.RemoveAt(i);
                }
            }

            if (HeroSpawned)
                DespawnHero();

            for (int i = 0; i < Info.Magics.Count; i++)
            {
                var magic = Info.Magics[i];

                if (Envir.Time < (magic.CastTime + magic.GetDelay()))
                {
                    magic.CastTime -= Envir.Time;
                }
                else
                {
                    magic.CastTime = int.MinValue;
                }
            }

            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                var buff = Buffs[i];
                buff.Caster = null;
                buff.ObjectID = 0;

                if (buff.Properties.HasFlag(BuffProperty.RemoveOnExit))
                {
                    Buffs.RemoveAt(i);
                }
            }

            for (int i = 0; i < PoisonList.Count; i++)
            {
                var poison = PoisonList[i];
                poison.Owner = null;
            }

            if (MyGuild != null)
            {
                MyGuild.PlayerLogged(this, false);
            }

            Envir.Players.Remove(this);

            CurrentMap.RemoveObject(this);
            Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

            Despawn();
            LeaveGroup();
            TradeCancel();
            CancelItemRental();
            RefineCancel();
            LogoutRelationship();
            LogoutMentor();

            string logReason = LogOutReason(reason);

            MessageQueue.Enqueue(logReason);

            Fishing = false;

            Info.LastIP = Connection.IPAddress;
            Info.LastLogoutDate = Envir.Now;

            Report.Disconnected(logReason);
            Connection.WorldMapSetupSent = false;

            if (!IsGM)
            {
                Envir.OnlineRankingCount[0]--;
                Envir.OnlineRankingCount[(int)Class + 1]--;
            }

            CleanUp();
        }
        private string LogOutReason(byte reason)
        {
            switch (reason)
            {
                //0-10 are 'senddisconnect to client'
                case 0:
                    return LanguageMgr.Get("ServerClosed", Name);
                case 1:
                    return LanguageMgr.Get("DoubleLogin", Name);
                case 2:
                    return LanguageMgr.Get("ChatMsgTooLong", Name);
                case 3:
                    return LanguageMgr.Get("ServerCrashed", Name);
                case 4:
                    return LanguageMgr.Get("KickedByAdmin", Name);
                case 5:
                    return LanguageMgr.Get("MaximunConnectionsReached", Name);
                case 6:
                    return LanguageMgr.Get("AccountBanded", Name);
                case 10:
                    return LanguageMgr.Get("WrongClientVersion", Name);
                case 20:
                    return LanguageMgr.Get("UserLoggedOutMissingReason", Name);
                case 21:
                    return LanguageMgr.Get("UserLogoutReasonTimeoutMessage", Name);
                case 22:
                    return LanguageMgr.Get("UserLogoutClosedGameMessage", Name);
                case 23:
                    return LanguageMgr.Get("UserLoggedOutSelectCharReasonMessage", Name);
                case 24:
                    return LanguageMgr.Get("UserLoggedOutReasonObserve", Name);
                default:
                    return LanguageMgr.Get("UserLoggedOutUnknownReason", Name);
            }
        }
        protected override void NewCharacter()
        {
            if (Envir.StartPoints.Count == 0) return;

            SetBind();

            base.NewCharacter();
        }
        public override void Process()
        {
            if (Connection == null || Node == null || Info == null) return;

            if (GroupInvitation != null && GroupInvitation.Node == null)
                GroupInvitation = null;

            base.Process();

            if (Settings.RestedPeriod > 0 && Envir.Time > RestedTime)
            {
                _restedCounter = InSafeZone ? _restedCounter + 1 : _restedCounter;

                if (_restedCounter > 0)
                {
                    int count = _restedCounter / (Settings.RestedPeriod * 60);

                    GiveRestedBonus(count);
                }

                RestedTime = Envir.Time + Settings.Second;
            }

            if (NewMail)
            {
                ReceiveChat(LanguageMgr.Get("NewMail"), ChatType.System);

                GetMail();
            }

            if (Account.HasExpandedStorage && Envir.Now > Account.ExpandedStorageExpiryDate)
            {
                Account.HasExpandedStorage = false;
                ReceiveChat(LanguageMgr.Get("ExpandedStorageExpired"), ChatType.System);
                Enqueue(new S.ResizeStorage { Size = Account.Storage.Length, HasExpandedStorage = Account.HasExpandedStorage, ExpiryTime = Account.ExpandedStorageExpiryDate });
            }

            if (Fishing && Envir.Time > FishingTime)
            {
                _fishCounter++;
                UpdateFish();
            }

            // [RG] Immediate enforcement
            if (!IsGM && Node != null && CurrentMap?.Info?.RequiredGroup == true)
            {
                int required = Math.Max(2, CurrentMap.Info.RequiredGroupSize);
                int have = GroupMembers?.Count ?? 0;

                if (have < required)
                {
                    ForceLeaveGroupRequiredMap();
                }
            }
            RefreshCreaturesTimeLeft();
        }
        public override void Process(DelayedAction action)
        {
            if (action.FlaggedToRemove)
                return;

            switch (action.Type)
            {
                case DelayedType.Magic:
                    CompleteMagic(action.Params);
                    break;
                case DelayedType.Damage:
                    CompleteAttack(action.Params);
                    break;
                case DelayedType.MapMovement:
                    CompleteMapMovement(action.Params);
                    break;
                case DelayedType.Mine:
                    CompleteMine(action.Params);
                    break;
                case DelayedType.NPC:
                    CompleteNPC(action.Params);
                    break;
                case DelayedType.Poison:
                    CompletePoison(action.Params);
                    break;
                case DelayedType.DamageIndicator:
                    CompleteDamageIndicator(action.Params);
                    break;
                case DelayedType.SpellEffect:
                    CompleteSpellEffect(action.Params);
                    break;
            }
        }
        protected override void Moved()
        {
            base.Moved();
            CheckConquest();
            if (TradePartner != null)
                TradeCancel();

            if (ItemRentalPartner != null)
                CancelItemRental();
        }
        public override void Die()
        {
            if (SpecialMode.HasFlag(SpecialItemMode.Revival) && Envir.Time > LastRevivalTime)
            {
                LastRevivalTime = Envir.Time + 300000;

                for (var i = (int)EquipmentSlot.RingL; i <= (int)EquipmentSlot.RingR; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null) continue;
                    if (!(item.Info.Unique.HasFlag(SpecialItemMode.Revival)) || item.CurrentDura < 1000) continue;
                    SetHP(Stats[Stat.HP]);
                    item.CurrentDura = (ushort)(item.CurrentDura - 1000);
                    Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                    RefreshStats();
                    ReceiveChat(LanguageMgr.Get("SecondChanceAtLife"), ChatType.System);
                    return;
                }
            }

            if (LastHitter != null && LastHitter.Race == ObjectType.Player)
            {
                PlayerObject hitter = (PlayerObject)LastHitter;

                if (AtWar(hitter) || WarZone)
                {
                    hitter.ReceiveChat(LanguageMgr.Get("ProtectedByLaw"), ChatType.System);
                }
                else if (Envir.Time > BrownTime && PKPoints < 200)
                {
                    UserItem weapon = hitter.Info.Equipment[(byte)EquipmentSlot.Weapon];

                    hitter.PKPoints = Math.Min(int.MaxValue, LastHitter.PKPoints + 100);
                    hitter.ReceiveChat(LanguageMgr.Get("MurderPlayer", Name), ChatType.System);
                    ReceiveChat(LanguageMgr.Get("MurderedByPlayer", LastHitter.Name), ChatType.System);

                    if (weapon != null && weapon.AddedStats[Stat.Luck] > (Settings.MaxLuck * -1) && Envir.Random.Next(4) == 0)
                    {
                        weapon.AddedStats[Stat.Luck]--;
                        hitter.ReceiveChat(LanguageMgr.Get("WeaponHasBeenCursed"), ChatType.System);
                        hitter.Enqueue(new S.RefreshItem { Item = weapon });
                    }
                }
            }

            UnSummonIntelligentCreature(SummonedCreatureType);

            if (HeroSpawned)
                DespawnHero();

            for (int i = Pets.Count - 1; i >= 0; i--)
            {
                if (Pets[i].Dead) continue;
                Pets[i].Die();
            }

            RemoveBuff(BuffType.MagicShield);
            RemoveBuff(BuffType.ElementalBarrier);

            if (PKPoints > 200)
                RedDeathDrop(LastHitter);
            else if (!InSafeZone)
                DeathDrop(LastHitter);

            HP = 0;
            Dead = true;

            LogTime = Envir.Time;
            BrownTime = Envir.Time;

            Enqueue(new S.Death { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            for (int i = 0; i < Buffs.Count; i++)
            {
                Buff buff = Buffs[i];

                if (!buff.Properties.HasFlag(BuffProperty.RemoveOnDeath)) continue;

                RemoveBuff(buff.Type);
            }

            PoisonList.Clear();
            InTrapRock = false;

            CallDefaultNPC(DefaultNPCType.Die);

            Report.Died(CurrentMap.Info.FileName);
        }
        private void RedDeathDrop(MapObject killer)
        {
            if (killer == null || killer.Race != ObjectType.Player)
            {
                for (var i = 0; i < Info.Equipment.Length; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null)
                        continue;

                    if (item.Info.Bind.HasFlag(BindMode.DontDeathdrop))
                        continue;

                    // TODO: Check this.
                    if ((item.WeddingRing != -1) && (Info.Equipment[(int)EquipmentSlot.RingL].UniqueID == item.UniqueID))
                        continue;

                    if (item.SealedInfo != null && item.SealedInfo.ExpiryDate > Envir.Now)
                        continue;

                    if (item.Info.Bind.HasFlag(BindMode.BreakOnDeath))
                    {
                        Info.Equipment[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        ReceiveChat(LanguageMgr.Get("ShatteredOnDeath", item.FriendlyName), ChatType.System);
                        Report.ItemChanged(item, item.Count, 1, "RedDeathDrop");
                    }

                    if (item.Count > 1)
                    {
                        var percent = Envir.RandomomRange(10, 4);
                        var count = (ushort)Math.Ceiling(item.Count / 10F * percent);

                        if (count > item.Count)
                            throw new ArgumentOutOfRangeException();

                        var temp2 = Envir.CreateFreshItem(item.Info);
                        temp2.Count = count;

                        if (!DropItem(temp2, Settings.DropRange, true))
                            continue;

                        if (count == item.Count)
                            Info.Equipment[i] = null;

                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = count });
                        item.Count -= count;

                        Report.ItemChanged(item, count, 1, "RedDeathDrop");
                    }
                    else if (Envir.Random.Next(10) == 0)
                    {
                        if (Envir.ReturnRentalItem(item, item.RentalInformation?.OwnerName, Info))
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                            ReceiveChat(LanguageMgr.Get("PlayerDeathItemReturn", item.Info.FriendlyName), ChatType.System);
                            Report.ItemMailed(item, 1, 1, "Death Dropped Rental Item");

                            continue;
                        }

                        if (!DropItem(item, Settings.DropRange, true))
                            continue;

                        if (item.Info.GlobalDropNotify)
                            foreach (var player in Envir.Players)
                            {
                                player.ReceiveChat(LanguageMgr.Get("PlayerDroppedItem", Name, item.FriendlyName), ChatType.System);
                            }

                        Info.Equipment[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                        Report.ItemChanged(item, item.Count, 1, "RedDeathDrop");
                    }
                }

            }

            for (var i = 0; i < Info.Inventory.Length; i++)
            {
                var item = Info.Inventory[i];

                if (item == null)
                    continue;

                if (item.Info.Bind.HasFlag(BindMode.DontDeathdrop))
                    continue;

                if (item.WeddingRing != -1)
                    continue;

                if (item.SealedInfo != null && item.SealedInfo.ExpiryDate > Envir.Now)
                    continue;

                if (Envir.ReturnRentalItem(item, item.RentalInformation?.OwnerName, Info))
                {
                    Info.Inventory[i] = null;
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                    ReceiveChat(LanguageMgr.Get("ItemReturnedToOwnerOnDeath", item.Info.FriendlyName), ChatType.System);
                    Report.ItemMailed(item, 1, 1, "Death Dropped Rental Item");

                    continue;
                }

                if (!DropItem(item, Settings.DropRange, true))
                    continue;

                if (item.Info.GlobalDropNotify)
                    foreach (var player in Envir.Players)
                    {
                        player.ReceiveChat(LanguageMgr.Get("PlayerItemDropped", Name, item.FriendlyName), ChatType.System);
                    }

                Info.Inventory[i] = null;
                Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });

                Report.ItemChanged(item, item.Count, 1, "RedDeathDrop");
            }

            RefreshStats();
        }
        // 本次是否用了 10 倍经验点（消耗 1 点 SExp）。ConsumeTenFoldExp 设置，GainExp 发包后清零。
        // 用标记而非固定增量：后续组队/情侣/师徒/装备加成还会叠加到经验上，
        // 必须在 GainExp 里按最终总额拆出"额外经验"（10 倍那 9/10），其余都算基础经验。
        private bool _tenFoldActive;

        // 组队经验倍率（下标=附近队友数-1），提到类级避免每次击杀新建数组产生 GC。
        private static readonly float[] PartyExpRate = { 1.0F, 1.3F, 1.4F, 1.5F, 1.6F, 1.7F, 1.8F, 1.9F, 2F, 2.1F, 2.2F };

        /// <summary>有 SExp 就消耗 1 点把本次经验 ×10，并标记本次为 10 倍经验（供 GainExp 拆出额外经验）。</summary>
        public uint ConsumeTenFoldExp(uint amount)
        {
            _tenFoldActive = false;
            if (Info.SExp == 0)
            {
                return amount;
            }

            Info.SExp--;
            _tenFoldActive = true;
            ulong scaled = (ulong)amount * 10UL;
            if (scaled > uint.MaxValue)
            {
                scaled = uint.MaxValue;
            }
            return (uint)scaled;
        }

        public override void WinExp(uint amount, uint targetLevel = 0)
        {
            if (CurrentMap?.Info?.NoExperience == true) return;

            int expPoint;
            uint originalAmount = amount;

            expPoint = ReduceExp(amount, targetLevel);
            expPoint = (int)(expPoint * Settings.ExpRate);

            // 10倍经验点（原始传奇 expMedal.actPoints）：有就消耗 1 点，本次击杀经验 ×10。
            // 消耗在击杀者身上，倍率作用于整份经验（组队时全队共享 10 倍收益）。
            expPoint = (int)ConsumeTenFoldExp((uint)expPoint);

            //party
            if (GroupMembers != null)
            {
                int sumLevel = 0;
                int nearCount = 0;
                for (int i = 0; i < GroupMembers.Count; i++)
                {
                    PlayerObject player = GroupMembers[i];

                    if (Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        sumLevel += player.Level;
                        nearCount++;
                    }
                }

                if (nearCount > PartyExpRate.Length) nearCount = PartyExpRate.Length;

                for (int i = 0; i < GroupMembers.Count; i++)
                {
                    PlayerObject player = GroupMembers[i];
                    if (player.CurrentMap == CurrentMap &&
                        Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    {
                        player.GainExp((uint)((float)expPoint * PartyExpRate[nearCount - 1] * (float)player.Level / (float)sumLevel));
                    }
                }
            }
            else
            {
                GainExp((uint)expPoint);
            }

            if (HeroSpawned && !Hero.Dead)
            {
                expPoint = Hero.ReduceExp(amount, targetLevel);
                expPoint = (int)(expPoint * Settings.ExpRate);
                Hero.GainExp((uint)expPoint);
            }
        }
        public override void GainExp(uint amount)
        {
            if (!CanGainExp) return;

            if (amount == 0) return;

            if (CurrentMap?.Info?.NoExperience == true) return;

            if (Info.Married != 0)
            {
                if (HasBuff(BuffType.Lover, out Buff buff))
                {
                    CharacterInfo lover = Envir.GetCharacterInfo(Info.Married);
                    PlayerObject loverPlayer = Envir.GetPlayer(lover.Name);
                    if (loverPlayer != null && loverPlayer.CurrentMap == CurrentMap && Functions.InRange(loverPlayer.CurrentLocation, CurrentLocation, Globals.DataRange) && !loverPlayer.Dead)
                    {
                        amount += (uint)Math.Max(0, (amount * Stats[Stat.LoverExpRatePercent]) / 100);
                    }
                }
            }

            if (Info.Mentor != 0 && !Info.IsMentor)
            {
                if (HasBuff(BuffType.Mentee, out _))
                {
                    CharacterInfo mentor = Envir.GetCharacterInfo(Info.Mentor);
                    PlayerObject mentorPlayer = Envir.GetPlayer(mentor.Name);
                    if (mentorPlayer != null && mentorPlayer.CurrentMap == CurrentMap && Functions.InRange(mentorPlayer.CurrentLocation, CurrentLocation, Globals.DataRange) && !mentorPlayer.Dead)
                    {
                        if (GroupMembers != null && GroupMembers.Contains(mentorPlayer))
                            amount += (uint)Math.Max(0, (amount * Stats[Stat.MentorExpRatePercent]) / 100);
                    }
                }
            }

            if (Stats[Stat.ExpRatePercent] > 0)
            {
                amount += (uint)Math.Max(0, (amount * Stats[Stat.ExpRatePercent]) / 100);
            }

            if (Info.Mentor != 0 && !Info.IsMentor)
            {
                MenteeEXP += (amount * Settings.MenteeExpBank) / 100;
            }

            Experience += amount;

            // 10 倍经验点：总额里 9/10 是 10 倍带来的额外经验，其余 1/10（含 ExpRate/组队/情侣等加成）算基础经验。
            // 按最终 amount 拆，避免后续加成被错误算进额外经验。读后清零，避免下次非 10 倍来源误带。
            uint baseExp = _tenFoldActive ? amount / 10 : amount;
            uint extraExp = _tenFoldActive ? amount - baseExp : 0;
            Enqueue(new S.GainExperience { Amount = baseExp, ExtraExp = extraExp });
            _tenFoldActive = false;

            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject monster = Pets[i];
                if (monster.CurrentMap == CurrentMap && Functions.InRange(monster.CurrentLocation, CurrentLocation, Globals.DataRange) && !monster.Dead)
                    monster.PetExp(amount);
            }

            if (MyGuild != null && MyGuild.Name != Settings.NewbieGuild)
                MyGuild.GainExp(amount);

            if (Experience < MaxExperience) return;
            if (Level >= ushort.MaxValue) return;

            //Calculate increased levels
            var experience = Experience;

            while (experience >= MaxExperience)
            {
                Level++;
                experience -= MaxExperience;

                RefreshLevelStats();

                if (Level >= ushort.MaxValue) break;
            }

            Experience = experience;

            LevelUp();

            if (IsGM) return;
            if ((LastRankUpdate + 3600 * 1000) > Envir.Time)
            {
                LastRankUpdate = Envir.Time;
                Envir.CheckRankUpdate(Info);
            }
        }
        public override void LevelUp()
        {
            CallDefaultNPC(DefaultNPCType.LevelUp);

            base.LevelUp();

            // 原始传奇技能：无需书的技能到 level 自动置 1；需书技能仍由技能书激活。
            LearnOriginSkills(true);

            // 升级时由任务调度器接取因等级达成而新开放的任务（等级任务）。
            QuestScheduler.CheckLevelUp(Level);

            CheckSkillGoalQuests(); // 等级到了 + 已学会对应技能 → 自动完成"学习技能"支线

            Enqueue(new S.LevelChanged { Level = Level, Experience = Experience, MaxExperience = MaxExperience });

            if (Info.Mentor != 0 && !Info.IsMentor)
            {
                CharacterInfo Mentor = Envir.GetCharacterInfo(Info.Mentor);
                if ((Mentor != null) && ((Info.Level + Settings.MentorLevelGap) > Mentor.Level))
                    MentorBreak();
            }

            for (int i = CurrentMap.NPCs.Count - 1; i >= 0; i--)
            {
                if (Functions.InRange(CurrentMap.NPCs[i].CurrentLocation, CurrentLocation, Globals.DataRange))
                    CurrentMap.NPCs[i].CheckVisible(this);
            }
            Report.Levelled(Level);

            if (IsGM) return;
            Envir.CheckRankUpdate(Info);
        }
        private void AddQuestItem(UserItem item)
        {
            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < Info.QuestInventory.Length; i++)
                {
                    UserItem temp = Info.QuestInventory[i];
                    if (temp == null || item.Info != temp.Info || temp.Count >= temp.Info.StackSize) continue;

                    if (item.Count + temp.Count <= temp.Info.StackSize)
                    {
                        temp.Count += item.Count;
                        return;
                    }
                    item.Count -= (ushort)(temp.Info.StackSize - temp.Count);
                    temp.Count = (ushort)Math.Min(temp.Info.StackSize, ushort.MaxValue);
                }
            }

            for (int i = 0; i < Info.QuestInventory.Length; i++)
            {
                if (Info.QuestInventory[i] != null) continue;
                Info.QuestInventory[i] = item;

                return;
            }
        }
        public void CheckQuestInfo(QuestInfoRunItem info)
        {
            if (Connection.SentQuestInfo.Contains(info)) return;
            Enqueue(new S.NewQuestInfo { Info = info.CreateClientQuestInfo(this) });
            Connection.SentQuestInfo.Add(info);
        }
        public void CheckRecipeInfo(RecipeInfo info)
        {
            if (Connection.SentRecipeInfo.Contains(info)) return;

            CheckItemInfo(info.Item.Info);

            foreach (var tool in info.Tools)
            {
                CheckItemInfo(tool.Info);
            }

            foreach (var ingredient in info.Ingredients)
            {
                CheckItemInfo(ingredient.Info);
            }

            Enqueue(new S.NewRecipeInfo { Info = info.CreateClientRecipeInfo() });
            Connection.SentRecipeInfo.Add(info);
        }
        public void CheckMapInfo(MapInfoTable mapInfo)
        {
            if (!Connection.WorldMapSetupSent)
            {
                Enqueue(new S.WorldMapSetupInfo { Setup = Settings.WorldMapSetup, TeleportToNPCCost = Settings.TeleportToNPCCost });
                Connection.WorldMapSetupSent = true;
            }

            if (Connection.SentMapInfo.Contains(mapInfo)) return;

            var map = Envir.GetMap(mapInfo.Index);
            if (map == null) return;

            var info = new ClientMapInfo()
            {
                Width = map.Width,
                Height = map.Height,
                BigMap = mapInfo.BigMap,
                Title = mapInfo.Title
            };

            foreach (MovementInfo mInfo in mapInfo.Movements.Where(x => x.ShowOnBigMap))
            {
                Map destMap = Envir.GetMap(mInfo.MapIndex);
                if (destMap is null)
                    continue;
                var cmInfo = new ClientMovementInfo()
                {
                    Destination = mInfo.MapIndex,
                    Location = mInfo.Source,
                    Icon = mInfo.Icon
                };

                cmInfo.Title = destMap.Info.Title;

                info.Movements.Add(cmInfo);
            }

            foreach (NPCObject npc in Envir.NPCs.Where(x => x.CurrentMap == map && x.Info.ShowOnBigMap).OrderBy(x => x.Info.BigMapIcon))
            {
                ClientNPCInfo clientInfo = npc.Info.ClientInformation;
                clientInfo.ObjectID = npc.ObjectID;
                info.NPCs.Add(clientInfo);
            }

            Enqueue(new S.NewMapInfo { MapIndex = mapInfo.Index, Info = info });
            Connection.SentMapInfo.Add(mapInfo);
        }
        private void SetBind()
        {
            SafeZoneInfo szi = null;

            MapInfoTable home = Envir.MapInfoList.FirstOrDefault(m => m.Index == Settings.HomeMapIndex);
            if (home != null)
            {
                // 只在该地图标记了 StartPoint 的安全区里按配置序号挑，不写死坐标。
                // 比奇省(103)当前有两个：0=比奇城(328,264)，1=新手村(291,619)。
                List<SafeZoneInfo> startPoints = home.SafeZones.Where(z => z.StartPoint).ToList();
                if (startPoints.Count > 0)
                {
                    int index = Math.Max(0, Math.Min(Settings.BirthStartPointIndex, startPoints.Count - 1));
                    szi = startPoints[index];
                }
            }

            // 兜底：配置地图无起始点时，取全局第一个可用起始点（确定性，不再随机）
            if (szi == null && Envir.StartPoints.Count > 0)
            {
                szi = Envir.StartPoints[0];
            }

            if (szi == null) return;

            BindMapIndex = szi.Info.Index;
            BindLocation = szi.Location;
        }
        protected override void SetBindSafeZone(SafeZoneInfo szi)
        {
            // 仅"城镇/出生点"安全区(StartPoint)才更新绑定城镇；普通安全区(如矿区入口)只提供庇护，不覆盖回城点
            if (szi.StartPoint)
            {
                BindLocation = szi.Location;
                BindMapIndex = CurrentMapIndex;
            }
        }
    }
}
