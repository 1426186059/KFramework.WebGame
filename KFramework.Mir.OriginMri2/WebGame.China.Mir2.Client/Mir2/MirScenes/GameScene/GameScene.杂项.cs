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
        public void CreateItemLabel(UserItem item, bool inspect = false, bool hideDura = false, bool hideAdded = false)
        {
            CMain.DebugText = CMain.Random.Next(1, 100).ToString();

            if (item == null || HoverItem != item)
            {
                DisposeItemLabel();

                if (item == null)
                {
                    HoverItem = null;
                    return;
                }
            }

            if (item == HoverItem && ItemLabel != null && !ItemLabel.IsDisposed) return;
            ushort level = inspect ? InspectDialog.Level : MapObject.User.Level;
            MirClass job = inspect ? InspectDialog.Class : MapObject.User.Class;
            HoverItem = item;
            ItemInfoTable realItem = Functions.GetRealItem(item.Info, level, job, ItemInfoList);

            ItemLabel = new MirControl
            {
                BackColour = CommonColor.FromArgb(255, 0, 0, 0),
                Border = true,
                BorderColour = ((HoverItem.CurrentDura == 0 && HoverItem.MaxDura != 0) ? CommonColor.Red : CommonColor.FromArgb(255, 148, 146, 148)),
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.8F
            };

            //Name Info Label
            MirControl[] outlines = new MirControl[11];
            outlines[0] = NameInfoLabel(item, inspect, hideDura);
            //Attribute Info1 Label - Attack Info
            outlines[1] = AttackInfoLabel(item, inspect, hideAdded);
            //Attribute Info2 Label - Defence Info
            outlines[2] = DefenceInfoLabel(item, inspect, hideAdded);
            //Attribute Info3 Label - Weight Info
            outlines[3] = WeightInfoLabel(item, inspect);
            //Awake Info Label
            outlines[4] = AwakeInfoLabel(item, inspect);
            //Socket Info Label
            outlines[5] = SocketInfoLabel(item, inspect);
            //need Info Label
            outlines[6] = NeedInfoLabel(item, inspect);
            //Bind Info Label
            outlines[7] = BindInfoLabel(item, inspect, hideAdded);
            //Overlap Info Label
            outlines[8] = OverlapInfoLabel(item, inspect);
            //Story Label
            outlines[9] = StoryInfoLabel(item, inspect);
            //GM Made
            outlines[10] = GMMadeLabel(item);

            foreach (var outline in outlines)
            {
                if (outline != null)
                {
                    outline.Size = new Size(ItemLabel.Size.Width, outline.Size.Height);
                }
            }

            //ItemLabel.Visible = true;
        }
        public void CreateMailLabel(ClientMail mail)
        {
            if (mail == null)
            {
                DisposeMailLabel();
                return;
            }

            if (MailLabel != null && !MailLabel.IsDisposed) return;

            MailLabel = new MirControl
            {
                BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = CommonColor.Gray,
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.7F
            };

            MirLabel nameLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.Yellow,
                Location = new Point(4, 4),
                OutLine = true,
                Parent = MailLabel,
                Text = mail.SenderName
            };

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, nameLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, nameLabel.DisplayRectangle.Bottom));

            MirLabel dateLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.White,
                Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = MailLabel,
                Text = LanguageMgr.Get("DateSent", mail.DateSent.ToString("dd/MM/yy H:mm:ss"))
            };

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, dateLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, dateLabel.DisplayRectangle.Bottom));

            if (mail.Gold > 0)
            {
                MirLabel goldLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = CommonColor.White,
                    Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                    OutLine = true,
                    Parent = MailLabel,
                    Text = LanguageMgr.Get("MailGoldLabel")+ mail.Gold
                };

                MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, goldLabel.DisplayRectangle.Right + 4),
                Math.Max(MailLabel.Size.Height, goldLabel.DisplayRectangle.Bottom));
            }

            MirLabel openedLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.Red,
                Location = new Point(4, MailLabel.DisplayRectangle.Bottom),
                OutLine = true,
                Parent = MailLabel,
                Text = mail.Opened ? LanguageMgr.Get("MailHeaderOld"): LanguageMgr.Get("MailHeaderNew")};

            MailLabel.Size = new Size(Math.Max(MailLabel.Size.Width, openedLabel.DisplayRectangle.Right + 4),
            Math.Max(MailLabel.Size.Height, openedLabel.DisplayRectangle.Bottom));
        }
        public void CreateMemoLabel(ClientFriend friend)
        {
            if (friend == null)
            {
                DisposeMemoLabel();
                return;
            }

            if (MemoLabel != null && !MemoLabel.IsDisposed) return;

            MemoLabel = new MirControl
            {
                BackColour = CommonColor.FromArgb(255, 50, 50, 50),
                Border = true,
                BorderColour = CommonColor.Gray,
                DrawControlTexture = true,
                NotControl = true,
                Parent = this,
                Opacity = 0.7F
            };

            MirLabel memoLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.White,
                Location = new Point(4, 4),
                OutLine = true,
                Parent = MemoLabel,
                Text = Functions.StringOverLines(friend.Memo, 5, 20)
            };

            MemoLabel.Size = new Size(Math.Max(MemoLabel.Size.Width, memoLabel.DisplayRectangle.Right + 4),
                Math.Max(MemoLabel.Size.Height, memoLabel.DisplayRectangle.Bottom + 4));
        }

        public string GetUserName(uint id)
        {
            for (int i = 0; i < UserIdList.Count; i++)
            {
                UserId who = UserIdList[i];
                if (id == who.Id)
                    return who.UserName;
            }
            Network.Enqueue(new C.RequestUserName { UserID = id });
            UserIdList.Add(new UserId() { Id = id, UserName = LanguageMgr.Get("UserNameUnknown")});
            return String.Empty;
        }

        public class UserId
        {
            public long Id = 0;
            public string UserName = String.Empty;
        }

        public class OutPutMessage
        {
            public string Message;
            public long ExpireTime;
            public OutputMessageType Type;
        }

        public void Rankings(S.Rankings p)
        {
            foreach (RankCharacterInfo info in p.ListingDetails)
            {
                if (RankingList.ContainsKey(info.PlayerId))
                    RankingList[info.PlayerId] = info;
                else
                    RankingList.Add(info.PlayerId, info);
            }
            List<RankCharacterInfo> listings = new List<RankCharacterInfo>();
            foreach (long id in p.Listings)
                listings.Add(RankingList[id]);

            RankingDialog.RecieveRanks(listings, p.RankType, p.MyRank, p.Count);
        }

        public void Opendoor(S.Opendoor p)
        {
            MapControl.OpenDoor(p.DoorIndex, p.Close);
        }

        private void RentedItems(S.GetRentedItems p)
        {
            ItemRentalDialog.ReceiveRentedItems(p.RentedItems);
        }

        private void ItemRentalRequest(S.ItemRentalRequest p)
        {
            if (!p.Renting)
            {
                GuestItemRentDialog.SetGuestName(p.Name);
                ItemRentingDialog.OpenItemRentalDialog();
            }
            else
            {
                GuestItemRentingDialog.SetGuestName(p.Name);
                ItemRentDialog.OpenItemRentDialog();
            }

            ItemRentalDialog.Visible = false;
        }

        private void ItemRentalFee(S.ItemRentalFee p)
        {
            GuestItemRentDialog.SetGuestFee(p.Amount);
            ItemRentDialog.RefreshInterface();
        }

        private void ItemRentalPeriod(S.ItemRentalPeriod p)
        {
            GuestItemRentingDialog.GuestRentalPeriod = p.Days;
            ItemRentingDialog.RefreshInterface();
        }

        private void DepositRentalItem(S.DepositRentalItem p)
        {
            var fromCell = p.From < User.BeltIdx ? BeltDialog.Grid[p.From] : InventoryDialog.Grid[p.From - User.BeltIdx];
            var toCell = ItemRentingDialog.ItemCell;

            if (toCell == null || fromCell == null)
                return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success)
                return;

            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();

            if (ItemRentingDialog.RentalPeriod == 0)
                ItemRentingDialog.InputRentalPeroid();
        }

        private void RetrieveRentalItem(S.RetrieveRentalItem p)
        {
            var fromCell = ItemRentingDialog.ItemCell;
            var toCell = p.To < User.BeltIdx ? BeltDialog.Grid[p.To] : InventoryDialog.Grid[p.To - User.BeltIdx];

            if (toCell == null || fromCell == null)
                return;

            toCell.Locked = false;
            fromCell.Locked = false;

            if (!p.Success)
                return;

            toCell.Item = fromCell.Item;
            fromCell.Item = null;
            User.RefreshStats();
        }

        private void UpdateRentalItem(S.UpdateRentalItem p)
        {
            GuestItemRentingDialog.GuestLoanItem = p.LoanItem;
            ItemRentDialog.RefreshInterface();
        }

        private void CancelItemRental(S.CancelItemRental p)
        {
            User.RentalGoldLocked = false;
            User.RentalItemLocked = false;

            ItemRentingDialog.Reset();
            ItemRentDialog.Reset();

            var messageBox = new MirMessageBox(LanguageMgr.Get("ItemRentalCancelledFaceOtherParty"));
            messageBox.Show();
        }

        private void ItemRentalLock(S.ItemRentalLock p)
        {
            if (!p.Success)
                return;

            User.RentalGoldLocked = p.GoldLocked;
            User.RentalItemLocked = p.ItemLocked;

            if (User.RentalGoldLocked)
                ItemRentDialog.Lock();
            else if (User.RentalItemLocked)
                ItemRentingDialog.Lock();
        }

        private void ItemRentalPartnerLock(S.ItemRentalPartnerLock p)
        {
            if (p.GoldLocked)
                GuestItemRentDialog.Lock();
            else if (p.ItemLocked)
                GuestItemRentingDialog.Lock();
        }

        private void CanConfirmItemRental(S.CanConfirmItemRental p)
        {
            ItemRentingDialog.EnableConfirmButton();
        }

        private void ConfirmItemRental(S.ConfirmItemRental p)
        {
            User.RentalGoldLocked = false;
            User.RentalItemLocked = false;

            ItemRentingDialog.Reset();
            ItemRentDialog.Reset();
        }

        private void OpenBrowser(S.OpenBrowser p)
        {
            BrowserHelper.OpenDefaultBrowser(p.Url);
        }

        public void PlaySound(S.PlaySound p)
        {
            SoundManager.PlaySound(p.Sound, false);
        }
        private void SetTimer(S.SetTimer p)
        {
            GameScene.Scene.TimerControl.AddTimer(p);
        }

        private void ExpireTimer(S.ExpireTimer p)
        {
            GameScene.Scene.TimerControl.ExpireTimer(p.Key);
        }

        private void SetCompass(S.SetCompass p)
        {
            GameScene.Scene.CompassControl.SetPoint(p.Location);
        }

        private void Roll(S.Roll p)
        {
            GameScene.Scene.RollControl.Setup(p.Type, p.Page, p.Result, p.AutoRoll);
        }

        public void ShowWelcomeNotice(S.UpdateNotice p)
        {
            WelcomeNoticeDialog.Update(p.Notice);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 退出 GameScene（回选人/回登录）时停掉地图背景音乐，否则回选人界面还在播上一张地图的 BGM。
                SoundManager.StopMusic();

                Scene = null;
                User = null;

                // 清掉跨场景残留的静态选中状态（脚底选中环 SelectionTarget/TargetObject 等）。
                // 必须在下面 MapControl=null 之前调用：ID setter 依赖 MapControl.Objects；
                // 本方法直接置字段与 ID，不依赖 MapControl，但顺序放前面更稳妥。
                MapObject.ClearSceneSelection();

                MoveTime = 0;
                AttackTime = 0;
                NextRunTime = 0;
                LastRunTime = 0;
                CanMove = false;
                CanRun = false;

                MapControl = null;
                MainDialog = null;
                ChatDialog = null;
                InventoryDialog = null;
                CharacterDialog = null;
                StorageDialog = null;
                BeltDialog = null;
                MiniMapDialog = null;
                InspectDialog = null;
                OptionDialog = null;
                MenuDialog = null;
                NPCDialog = null;
                QuestDetailDialog = null;
                QuestListDialog = null;
                QuestLogDialog = null;
                QuestTrackingDialog = null;
                GameShopDialog = null;
                MentorDialog = null;

                NewHeroDialog = null;
                HeroInventoryDialog = null;
                HeroDialog = null;
                HeroBeltDialog = null;

                RelationshipDialog = null;

                HoverItem = null;
                SelectedCell = null;
                PickedUpGold = false;

                UseItemTime = 0;
                PickUpTime = 0;
                InspectTime = 0;

                DisposeItemLabel();

                AMode = 0;
                PMode = 0;

                NPCTime = 0;
                NPCID = 0;
                DefaultNPCID = 0;

                for (int i = 0; i < OutputLines.Length; i++)
                    if (OutputLines[i] != null && OutputLines[i].IsDisposed)
                        OutputLines[i].Dispose();

                OutputMessages.Clear();
                OutputMessages = null;
            }

            base.Dispose(disposing);
        }


    }
}
