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
        private void SendOutputMessage(S.SendOutputMessage p)
        {
            OutputMessage(p.Message, p.Type);
        }

        private void NPCConsign()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Consign;
            NPCDropDialog.Show();
        }
        private void NPCMarket(S.NPCMarket p)
        {
            for (int i = 0; i < p.Listings.Count; i++)
                Bind(p.Listings[i].Item);

            TrustMerchantDialog.Show();
            TrustMerchantDialog.UserMode = p.UserMode;
            TrustMerchantDialog.Listings = p.Listings;
            TrustMerchantDialog.Page = 0;
            TrustMerchantDialog.PageCount = p.Pages;
            TrustMerchantDialog.UpdateInterface();
        }
        private void NPCMarketPage(S.NPCMarketPage p)
        {
            if (!TrustMerchantDialog.Visible) return;

            for (int i = 0; i < p.Listings.Count; i++)
                Bind(p.Listings[i].Item);

            TrustMerchantDialog.Listings.AddRange(p.Listings);
            TrustMerchantDialog.Page = (TrustMerchantDialog.Listings.Count - 1) / 10;
            TrustMerchantDialog.UpdateInterface();
        }
        private void ConsignItem(S.ConsignItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID) ?? BeltDialog.GetCell(p.UniqueID);

            if (cell == null) return;

            cell.Locked = false;

            if (!p.Success) return;

            cell.Item = null;

            User.RefreshStats();
        }
        private void MarketFail(S.MarketFail p)
        {
            TrustMerchantDialog.MarketTime = 0;
            switch (p.Reason)
            {
                case 0:
                    MirMessageBox.Show(LanguageMgr.Get("YouCannotUseTrustMerchantWhenDead"));
                    break;
                case 1:
                    MirMessageBox.Show(LanguageMgr.Get("YouCannotBuyFromTrustMerchantWithoutUsing"));
                    break;
                case 2:
                    MirMessageBox.Show(LanguageMgr.Get("ItemHasBeenSold"));
                    break;
                case 3:
                    MirMessageBox.Show(LanguageMgr.Get("ItemExpiredCannotBeBought"));
                    break;
                case 4:
                    MirMessageBox.Show(LanguageMgr.Get("LowGold"));
                    break;
                case 5:
                    MirMessageBox.Show(LanguageMgr.Get("NotEnoughWeightOrSpaceToBuyItem"));
                    break;
                case 6:
                    MirMessageBox.Show(LanguageMgr.Get("CannotBuyOwnItems"));
                    break;
                case 7:
                    MirMessageBox.Show(LanguageMgr.Get("TooFarFromTrustMerchant"));
                    break;
                case 8:
                    MirMessageBox.Show(LanguageMgr.Get("CannotHoldGoldForSale"));
                    break;
                case 9:
                    MirMessageBox.Show(LanguageMgr.Get("ItemNotMetMinimumBid"));
                    break;
                case 10:
                    MirMessageBox.Show(LanguageMgr.Get("AuctionAlreadyEndedForItem"));
                    break;
            }

        }
        private void MarketSuccess(S.MarketSuccess p)
        {
            TrustMerchantDialog.MarketTime = 0;
            MirMessageBox.Show(p.Message);
        }
        private void ObjectSitDown(S.ObjectSitDown p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob) && ob.Race == ObjectType.Monster)
            {
                ob.SitDown = p.Sitting;
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.SitDown, Direction = p.Direction, Location = p.Location });
            }
        }

        private void BaseStatsInfo(S.BaseStatsInfo p)
        {
            User.CoreStats = p.Stats;
            User.RefreshStats();
        }

        private void HeroBaseStatsInfo(S.HeroBaseStatsInfo p)
        {
            if (Hero == null) return;

            Hero.CoreStats = p.Stats;
            Hero.RefreshStats();
        }

        private void UserName(S.UserName p)
        {
            for (int i = 0; i < UserIdList.Count; i++)
                if (UserIdList[i].Id == p.Id)
                {
                    UserIdList[i].UserName = p.Name;
                    break;
                }
            DisposeItemLabel();
            HoverItem = null;
        }

        private void ChatItemStats(S.ChatItemStats p)
        {
            //for (int i = 0; i < ChatItemList.Count; i++)
            //    if (ChatItemList[i].ID == p.ChatItemId)
            //    {
            //        ChatItemList[i].ItemStats = p.Stats;
            //        ChatItemList[i].RecievedTick = CMain.Time;
            //    }
        }

        private void GuildInvite(S.GuildInvite p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("JoinGuildPrompt", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.GuildInvite { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => Network.Enqueue(new C.GuildInvite { AcceptInvite = false });

            messageBox.Show();
        }

        private void GuildNameRequest(S.GuildNameRequest p)
        {
            MirInputBox inputBox = new MirInputBox(LanguageMgr.Get("EnterGuildNameLengthLimit"));
            inputBox.InputTextBox.TextBox.KeyPress += (o, e) =>
            {
                // 只允许字母 / 数字 / 退格（KeyPress 现在给键码，大小写由 Shift 位体现，故按键码放行全部字母）
                Keys k = e.KeyCode;
                bool letter = k >= Keys.KeyA && k <= Keys.KeyZ;
                bool digit = (k >= Keys.Digit0 && k <= Keys.Digit9) || (k >= Keys.Numpad0 && k <= Keys.Numpad9);
                if (!letter && !digit && k != Keys.Backspace)
                    e.Handled = true;
            };
            inputBox.OKButton.Click += (o, e) =>
            {
                if (inputBox.InputTextBox.Text.Contains('\\'))
                {
                    ChatDialog.ReceiveChat(LanguageMgr.Get("CannotUseBackslashInGuildName"), ChatType.System);
                    inputBox.InputTextBox.Text = "";
                }
                Network.Enqueue(new C.GuildNameReturn { Name = inputBox.InputTextBox.Text });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void GuildRequestWar(S.GuildRequestWar p)
        {
            MirInputBox inputBox = new MirInputBox(LanguageMgr.Get("EnterGuildToWarWith"));

            inputBox.OKButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.GuildWarReturn { Name = inputBox.InputTextBox.Text });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void GuildNoticeChange(S.GuildNoticeChange p)
        {
            if (p.update == -1)
                GuildDialog.NoticeChanged = true;
            else
                GuildDialog.NoticeChange(p.notice);
        }
        private void GuildMemberChange(S.GuildMemberChange p)
        {
            switch (p.Status)
            {
                case 0: // logged of
                    GuildDialog.MemberStatusChange(p.Name, false);
                    break;
                case 1: // logged on
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerLoggedOn", p.Name), ChatType.Guild);
                    GuildDialog.MemberStatusChange(p.Name, true);
                    break;
                case 2: //new member
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerJoinedGuild", p.Name), ChatType.Guild);
                    GuildDialog.MemberCount++;
                    GuildDialog.MembersChanged = true;
                    break;
                case 3: //kicked member
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerRemovedFromGuild", p.Name), ChatType.Guild);
                    GuildDialog.MemberCount = Math.Max(0, GuildDialog.MemberCount - 1);
                    GuildDialog.MembersChanged = true;
                    break;
                case 4: //member left
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerLeftGuild", p.Name), ChatType.Guild);
                    GuildDialog.MemberCount = Math.Max(0, GuildDialog.MemberCount - 1);
                    GuildDialog.MembersChanged = true;
                    break;
                case 5://rank change (name or different rank)
                    GuildDialog.MembersChanged = true;
                    break;
                case 6: //new rank
                    if (p.Ranks.Count > 0)
                        GuildDialog.NewRankRecieved(p.Ranks[0]);
                    break;
                case 7: //rank option changed
                    if (p.Ranks.Count > 0)
                        GuildDialog.RankChangeRecieved(p.Ranks[0]);
                    break;
                case 8: //my rank changed
                    if (p.Ranks.Count > 0)
                        GuildDialog.MyRankChanged(p.Ranks[0]);
                    break;
                case 255:
                    GuildDialog.NewMembersList(p.Ranks);
                    break;
            }
        }

        private void GuildStatus(S.GuildStatus p)
        {
            if ((User.GuildName == "") && (p.GuildName != ""))
            {
                GuildDialog.NoticeChanged = true;
                GuildDialog.MembersChanged = true;
            }
            if (p.GuildName == "")
            {
                GuildDialog.Hide();
            }

            if ((User.GuildName == p.GuildName) && (GuildDialog.Level < p.Level))
            {
                //guild leveled
            }
            bool GuildChange = User.GuildName != p.GuildName;
            User.GuildName = p.GuildName;
            User.GuildRankName = p.GuildRankName;
            GuildDialog.Level = p.Level;
            GuildDialog.Experience = p.Experience;
            GuildDialog.MaxExperience = p.MaxExperience;
            GuildDialog.Gold = p.Gold;
            GuildDialog.SparePoints = p.SparePoints;
            GuildDialog.MemberCount = p.MemberCount;
            GuildDialog.MaxMembers = p.MaxMembers;
            GuildDialog.Voting = p.Voting;
            GuildDialog.ItemCount = p.ItemCount;
            GuildDialog.BuffCount = p.BuffCount;
            GuildDialog.StatusChanged(p.MyOptions);
            GuildDialog.MyRankId = p.MyRankId;
            GuildDialog.UpdateMembers();
            //reset guildbuffs
            if (GuildChange)
            {
                GuildDialog.EnabledBuffs.Clear();
                GuildDialog.UpdateActiveStats();
                RemoveBuff(new S.RemoveBuff { ObjectID = User.ObjectID, Type = BuffType.Guild });
                User.RefreshStats();
            }
        }

        private void GuildExpGain(S.GuildExpGain p)
        {
            //OutputMessage(string.Format("Guild Experience Gained {0}.", p.Amount));
            GuildDialog.Experience += p.Amount;
        }

        private void GuildStorageGoldChange(S.GuildStorageGoldChange p)
        {
            switch (p.Type)
            {
                case 0:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerDonatedGoldToGuildFunds", p.Name, p.Amount), ChatType.Guild);
                    GuildDialog.Gold += p.Amount;
                    break;
                case 1:
                    ChatDialog.ReceiveChat(LanguageMgr.Get("PlayerRetrievedGoldFromGuildFunds", p.Name, p.Amount), ChatType.Guild);
                    if (GuildDialog.Gold > p.Amount)
                        GuildDialog.Gold -= p.Amount;
                    else
                        GuildDialog.Gold = 0;
                    break;
                case 2:
                    if (GuildDialog.Gold > p.Amount)
                        GuildDialog.Gold -= p.Amount;
                    else
                        GuildDialog.Gold = 0;
                    break;
                case 3:
                    GuildDialog.Gold += p.Amount;
                    break;
            }
        }

        private void GuildStorageItemChange(S.GuildStorageItemChange p)
        {
            MirItemCell fromCell = null;
            MirItemCell toCell = null;
            switch (p.Type)
            {
                case 0://store
                    toCell = GuildDialog.StorageGrid[p.To];

                    if (toCell == null) return;

                    toCell.Locked = false;
                    toCell.Item = p.Item.Item;
                    Bind(toCell.Item);
                    if (p.User != User.Id) return;
                    fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
                    fromCell.Locked = false;
                    if (fromCell != null)
                        fromCell.Item = null;
                    User.RefreshStats();
                    break;
                case 1://retrieve
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (fromCell == null) return;
                    fromCell.Locked = false;

                    if (p.User != User.Id)
                    {
                        fromCell.Item = null;
                        return;
                    }
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    if (toCell == null) return;
                    toCell.Locked = false;
                    toCell.Item = fromCell.Item;
                    fromCell.Item = null;
                    break;

                case 2:
                    toCell = GuildDialog.StorageGrid[p.To];
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    fromCell.Item = toCell.Item;
                    toCell.Item = p.Item.Item;

                    Bind(toCell.Item);
                    if (fromCell.Item != null)
                        Bind(fromCell.Item);
                    break;
                case 3://failstore
                    fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
                    toCell = GuildDialog.StorageGrid[p.To];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    break;
                case 4://failretrieve
                    toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];
                    fromCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    toCell.Locked = false;
                    fromCell.Locked = false;
                    break;
                case 5://failmove
                    fromCell = GuildDialog.StorageGrid[p.To];
                    toCell = GuildDialog.StorageGrid[p.From];

                    if (toCell == null || fromCell == null) return;

                    GuildDialog.StorageGrid[p.From].Locked = false;
                    GuildDialog.StorageGrid[p.To].Locked = false;
                    break;
            }
        }
        private void GuildStorageList(S.GuildStorageList p)
        {
            for (int i = 0; i < p.Items.Length; i++)
            {
                if (i >= GuildDialog.StorageGrid.Length) break;
                if (p.Items[i] == null)
                {
                    GuildDialog.StorageGrid[i].Item = null;
                    continue;
                }
                GuildDialog.StorageGrid[i].Item = p.Items[i].Item;
                Bind(GuildDialog.StorageGrid[i].Item);
            }
        }
        private void GuildTerritoryPage(S.GuildTerritoryPage p)
        {
            if (!GuildTerritoryDialog.Visible)
            {
                GuildTerritoryDialog.Show();
            }

            GuildTerritoryDialog.GTMapList = p.Listings;
            GuildTerritoryDialog.Lenght = p.length;
            GuildTerritoryDialog.UpdateInterface();
        }

        private void HeroCreateRequest(S.HeroCreateRequest p)
        {
            NewHeroDialog.WarriorButton.Visible = p.CanCreateClass[(int)MirClass.Warrior];
            NewHeroDialog.WizardButton.Visible = p.CanCreateClass[(int)MirClass.Wizard];
            NewHeroDialog.TaoistButton.Visible = p.CanCreateClass[(int)MirClass.Taoist];
            NewHeroDialog.AssassinButton.Visible = p.CanCreateClass[(int)MirClass.Assassin];
            NewHeroDialog.ArcherButton.Visible = p.CanCreateClass[(int)MirClass.Archer];

            NewHeroDialog.Show();
        }

        private void ManageHeroes(S.ManageHeroes p)
        {
            if (p.Heroes != null)
            {
                for (int i = 0; i < p.Heroes.Length; i++)
                    AddHeroInformation(p.Heroes[i], i);
            }

            MaximumHeroCount = p.MaximumCount;
            HeroManageDialog.SetCurrentHero(p.CurrentHero);
            HeroManageDialog.Show();
        }

        private void ChangeHero(S.ChangeHero p)
        {
            ClientHeroInformation temp = HeroStorage[p.FromIndex];
            HeroStorage[p.FromIndex] = HeroManageDialog.CurrentAvatar.Info;
            HeroManageDialog.SetCurrentHero(temp);
            HeroManageDialog.RefreshInterface();
        }

        public int HeroAvatar(MirClass job, MirGender gender) => 1400 + (byte)job + 10 * (byte)gender;

        private void UnlockHeroAutoPot(bool value)
        {
            if (Hero == null) return;

            Hero.AutoPot = value;
            HeroInventoryDialog.RefreshInterface();
        }

        private void SetAutoPotValue(S.SetAutoPotValue p)
        {
            if (Hero == null) return;

            if (p.Stat == Stat.HP)
                Hero.AutoHPPercent = p.Value;
            else
                Hero.AutoMPPercent = p.Value;

            HeroInventoryDialog.RefreshInterface();
        }

        private void SetAutoPotItem(S.SetAutoPotItem p)
        {
            if (Hero == null) return;

            if (p.Grid == MirGridType.HeroHPItem)
                Hero.HPItem[0] = p.ItemIndex > 0 ? new UserItem(GetItemInfo(p.ItemIndex)) : null;
            else
                Hero.MPItem[0] = p.ItemIndex > 0 ? new UserItem(GetItemInfo(p.ItemIndex)) : null;
        }

        private void SetHeroBehaviour(S.SetHeroBehaviour p)
        {
            if (Hero == null) return;
            HeroBehaviourPanel.UpdateBehaviour(p.Behaviour);
        }

        private void NewHero(S.NewHero p)
        {
            NewHeroDialog.OKButton.Enabled = true;

            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show(LanguageMgr.Get("CreatingNewHeroesDisabled"));
                    NewHeroDialog.Hide();
                    break;
                case 1:
                    MirMessageBox.Show(LanguageMgr.Get("YourHeroNameNotAcceptable"));
                    NewHeroDialog.NameTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show(LanguageMgr.Get("SelectedGenderNotExist"));
                    break;
                case 3:
                    MirMessageBox.Show(LanguageMgr.Get("SelectedClassNotExist"));
                    break;
                case 4:
                    MirMessageBox.Show(LanguageMgr.Get("CannotMakeMoreHeroes"));
                    NewHeroDialog.Hide();
                    break;
                case 5:
                    MirMessageBox.Show(LanguageMgr.Get("CharacterNameAlreadyExists"));
                    NewHeroDialog.NameTextBox.SetFocus();
                    break;
                case 6:
                    MirMessageBox.Show(LanguageMgr.Get("NoBagSpace"));
                    NewHeroDialog.Hide();
                    break;
                case 10:
                    MirMessageBox.Show(LanguageMgr.Get("HeroCreatedSuccessfully"));
                    NewHeroDialog.Hide();
                    break;
            }
        }

        private void HeroInformation(S.HeroInformation p)
        {
            Hero = new UserHeroObject(p.ObjectID);
            Hero.Load(p);

            Hero.AutoPot = p.AutoPot;
            Hero.AutoHPPercent = p.AutoHPPercent;
            Hero.AutoMPPercent = p.AutoMPPercent;

            if (p.HPItemIndex > 0)
                Hero.HPItem[0] = new UserItem(GetItemInfo(p.HPItemIndex));
            if (p.MPItemIndex > 0)
                Hero.MPItem[0] = new UserItem(GetItemInfo(p.MPItemIndex));

            HeroDialog = new CharacterDialog(MirGridType.HeroEquipment, Hero) { Parent = this, Visible = false };
            HeroInventoryDialog = new HeroInventoryDialog { Parent = this };
            HeroBeltDialog = new HeroBeltDialog { Parent = this };
            MainDialog.HeroInfoPanel.Update();

            Hero.RefreshStats();
        }

        private void UpdateHeroSpawnState(S.UpdateHeroSpawnState p)
        {
            HeroSpawnState = p.State;

            HasHero = p.State > HeroSpawnState.None;
            MainDialog.HeroInfoPanel.Visible = p.State > HeroSpawnState.Unsummoned;
            MainDialog.HeroMenuButton.Visible = p.State > HeroSpawnState.Unsummoned;
            HeroBehaviourPanel.Visible = p.State > HeroSpawnState.Unsummoned;
            HeroMenuPanel.Visible = HeroMenuPanel.Visible && MainDialog.HeroMenuButton.Visible;

            if (p.State < HeroSpawnState.Summoned)
            {
                HeroInventoryDialog.Dispose();
                HeroDialog.Dispose();
                HeroBeltDialog.Dispose();
            }
        }

        private void MarriageRequest(S.MarriageRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("PlayerAskedForMarriage", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.MarriageReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.MarriageReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private void DivorceRequest(S.DivorceRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("PlayerRequestedDivorce", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.DivorceReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.DivorceReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private void MentorRequest(S.MentorRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("PlayerRequestedTeachClass", p.Name, p.Level, GameScene.User.Class.ToString()),
                MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.MentorReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.MentorReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }

        private bool UpdateGuildBuff(GuildBuff buff, bool Remove = false)
        {
            for (int i = 0; i < GuildDialog.EnabledBuffs.Count; i++)
            {
                if (GuildDialog.EnabledBuffs[i].Id == buff.Id)
                {
                    if (Remove)
                    {
                        GuildDialog.EnabledBuffs.RemoveAt(i);
                    }
                    else
                        GuildDialog.EnabledBuffs[i] = buff;
                    return true;
                }
            }
            return false;
        }

        private void GuildBuffList(S.GuildBuffList p)
        {
            //getting the list of all guildbuffs on server?
            if (p.GuildBuffs.Count > 0)
                GuildDialog.GuildBuffInfos.Clear();
            for (int i = 0; i < p.GuildBuffs.Count; i++)
            {
                GuildDialog.GuildBuffInfos.Add(p.GuildBuffs[i]);
            }
            //getting the list of all active/removedbuffs?
            for (int i = 0; i < p.ActiveBuffs.Count; i++)
            {
                //if (p.ActiveBuffs[i].ActiveTimeRemaining > 0)
                //    p.ActiveBuffs[i].ActiveTimeRemaining = Convert.ToInt32(CMain.Time / 1000) + (p.ActiveBuffs[i].ActiveTimeRemaining * 60);
                if (UpdateGuildBuff(p.ActiveBuffs[i], p.Remove == 1)) continue;
                if (!(p.Remove == 1))
                {
                    GuildDialog.EnabledBuffs.Add(p.ActiveBuffs[i]);
                    //CreateGuildBuff(p.ActiveBuffs[i]);
                }
            }

            for (int i = 0; i < GuildDialog.EnabledBuffs.Count; i++)
            {
                if (GuildDialog.EnabledBuffs[i].Info == null)
                {
                    GuildDialog.EnabledBuffs[i].Info = GuildDialog.FindGuildBuffInfo(GuildDialog.EnabledBuffs[i].Id);
                }
            }

            ClientBuff buff = BuffsDialog.Buffs.FirstOrDefault(e => e.Type == BuffType.Guild);

            if (GuildDialog.EnabledBuffs.Any(e => e.Active))
            {
                if (buff == null)
                {
                    buff = new ClientBuff { Type = BuffType.Guild, ObjectID = User.ObjectID, Caster = LanguageMgr.Get("Guild"), Infinite = true, Values = new int[0] };

                    BuffsDialog.Buffs.Add(buff);
                    BuffsDialog.CreateBuff(buff);
                }

                GuildDialog.UpdateActiveStats();
            }
            else
            {
                RemoveBuff(new S.RemoveBuff { ObjectID = User.ObjectID, Type = BuffType.Guild });
            }

            User.RefreshStats();
        }

        private void TradeRequest(S.TradeRequest p)
        {
            MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("PlayerRequestedTrade", p.Name), MirMessageBoxButtons.YesNo);

            messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.TradeReply { AcceptInvite = true });
            messageBox.NoButton.Click += (o, e) => { Network.Enqueue(new C.TradeReply { AcceptInvite = false }); messageBox.Dispose(); };

            messageBox.Show();
        }
        private void TradeAccept(S.TradeAccept p)
        {
            GuestTradeDialog.GuestName = p.Name;
            TradeDialog.TradeAccept();
        }
        private void TradeGold(S.TradeGold p)
        {
            GuestTradeDialog.GuestGold = p.Amount;
            TradeDialog.ChangeLockState(false);
            TradeDialog.RefreshInterface();
        }
        private void TradeItem(S.TradeItem p)
        {
            GuestTradeDialog.GuestItems = p.TradeItems;
            TradeDialog.ChangeLockState(false);
            TradeDialog.RefreshInterface();
        }
        private void TradeConfirm()
        {
            TradeDialog.TradeReset();
        }
        private void TradeCancel(S.TradeCancel p)
        {
            if (p.Unlock)
            {
                TradeDialog.ChangeLockState(false);
            }
            else
            {
                TradeDialog.TradeReset();

                MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("DealCancelledMustFace"), MirMessageBoxButtons.OK);
                messageBox.Show();
            }
        }
        private void NPCAwakening()
        {
            if (NPCAwakeDialog.Visible != true)
                NPCAwakeDialog.Show();
        }
        private void NPCDisassemble()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Disassemble;
            NPCDropDialog.Show();
        }
        private void NPCDowngrade()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Downgrade;
            NPCDropDialog.Show();
        }
        private void NPCReset()
        {
            if (!NPCDialog.Visible) return;
            NPCDropDialog.PType = PanelType.Reset;
            NPCDropDialog.Show();
        }
        private void AwakeningNeedMaterials(S.AwakeningNeedMaterials p)
        {
            NPCAwakeDialog.setNeedItems(p.Materials, p.MaterialsCount);
        }
        private void AwakeningLockedItem(S.AwakeningLockedItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID);
            if (cell != null)
                cell.Locked = p.Locked;
        }

        private void Awakening(S.Awakening p)
        {
            if (NPCAwakeDialog.Visible)
                NPCAwakeDialog.Hide();
            if (InventoryDialog.Visible)
                InventoryDialog.Hide();

            MirItemCell cell = InventoryDialog.GetCell((ulong)p.removeID);
            if (cell != null)
            {
                cell.Locked = false;
                cell.Item = null;
            }

            for (int i = 0; i < InventoryDialog.Grid.Length; i++)
            {
                if (InventoryDialog.Grid[i].Locked == true)
                {
                    InventoryDialog.Grid[i].Locked = false;

                    //if (InventoryDialog.Grid[i].Item.UniqueID == (ulong)p.removeID)
                    //{
                    //    InventoryDialog.Grid[i].Item = null;
                    //}
                }
            }

            for (int i = 0; i < NPCAwakeDialog.ItemsIdx.Length; i++)
            {
                NPCAwakeDialog.ItemsIdx[i] = 0;
            }

            MirMessageBox messageBox = null;

            switch (p.result)
            {
                case -4:
                    messageBox = new MirMessageBox(LanguageMgr.Get("NotEnoughMaterials"), MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -3:
                    messageBox = new MirMessageBox(LanguageMgr.Get("LowGold"), MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -2:
                    messageBox = new MirMessageBox(LanguageMgr.Get("AwakeningMaxLevel"), MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case -1:
                    messageBox = new MirMessageBox(LanguageMgr.Get("CannotAwakenItem"), MirMessageBoxButtons.OK);
                    MapControl.AwakeningAction = false;
                    break;
                case 0:
                    //messageBox = new MirMessageBox("Upgrade Failed.", MirMessageBoxButtons.OK);
                    break;
                case 1:
                    //messageBox = new MirMessageBox("Upgrade Success.", MirMessageBoxButtons.OK);
                    break;

            }

            if (messageBox != null) messageBox.Show();
        }

        private void ReceiveMail(S.ReceiveMail p)
        {
            NewMail = false;
            NewMailCounter = 0;
            User.Mail.Clear();

            User.Mail = p.Mail.OrderByDescending(e => !e.Locked).ThenByDescending(e => e.DateSent).ToList();

            foreach (ClientMail mail in User.Mail)
            {
                foreach (UserItem itm in mail.Items)
                {
                    Bind(itm);
                }
            }

            //display new mail received
            if (User.Mail.Any(e => e.Opened == false))
            {
                NewMail = true;
            }

            GameScene.Scene.MailListDialog.UpdateInterface();
        }

        private void MailLockedItem(S.MailLockedItem p)
        {
            MirItemCell cell = InventoryDialog.GetCell(p.UniqueID);
            if (cell != null)
                cell.Locked = p.Locked;
        }

        private void MailSendRequest(S.MailSendRequest p)
        {
            MirInputBox inputBox = new MirInputBox(LanguageMgr.Get("EnterMailRecipientName"));

            inputBox.OKButton.Click += (o1, e1) =>
            {
                GameScene.Scene.MailComposeParcelDialog.ComposeMail(inputBox.InputTextBox.Text);
                GameScene.Scene.InventoryDialog.Show();

                //open letter dialog, pass in name
                inputBox.Dispose();
            };

            inputBox.Show();
        }

        private void MailSent(S.MailSent p)
        {
            for (int i = 0; i < InventoryDialog.Grid.Length; i++)
            {
                if (InventoryDialog.Grid[i].Locked == true)
                {
                    InventoryDialog.Grid[i].Locked = false;
                }
            }

            for (int i = 0; i < BeltDialog.Grid.Length; i++)
            {
                if (BeltDialog.Grid[i].Locked == true)
                {
                    BeltDialog.Grid[i].Locked = false;
                }
            }

            GameScene.Scene.MailComposeParcelDialog.Hide();
        }

        private void ParcelCollected(S.ParcelCollected p)
        {
            switch (p.Result)
            {
                case -1:
                    MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("NoParcelsToCollect"), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    break;
                case 0:
                    messageBox = new MirMessageBox(LanguageMgr.Get("AllParcelsCollected"), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    break;
                case 1:
                    GameScene.Scene.MailReadParcelDialog.Hide();
                    break;
            }
        }

        private void ResizeInventory(S.ResizeInventory p)
        {
            Array.Resize(ref User.Inventory, p.Size);
            InventoryDialog.RefreshInventory2();
        }

        private void ResizeStorage(S.ResizeStorage p)
        {
            Array.Resize(ref Storage, p.Size);
            User.HasExpandedStorage = p.HasExpandedStorage;
            User.ExpandedStorageExpiryTime = p.ExpiryTime;

            StorageDialog.RefreshStorage2();
        }

        private void MailCost(S.MailCost p)
        {
            if (GameScene.Scene.MailComposeParcelDialog.Visible)
            {
                if (p.Cost > 0)
                    SoundManager.PlaySound(SoundList.Gold);

                GameScene.Scene.MailComposeParcelDialog.ParcelCostLabel.Text = p.Cost.ToString();
            }
        }

        public void AddQuestItem(UserItem item)
        {
            Redraw();

            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < User.QuestInventory.Length; i++)
                {
                    UserItem temp = User.QuestInventory[i];
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

            for (int i = 0; i < User.QuestInventory.Length; i++)
            {
                if (User.QuestInventory[i] != null) continue;
                User.QuestInventory[i] = item;
                return;
            }
        }

        private void RequestReincarnation()
        {
            if (CMain.Time > User.DeadTime && User.CurrentAction == MirAction.Dead)
            {
                MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("WouldYouLikeToBeRevived"), MirMessageBoxButtons.YesNo);

                messageBox.YesButton.Click += (o, e) => Network.Enqueue(new C.AcceptReincarnation());

                messageBox.Show();
            }
        }

        private void NewIntelligentCreature(S.NewIntelligentCreature p)
        {
            User.IntelligentCreatures.Add(p.Creature);

            MirInputBox inputBox = new MirInputBox(LanguageMgr.Get("PleaseGiveYourCreatureName"));
            inputBox.InputTextBox.Text = GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count - 1].CustomName;
            inputBox.OKButton.Click += (o1, e1) =>
            {
                if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();//refresh changes
                GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count - 1].CustomName = inputBox.InputTextBox.Text;
                Network.Enqueue(new C.UpdateIntelligentCreature { Creature = GameScene.User.IntelligentCreatures[User.IntelligentCreatures.Count - 1] });
                inputBox.Dispose();
            };
            inputBox.Show();
        }

        private void UpdateIntelligentCreatureList(S.UpdateIntelligentCreatureList p)
        {
            User.CreatureSummoned = p.CreatureSummoned;
            User.SummonedCreatureType = p.SummonedCreatureType;
            User.PearlCount = p.PearlCount;
            if (p.CreatureList.Count != User.IntelligentCreatures.Count)
            {
                User.IntelligentCreatures.Clear();
                for (int i = 0; i < p.CreatureList.Count; i++)
                    User.IntelligentCreatures.Add(p.CreatureList[i]);

                for (int i = 0; i < IntelligentCreatureDialog.CreatureButtons.Length; i++)
                    IntelligentCreatureDialog.CreatureButtons[i].Clear();

                IntelligentCreatureDialog.Hide();
            }
            else
            {
                for (int i = 0; i < p.CreatureList.Count; i++)
                    User.IntelligentCreatures[i] = p.CreatureList[i];
                if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();
            }
        }

        private void IntelligentCreatureEnableRename(S.IntelligentCreatureEnableRename p)
        {
            IntelligentCreatureDialog.CreatureRenameButton.Visible = true;
            if (IntelligentCreatureDialog.Visible) IntelligentCreatureDialog.Update();
        }

        private void IntelligentCreaturePickup(S.IntelligentCreaturePickup p)
        {
            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob) && ob is MonsterObject monOb)
                monOb.PlayPickupSound();
        }


        private void FriendUpdate(S.FriendUpdate p)
        {
            FriendDialog.Friends = p.Friends;

            if (FriendDialog.Visible)
                FriendDialog.Update(false);
        }

        private void LoverUpdate(S.LoverUpdate p)
        {
            RelationshipDialog.LoverName = p.Name;
            RelationshipDialog.Date = p.Date;
            RelationshipDialog.MapName = p.MapName;
            RelationshipDialog.MarriedDays = p.MarriedDays;
            RelationshipDialog.UpdateInterface();
        }

        private void MentorUpdate(S.MentorUpdate p)
        {
            MentorDialog.MentorName = p.Name;
            MentorDialog.MentorLevel = p.Level;
            MentorDialog.MentorOnline = p.Online;
            MentorDialog.MenteeEXP = p.MenteeEXP;
            MentorDialog.UpdateInterface();
        }

        private void GameShopUpdate(S.GameShopInfo p)
        {
            p.Item.Stock = p.StockLevel;
            GameShopInfoList.Add(p.Item);
            if (p.Item.Date > CMain.Now.AddDays(-7)) GameShopDialog.New.Visible = true;
        }

        private void GameShopStock(S.GameShopStock p)
        {
            for (int i = 0; i < GameShopInfoList.Count; i++)
            {
                if (GameShopInfoList[i].GIndex == p.GIndex)
                {
                    if (p.StockLevel == 0) GameShopInfoList.Remove(GameShopInfoList[i]);
                    else GameShopInfoList[i].Stock = p.StockLevel;

                    if (GameShopDialog.Visible) GameShopDialog.UpdateShop();
                }
            }
        }
        public void AddItem(UserItem item)
        {
            Redraw();

            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < User.Inventory.Length; i++)
                {
                    UserItem temp = User.Inventory[i];
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

            if (item.Info.Type == ItemType.Potion || item.Info.Type == ItemType.Scroll || (item.Info.Type == ItemType.Script && item.Info.Effect == 1))
            {
                for (int i = 0; i < User.BeltIdx - 2; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }
            else if (item.Info.Type == ItemType.Amulet)
            {
                for (int i = 4; i < User.BeltIdx; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }
            else
            {
                for (int i = User.BeltIdx; i < User.Inventory.Length; i++)
                {
                    if (User.Inventory[i] != null) continue;
                    User.Inventory[i] = item;
                    return;
                }
            }

            for (int i = 0; i < User.Inventory.Length; i++)
            {
                if (User.Inventory[i] != null) continue;
                User.Inventory[i] = item;
                return;
            }
        }
        public static void Bind(UserItem item)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].Index != item.ItemIndex) continue;

                item.Info = ItemInfoList[i];
                if (item.Info.Image > 0) Mir2OriginObjectLibrary.PreloadItemIcon((int)item.Info.Image);

                for (int s = 0; s < item.Slots.Length; s++)
                {
                    if (item.Slots[s] == null) continue;

                    Bind(item.Slots[s]);
                }

                return;
            }
        }

        public ItemInfoTable GetItemInfo(int index)
        {
            var info = ItemInfoList.Find(x => x.Index == index);
            if (info != null && info.Image > 0) Mir2OriginObjectLibrary.PreloadItemIcon((int)info.Image);
            return info;
        }

        public static void BindQuest(ClientQuestProgress quest)
        {
            for (int i = 0; i < QuestInfoList.Count; i++)
            {
                if (QuestInfoList[i].Index != quest.Id) continue;

                quest.QuestInfo = QuestInfoList[i];

                return;
            }
        }

        public Color GradeNameColor(ItemGrade grade)
        {
            switch (grade)
            {
                case ItemGrade.Common:
                    return CommonColor.Yellow;
                case ItemGrade.Rare:
                    return CommonColor.DeepSkyBlue;
                case ItemGrade.Legendary:
                    return CommonColor.DarkOrange;
                case ItemGrade.Mythical:
                    return CommonColor.Plum;
                case ItemGrade.Heroic:
                    return CommonColor.Red;
                default:
                    return CommonColor.Yellow;
            }
        }

        public void DisposeItemLabel()
        {
            if (ItemLabel != null && !ItemLabel.IsDisposed)
                ItemLabel.Dispose();
            ItemLabel = null;
        }
        public void DisposeMailLabel()
        {
            if (MailLabel != null && !MailLabel.IsDisposed)
                MailLabel.Dispose();
            MailLabel = null;
        }
        public void DisposeMemoLabel()
        {
            if (MemoLabel != null && !MemoLabel.IsDisposed)
                MemoLabel.Dispose();
            MemoLabel = null;
        }
        public void DisposeGuildBuffLabel()
        {
            if (GuildBuffLabel != null && !GuildBuffLabel.IsDisposed)
                GuildBuffLabel.Dispose();
            GuildBuffLabel = null;
        }

    }
}
