using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using System.Globalization;
using System.Text.RegularExpressions;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class NPCGoodsDialog : MirImageControl
    {
        public PanelType PType;
        public bool UsePearls;

        public int StartIndex;
        public UserItem SelectedItem;

        public List<UserItem> Goods = new List<UserItem>();
        public List<UserItem> DisplayGoods = new List<UserItem>();
        public MirGoodsCell[] Cells;
        public MirButton BuyButton, CloseButton;
        public MirImageControl BuyLabel;

        public MirButton UpButton, DownButton, PositionBar;

        public NPCGoodsDialog(PanelType type)
        {
            PType = type;

            Index = 1000;
            Library = Libraries.Prguse;
            Location = new Point(0, 224);
            Cells = new MirGoodsCell[8];
            Sort = true;

            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i] = new MirGoodsCell
                {
                    Parent = this,
                    Location = new Point(10, 34 + i * 33),
                    Sound = SoundList.ButtonC
                };
                Cells[i].Click += (o, e) =>
                {
                    SelectedItem = ((MirGoodsCell)o).Item;
                    Update();

                    if (PType == PanelType.Craft)
                    {
                        GameScene.Scene.CraftDialog.ResetCells();
                        GameScene.Scene.CraftDialog.RefreshCraftCells(SelectedItem);

                        if (!GameScene.Scene.CraftDialog.Visible)
                        {
                            GameScene.Scene.CraftDialog.Show();
                        }
                    }
                };
                Cells[i].MouseWheel += NPCGoodsPanel_MouseWheel;
                Cells[i].DoubleClick += (o, e) =>
                {
                    if (PType == PanelType.Craft) return;

                    BuyItem();
                };
            }

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(217, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            BuyButton = new MirButton
            {
                HoverIndex = 313,
                Index = 312,
                Location = new Point(77, 304),
                Library = Libraries.Title,
                Parent = this,
                PressedIndex = 314,
                Sound = SoundList.ButtonA,
            };
            BuyButton.Click += (o, e) => BuyItem();

            BuyLabel = new MirImageControl
            {
                Index = 27,
                Library = Libraries.Title,
                Parent = this,
                Location = new Point(20, 9),
            };

            if (PType == PanelType.Craft)
            {
                BuyLabel.Index = 12;
                BuyButton.Visible = false;
            }

            UpButton = new MirButton
            {
                Index = 197,
                HoverIndex = 198,
                Library = Libraries.Prguse2,
                Location = new Point(219, 35),
                Parent = this,
                PressedIndex = 199,
                Sound = SoundList.ButtonA
            };
            UpButton.Click += (o, e) =>
            {
                if (StartIndex == 0) return;
                StartIndex--;
                Update();
            };

            DownButton = new MirButton
            {
                Index = 207,
                HoverIndex = 208,
                Library = Libraries.Prguse2,
                Location = new Point(219, 284),
                Parent = this,
                PressedIndex = 209,
                Sound = SoundList.ButtonA
            };
            DownButton.Click += (o, e) =>
            {
                if (DisplayGoods.Count <= 8) return;

                if (StartIndex == DisplayGoods.Count - 8) return;
                StartIndex++;
                Update();
            };

            PositionBar = new MirButton
            {
                Index = 205,
                HoverIndex = 206,
                Library = Libraries.Prguse2,
                Location = new Point(219, 49),
                Parent = this,
                PressedIndex = 206,
                Movable = true,
                Sound = SoundList.None
            };
            PositionBar.OnMoving += PositionBar_OnMoving;
            PositionBar.MouseUp += (o, e) => Update();
        }

        private bool CheckSubGoods()
        {
            if (SelectedItem == null) return false;

            if (PType == PanelType.Buy && !UsePearls)
            {
                var list = Goods.Where(x => x.Info.Index == SelectedItem.Info.Index).ToList();

                if (list.Count > 1 || GameScene.Scene.NPCSubGoodsDialog.Visible)
                {
                    GameScene.Scene.NPCSubGoodsDialog.NewGoods(list);
                    GameScene.Scene.NPCSubGoodsDialog.Show();
                    return true;
                }
            }

            return false;
        }

        private void BuyItem()
        {
            if (SelectedItem == null) return;

            if (CheckSubGoods())
            {
                return;
            }

            if (SelectedItem.Info.StackSize > 1)
            {
                ushort tempCount = SelectedItem.Count;
                ushort maxQuantity = (ushort)Math.Min(SelectedItem.Info.StackSize, ushort.MaxValue);

                SelectedItem.Count = maxQuantity;

                if (UsePearls)
                {
                    if (SelectedItem.Price() > GameScene.User.PearlCount)
                    {
                        maxQuantity = Math.Min(ushort.MaxValue, (ushort)(GameScene.Gold / (SelectedItem.Price() / SelectedItem.Count)));
                        if (maxQuantity == 0)
                        {
                            GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.NotEnoughPearls), ChatType.System);
                            return;
                        }
                    }
                }

                else if (SelectedItem.Price() > GameScene.Gold)
                {
                    maxQuantity = Math.Min(ushort.MaxValue, (ushort)(GameScene.Gold / (SelectedItem.Price() / SelectedItem.Count)));
                    if (maxQuantity == 0)
                    {
                        GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LowGold), ChatType.System);
                        return;
                    }
                }

                MapObject.User.GetMaxGain(SelectedItem);

                if (SelectedItem.Count == 0)
                {
                    SelectedItem.Count = tempCount;
                    GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.YouNoBagSpace), ChatType.System);
                    return;
                }

                if (SelectedItem.Count < maxQuantity)
                {
                    maxQuantity = SelectedItem.Count;
                }

                if (SelectedItem.Count > tempCount)
                {
                    SelectedItem.Count = tempCount;
                }

                MirAmountBox amountBox = new(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PurchaseAmount), SelectedItem.Image, maxQuantity, 0, SelectedItem.Count);

                amountBox.OKButton.Click += (o, e) =>
                {
                    if (amountBox.Amount > 0)
                    {
                        Network.Enqueue(new C.BuyItem { ItemIndex = SelectedItem.UniqueID, Count = (ushort)amountBox.Amount, Type = PanelType.Buy });
                    }
                };

                amountBox.Show();
            }
            else
            {
                if (SelectedItem.Info.Price > GameScene.Gold)
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LowGold), ChatType.System);
                    return;
                }

                for (int i = 0; i < MapObject.User.Inventory.Length; i++)
                {
                    if (MapObject.User.Inventory[i] == null) break;
                    if (i == MapObject.User.Inventory.Length - 1)
                    {
                        GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.YouNoBagSpace), ChatType.System);
                        return;
                    }
                }

                Network.Enqueue(new C.BuyItem { ItemIndex = SelectedItem.UniqueID, Count = 1, Type = PanelType.Buy });
            }
        }

        private void NPCGoodsPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (StartIndex == 0 && count >= 0) return;
            if (StartIndex == DisplayGoods.Count - 1 && count <= 0) return;

            StartIndex -= count;
            Update();
        }
        private void Update()
        {
            if (StartIndex > DisplayGoods.Count - 8) StartIndex = DisplayGoods.Count - 8;
            if (StartIndex <= 0) StartIndex = 0;

            if (DisplayGoods.Count > 8)
            {
                PositionBar.Visible = true;
                int h = 233 - PositionBar.Size.Height;
                h = (int)((h / (float)(DisplayGoods.Count - 8)) * StartIndex);
                PositionBar.Location = new Point(219, 49 + h);
            }
            else
                PositionBar.Visible = false;


            for (int i = 0; i < 8; i++)
            {
                if (i + StartIndex >= DisplayGoods.Count)
                {
                    Cells[i].Visible = false;
                    continue;
                }
                Cells[i].Visible = true;

                var matchingGoods = Goods.Where(x => x.Info.Index == Cells[i].Item.Info.Index);

                Cells[i].Item = DisplayGoods[i + StartIndex];
                Cells[i].MultipleAvailable = matchingGoods.Count() > 1 && matchingGoods.Any(x => x.IsShopItem == false);
                Cells[i].Border = SelectedItem != null && Cells[i].Item == SelectedItem;
                Cells[i].UsePearls = UsePearls;
            }
        }

        private void PositionBar_OnMoving(object sender, MouseEventArgs e)
        {
            const int x = 219;
            int y = PositionBar.Location.Y;
            if (y >= 282 - PositionBar.Size.Height) y = 282 - PositionBar.Size.Height;
            if (y < 49) y = 49;

            int h = 233 - PositionBar.Size.Height;
            h = (int)Math.Round(((y - 49) / (h / (float)(DisplayGoods.Count - 8))));

            PositionBar.Location = new Point(x, y);

            if (h == StartIndex) return;
            StartIndex = h;
            Update();
        }

        public void NewGoods(IEnumerable<UserItem> list)
        {
            Goods.Clear();
            DisplayGoods.Clear();

            AddGoods(list);
        }

        public void AddGoods(IEnumerable<UserItem> list)
        {
            if (PType == PanelType.BuySub)
            {
                StartIndex = 0;
                SelectedItem = null;

                list = list.OrderBy(x => x.Price());
            }

            foreach (UserItem item in list)
            {
                //Normal shops just want to show one of each item type
                if (PType == PanelType.Buy && !UsePearls)
                {
                    Goods.Add(item);

                    if (DisplayGoods.Any(x => x.Info.Index == item.Info.Index)) continue;
                }

                DisplayGoods.Add(item);
            }

            if (GameScene.Scene.NPCSubGoodsDialog.Visible)
            {
                CheckSubGoods();
            }

            Update();
        }

        public override void Hide()
        {
            Visible = false;

            if (GameScene.Scene.CraftDialog.Visible)
            {
                GameScene.Scene.CraftDialog.Hide();
            }
        }

        public override void Show()
        {
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i].Recipe = PType == PanelType.Craft;
            }

            Location = new Point(Location.X, GameScene.Scene.NPCDialog.Size.Height);
            Visible = true;

            GameScene.Scene.InventoryDialog.Show();
        }
    }
}
