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
        public void StartGame()
        {
            Map temp = Envir.GetMap(CurrentMapIndex);

            if (temp != null && temp.Info.NoReconnect)
            {
                Map temp1 = Envir.GetMapByNameAndInstance(temp.Info.NoReconnectMap);
                if (temp1 != null)
                {
                    temp = temp1;
                    CurrentMapIndex = temp.Info.Index;
                    CurrentLocation = GetRandomPoint(40, 0, temp);
                }
            }

            if (temp == null || !temp.ValidPoint(CurrentLocation))
            {
                temp = Envir.GetMap(BindMapIndex);

                if (temp == null || !temp.ValidPoint(BindLocation))
                {
                    SetBind();
                    temp = Envir.GetMap(BindMapIndex);

                    if (temp == null || !temp.ValidPoint(BindLocation))
                    {
                        // [出生诊断] 精确打印失败原因，定位是"地图实例未加载"还是"出生格被判成墙/越界"。
                        MessageQueue.Enqueue($"[出生诊断] BindMapIndex={BindMapIndex}, BindLocation={BindLocation}, mapNull={(temp == null)}");
                        if (temp != null)
                        {
                            bool inBounds = BindLocation.X >= 0 && BindLocation.X < temp.Width
                                && BindLocation.Y >= 0 && BindLocation.Y < temp.Height;
                            MessageQueue.Enqueue($"[出生诊断] map WxH={temp.Width}x{temp.Height}, inBounds={inBounds}");
                            if (inBounds)
                            {
                                var cell = temp.GetCell(BindLocation.X, BindLocation.Y);
                                MessageQueue.Enqueue($"[出生诊断] cell.Attribute={cell?.Attribute} (Walk=可走, HighWall=墙)");
                                // 顺带打印周围 3x3，看出生点是不是被墙包住（可判断解码位语义是否反了）。
                                for (int dy = -1; dy <= 1; dy++)
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        int cx = BindLocation.X + dx, cy = BindLocation.Y + dy;
                                        if (cx < 0 || cy < 0 || cx >= temp.Width || cy >= temp.Height) continue;
                                        var c = temp.GetCell(cx, cy);
                                        MessageQueue.Enqueue($"[出生诊断]   周围({cx},{cy})={c?.Attribute}");
                                    }
                            }
                        }
                        StartGameFailed();
                        return;
                    }
                }
                CurrentMapIndex = BindMapIndex;
                CurrentLocation = BindLocation;
            }
            CurrentMapIndex = temp.Info.Index;
            temp.AddObject(this);
            CurrentMap = temp;
            Envir.Players.Add(this);

            StartGameSuccess();

            if (!IsGM && CurrentMap?.Info?.RequiredGroup == true)
            {
                int required = Math.Max(2, CurrentMap.Info.RequiredGroupSize);
                int have = GroupMembers?.Count ?? 0;

                if (have < required)
                {
                    ForceLeaveGroupRequiredMap();
                }
            }

            //Call Login NPC
            CallDefaultNPC(DefaultNPCType.Login);

            //Call Daily NPC
            if (Info.NewDay)
            {
                CallDefaultNPC(DefaultNPCType.Daily);
            }
        }
        private void StartGameSuccess()
        {
            Connection.Stage = GameStage.Game;

            Enqueue(new S.StartGame { Result = 4, Resolution = Settings.AllowedResolution });
            ReceiveChat(LanguageMgr.Get("Welcome", LanguageMgr.Get("GameName")), ChatType.System);

            if (Settings.TestServer)
            {
                ReceiveChat(LanguageMgr.Get("GameIsTestMode"), ChatType.System);
                Chat("@GAMEMASTER");
            }

            for (int i = 0; i < Info.Magics.Count; i++)
            {
                var magic = Info.Magics[i];
                magic.CastTime += Envir.Time;

                if (magic.CastTime + magic.GetDelay() < Envir.Time)
                {
                    magic.CastTime = int.MinValue;
                }
            }

            if (Info.GuildIndex != -1)
            {
                if (MyGuild == null)
                {
                    Info.GuildIndex = -1;
                    ReceiveChat(LanguageMgr.Get("RemoveGuild"), ChatType.System);
                }
                else
                {
                    MyGuildRank = MyGuild.FindRank(Info.Name);
                    if (MyGuildRank == null)
                    {
                        MyGuild = null;
                        Info.GuildIndex = -1;
                        ReceiveChat(LanguageMgr.Get("RemoveGuild"), ChatType.System);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(Settings.Notice.Message) && Settings.Notice.LastUpdate > Info.LastLogoutDate)
            {
                Enqueue(new S.UpdateNotice { Notice = Settings.Notice });
            }

            SetLevelEffects();

            GetItemInfo(Connection);
            GetMapInfo(Connection);
            GetUserInfo(Connection);

            Spawned();

            GetQuestInfo();
            GetRecipeInfo();

            GetCompletedQuests();

            GetMail();
            GetFriends();
            GetRelationship();

            if (Info.Mentor != 0 && Info.MentorDate.AddDays(Settings.MentorLength) < Envir.Now)
            {
                MentorBreak();
            }
            else
            {
                GetMentor();
            }

            CheckConquest();

            GetGameShop();

            for (int i = 0; i < CurrentQuests.Count; i++)
            {
                var quest = CurrentQuests[i];
                quest.Init(this);
                bool tracked = quest.Info != null && TrackedQuests.Contains(quest.Info.Index);
                SendUpdateQuest(quest, QuestState.Add, tracked);
            }

            SendBaseStats();
            GetObjectsPassive();
            Enqueue(new S.TimeOfDay { Lights = Envir.Lights });
            Enqueue(new S.ChangeAMode { Mode = AMode });
            Enqueue(new S.ChangePMode { Mode = PMode });
            Enqueue(new S.SwitchGroup { AllowGroup = AllowGroup });

            Enqueue(new S.DefaultNPC { ObjectID = Envir.DefaultNPC.LoadedObjectID });

            Enqueue(new S.GuildBuffList() { GuildBuffs = Settings.Guild_BuffList });
            RequestedGuildBuffInfo = true;

            if (Info.Thrusting) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.刺杀剑术, CanUse = true });
            if (Info.HalfMoon) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.半月弯刀, CanUse = true });
            if (Info.CrossHalfMoon) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.双半月, CanUse = true });
            if (Info.DoubleSlash) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.双斩, CanUse = true });

            // --- Re-spawn saved pets ---
            for (int i = 0; i < Info.Pets.Count; i++)
            {
                MonsterObject monster;

                PetInfo info = Info.Pets[i];

                var monsterInfo = Envir.GetMonsterInfo(info.MonsterIndex);
                if (monsterInfo == null) continue;

                monster = MonsterObject.GetMonster(monsterInfo);
                if (monster == null) continue;

                monster.PetLevel = info.Level;
                monster.MaxPetLevel = info.MaxPetLevel;
                monster.PetExperience = info.Experience;
                monster.Master = this;

                switch (Settings.PetSave)
                {
                    case true when Settings.PetSave is true:
                        if (monster.Info.Name == Settings.CloneName)
                        {
                            monster.ActionTime = Envir.Time + 1000;
                            monster.RefreshNameColour(false);
                        }
                        break;

                    case false when Settings.PetSave is false:
                        switch (Class)
                        {
                            case (MirClass.Wizard):
                                if (monster.Info.Name == Settings.CloneName)
                                {
                                    monster.ActionTime = Envir.Time + 1000;
                                    monster.RefreshNameColour(false);
                                }
                                else
                                {
                                    monster.TameTime = Envir.Time + info.TameTime;
                                }
                                break;
                        }
                        break;
                }

                // [grimchamp] leave refresh here incase future code sets levels or stats in above switch
                monster.RefreshAll();

                Pets.Add(monster);

                if (!monster.Spawn(CurrentMap, Back))
                {
                    monster.Spawn(CurrentMap, CurrentLocation);
                }

                monster.SetHP(info.HP);
            }

            Info.Pets.Clear();

            // Restore buffs
            for (int i = 0; i < Buffs.Count; i++)
            {
                var buff = Buffs[i];
                buff.LastTime = Envir.Time;
                buff.ObjectID = ObjectID;

                AddBuff(buff.Type, null, (int)buff.ExpireTime, buff.Stats, true, true, buff.Values);
            }

            // Restore poisons
            for (int i = 0; i < PoisonList.Count; i++)
            {
                var poison = PoisonList[i];
                poison.TickTime = Envir.Time;
            }

            if (MyGuild != null)
            {
                MyGuild.PlayerLogged(this, true);
                if (MyGuild.BuffList.Count > 0)
                {
                    Enqueue(new S.GuildBuffList() { ActiveBuffs = MyGuild.BuffList });
                }
            }

            if (HasHero && Info.HeroSpawned)
                SummonHero();

            // **** NEW: apply map entry rules on login (NoPets/NoGroup/NoHero + MapEnter + party UI) ****
            // This ensures pets unfreeze if the current map allows pets, disbands on NoGroup, and despawns hero on NoHero.
            ApplyMapEntryRules(true);

            if (InSafeZone && Info.LastLogoutDate > DateTime.MinValue)
            {
                double totalMinutes = (Envir.Now - Info.LastLogoutDate).TotalMinutes;
                _restedCounter = (int)(totalMinutes * 60);
            }

            if (Info.Mail.Count > Settings.MailCapacity)
            {
                ReceiveChat(LanguageMgr.Get("MailOverflowing"), ChatType.System);
            }

            Report.Connected(Connection.IPAddress);

            MessageQueue.Enqueue(string.Format("{0} has connected.", Info.Name));

            if (IsGM)
            {
                UpdateGMBuff();
            }
            else
            {
                LastRankUpdate = Envir.Time;
                Envir.CheckRankUpdate(Info);
                Envir.OnlineRankingCount[0]++;
                Envir.OnlineRankingCount[(int)Class + 1]++;
            }
        }

        private void StartGameFailed()
        {
            Enqueue(new S.StartGame { Result = 3 });
            CleanUp();
        }
        public void GiveRestedBonus(int count)
        {
            if (count > 0)
            {
                long existingTime = 0;

                if (HasBuff(BuffType.Rested, out Buff rested))
                {
                    existingTime = rested.ExpireTime;
                }

                int duration = (int)Math.Min(int.MaxValue, ((Settings.RestedBuffLength * Settings.Minute) * count) + existingTime);
                int maxDuration = (Settings.RestedBuffLength * Settings.Minute) * Settings.RestedMaxBonus;

                if (duration > maxDuration) duration = maxDuration;

                AddBuff(BuffType.Rested, this, duration, new Stats { [Stat.ExpRatePercent] = Settings.RestedExpBonus });

                _restedCounter = 0;
            }
        }
        public override void Revive(int hp, bool effect)
        {
            if (!Dead) return;

            base.Revive(hp, effect);

            GetObjects();

            Fishing = false;
            Enqueue(GetFishInfo());
            GroupMemberMapNameChanged();
            GetPlayerLocation();
        }
        public void TownRevive()
        {
            if (!Dead) return;

            // 若绑定地图并非真正的"城镇/出生点"（如被误标为 StartPoint 的矿区或数据异常），
            // 重置到合法出生点，避免死亡后原地复活循环。
            bool bindIsTown = false;
            foreach (SafeZoneInfo sp in Envir.StartPoints) { if (sp.Info.Index == BindMapIndex) { bindIsTown = true; break; } }
            if (!bindIsTown) SetBind();

            Map temp = Envir.GetMap(BindMapIndex);
            Point bindLocation = BindLocation;

            if (Info.PKPoints >= 200)
            {
                temp = Envir.GetMapByNameAndInstance(Settings.PKTownMapName, 1);
                bindLocation = new Point(Settings.PKTownPositionX, Settings.PKTownPositionY);

                if (temp == null)
                {
                    temp = Envir.GetMap(BindMapIndex);
                    bindLocation = BindLocation;
                }
            }

            if (temp == null || !temp.ValidPoint(bindLocation)) return;

            Dead = false;
            SetHP(Stats[Stat.HP]);
            SetMP(Stats[Stat.MP]);
            RefreshStats();

            CurrentMap.RemoveObject(this);
            Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

            CurrentMap = temp;
            CurrentLocation = bindLocation;

            CurrentMap.AddObject(this);

            Enqueue(new S.MapChanged
            {
                MapIndex = CurrentMap.Info.Index,
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                Weather = CurrentMap.Info.WeatherParticles,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight,
                Music = CurrentMap.Info.Music
            });

            GetObjects();
            Enqueue(new S.Revived());
            Broadcast(new S.ObjectRevived { ObjectID = ObjectID, Effect = true });

            InSafeZone = true;
            Fishing = false;
            Enqueue(GetFishInfo());
            GroupMemberMapNameChanged();
            GetPlayerLocation();
        }
        public override bool Teleport(Map temp, Point location, bool effects = true, byte effectnumber = 0)
        {
            Map oldMap = CurrentMap;
            Point oldLocation = CurrentLocation;
            bool mapChanged = temp != oldMap;

            // RequiredGroup PRE-GATE (deny before leaving source) ---
            if (temp.Info.RequiredGroup && !IsGM)
            {
                int required = Math.Max(2, temp.Info.RequiredGroupSize);
                int have = GroupMembers?.Count ?? 0;
                if (have < required)
                {
                    ReceiveChat(LanguageMgr.Get("MapGroupRequirement", required), ChatType.System);
                    return false;
                }
            }

            if (!base.Teleport(temp, location, effects)) return false;

            if (!temp.Info.RequiredGroup)
            {
                LastValidMap = temp;
                LastValidLocation = location;
            }

            if (TradePartner != null) TradeCancel();
            if (ItemRentalPartner != null) CancelItemRental();

            GetObjectsPassive();
            CheckConquest();

            Fishing = false;
            Enqueue(GetFishInfo());

            if (mapChanged)
            {
                ApplyMapEntryRules(mapChanged);
            }

            GetPlayerLocation();


            if (MapHasGroupRequirement(CurrentMap) && !IsValidForGroupRequiredMap())
            {
                ForceLeaveGroupRequiredMap();
            }

            return true;
        }
        // Run after a successful map change (movement or teleport)
        private void ApplyMapEntryRules(bool mapChanged)
        {
            if (!mapChanged) return;

            // MapEnter NPC hook
            CallDefaultNPC(DefaultNPCType.MapEnter, CurrentMap.Info.FileName);

            // NoGroup: solo-only maps
            if (CurrentMap.Info.NoGroup && GroupMembers != null)
            {
                DisbandGroup(LanguageMgr.Get("GroupingDisabledOnMap"));
            }

            // NoPets: freeze combat pets while allowing pickup creatures to keep working
            if (CurrentMap.Info.NoPets)
            {
                bool restrictedPetFound = false;

                foreach (var pet in Pets)
                {
                    if (!PetAffectedByNoPetRule(pet)) continue;

                    pet.Target = null;
                    pet.Frozen = true;
                    pet.PMode = PetMode.None;

                    // small visual nudge
                    pet.Broadcast(new S.ObjectTurn { Direction = pet.Direction, Location = pet.CurrentLocation });
                    restrictedPetFound = true;
                }

                if (restrictedPetFound)
                    ReceiveChat(LanguageMgr.Get("PetsNotAllowedOnMap"), ChatType.System);
            }

            else
            {
                foreach (var pet in Pets)
                {
                    if (!PetAffectedByNoPetRule(pet)) continue;

                    pet.Frozen = false;
                    pet.PMode = PetMode.Both;
                    pet.BroadcastInfo();
                }
            }

            // NoIntelligentCreatures: unsummon pickup pets
            if (CurrentMap.Info.NoIntelligentCreatures && CreatureSummoned && SummonedCreatureType != IntelligentCreatureType.None)
            {
                IntelligentCreatureType dismissedType = SummonedCreatureType;
                UnSummonIntelligentCreature(dismissedType);
                ReceiveChat(LanguageMgr.Get("IntelligentCreaturesNotAllowedOnMap"), ChatType.System);
            }

            // NoHero: despawn on entry
            if (CurrentMap.Info.NoHero && Hero != null)
            {
                DespawnHero();
                ReceiveChat(LanguageMgr.Get("HeroesNotAllowedOnMap"), ChatType.System);

                if (Hero != null && Envir.Heroes.Contains(Hero))
                    Envir.Heroes.Remove(Hero);
            }

            // Party UI refresh
            GroupMemberMapNameChanged();
        }

        private static bool PetAffectedByNoPetRule(MonsterObject pet)
        {
            return pet != null && !pet.Dead && !pet.IgnoresNoPetRestriction;
        }

        static readonly ServerPacketIds[] BroadcastObservePackets = new ServerPacketIds[]
        {
            ServerPacketIds.ObjectTurn,
            ServerPacketIds.ObjectWalk,
            ServerPacketIds.ObjectRun,
            ServerPacketIds.ObjectAttack,
            ServerPacketIds.ObjectRangeAttack,
            ServerPacketIds.ObjectMagic,
            ServerPacketIds.ObjectHarvest
        };

        public override void Broadcast(Packet p)
        {
            if (p == null || CurrentMap == null) return;

            base.Broadcast(p);

            if (Array.Exists(BroadcastObservePackets, x => x == (ServerPacketIds)p.Index))
            {
                foreach (MirConnection c in Connection.Observers)
                    c.Enqueue(p);
            }
        }

        public void AddObserver(MirConnection observer)
        {
            if (observer == Connection) return;

            Connection.AddObserver(observer);
            GetItemInfo(observer);
            GetMapInfo(observer);
            GetUserInfo(observer);
            GetObjectsPassive(observer);

            if (Info.Thrusting) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.刺杀剑术, CanUse = true }, observer);
            if (Info.HalfMoon) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.半月弯刀, CanUse = true }, observer);
            if (Info.CrossHalfMoon) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.双半月, CanUse = true }, observer);
            if (Info.DoubleSlash) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.双斩, CanUse = true }, observer);
            if (Slaying) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.攻杀剑术, CanUse = true }, observer);
            if (FlamingSword) Enqueue(new S.SpellToggle { ObjectID = ObjectID, Spell = Spell.烈火剑法, CanUse = true }, observer);

            if (observer.Player != null)
                observer.Player.StopGame(24);
        }

        protected virtual void GetItemInfo(MirConnection c)
        {
            UserItem item;
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null) continue;

                c.CheckItem(item);
            }

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                item = Info.Equipment[i];

                if (item == null) continue;

                c.CheckItem(item);
            }

            for (int i = 0; i < Info.QuestInventory.Length; i++)
            {
                item = Info.QuestInventory[i];

                if (item == null) continue;
                c.CheckItem(item);
            }
        }
        public void GetUserInfo(MirConnection c)//was private luke
        {
            string guildname = MyGuild != null ? MyGuild.Name : "";
            string guildrank = MyGuild != null ? MyGuildRank.Name : "";
            S.UserInformation packet = new S.UserInformation
            {
                ObjectID = ObjectID,
                RealId = (uint)Info.Index,
                Name = Name,
                GuildName = guildname,
                GuildRank = guildrank,
                NameColour = GetNameColour(this),
                Class = Class,
                Gender = Gender,
                Level = Level,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = Hair,
                HP = HP,
                MP = MP,

                Experience = Experience,
                MaxExperience = MaxExperience,

                LevelEffects = LevelEffects,

                HasHero = HasHero,
                HeroBehaviour = Info.HeroBehaviour,

                Inventory = new UserItem[Info.Inventory.Length],
                Equipment = new UserItem[Info.Equipment.Length],
                QuestInventory = new UserItem[Info.QuestInventory.Length],
                Gold = Info.Gold,
                Credit = Info.Credit,
                LegendCoin = Info.LegendCoin,
                SExp = Info.SExp,
                HasExpandedStorage = Account.ExpandedStorageExpiryDate > Envir.Now ? true : false,
                HasStoragePassword = Account.HasStoragePassword,
                RequireStoragePassword = Settings.RequireStoragePassword,
                StoragePasswordLastSet = Account.StoragePasswordLastSet,
                ExpandedStorageExpiryTime = Account.ExpandedStorageExpiryDate,
                AllowObserve = AllowObserve,
                Observer = c != Connection,
            };

            //Copy this method to prevent modification before sending packet information.
            for (int i = 0; i < Info.Magics.Count; i++)
                packet.Magics.Add(Info.Magics[i].CreateClientMagic());

            Info.Inventory.CopyTo(packet.Inventory, 0);
            Info.Equipment.CopyTo(packet.Equipment, 0);
            Info.QuestInventory.CopyTo(packet.QuestInventory, 0);

            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
            {
                packet.IntelligentCreatures.Add(Info.IntelligentCreatures[i].CreateClientIntelligentCreature());
            }

            packet.SummonedCreatureType = SummonedCreatureType;
            packet.CreatureSummoned = CreatureSummoned;

            Enqueue(packet, c);
        }
        private void GetMapInfo(MirConnection c)
        {
            Enqueue(new S.MapInformation
            {
                MapIndex = CurrentMap.Info.Index,
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                MiniMap = CurrentMap.Info.MiniMap,
                Lights = CurrentMap.Info.Light,
                BigMap = CurrentMap.Info.BigMap,
                WeatherParticles = CurrentMap.Info.WeatherParticles,
                Lightning = CurrentMap.Info.Lightning,
                Fire = CurrentMap.Info.Fire,
                MapDarkLight = CurrentMap.Info.MapDarkLight,
                Music = CurrentMap.Info.Music,
            }, c);
        }
        private void GetQuestInfo()
        {
            for (int i = 0; i < Envir.QuestInfoList.Count; i++)
            {
                CheckQuestInfo(Envir.QuestInfoList[i]);
            }

            // 登录时由任务调度器接取已达等级的等级任务（如 10 级"前往比奇城"）。
            QuestScheduler.CheckLevelUp(Level);
        }
        private void GetRecipeInfo()
        {
            for (int i = 0; i < Envir.RecipeInfoList.Count; i++)
            {
                CheckRecipeInfo(Envir.RecipeInfoList[i]);
            }
        }
        private void GetObjects()
        {
            for (int y = CurrentLocation.Y - Globals.DataRange; y <= CurrentLocation.Y + Globals.DataRange; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - Globals.DataRange; x <= CurrentLocation.X + Globals.DataRange; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;
                    if (x < 0 || x >= CurrentMap.Width) continue;

                    Cell cell = CurrentMap.GetCell(x, y);

                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];

                        //if (ob.Race == ObjectType.Player && ob.Observer) continue;

                        ob.Add(this);
                    }
                }
            }
        }
        private void GetObjectsPassive(MirConnection c = null)
        {
            for (int y = CurrentLocation.Y - Globals.DataRange; y <= CurrentLocation.Y + Globals.DataRange; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - Globals.DataRange; x <= CurrentLocation.X + Globals.DataRange; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;
                    if (x < 0 || x >= CurrentMap.Width) continue;

                    Cell cell = CurrentMap.GetCell(x, y);

                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (ob == this) continue;

                        if (ob.Race == ObjectType.Player)
                        {
                            PlayerObject Player = (PlayerObject)ob;
                            Enqueue(Player.GetInfoEx(this), c);
                        }
                        else if (ob.Race == ObjectType.Spell)
                        {
                            SpellObject obSpell = (SpellObject)ob;

                            if ((obSpell.Spell != Spell.爆炸陷阱) || (obSpell.Caster != null && IsFriendlyTarget(obSpell.Caster)))
                                Enqueue(ob.GetInfo(), c);
                        }
                        else if (ob.Race == ObjectType.Merchant)
                        {
                            NPCObject NPC = (NPCObject)ob;

                            NPC.CheckVisible(this);

                            if (NPC.VisibleLog[Info.Index] && NPC.Visible) Enqueue(ob.GetInfo(), c);
                        }
                        else
                        {
                            Enqueue(ob.GetInfo(), c);
                        }

                        if (ob.Race == ObjectType.Player || ob.Race == ObjectType.Monster || ob.Race == ObjectType.Hero)
                        {
                            ob.SendHealth(this);
                        }
                    }
                }
            }
        }
        public override void RefreshGuildBuffs()
        {
            if (MyGuild == null) return;
            if (MyGuild.BuffList.Count == 0) return;

            for (int i = 0; i < MyGuild.BuffList.Count; i++)
            {
                GuildBuff buff = MyGuild.BuffList[i];
                if ((buff.Info == null) || (!buff.Active)) continue;

                Stats.Add(buff.Info.Stats);
            }
        }

        public override void RefreshNameColour()
        {
            var prevColor = NameColour;
            NameColour = GetNameColour(this);

            if (prevColor == NameColour) return;

            Enqueue(new S.ColourChanged { NameColour = NameColour });
            BroadcastColourChange();
        }

        public override Color GetNameColour(HumanObject human)
        {
            if (human == null) return NameColour;

            if (human is PlayerObject player)
            {
                if (player.PKPoints >= 200)
                    return Color.Red;

                if (Envir.Time < player.BrownTime)
                    return Color.SaddleBrown;

                if (player.WarZone)
                {
                    if (player.MyGuild == null)
                        return Color.Green;

                    if (player.MyGuild == MyGuild)
                        return Color.Green;
                    else
                        return Color.Orange;
                }

                if (MyGuild != null)
                {
                    if (MyGuild.IsAtWar())
                    {
                        if (player.MyGuild != null)
                        {
                            if (player.MyGuild == MyGuild)
                                return Color.Blue;
                            if (MyGuild.IsEnemy(player.MyGuild))
                                return Color.Orange;
                        }
                    }
                }

                if (player.PKPoints >= 100)
                    return Color.Yellow;
            }

            return Color.White;
        }
    }
}
