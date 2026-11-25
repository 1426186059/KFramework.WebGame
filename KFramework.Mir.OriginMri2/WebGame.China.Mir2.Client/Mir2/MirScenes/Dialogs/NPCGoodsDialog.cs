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
    /// <summary>
    /// NPC 商店商品列表：移植原始传奇 UI 779（NpcStore / NpcStoreSkin + NpcStoreRenderSkin）。
    /// 335x433 独立面板，npc_storebg 背景，纵向滚动 60px 商品行（物品图标 / 名称 / 价格 / 限购 / 购买按钮）。
    /// </summary>
    public sealed class NPCGoodsDialog : MirImageControl
    {
        // 原始传奇 NpcStoreSkin：$bs 335x433，scroll x10 y10 317x390；NpcStoreRenderSkin 行高 60
        private const int PanelW = 335, PanelH = 433;
        private const int ListX = 10, ListY = 10, ListW = 317, ListH = 390;
        private const int RowH = 60;

        public PanelType PType;
        public bool UsePearls;

        public int StartIndex;
        public UserItem SelectedItem;

        public List<UserItem> Goods = new List<UserItem>();
        public List<UserItem> DisplayGoods = new List<UserItem>();
        private readonly List<NpcStoreRow> _rows = new List<NpcStoreRow>();

        private MirScrollView _scroll;
        public MirButton BuyButton, CloseButton;
        public MirImageControl BuyLabel;

        public NPCGoodsDialog(PanelType type)
        {
            PType = type;

            // 背景：原始传奇 npc_storebg.png（manifest 独立图）
            AutoSize = false;
            Size = new Size(PanelW + 20, PanelH);

            var mBg = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0),
                ImageKey = "resource/assets/gameui4/jpg/npc_storebg.png",
                Size = new Size(PanelW, PanelH)
            };

            // 关闭按钮：y_common-sheet#com_btn_close0（原始传奇 btn_close x323 y-9）
            CloseButton = new MirButton
            {
                Parent = this,
                Location = new Point(PanelW, 3),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
            };
            CloseButton.Click += (o, e) => Hide();

            // 纵向商品列表：原始传奇 storeList（gap 0，行高 60）
            _scroll = new MirScrollView
            {
                Parent = this,
                Location = new Point(ListX, ListY),
                Size = new Size(ListW, ListH),
            };
            _scroll.ShowScrollBar = true;

            // Craft 面板沿用原逻辑的底部确认按钮（隐藏），保留字段避免外部引用失效
            BuyButton = new MirButton
            {
                Parent = this,
                Location = new Point(228, 304),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet#store_z_btn_1_1",
                PressedAtlasFrame = "store_z_btn_1",
                Text = "购买",
                CenterText = true,
                BackColour = CommonColor.FromArgb(0, 0, 0, 0),
                Border = false,
                Visible = false,
            };
            BuyButton.Click += (o, e) => BuyItem();

            BuyLabel = new MirImageControl
            {
                Parent = this,
                Location = new Point(20, 9),
                Visible = false,
            };

            if (PType == PanelType.Craft)
                BuyButton.Visible = false;
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

            // 原始传奇：点购买按钮直接买 1 件，不弹数量输入框，也不弹子商品面板（Remove: CheckSubGoods 会把同 Index 的多条商品弹成子列表，导致“2 个修复油”）。
            // 同一物品在原始传奇商店里出现多条时，直接用当前选中那条（UniqueID 精确匹配）购买即可。
            // 按商店下发的货币类型/价格做本地校验（玉佩是传奇币 1000，不能用 Info.Price/Gold 判断）。
            uint price = SelectedItem.ShopPrice > 0 ? SelectedItem.ShopPrice : SelectedItem.Price();
            string currency;
            uint owned;
            switch (SelectedItem.ShopCurrencyType)
            {
                case 1:
                    currency = "元宝";
                    owned = GameScene.Credit;
                    break;
                case 2:
                    currency = "传奇币";
                    owned = GameScene.LegendCoin;
                    break;
                default:
                    currency = "金币";
                    owned = GameScene.Gold;
                    break;
            }

            if (price > owned)
            {
                GameScene.Scene.OutputMessage($"购买失败，{currency}不足", OutputMessageType.Error);
                return;
            }

            Network.Enqueue(new C.BuyItem { ItemIndex = SelectedItem.UniqueID, Count = 1, Type = PanelType.Buy });
        }

        private void Update()
        {
            foreach (var row in _rows)
            {
                _scroll.Remove(row);
                row.Dispose();
            }
            _rows.Clear();

            for (int i = 0; i < DisplayGoods.Count; i++)
            {
                UserItem item = DisplayGoods[i];
                var matchingGoods = Goods.Where(x => x.Info.Index == item.Info.Index).ToList();
                var row = new NpcStoreRow(item)
                {
                    Parent = _scroll,
                    Location = new Point(0, i * RowH),
                    MultipleAvailable = matchingGoods.Count > 1 && matchingGoods.Any(x => x.IsShopItem == false),
                    Selected = SelectedItem != null && item == SelectedItem,
                    UsePearls = UsePearls,
                };
                row.Click += (o, e) =>
                {
                    SelectedItem = ((NpcStoreRow)o).Item;
                    foreach (var r in _rows) { r.Selected = r.Item == SelectedItem; r.Refresh(); }
                };
                row.BuyClicked += (o, e) =>
                {
                    SelectedItem = ((NpcStoreRow)o).Item;
                    BuyItem();
                };
                _rows.Add(row);
                _scroll.Add(row);
            }

            _scroll.SetScrollOffset(new Point(0, 0));
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
            Location = new Point(0, GameScene.Scene.NPCDialog.Size.Height + 10);
            Visible = true;
            KFramework.MonoGame.PrintTool.Log($"[商店诊断] NPCGoodsDialog.Show Loc={Location} NPC高度={GameScene.Scene.NPCDialog.Size.Height} 行数={DisplayGoods.Count} PType={PType}");
        }

        public override void Draw()
        {
            if (Visible && _scroll != null)
            {
                _scroll.InvalidateTexture(); // 商品图标走 CDN 异步加载，加载完需重烘滚动视图才会显示
            }
            base.Draw();
        }
    }

    /// <summary>
    /// 商店商品行：移植原始传奇 NpcStoreRenderSkin（317x60）。
    /// npc_storebg1 底图 + 60x60 物品图标(MirItemCell 走原始传奇图集) + 名称/价格/限购 + store_z_btn 购买按钮。
    /// </summary>
    public sealed class NpcStoreRow : MirControl
    {
        public const int RowHeight = 60;
        public UserItem Item { get; private set; }
        public bool MultipleAvailable, Selected, UsePearls;
        public event EventHandler BuyClicked;

        private readonly MirImageControl _bg;
        private readonly StoreIcon _itemCell;
        private readonly MirLabel _nameLabel, _costLabel, _limitLabel;
        private readonly MirButton _buyButton;

        public NpcStoreRow(UserItem item)
        {
            Item = item;
            Size = new Size(317, RowHeight);
            // 不设 Sort：Sort=true 会在 OnVisibleChanged 里 Remove/Add 父控件 Controls 集合，
            // 而父级正 foreach 该集合，导致 InvalidOperation_EnumFailedVersion 崩溃。

            // 行底：不用 npc_storebg1 图集帧（其纹理自带分段横线，左右 Y 不齐），改纯色底 + 底部一条统一横线
            _bg = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = new Size(317, RowHeight),
                BackColour = CommonColor.FromArgb(30, 20, 20, 30),
                NotControl = true,
            };
            new RowDivider
            {
                Parent = this,
                Location = new Point(0, RowHeight - 1),
                Size = new Size(317, 1),
                NotControl = true,
            };
            // 竖线：把左侧图标区(x=1..61)与右侧名称/价格区分开
            new RowDivider
            {
                Parent = this,
                Location = new Point(62, 0),
                Size = new Size(1, RowHeight - 1),
                NotControl = true,
            };

            // 物品图标：独立 StoreIcon 直接调原始传奇图集绘制（MirItemCell.Item setter 依赖 GridType→ItemArray，商店格子没有对应数组会抛异常）
            _itemCell = new StoreIcon
            {
                Parent = this,
                Location = new Point(1, 0),
                Size = new Size(60, 60),
                Item = item,
                NotControl = true,
            };

            _nameLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(74, 10),
                Size = new Size(150, 24),
                AutoSize = false,
                Font = FontConst.F12,
                ForeColour = CommonColor.FromArgb(255, 230, 200),
                BackColour = CommonColor.Transparent,
                DrawFormat = new TextStyle(TextAnchor.UpperLeft, HorizontalWrapMode.Wrap),
                NotControl = true,
            };

            _costLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(74, 35), // 与名字同 x，整体左对齐
                Size = new Size(150, 20),
                Font = FontConst.F12,
                ForeColour = CommonColor.Gold,
                BackColour = CommonColor.Transparent,
                DrawFormat = new TextStyle(TextAnchor.UpperLeft, HorizontalWrapMode.Wrap),
                NotControl = true,
            };

            _limitLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(189, 37),
                Size = new Size(40, 20),
                Font = FontConst.F12,
                ForeColour = CommonColor.Yellow,
                BackColour = CommonColor.Transparent,
                NotControl = true,
            };

            _buyButton = new MirButton
            {
                Parent = this,
                Location = new Point(210, 15),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet#store_z_btn_1_1",
                PressedAtlasFrame = "store_z_btn_1",
                Text = "购买",
                CenterText = true,
                BackColour = CommonColor.FromArgb(0, 0, 0, 0),
                Border = false,
                Size = new Size(78, 36),
            };
            _buyButton.Click += (o, e) => BuyClicked?.Invoke(this, EventArgs.Empty);

            Refresh();
        }

        public void Refresh()
        {
            if (Item?.Info == null) return;

            _itemCell.Item = Item;
            _nameLabel.Text = Item.Info.FriendlyName;

            // 原始传奇：货币名+价格来自商店 cost（服务端下发），0=金币/1=元宝/2=传奇币
            string currency;
            uint price;
            uint owned;
            switch (Item.ShopCurrencyType)
            {
                case 1:
                    currency = "元宝";
                    price = Item.ShopPrice;
                    owned = GameScene.Credit;
                    break;
                case 2:
                    currency = "传奇币";
                    price = Item.ShopPrice;
                    owned = GameScene.LegendCoin;
                    break;
                default:
                    currency = UsePearls ? "珍珠" : "金币";
                    price = Item.ShopPrice > 0 ? Item.ShopPrice : Item.Info.Price;
                    owned = GameScene.Gold;
                    break;
            }
            _costLabel.Text = currency + ":" + price;
            _costLabel.ForeColour = price <= owned ? CommonColor.Gold : CommonColor.FromArgb(255, 90, 90);

            _limitLabel.Text = MultipleAvailable ? "多件" : string.Empty;
            _bg.BackColour = Selected ? CommonColor.FromArgb(60, 255, 255, 255) : CommonColor.Transparent;
            _itemCell.Item = Item;
        }
    }

    /// <summary>
    /// 商店商品图标：直接走原始传奇图集异步加载 + DrawItemIconFit，不依赖 MirItemCell 的 GridType/ItemArray。
    /// </summary>
    public sealed class StoreIcon : MirControl
    {
        private UserItem _item;
        public UserItem Item
        {
            get { return _item; }
            set { _item = value; Redraw(); }
        }

        protected internal override void DrawControl()
        {
            base.DrawControl();
            if (_item?.Info == null) return;

            int iconId = (int)_item.Info.Image; // Image 已是完整 uint（协议两端对齐）
            if (iconId <= 0) return;

            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Item, iconId, priority: Mir2OriginObjectLibrary.PriorityItem);
            Mir2OriginObjectLibrary.DrawItemIconFit(iconId, DisplayLocation.X, DisplayLocation.Y,
                                                    Size.Width, Size.Height, CommonColor.White);
        }
    }

    /// <summary>
    /// 商品行底部横线：1px 高的控件 + Border 画一条贯通整行的线，左右 Y 统一。
    /// </summary>
    public sealed class RowDivider : MirControl
    {
        public RowDivider()
        {
            BackColour = CommonColor.FromArgb(255, 90, 80, 70);
            Border = true;
            BorderColour = CommonColor.FromArgb(255, 90, 80, 70);
        }
    }
}
