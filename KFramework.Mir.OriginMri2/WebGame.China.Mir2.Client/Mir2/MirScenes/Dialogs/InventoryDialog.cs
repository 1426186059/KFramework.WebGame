using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class InventoryDialog : MirImageControl
    {
        public MirImageControl WeightBar;
        public MirImageControl[] LockBar = new MirImageControl[10];
        public MirItemCell[] Grid;
        public MirItemCell[] QuestGrid;

        public MirButton CloseButton, ItemButton, ItemButton2, QuestButton, AddButton, DelItemButton;
        public MirLabel GoldLabel, WeightLabel;

        private bool _deleteMode;
        private Size _binSize;
        private MirImageControl _deleteCursorIcon;
        public bool DeleteMode => _deleteMode;

        public InventoryDialog()
        {
            Index = 196;
            Library = Libraries.Title;
            Movable = true;
            Sort = true;
            Visible = false;

            // 《原始传奇》背包背景：manifest 逻辑名 resource/assets/gameui4/jpg/bag_bg3.png
            // （CDN 哈希 resource/73/73844c34_153164.png，542×464，自带 8×6 格子线与底部金币条）。
            // 必须走 ImageKey 经 manifest 解析，直接拼 CDN 路径会 404。整图拉伸到面板尺寸，
            // 背景已画好格子，故不再额外叠格子底框；加载失败时自动露出老 .Lib 背景。
            var originBackground = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg3.png",
                AutoSize = false,
                NotControl = true,
            };
            originBackground.BeforeDraw += (o, e) => originBackground.Size = Size;

            // 金币堆：y_bag-sheet 的 bag_coin0 原生 35×21，按原比例贴在 GoldLabel 左侧，
            // 正好落在 bag_bg3 背景左下角的圆形凹槽里。
            new MirImageControl
            {
                Parent = this,
                AtlasNameKey = "y_bag-sheet#bag_coin0",
                AutoSize = false,
                Size = new Size(35, 21),
                Location = new Point(4, 209),
                NotControl = true,
            };

            WeightBar = new MirImageControl
            {
                Index = 24,
                Library = Libraries.Prguse,
                Location = new Point(182, 217),
                Parent = this,
                DrawImage = false,
                NotControl = true,
            };

            // 三个页签用《原始传奇》背包按钮底：bag_Btn0(蓝/选中)、bag_Btn1(棕/未选中)，原生 82×29。
            // AutoSize=false 固定成原来的 72×23 命中区，页签间距不变；图集未就绪时老 .Lib 帧仍是回退。
            ItemButton = new MirButton
            {
                Index = 197,
                Library = Libraries.Title,
                Location = new Point(6, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn0",
                AutoSize = false,
            };
            ItemButton.Click += Button_Click;

            ItemButton2 = new MirButton
            {
                Index = 738,
                Library = Libraries.Title,
                Location = new Point(76, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn1",
                AutoSize = false,
            };
            ItemButton2.Click += Button_Click;

            QuestButton = new MirButton
            {
                Index = 739,
                Library = Libraries.Title,
                Location = new Point(146, 7),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_bag-sheet",
                AtlasFrame = "bag_Btn1",
                AutoSize = false,
            };
            QuestButton.Click += Button_Click;

            AddButton = new MirButton
            {
                Index = 483,
                HoverIndex = 484,
                PressedIndex = 485,
                Library = Libraries.Title,
                Location = new Point(235, 5),
                Parent = this,
                Size = new Size(72, 23),
                Sound = SoundList.ButtonA,
                Visible = false,
            };
            AddButton.Click += (o1, e) =>
            {
                int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
                int openGold = (1000000 + openLevel * 1000000);
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ExtraSlots4), openGold), MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(289, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
                // 《原始传奇》关闭按钮；图集未就绪时上面的 .Lib 帧仍是回退。
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                HoverAtlasFrame = "com_btn_close1",
                AutoSize = false,
                Size = new Size(20, 20),
            };
            CloseButton.Click += (o, e) => Hide();

            // 删除物品开关仍用老 .Lib 图标：y_bag-sheet 里 bag_recy1 是 87×23 的「快速回收」标题文字，
            // 不是小图标，压成 18×18 会糊掉；底部条也没有放下整块文字的空间，故暂不接入。
            // 同图集的 storageSuo（锁，43×49）对应 LockBar，尺寸/可见性逻辑不同，也先不动。
            DelItemButton = new MirButton
            {
                Index = 366,
                HoverIndex = 367,
                PressedIndex = 368,
                Location = new Point(291, 212),
                Library = Libraries.Prguse2,
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            DelItemButton.Click += DelItemButton_Click;

            _deleteCursorIcon = new MirImageControl
            {
                Parent = GameScene.Scene,
                Library = Libraries.Prguse2,
                Index = 366,
                NotControl = true,
                Visible = false
            };

            GoldLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(40, 212),
                Size = new Size(111, 14),
                Sound = SoundList.Gold,
            };
            GoldLabel.Click += (o, e) =>
            {
                if (GameScene.SelectedCell == null)
                    GameScene.PickedUpGold = !GameScene.PickedUpGold && GameScene.Gold > 0;
            };


            Grid = new MirItemCell[8 * 10];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    int idx = 8 * y + x;
                    Grid[idx] = new MirItemCell
                    {
                        ItemSlot = 6 + idx,
                        GridType = MirGridType.Inventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y % 5 * 32 + 37 + y % 5),
                    };

                    if (idx >= 40)
                        Grid[idx].Visible = false;
                }
            }

            QuestGrid = new MirItemCell[8 * 5];

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    QuestGrid[8 * y + x] = new MirItemCell
                    {
                        ItemSlot = 8 * y + x,
                        GridType = MirGridType.QuestInventory,
                        Library = Libraries.Items,
                        Parent = this,
                        Location = new Point(x * 36 + 9 + x, y * 32 + 37 + y),
                        Visible = false
                    };
                }
            }

            WeightLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(268, 212),
                Size = new Size(26, 14)
            };
            WeightBar.BeforeDraw += WeightBar_BeforeDraw;

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i] = new MirImageControl
                {
                    Index = 307,
                    Library = Libraries.Prguse2,
                    Location = new Point(9 + i % 2 * 148, 37 + i / 2 * 33),
                    Parent = this,
                    DrawImage = true,
                    NotControl = true,
                    Visible = false,
                };
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
                if (sender == ItemButton)
                {
                    TryMoveSelectedInventoryItem(false);
                }
                else if (sender == ItemButton2)
                {
                    TryMoveSelectedInventoryItem(true);
                }
                return;
            }

            if (GameScene.User.Inventory.Length == 46 && sender == ItemButton2)
            {
                MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ExtraSlots8), MirMessageBoxButtons.OKCancel);

                messageBox.OKButton.Click += (o, a) =>
                {
                    Network.Enqueue(new C.Chat { Message = "@ADDINVENTORY" });
                };
                messageBox.Show();
            }
            else
            {
                if (sender == ItemButton)
                {
                    RefreshInventory();
                }
                else if (sender == ItemButton2)
                {
                    RefreshInventory2();
                }
                else if (sender == QuestButton)
                {
                    Reset();
                    SetBagTab(2);

                    ItemButton.Index = 737;
                    ItemButton2.Index = 738;
                    QuestButton.Index = 198;

                    if (GameScene.User.Inventory.Length == 46)
                    {
                        ItemButton2.Index = 169;
                    }

                    foreach (var grid in QuestGrid)
                    {
                        grid.Visible = true;
                    }
                }
            }
        }

        private bool TryMoveSelectedInventoryItem(bool toSecondBag)
        {
            // Allow dragging an item (inventory or belt) onto a bag tab to move it to that bag page.
            var selected = GameScene.SelectedCell;
            if (selected == null || selected.Item == null || selected.Locked || selected.GridType != MirGridType.Inventory)
                return false;

            const int firstBagVisibleSlots = 40; // 40 slots per bag page, belt is handled separately.
            int firstBagStart = GameScene.User.BeltIdx;
            int secondBagStart = firstBagStart + firstBagVisibleSlots;
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
            ItemButton.AtlasFrame = active == 0 ? "bag_Btn0" : "bag_Btn1";
            ItemButton2.AtlasFrame = active == 1 ? "bag_Btn0" : "bag_Btn1";
            QuestButton.AtlasFrame = active == 2 ? "bag_Btn0" : "bag_Btn1";
        }

        void Reset()
        {
            foreach (MirItemCell grid in QuestGrid)
            {
                grid.Visible = false;
            }

            foreach (MirItemCell grid in Grid)
            {
                grid.Visible = false;
            }

            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = false;
            }

            AddButton.Visible = false;
        }



        public void RefreshInventory()
        {
            Reset();
            SetBagTab(0);

            ItemButton.Index = 197;
            ItemButton2.Index = 738;
            QuestButton.Index = 739;

            if (GameScene.User.Inventory.Length == 46)
            {
                ItemButton2.Index = 169;
            }

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46)
                    grid.Visible = true;
                else
                    grid.Visible = false;
            }
        }

        public void RefreshInventory2()
        {
            Reset();
            SetBagTab(1);

            ItemButton.Index = 737;
            ItemButton2.Index = 168;
            QuestButton.Index = 739;

            foreach (var grid in Grid)
            {
                if (grid.ItemSlot < 46 || grid.ItemSlot >= GameScene.User.Inventory.Length)
                    grid.Visible = false;
                else
                    grid.Visible = true;
            }

            int openLevel = (GameScene.User.Inventory.Length - 46) / 4;
            for (int i = 0; i < LockBar.Length; i++)
            {
                LockBar[i].Visible = (i < openLevel) ? false : true;
            }

            AddButton.Visible = openLevel >= 10 ? false : true;
        }

        public void Process()
        {
            WeightLabel.Text = GameScene.User.Inventory.Count(t => t == null).ToString();
            //WeightLabel.Text = (MapObject.User.MaxBagWeight - MapObject.User.CurrentBagWeight).ToString();
            GoldLabel.Text = GameScene.Gold.ToString("###,###,##0");

            // Delete-mode cursor icon follows the mouse
            if (_deleteMode && _deleteCursorIcon != null && _deleteCursorIcon.Visible)
                UpdateDeleteCursorPos();
        }


        private void WeightBar_BeforeDraw(object sender, EventArgs e)
        {
            if (WeightBar.Library == null) return;

            double percent = MapObject.User.CurrentBagWeight / (double)MapObject.User.Stats[Stat.BagWeight];
            if (percent > 1) percent = 1;
            if (percent <= 0) return;

            // Weight bar art based on fill
            if (percent <= 0.50)
            {
                WeightBar.Library = Libraries.Prguse;
                WeightBar.Index = 24;
            }
            else if (percent <= 0.75)
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 471;
            }
            else
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 470;
            }

            Rectangle section = new Rectangle
            {
                Size = new Size((int)((WeightBar.Size.Width - 3) * percent), WeightBar.Size.Height)
            };

            WeightBar.Library.Draw(WeightBar.Index, section, WeightBar.DisplayLocation, Color.White, false);
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

            DelItemButton.Index = on ? 368 : 366;

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
                    Size = new Size(32, 32),
                    GridType = MirGridType.Inventory,
                    Library = Libraries.Items,
                    Parent = this,
                    Location = new Point(x * 35 + 12, 3),
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
