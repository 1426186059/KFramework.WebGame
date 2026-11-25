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
        #region Heroes
        public void NewHero(C.NewHero p)
        {
            if (!Envir.CanCreateHero(p, Connection, IsGM))
                return;

            int heroCount = Info.Heroes.Count(x => x != null);
            if (heroCount >= Info.MaximumHeroCount)
            {
                Enqueue(new S.NewHero { Result = 4 });
                return;
            }

            bool passedItemCheck = true;
            ItemInfoTable itemInfo = Envir.GetItemInfo(Settings.HeroSealItemName);
            if (itemInfo != null && FreeSpace(Info.Inventory) == 0)
                passedItemCheck = false;

            if (!passedItemCheck)
            {
                Enqueue(new S.NewHero { Result = 6 });
                return;
            }

            var info = new HeroInfo(p) { Index = ++Envir.NextHeroID };
            Envir.HeroList.Add(info);

            if (itemInfo != null)
            {
                UserItem item = Envir.CreateFreshItem(itemInfo);
                item.AddedStats[Stat.Hero] = info.Index;
                GainItem(item);
            }
            else
                AddHero(info);

            Enqueue(new S.NewHero { Result = 10 });
        }
        public override void RefreshMaxExperience()
        {
            MaxExperience = Level < Settings.ExperienceList.Count ? Settings.ExperienceList[Level - 1] : 0;
        }
        public HeroObject GetHero()
        {
            if (HasHero && HeroSpawned)
                return Hero;

            return null;
        }

        public void SetAutoPotValue(Stat stat, uint value)
        {
            if (!HeroSpawned || !Hero.AutoPot) return;

            if (stat == Stat.HP)
                Hero.AutoHPPercent = (byte)Math.Min(99, value);
            else
                Hero.AutoMPPercent = (byte)Math.Min(99, value);

            Enqueue(new S.SetAutoPotValue() { Stat = stat, Value = value });
        }

        public void SetAutoPotItem(MirGridType Grid, int ItemIndex)
        {
            if (!HeroSpawned || !Hero.AutoPot) return;

            if (Envir.GetItemInfo(ItemIndex) == null)
                ItemIndex = 0;

            if (Grid == MirGridType.HeroHPItem)
                Hero.HPItemIndex = ItemIndex;
            else
                Hero.MPItemIndex = ItemIndex;
            Enqueue(new S.SetAutoPotItem() { Grid = Grid, ItemIndex = ItemIndex });
        }

        public void SetHeroBehaviour(HeroBehaviour behaviour)
        {
            if (!HeroSpawned) return;
            if (Info.HeroBehaviour == behaviour) return;

            Info.HeroBehaviour = behaviour;
            Enqueue(new S.SetHeroBehaviour() { Behaviour = behaviour });

            Hero.Target = null;
            Hero.SearchTime = 0;
        }

        public void ChangeHero(int index)
        {
            if (Info.Heroes.Length <= index) return;
            bool respawn = Info.HeroSpawned;

            if (Hero != null)
            {
                DespawnHero();
                Info.HeroSpawned = false;
                Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.None });
            }

            HeroInfo temp = Info.Heroes[index];
            Info.Heroes[index] = Info.Heroes[0];
            Info.Heroes[0] = temp;
            CurrentHero = Info.Heroes[0];

            Enqueue(new S.ChangeHero() { FromIndex = index - 1 });

            if (Info.Heroes[0] == null || !respawn)
            {
                Enqueue(new S.UpdateHeroSpawnState { State = HeroSpawnState.Unsummoned });
                return;
            }

            SummonHero();
        }
        #endregion

        #region Guilds

        public bool CreateGuild(string guildName)
        {
            if ((MyGuild != null) || (Info.GuildIndex != -1)) return false;
            if (Envir.GetGuild(guildName) != null) return false;

            if (Info.Level < Settings.Guild_RequiredLevel)
            {
                ReceiveChat(LanguageMgr.Get("LevelNotEnoughCreateGuild", Settings.Guild_RequiredLevel), ChatType.System);
                return false;
            }

            if (!Info.AccountInfo.AdminAccount && String.Equals(guildName, Settings.NewbieGuild, StringComparison.OrdinalIgnoreCase))
            {
                ReceiveChat(LanguageMgr.Get("CannotCreateNewbieGuild"), ChatType.System);
                return false;
            }

            if (!Info.AccountInfo.AdminAccount)
            {
                //check if we have the required items
                for (int i = 0; i < Settings.Guild_CreationCostList.Count; i++)
                {
                    GuildItemVolume Required = Settings.Guild_CreationCostList[i];
                    if (Required.Item == null)
                    {
                        if (Info.Gold < Required.Amount)
                        {
                            ReceiveChat(LanguageMgr.Get("InsufficientGoldCreateGuild", Required.Amount), ChatType.System);
                            return false;
                        }
                    }
                    else
                    {
                        ushort count = (ushort)Math.Min(Required.Amount, ushort.MaxValue);

                        foreach (var item in Info.Inventory.Where(item => item != null && item.Info == Required.Item))
                        {
                            if ((Required.Item.Type == ItemType.Ore) && (item.CurrentDura / 1000 > Required.Amount))
                            {
                                count = 0;
                                break;
                            }
                            if (item.Count > count)
                                count = 0;
                            else
                                count = (ushort)(count - item.Count);
                            if (count == 0) break;
                        }
                        if (count != 0)
                        {
                            if (Required.Amount == 1)
                                ReceiveChat(LanguageMgr.Get("RequiredToCreateGuild", Required.Item.FriendlyName), ChatType.System);
                            else
                            {
                                if (Required.Item.Type == ItemType.Ore)
                                    ReceiveChat(LanguageMgr.Get("ItemPurityRequiredForGuildCreation", Required.Item.FriendlyName, Required.Amount / 1000), ChatType.System);
                                else
                                    ReceiveChat(LanguageMgr.Get("InsufficientNeedCreateGuild", Required.Item.FriendlyName, Required.Amount), ChatType.System);
                            }
                            return false;
                        }
                    }
                }

                //take the required items
                for (int i = 0; i < Settings.Guild_CreationCostList.Count; i++)
                {
                    GuildItemVolume Required = Settings.Guild_CreationCostList[i];
                    if (Required.Item == null)
                    {
                        if (Info.Gold >= Required.Amount)
                        {
                            Info.Gold -= Required.Amount;
                            Enqueue(new S.LoseGold { Gold = Required.Amount });
                        }
                    }
                    else
                    {
                        ushort count = (ushort)Math.Min(Required.Amount, ushort.MaxValue);

                        for (int o = 0; o < Info.Inventory.Length; o++)
                        {
                            UserItem item = Info.Inventory[o];
                            if (item == null) continue;
                            if (item.Info != Required.Item) continue;

                            if ((Required.Item.Type == ItemType.Ore) && (item.CurrentDura / 1000 > Required.Amount))
                            {
                                Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                                Info.Inventory[o] = null;
                                break;
                            }
                            if (count > item.Count)
                            {
                                Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                                Info.Inventory[o] = null;
                                count -= item.Count;
                                continue;
                            }

                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = (ushort)count });
                            if (count == item.Count)
                                Info.Inventory[o] = null;
                            else
                                item.Count -= (ushort)count;
                            break;
                        }
                    }
                }
                RefreshStats();
            }

            //make the guild
            var guildInfo = new GuildInfo(this, guildName) { GuildIndex = ++Envir.NextGuildID };
            Envir.GuildList.Add(guildInfo);

            GuildObject guild = new GuildObject(guildInfo);
            Info.GuildIndex = guildInfo.GuildIndex;

            MyGuild = guild;
            MyGuildRank = guild.FindRank(Name);
            GuildMembersChanged = true;
            GuildNoticeChanged = true;
            GuildCanRequestItems = true;

            //tell us we now have a guild
            BroadcastInfo();
            MyGuild.SendGuildStatus(this);

            return true;
        }

        public void EditGuildMember(string Name, string RankName, byte RankIndex, byte ChangeType)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat(LanguageMgr.Get("NotInGuild"), ChatType.System);
                return;
            }
            switch (ChangeType)
            {
                case 0: //add member
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanRecruit))
                    {
                        ReceiveChat(LanguageMgr.Get("NotAllowedRecruitMembers"), ChatType.System);
                        return;
                    }

                    if (Name == "") return;

                    PlayerObject player = Envir.GetPlayer(Name);
                    if (player == null)
                    {
                        ReceiveChat(LanguageMgr.Get("PlayerIsNotOnline", Name), ChatType.System);
                        return;
                    }
                    if ((player.MyGuild != null) || (player.MyGuildRank != null) || (player.Info.GuildIndex != -1))
                    {
                        ReceiveChat(LanguageMgr.Get("AlreadyInGuild", Name), ChatType.System);
                        return;
                    }
                    if (!player.EnableGuildInvite)
                    {
                        ReceiveChat(LanguageMgr.Get("DisablingGuildInvites", Name), ChatType.System);
                        return;
                    }
                    if (player.PendingGuildInvite != null)
                    {
                        ReceiveChat(LanguageMgr.Get("GuildInvitePending", Name), ChatType.System);
                        return;
                    }

                    if (MyGuild.IsAtWar())
                    {
                        ReceiveChat(LanguageMgr.Get("CannotRecruitDuringWar"), ChatType.System);
                        return;
                    }

                    player.Enqueue(new S.GuildInvite { Name = MyGuild.Name });
                    player.PendingGuildInvite = MyGuild;
                    break;
                case 1: //delete member
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanKick))
                    {
                        ReceiveChat(LanguageMgr.Get("CannotRemoveMembers"), ChatType.System);
                        return;
                    }
                    if (Name == "") return;

                    if (!MyGuild.DeleteMember(this, Name))
                    {
                        return;
                    }
                    break;
                case 2: //promote member (and it'll auto create a new rank at bottom if the index > total ranks!)
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank))
                    {
                        ReceiveChat(LanguageMgr.Get("NotAllowedChangeOtherRank"), ChatType.System);
                        return;
                    }
                    if (Name == "") return;
                    MyGuild.ChangeRank(this, Name, RankIndex, RankName);
                    break;
                case 3: //change rank name
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank))
                    {
                        ReceiveChat(LanguageMgr.Get("NotAllowedChangeRank"), ChatType.System);
                        return;
                    }
                    if ((RankName == "") || (RankName.Length < 3))
                    {
                        ReceiveChat(LanguageMgr.Get("RankNameTooShort"), ChatType.System);
                        return;
                    }
                    if (RankName.Contains("\\") || RankName.Length > 20)
                    {
                        return;
                    }
                    if (!MyGuild.ChangeRankName(this, RankName, RankIndex))
                        return;
                    break;
                case 4: //new rank
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank))
                    {
                        ReceiveChat(LanguageMgr.Get("NotAllowedChangeRank"), ChatType.System);
                        return;
                    }
                    if (MyGuild.Ranks.Count > 254)
                    {
                        ReceiveChat(LanguageMgr.Get("NoMoreRankSlotsAvailable"), ChatType.System);
                        return;
                    }
                    MyGuild.NewRank(this);
                    break;
                case 5: //change rank setting
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeRank))
                    {
                        ReceiveChat(LanguageMgr.Get("NotAllowedChangeRank"), ChatType.System);
                        return;
                    }
                    int temp;

                    if (!int.TryParse(RankName, out temp))
                    {
                        return;
                    }
                    MyGuild.ChangeRankOption(this, RankIndex, temp, Name);
                    break;
            }
        }
        public void EditGuildNotice(List<string> notice)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat(LanguageMgr.Get("NotInGuild"), ChatType.System);
                return;
            }
            if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanChangeNotice))
            {

                ReceiveChat(LanguageMgr.Get("GuildNoticeChangeNotAllowed"), ChatType.System);
                return;
            }
            if (notice.Count > 200)
            {
                ReceiveChat(LanguageMgr.Get("GuildNoticeMaxLines"), ChatType.System);
                return;
            }
            MyGuild.NewNotice(notice);
        }
        public void GuildInvite(bool accept)
        {
            if (PendingGuildInvite == null)
            {
                ReceiveChat(LanguageMgr.Get("GuildNotInvited"), ChatType.System);
                return;
            }
            if (!accept)
            {
                PendingGuildInvite = null;
                return;
            }
            if (!PendingGuildInvite.HasRoom())
            {
                ReceiveChat(LanguageMgr.Get("GuildInviteFull", PendingGuildInvite.Name), ChatType.System);
                return;
            }
            PendingGuildInvite.NewMember(this);
            Info.GuildIndex = PendingGuildInvite.Guildindex;
            MyGuild = PendingGuildInvite;
            MyGuildRank = PendingGuildInvite.FindRank(Name);
            GuildMembersChanged = true;
            GuildNoticeChanged = true;
            //tell us we now have a guild
            BroadcastInfo();
            MyGuild.SendGuildStatus(this);
            PendingGuildInvite = null;
            EnableGuildInvite = false;
            GuildCanRequestItems = true;
            //refresh guildbuffs
            RefreshStats();
            if (MyGuild.BuffList.Count > 0)
                Enqueue(new S.GuildBuffList() { ActiveBuffs = MyGuild.BuffList });
        }
        public void RequestGuildInfo(byte Type)
        {
            if (MyGuild == null) return;
            if (MyGuildRank == null) return;
            switch (Type)
            {
                case 0://notice
                    if (GuildNoticeChanged)
                        Enqueue(new S.GuildNoticeChange() { notice = MyGuild.Info.Notice });
                    GuildNoticeChanged = false;
                    break;
                case 1://memberlist
                    if (GuildMembersChanged)
                        Enqueue(new S.GuildMemberChange() { Status = 255, Ranks = MyGuild.Ranks });
                    break;
            }
        }
        public void GuildNameReturn(string Name)
        {
            if (Name == "") CanCreateGuild = false;
            if (!CanCreateGuild) return;
            if ((Name.Length < 3) || (Name.Length > 20))
            {
                ReceiveChat(LanguageMgr.Get("GuildNameTooLong"), ChatType.System);
                CanCreateGuild = false;
                return;
            }
            if (Name.Contains('\\'))
            {
                CanCreateGuild = false;
                return;
            }
            if (MyGuild != null)
            {
                ReceiveChat(LanguageMgr.Get("YouAlreadyInGuild"), ChatType.System);
                CanCreateGuild = false;
                return;
            }
            GuildObject guild = Envir.GetGuild(Name);
            if (guild != null)
            {
                ReceiveChat(LanguageMgr.Get("GuildAlreadyExists", Name), ChatType.System);
                CanCreateGuild = false;
                return;
            }

            CreateGuild(Name);
            CanCreateGuild = false;
        }
        public void GuildStorageGoldChange(byte type, uint amount)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat(LanguageMgr.Get("NotPartOfGuild"), ChatType.System);
                return;
            }

            if (!InSafeZone)
            {
                ReceiveChat(LanguageMgr.Get("CannotUseGuildStorageOutsideSafezones"), ChatType.System);
                return;
            }

            if (type == 0)//donate
            {
                if (Info.Gold < amount)
                {
                    ReceiveChat(LanguageMgr.Get("InsufficientGold"), ChatType.System);
                    return;
                }

                if ((MyGuild.Gold + (ulong)amount) > uint.MaxValue)
                {
                    ReceiveChat(LanguageMgr.Get("GuildGoldLimitReached"), ChatType.System);
                    return;
                }

                Info.Gold -= amount;
                MyGuild.Gold += amount;
                Enqueue(new S.LoseGold { Gold = amount });
                MyGuild.SendServerPacket(new S.GuildStorageGoldChange() { Type = 0, Name = Info.Name, Amount = amount });
                MyGuild.NeedSave = true;
            }
            else
            {
                if (MyGuild.Gold < amount)
                {
                    ReceiveChat(LanguageMgr.Get("InsufficientGold"), ChatType.System);
                    return;
                }

                if (!CanGainGold(amount))
                {
                    ReceiveChat(LanguageMgr.Get("GoldLimitReached"), ChatType.System);
                    return;
                }

                if (MyGuildRank.Index != 0)
                {
                    ReceiveChat(LanguageMgr.Get("InsufficientRank"), ChatType.System);
                    return;
                }

                MyGuild.Gold -= amount;
                GainGold(amount);
                MyGuild.SendServerPacket(new S.GuildStorageGoldChange() { Type = 1, Name = Info.Name, Amount = amount });
                MyGuild.NeedSave = true;
            }
        }
        public void GuildStorageItemChange(byte type, int from, int to)
        {
            S.GuildStorageItemChange p = new S.GuildStorageItemChange { Type = (byte)(3 + type), From = from, To = to };
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                Enqueue(p);
                ReceiveChat(LanguageMgr.Get("NotPartOfGuild"), ChatType.System);
                return;
            }

            if (!InSafeZone && type != 3)
            {
                Enqueue(p);
                ReceiveChat(LanguageMgr.Get("GuildStorageOutsideSafezone"), ChatType.System);
                return;
            }

            switch (type)
            {
                case 0://store
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanStoreItem))
                    {
                        Enqueue(p);
                        ReceiveChat(LanguageMgr.Get("NoPermissionGuildStorage"), ChatType.System);
                        return;
                    }
                    if (from < 0 || from >= Info.Inventory.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (to < 0 || to >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[from] == null)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[from].Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[from].RentalInformation != null && Info.Inventory[from].RentalInformation.BindingFlags.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[to] != null)
                    {
                        ReceiveChat(LanguageMgr.Get("TargetSlotNotEmpty"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    MyGuild.StoredItems[to] = new GuildStorageItem() { Item = Info.Inventory[from], UserId = Info.Index };
                    Info.Inventory[from] = null;
                    RefreshBagWeight();
                    MyGuild.SendItemInfo(MyGuild.StoredItems[to].Item);
                    MyGuild.SendServerPacket(new S.GuildStorageItemChange() { Type = 0, User = Info.Index, Item = MyGuild.StoredItems[to], To = to, From = from });
                    MyGuild.NeedSave = true;
                    break;
                case 1://retrieve
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanRetrieveItem))
                    {

                        ReceiveChat(LanguageMgr.Get("NoPermissionGuildStorageRetrieve"), ChatType.System);
                        return;
                    }
                    if (from < 0 || from >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (to < 0 || to >= Info.Inventory.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[to] != null)
                    {
                        ReceiveChat(LanguageMgr.Get("TargetSlotNotEmpty"), ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from] == null)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from].Item.Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    Info.Inventory[to] = MyGuild.StoredItems[from].Item;
                    MyGuild.StoredItems[from] = null;
                    MyGuild.SendServerPacket(new S.GuildStorageItemChange() { Type = 1, User = Info.Index, To = to, From = from });
                    RefreshBagWeight();
                    MyGuild.NeedSave = true;
                    break;
                case 2: // Move Item
                    GuildStorageItem q = null;
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanStoreItem))
                    {
                        Enqueue(p);
                        ReceiveChat(LanguageMgr.Get("NoGuildStorageMovePermission"), ChatType.System);
                        return;
                    }
                    if (from < 0 || from >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (to < 0 || to >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from] == null)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from].Item.Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[to] != null)
                    {
                        q = MyGuild.StoredItems[to];
                    }
                    MyGuild.StoredItems[to] = MyGuild.StoredItems[from];
                    if (q != null) MyGuild.StoredItems[from] = q;
                    else MyGuild.StoredItems[from] = null;

                    MyGuild.SendItemInfo(MyGuild.StoredItems[to].Item);

                    if (MyGuild.StoredItems[from] != null) MyGuild.SendItemInfo(MyGuild.StoredItems[from].Item);

                    MyGuild.SendServerPacket(new S.GuildStorageItemChange() { Type = 2, User = Info.Index, Item = MyGuild.StoredItems[to], To = to, From = from });
                    MyGuild.NeedSave = true;
                    break;
                case 3://request list
                    if (!GuildCanRequestItems) return;
                    GuildCanRequestItems = false;
                    for (int i = 0; i < MyGuild.StoredItems.Length; i++)
                    {
                        if (MyGuild.StoredItems[i] == null) continue;
                        UserItem item = MyGuild.StoredItems[i].Item;
                        if (item == null) continue;
                        //CheckItemInfo(item.Info);
                        CheckItem(item);
                    }
                    Enqueue(new S.GuildStorageList() { Items = MyGuild.StoredItems });
                    break;
            }

        }
        public void GuildWarReturn(string Name)
        {
            if (MyGuild == null || MyGuildRank != MyGuild.Ranks[0]) return;

            GuildObject enemyGuild = Envir.GetGuild(Name);

            if (enemyGuild == null)
            {
                ReceiveChat(LanguageMgr.Get("GuildNotFound", Name), ChatType.System);
                return;
            }

            if (MyGuild == enemyGuild)
            {
                ReceiveChat(LanguageMgr.Get("CannotWarOwnGuild"), ChatType.System);
                return;
            }

            if (enemyGuild.Name == Settings.NewbieGuild)
            {
                ReceiveChat(LanguageMgr.Get("CannotWarNewPlayersGuild"), ChatType.System);
                return;
            }

            if (MyGuild.WarringGuilds.Contains(enemyGuild))
            {
                ReceiveChat(LanguageMgr.Get("AlreadyAtWarWithGuild"), ChatType.System);
                return;
            }

            if (MyGuild.Gold < Settings.Guild_WarCost)
            {
                ReceiveChat(LanguageMgr.Get("GuildBankFundsInsufficient"), ChatType.System);
                return;
            }

            if (MyGuild.GoToWar(enemyGuild))
            {
                ReceiveChat(LanguageMgr.Get("YouStartedWarWith", Name), ChatType.System);
                enemyGuild.SendMessage(LanguageMgr.Get("HasStartedWar", MyGuild.Name), ChatType.System);

                MyGuild.Gold -= Settings.Guild_WarCost;
                MyGuild.SendServerPacket(new S.GuildStorageGoldChange() { Type = 2, Name = Info.Name, Amount = Settings.Guild_WarCost });
            }
        }

        public override bool AtWar(HumanObject attacker)
        {
            if (CurrentMap.Info.Fight) return true;

            if (MyGuild == null) return false;

            if (attacker is PlayerObject playerAttacker)
            {
                if (attacker == null || playerAttacker.MyGuild == null) return false;

                if (!MyGuild.WarringGuilds.Contains(playerAttacker.MyGuild)) return false;
            }

            return true;
        }
        protected override void CleanUp()
        {
            base.CleanUp();
            Account = null;
        }

        public void GuildBuffUpdate(byte type, int id)
        {
            if (MyGuild == null) return;
            if (MyGuildRank == null) return;
            if (id < 0) return;
            switch (type)
            {
                case 0://request info list
                    if (RequestedGuildBuffInfo) return;
                    Enqueue(new S.GuildBuffList() { GuildBuffs = Settings.Guild_BuffList });
                    break;
                case 1://buy the buff
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanActivateBuff))
                    {
                        ReceiveChat(LanguageMgr.Get("NoCorrectGuildRank"), ChatType.System);
                        return;
                    }
                    GuildBuffInfo BuffInfo = Envir.FindGuildBuffInfo(id);
                    if (BuffInfo == null)
                    {
                        ReceiveChat(LanguageMgr.Get("BuffNotExist"), ChatType.System);
                        return;
                    }
                    if (MyGuild.GetBuff(id) != null)
                    {
                        ReceiveChat(LanguageMgr.Get("BuffAlreadyObtained"), ChatType.System);
                        return;
                    }
                    if (MyGuild.Info.Level < BuffInfo.LevelRequirement)
                    {
                        ReceiveChat(LanguageMgr.Get("GuildLevelRequirementNotMet", BuffInfo.LevelRequirement), ChatType.System);
                        return;
                    }
                    if (MyGuild.Info.SparePoints < BuffInfo.PointsRequirement)
                    {
                        ReceiveChat(LanguageMgr.Get("GuildSparePointsInsufficient", BuffInfo.PointsRequirement), ChatType.System);
                        return;
                    }
                    uint activationCost = 0;
                    bool requiresGold = BuffInfo.TimeLimit > 0 && BuffInfo.ActivationCost > 0;
                    if (requiresGold)
                    {
                        activationCost = (uint)BuffInfo.ActivationCost;
                        if (MyGuild.Gold < activationCost)
                        {
                            ReceiveChat(LanguageMgr.Get("GuildBankFundsInsufficient"), ChatType.System);
                            return;
                        }
                    }
                    if (MyGuild.NewBuff(id))
                    {
                        bool hasPointCost = BuffInfo.PointsRequirement > 0;
                        if (requiresGold && hasPointCost)
                        {
                            MyGuild.SendMessage(LanguageMgr.Get("GuildBuffPurchaseSuccessPointsGold", Name, BuffInfo.Name, BuffInfo.PointsRequirement, activationCost));
                        }
                        else if (requiresGold)
                        {
                            MyGuild.SendMessage(LanguageMgr.Get("GuildBuffPurchaseSuccessGold", Name, BuffInfo.Name, activationCost));
                        }
                        else if (hasPointCost)
                        {
                            MyGuild.SendMessage(LanguageMgr.Get("GuildBuffPurchaseSuccessPoints", Name, BuffInfo.Name, BuffInfo.PointsRequirement));
                        }
                        else
                        {
                            MyGuild.SendMessage(LanguageMgr.Get("GuildBuffPurchaseSuccessFree", Name, BuffInfo.Name));
                        }
                    }
                    break;
                case 2://activate the buff
                    if (!MyGuildRank.Options.HasFlag(GuildRankOptions.CanActivateBuff))
                    {
                        ReceiveChat(LanguageMgr.Get("IncorrectGuildRank"), ChatType.System);
                        return;
                    }
                    GuildBuff Buff = MyGuild.GetBuff(id);
                    if (Buff == null)
                    {
                        ReceiveChat(LanguageMgr.Get("BuffNotObtained"), ChatType.System);
                        return;
                    }
                    if ((MyGuild.Gold < Buff.Info.ActivationCost) || (Buff.Active)) return;
                    MyGuild.ActivateBuff(id);
                    break;
            }
        }

        public void GetGuildTerritories(int page)
        {
            if (Dead) return;

            List<ClientGTMap> tempList = new List<ClientGTMap>();

            if (page < 0) return;

            var max = Math.Min(Envir.GTMapList.Count, (page + 1) * 7);
            for (int i = page * 7; i < max; i++)
            {
                tempList.Add(Envir.GTMapList[i].ToClientGTMap());
            }

            Enqueue(new S.GuildTerritoryPage { Listings = tempList, length = Envir.GTMapList.Count });
        }

        public void PurchaseGuildTerritory(string owner)
        {
            var gt = Envir.GTMapList.FirstOrDefault(x => x.Owner == owner);

            if (gt == null)
            {
                ReceiveChat(LanguageMgr.Get("OwnerGuildNotFound"), ChatType.System);
                return;
            }

            if (gt.Price == 0)
            {
                ReceiveChat(LanguageMgr.Get("TerritoryNoLongerForSale"), ChatType.System);
                return;
            }

            if (MyGuild == null || MyGuildRank.Index != 0)
            {
                ReceiveChat(LanguageMgr.Get("GuildLeaderToPurchaseTerritory"), ChatType.System);
                return;
            }

            if (gt.Owner == MyGuild.Name)
            {
                ReceiveChat(LanguageMgr.Get("AlreadyOwnTerritory"), ChatType.System);
                return;
            }

            if (MyGuild.HasGT)
            {
                ReceiveChat(LanguageMgr.Get("AlreadyOwnATerritory"), ChatType.System);
                return;
            }

            if (MyGuild.Gold < gt.Price)
            {
                ReceiveChat(LanguageMgr.Get("InsufficientFunds"), ChatType.System);
                return;
            }

            MyGuild.Gold -= (uint)gt.Price;
            MyGuild.SendServerPacket(new S.GuildStorageGoldChange { Type = 2, Amount = (uint)gt.Price });
            ReceiveChat(LanguageMgr.Get("GuildTerritoryPurchaseProcess24Hrs"), ChatType.System);

            GuildObject guild = Envir.GetGuild(gt.Owner);
            if (guild != null)
            {
                guild.Gold += (uint)gt.Price;
                guild.SendServerPacket(new S.GuildStorageGoldChange { Type = 3, Amount = (uint)gt.Price });
                guild.EndGT();
                guild.SendServerPacket(new S.Chat
                {
                    Message = LanguageMgr.Get("TerritorySold"),
                    Type = ChatType.System
                });
            }

            MyGuild.GTIndex = gt.Index;
            MyGuild.GTRent = DateTime.Now.AddDays(Settings.GTDays + 1);
            MyGuild.GTBegin = DateTime.Now.AddDays(1);
            MyGuild.GTKey = Envir.Random.Next(100, int.MaxValue - 100);
            MyGuild.GTPrice = 0;
            gt.Owner = MyGuild.Name;
            gt.Leader = MyGuild.Ranks[0].Members[0].Name;
            if (MyGuild.Ranks[0].Members.Count > 1)
                gt.Leader = MyGuild.Ranks[0].Members[1].Name;
            gt.Price = 0;
            gt.Key = MyGuild.GTKey;
            gt.Days = (Envir.Now - MyGuild.GTRent).Days;
            gt.Begin = (MyGuild.GTBegin - Envir.Now).Seconds;
        }
        #endregion
    }
}
