using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class InventoryDialog : MirImageControl
    {
        /// <summary>第二页未解锁格位上的锁：点击锁才弹解锁提示。</summary>
        public MirButton[] LockCells = new MirButton[InventoryLayout.SlotsPerBagPage];
        public MirItemCell[] Grid;
        public MirItemCell[] QuestGrid;

        public MirButton CloseButton, Bag1Button, Bag2Button, StorageButton;
        public MirButton Bag1SelectButton, Bag2SelectButton, StorageSelectButton;
        public MirButton YiJianShiYongButton, ZhengLiButton, HuiShouButton;

        public MirLabel GoldLabel, WeightLabel;

        private bool _deleteMode;
        private Size _binSize;
        private MirImageControl _deleteCursorIcon;
        public bool DeleteMode => _deleteMode;

        public InventoryDialog()
        {
            Movable = true;
            Sort = true;
            Visible = false;
            Size = new Size(600, 464);
            DrawImage = false;

            var originBackground = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg3.png",
                NotControl = true,
                Size = new Size(542, 464),
                Location = new Point(0, 0)
            };

            CloseButton = new MirButton
            {
                Location = new Point(542, 3),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                Size = new Size(26, 42),
            };
            CloseButton.Click += (o, e) => Hide();

            Bag1Button = new MirButton
            {
                Location = new Point(542, 50),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page0",
                Size = new Size(27, 79),
            };
            Bag1Button.Click += Button_Click;
            Bag1Button.Label.Text = "背包";
            Bag1Button.Label.OutLine = true;
            Bag1Button.Label.ForeColour = Color.Gray;
            Bag1Button.Label.Location = new Point(0, 10);

            Bag2Button = new MirButton
            {
                Location = new Point(542, 120),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page0",
                Size = new Size(27, 79),
            };
            Bag2Button.Click += Button_Click;
            Bag2Button.Label.Text = "背包";
            Bag2Button.Label.OutLine = true;
            Bag2Button.Label.ForeColour = Color.Gray;
            Bag2Button.Label.Location = new Point(0, 10);

            StorageButton = new MirButton
            {
                Location = new Point(542, 190),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page0",
                Size = new Size(27, 79),
            };
            StorageButton.Click += Button_Click;
            StorageButton.Label.Text = "仓库";
            StorageButton.Label.OutLine = true;
            StorageButton.Label.ForeColour = Color.Gray;
            StorageButton.Label.Location = new Point(0, 10);


            Bag1SelectButton = new MirButton
            {
                Location = new Point(542, 50),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page1",
                Size = new Size(34, 92),
                NotControl = true,
            };
            Bag1SelectButton.Label.Text = "背包";
            Bag1SelectButton.Label.OutLine = true;
            Bag1SelectButton.Label.ForeColour = Color.White;
            Bag1SelectButton.Label.Location = new Point(5, 10);
            Bag1SelectButton.Label.Size = new Size(20, 92);

            Bag2SelectButton = new MirButton
            {
                Location = new Point(542, 120),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page1",
                Size = new Size(34, 92),
                NotControl = true,
            };
            Bag2SelectButton.Label.Text = "背包";
            Bag2SelectButton.Label.OutLine = true;
            Bag2SelectButton.Label.ForeColour = Color.White;
            Bag2SelectButton.Label.Location = new Point(5, 10);
            Bag2SelectButton.Label.Size = new Size(20, 92);

            StorageSelectButton = new MirButton
            {
                Location = new Point(542, 190),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page1",
                Size = new Size(34, 92),
                NotControl = true,
            };
            StorageSelectButton.Label.Text = "仓库";
            StorageSelectButton.Label.OutLine = true;
            StorageSelectButton.Label.ForeColour = Color.White;
            StorageSelectButton.Label.Location = new Point(5, 10);
            StorageSelectButton.Label.Size = new Size(20, 92);

            YiJianShiYongButton = new MirButton
            {
                Location = new Point(250, 410),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn0",
                Size = new Size(27, 79),
            
            };
            YiJianShiYongButton.Click += Button_Click;
            YiJianShiYongButton.Label.Text = "一键使用";
            YiJianShiYongButton.Label.OutLine = true;
            YiJianShiYongButton.Label.ForeColour = Color.Yellow;
            YiJianShiYongButton.Label.Alignment = KFramework.MonoGame.TextAnchor.MiddleCenter;

            ZhengLiButton = new MirButton
            {
                Location = new Point(340, 410),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn0",
                Size = new Size(27, 79),
            };
            ZhengLiButton.Click += (o, e) => Network.Enqueue(new C.SortInventory());
            ZhengLiButton.Label.Text = "整理";
            ZhengLiButton.Label.OutLine = true;
            ZhengLiButton.Label.ForeColour = Color.Yellow;
            ZhengLiButton.Label.Alignment = KFramework.MonoGame.TextAnchor.MiddleCenter;

            HuiShouButton = new MirButton
            {
                Location = new Point(430, 410),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn0",
                Size = new Size(27, 79),
            };
            HuiShouButton.Click += Button_Click;
            HuiShouButton.Label.Text = "回收";
            HuiShouButton.Label.OutLine = true;
            HuiShouButton.Label.ForeColour = Color.Yellow;
            HuiShouButton.Label.Alignment = KFramework.MonoGame.TextAnchor.MiddleCenter;

            var mCoin = new MirImageControl
            {
                Parent = this,
                AtlasNameKey = "y_bag-sheet#bag_coin0",
                Location = new Point(10, 420),
                NotControl = true,
                Size = new Size(35, 21),
            };

            GoldLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(80, 415),
                AutoSize = true,
                Sound = SoundList.Gold,
            };

            GoldLabel.Click += (o, e) =>
            {
                if (GameScene.SelectedCell == null)
                {
                    GameScene.PickedUpGold = !GameScene.PickedUpGold && GameScene.Gold > 0;
                }
            };

            // 两页各 8 x 6 = 48 格，共 96 个背包格；5 个快捷栏格由 BeltDialog 单独显示。
            // Grid 下标 = 数组下标 - BeltSlots，GameScene 里所有 p.From / p.To 都按此换算。
            Grid = new MirItemCell[InventoryLayout.BagSlots];
            for (int idx = 0; idx < Grid.Length; idx++)
            {
                int x = idx % InventoryLayout.BagColumns;
                int y = idx / InventoryLayout.BagColumns;

                Grid[idx] = new MirItemCell
                {
                    ItemSlot = InventoryLayout.BeltSlots + idx,
                    GridType = MirGridType.Inventory,
                    Parent = this,
                    Size = new Size(60, 60),
                    // 60x60 格子 + 3px 间距（对齐原始传奇 BagPopSkin _TileLayout1）。
                    // 两页复用同一显示区，y % BagRows 让第二页回到顶部。
                    Location = new Point(x * 63 + 20, y % InventoryLayout.BagRows * 63 + 20),
                    Visible = false,
                };
            }

            // 第二页未解锁的格位显示锁，点击锁才弹扩容提示（点标签页本身不再弹）。
            for (int i = 0; i < LockCells.Length; i++)
            {
                int x = i % InventoryLayout.BagColumns;
                int y = i / InventoryLayout.BagColumns;

                LockCells[i] = new MirButton
                {
                    Parent = this,
                    AtlasNameKey = "y_bag-sheet",
                    AtlasFrame = "storageSuo",
                    Size = new Size(60, 60),
                    Location = new Point(x * 63 + 20, y * 63 + 20),
                    Sound = SoundList.ButtonA,
                    Visible = false,
                };
                LockCells[i].Click += (o, e) => PromptUnlockSlot();
            }

        }

        void Button_Click(object sender, EventArgs e)
        {
            // Ctrl + Left-click: move selected item to the tab's bag without switching tabs.
            if (GameScene.SelectedCell != null &&
                e is MouseEventArgs me &&
                me.Button == MouseButtons.Left &&
                (Control.ModifierKeys == Keys.ControlLeft))
            {
                if (sender == Bag1Button)
                {
                    TryMoveSelectedInventoryItem(false);
                }
                else if (sender == Bag2Button)
                {
                    TryMoveSelectedInventoryItem(true);
                }
                return;
            }

            if (sender == Bag1Button)
            {
                RefreshInventory();
            }
            else if (sender == Bag2Button)
            {
                // 第二页始终可以打开，锁着的格子由 LockCells 自己弹提示。
                RefreshInventory2();
            }
        }

        /// <summary>只有点击未解锁的格子（锁）才询问是否扩容。</summary>
        private void PromptUnlockSlot()
        {
            if (GameScene.User.Inventory.Length >= InventoryLayout.MaxSlots) return;

            int openLevel = (GameScene.User.Inventory.Length - InventoryLayout.InitialSlots) / InventoryLayout.InventoryExpandStep;
            uint openGold = (uint)(InventoryLayout.InventoryExpandBaseGold * (1 + openLevel));

            MirMessageBox messageBox = new MirMessageBox(
                GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ExtraSlots4, openGold),
                MirMessageBoxButtons.OKCancel);

            messageBox.OKButton.Click += (o, a) =>
            {
                Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
            };
            messageBox.Show();
        }

        private bool TryMoveSelectedInventoryItem(bool toSecondBag)
        {
            // Allow dragging an item (inventory or belt) onto a bag tab to move it to that bag page.
            var selected = GameScene.SelectedCell;
            if (selected == null || selected.Item == null || selected.Locked || selected.GridType != MirGridType.Inventory)
                return false;

            int firstBagStart = InventoryLayout.BeltSlots;
            int secondBagStart = InventoryLayout.FirstPageEndSlot;
            int inventoryLength = GameScene.User.Inventory.Length;

            bool targetIsSecond = toSecondBag;
            if (targetIsSecond && inventoryLength <= secondBagStart) return false;          // no expanded bag
            if (targetIsSecond && selected.ItemSlot >= secondBagStart) return false;        // already there
            if (!targetIsSecond && selected.ItemSlot >= firstBagStart && selected.ItemSlot < secondBagStart) return false; // already in first bag

            int start = targetIsSecond ? secondBagStart : firstBagStart;
            int end = targetIsSecond ? inventoryLength : secondBagStart;

            int? targetSlot = FindFirstEmptySlot(start, end, GameScene.User.Inventory);
            if (targetSlot == null) return false; // no space

            Network.Enqueue(new C.MoveItem { Grid = MirGridType.Inventory, From = selected.ItemSlot, To = targetSlot.Value });

            selected.Locked = true;
            MirItemCell targetCell = Grid.FirstOrDefault(c => c.ItemSlot == targetSlot.Value);
            if (targetCell != null)
                targetCell.Locked = true;

            GameScene.SelectedCell = null;
            return true;
        }

        private static int? FindFirstEmptySlot(int start, int end, UserItem[] items)
        {
            for (int i = start; i < end; i++)
            {
                if (items[i] == null)
                    return i;
            }
            return null;
        }

        // 页签选中态由 AtlasFrame 接管：AtlasNameKey 用裸图集名（不带 #帧），
        // 加载完成时不会覆盖这里设置的帧；未加载好时老 .Lib 的 Index 切换照常回退。
        private void SetBagTab(int active)
        {
            Bag1SelectButton.Visible = false;
            Bag2SelectButton.Visible = false;
            StorageSelectButton.Visible = false;

            if (active == 0)
            {
                Bag1SelectButton.Visible = true;
            }
            else if (active == 1)
            {
                Bag2SelectButton.Visible = true;
            }
            else
            {
                Bag1SelectButton.Visible = true;
            }
        }

        void Reset()
        {
            foreach (MirItemCell grid in Grid)
            {
                grid.Visible = false;
            }

            foreach (MirButton lockCell in LockCells)
            {
                lockCell.Visible = false;
            }
        }

        public void RefreshInventory()
        {
            Reset();
            SetBagTab(0);

            // 第一页在开局就已全部解锁。
            for (int i = 0; i < InventoryLayout.SlotsPerBagPage; i++)
            {
                Grid[i].Visible = true;
            }
        }

        public void RefreshInventory2()
        {
            Reset();
            SetBagTab(1);

            int unlocked = GameScene.User.Inventory.Length;

            for (int i = 0; i < InventoryLayout.SlotsPerBagPage; i++)
            {
                MirItemCell cell = Grid[InventoryLayout.SlotsPerBagPage + i];
                bool isUnlocked = cell.ItemSlot < unlocked;

                cell.Visible = isUnlocked;
                LockCells[i].Visible = !isUnlocked;
            }
        }

        public void Process()
        {
            GoldLabel.Text = GameScene.Gold.ToString("###,###,##0");
        }

        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }

        public MirItemCell GetQuestCell(ulong id)
        {
            return QuestGrid.FirstOrDefault(t => t.Item != null && t.Item.UniqueID == id);
        }

        public void DisplayItemGridEffect(ulong id, int type = 0)
        {
            MirItemCell cell = GetCell(id);

            if (cell.Item == null) return;

            MirAnimatedControl animEffect = null;

            switch (type)
            {
                case 0:
                    animEffect = new MirAnimatedControl
                    {
                        Animated = true,
                        AnimationCount = 9,
                        AnimationDelay = 150,
                        Index = 410,
                        Library = Libraries.Prguse,
                        Location = cell.Location,
                        Parent = this,
                        Loop = false,
                        NotControl = true,
                        UseOffSet = true,
                        Blending = true,
                        BlendingRate = 1F
                    };
                    animEffect.AfterAnimation += (o, e) => animEffect.Dispose();
                    SoundManager.PlaySound(20000 + (ushort)Spell.MagicShield * 10);
                    break;
            }
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            if (_deleteMode) UpdateDeleteCursorPos();
            base.OnMouseMove(e);
        }

        private void DelItemButton_Click(object sender, EventArgs e)
        {
            if (GameScene.SelectedCell != null &&
                GameScene.SelectedCell.GridType == MirGridType.Inventory &&
                GameScene.SelectedCell.Item != null)
            {
                PromptDelete(GameScene.SelectedCell);
                return;
            }
            ToggleDeleteMode(!_deleteMode);
        }
        public override void OnMouseClick(MouseEventArgs e)
        {
            // Right-click anywhere on the inventory cancels the bin toggle
            if (_deleteMode && e.Button == MouseButtons.Right)
            {
                ToggleDeleteMode(false);
                return;
            }

            base.OnMouseClick(e);
        }

        public void ToggleDeleteMode(bool on)
        {
            _deleteMode = on;

            if (_deleteCursorIcon != null)
            {
                _deleteCursorIcon.Visible = on;

                if (on)
                {
                    // Use the same library as you created the icon with (Prguse2)
                    _deleteCursorIcon.Index = 366;

                    _binSize = _deleteCursorIcon.Library != null
                        ? _deleteCursorIcon.Library.GetSize(366)
                        : Size.Empty;

                    UpdateDeleteCursorPos();
                }
            }

            SoundManager.PlaySound(on ? SoundList.ButtonA : SoundList.ButtonB);
        }

        private void UpdateDeleteCursorPos()
        {
            if (!_deleteMode || _deleteCursorIcon == null || !_deleteCursorIcon.Visible) return;

            // Top-center above the pointer: bottom edge of the icon touches the cursor
            int x = CMain.MPoint.X - (_binSize.Width / 2);
            int y = CMain.MPoint.Y - _binSize.Height;

            _deleteCursorIcon.Location = new Point(x, y);
            _deleteCursorIcon.BringToFront();
        }

        public void PromptDelete(MirItemCell cell)
        {
            if (cell == null || cell.Item == null) return;

            var item = cell.Item;
            var name = item.FriendlyName;

            void CancelDelete()
            {
                GameScene.SelectedCell = null;
                cell.Locked = false;
                cell.Opacity = 1F;
                cell.Redraw();

                ToggleDeleteMode(false);
            }

            void DoDelete(ushort amt)
            {
                SendDeleteItem(item.UniqueID, amt, false);

                CancelDelete();
            }

            if (item.Count > 1)
            {
                var amountPrompt = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DeleteItemAmountPrompt, name);
                var amountBox = new MirAmountBox(amountPrompt, item.Image, item.Count);

                amountBox.OKButton.Click += (o, a) =>
                {
                    var amt = (ushort)Math.Max(1, Math.Min(amountBox.Amount, item.Count));
                    DoDelete(amt);
                };

                // Cancel closes the amount box AND exits delete mode
                if (amountBox.CancelButton != null)
                    amountBox.CancelButton.Click += (o, a) => CancelDelete();

                amountBox.Show();
            }
            else
            {
                var confirmPrompt = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DeleteItemConfirmPrompt, name);
                var mb = new MirMessageBox(confirmPrompt, MirMessageBoxButtons.YesNo);
                mb.YesButton.Click += (o, a) => { DoDelete(1); };
                mb.NoButton.Click += (o, a) => { CancelDelete(); };
                mb.Show();
            }
        }

        private void SendDeleteItem(ulong uniqueId, ushort count, bool heroInventory)
        {
            Network.Enqueue(new C.DeleteItem { UniqueID = uniqueId, Count = count, HeroInventory = heroInventory });
        }

        public override void Show()
        {
            base.Show();
            Location = new Point(Settings.ScreenWidth / 2 - 100, Settings.ScreenHeight / 2 - 300);
        }

    }

    public sealed class BeltDialog : MirImageControl
    {
        public MirLabel[] Key = new MirLabel[5];
        public MirItemCell[] Grid;

        public BeltDialog()
        {
            AtlasNameKey = "y_homeUI-sheet";
            AtlasFrame = "bg_daoju_1";

            Movable = false;
            Sort = true;
            Visible = true;

            Size = new Size(286, 58);
            Anchor = EAnchorType.BottomCenter;
            AnchorPos = new Point(0, -155);
            ApplyAnchor();

            var m1 = new MirImageControl
            {
                Parent = this,
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "bg_daoju_2",
                Location = new Point(-75, 20),
            };

            var m2 = new MirImageControl
            {
                Parent = this,
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "bg_daoju_3",
                Location = new Point(this.Size.Width + 2, 20),
            };

            for (int i = 0; i < Key.Length; i++)
            {
                Key[i] = new MirLabel
                {
                    Parent = this,
                    Size = new Size(26, 14),
                    Location = new Point(8 + i * 57, 2),
                    Text = (i + 1).ToString()
                };
            }

            Grid = new MirItemCell[5];

            for (int x = 0; x < Grid.Length; x++)
            {
                Grid[x] = new MirItemCell
                {
                    ItemSlot = x,
                    GridType = MirGridType.Inventory,
                    Library = Libraries.Items,
                    Parent = this,
                    Location = new Point(x * 63 + 12, 3),
                };
            }

        }

        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item == null || Grid[i].Item.UniqueID != id) continue;
                return Grid[i];
            }
            return null;
        }
    }
}
