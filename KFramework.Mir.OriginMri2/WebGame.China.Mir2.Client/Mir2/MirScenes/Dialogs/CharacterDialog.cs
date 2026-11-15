using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;
using KFramework.MonoGameExtend;
using WebGame.Mir2.MonoGame.Client; // CMain.MPoint

namespace Client.MirScenes.Dialogs
{
    public sealed class CharacterDialog : MirImageControl
    {
        public MirButton CloseButton;

        // 右侧竖排页签：与背包一致，普通态(com_btn_page0) + 选中态(com_btn_page1) 各一组。
        public const int TabCount = 4;
        public static readonly string[] TabNames = { "装备", "属性", "时装", "套装" };
        private readonly MirButton[] _tabButtons = new MirButton[TabCount];
        private readonly MirButton[] _tabSelectButtons = new MirButton[TabCount];

        public MirImageControl CharacterPage, StatusPage;
        public MirImageControl FashionPage, SuitPage;
        private MirImageControl[] _pages;

        public MirLabel NameLabel;

        // 状态与属性合并成一页：仿页游 NewheroPop_roleSkin 的 eui.Scroller + VerticalLayout，
        // 行由数据驱动生成，滚动/裁剪/滚轮交给 MirScrollView。
        private const int StatusRowHeight = 22;
        private MirScrollView _statusScroll;
        private AttrRow[] _statusRows;

        public MirItemCell[] Grid;
        private MirGridType GridType;
        private UserObject Actor;

        // 角色面板人物立绘分层（页游 UIStaticModel 的 Body/Head/Weapon/Shield/Miansha）。
        private MirImageControl[] _modelLayers;
        private readonly int[] _modelIds = new int[Mir2OriginUIModel.LayerCount];
        private int _modelPosVersion = -1;
        // 立绘热区当前正在显示提示框的物品：同一物品不重建提示框，换装/移出时才刷新。
        private UserItem _modelHoverItem;

        /// <summary>装扮装备的页游物品 id：斗笠(401) / 护盾(402) / 面纱(406)。0 = 未装备。
        /// 本服务端 UserInformation 暂未下发 ActiveDouLi/ActiveHuDun/ActiveMianSha（只有 ObjectPlayer 包有），
        /// 默认 0 即隐藏这三层；下发后由 GameScene 填入即可直接点亮，无需改绘制逻辑。</summary>
        public int DouLiItemId, HuDunItemId, MianShaItemId;

        public CharacterDialog(MirGridType gridType, UserObject actor)
        {
            Actor = actor;
            GridType = gridType;
            Movable = true;
            Sort = true;
            Size = new Size(390, 525);
            DrawImage = false;

            // 装配各标签页 / 控件，具体 UI 创建分散到各自的 Create 方法里，避免改动互相牵连。
            CreateBackground();
            CreateHeader();
            CreateTabButtons();
            CreateCharacterPage(actor, gridType);
            CreateStatusPage(actor, gridType);
            CreateExtraPages();

            // 「状态」与「属性」共用同一个滚动页：状态停在顶部角色资料，属性滚到属性表起点。
            _pages = new[] { CharacterPage, StatusPage, FashionPage, SuitPage };
            ShowPage(0);

            BeforeDraw += (o, e) => RefreshInterface();
        }

        private void CreateBackground()
        {
            var originBackground = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg.png",
                NotControl = true,
                Size = new Size(318, 475)
            };

            var originInner = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg2.jpg",
                NotControl = true,
                Location = new Point(16, 65),
                Size = new Size(286, 396)
            };
        }


        private void CreateHeader()
        {
            NameLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Location = new Point(0, 20),
                Size = new Size(300, 20),
                NotControl = true,
            };
        }


        private void CreateTabButtons()
        {
            CloseButton = new MirButton
            {
                Parent = this,
                Location = new Point(318, 60),
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
            };
            CloseButton.Click += (o, e) => Hide();

            // 与背包右侧竖排页签同一套写法：普通态 com_btn_page0 负责点击，
            // 选中态 com_btn_page1 覆盖在上层、NotControl 不接收鼠标，只做高亮。
            const int tabX = 318;
            const int firstY = 105;
            const int stepY = 70;

            for (int i = 0; i < TabCount; i++)
            {
                int index = i;

                var normal = new MirButton
                {
                    Location = new Point(tabX, firstY + i * stepY),
                    Parent = this,
                    Sound = SoundList.ButtonA,
                    AtlasNameKey = "y_common-sheet",
                    AtlasFrame = "com_btn_page0",
                    Size = new Size(27, 79),
                };
                normal.Click += (o, e) => ShowPage(index);
                normal.Label.Text = TabNames[i];
                normal.Label.OutLine = true;
                normal.Label.ForeColour = Color.Gray;
                normal.Label.Location = new Point(0, 10);
                _tabButtons[i] = normal;
            }

            for (int i = 0; i < TabCount; i++)
            {
                var selected = new MirButton
                {
                    Location = new Point(tabX, firstY + i * stepY),
                    Parent = this,
                    Sound = SoundList.ButtonA,
                    AtlasNameKey = "y_common-sheet",
                    AtlasFrame = "com_btn_page1",
                    Size = new Size(34, 92),
                    NotControl = true,
                    Visible = false,
                };
                selected.Label.Text = TabNames[i];
                selected.Label.OutLine = true;
                selected.Label.ForeColour = Color.White;
                selected.Label.Location = new Point(5, 10);
                selected.Label.Size = new Size(20, 92);
                _tabSelectButtons[i] = selected;
            }
        }

        /// <summary>时装 / 套装暂未接入数据，先建占位容器，保证切页时页面不为空。</summary>
        private void CreateExtraPages()
        {
            FashionPage = CreatePlaceholderPage("时装");
            SuitPage = CreatePlaceholderPage("套装");
        }

        private MirImageControl CreatePlaceholderPage(string title)
        {
            var page = new MirImageControl
            {
                Parent = this,
                Location = new Point(8, 90),
                Size = new Size(286, 396),
                Visible = false,
                NotControl = true,
            };
            new MirLabel
            {
                Parent = page,
                AutoSize = true,
                NotControl = true,
                Text = title + "暂未开放",
                ForeColour = Color.White,
                OutLine = true,
                Location = new Point(70, 180),
            };
            return page;
        }


        private void CreateCharacterPage(UserObject actor, MirGridType gridType)
        {
            CharacterPage = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 50),
                AutoSize = false,
                Size = new Size(302, 461),
                Texture = KDefaultRes.DefaultTexture2D,
                DrawImage = false,
            };

            // —— 人物立绘：对齐页游 UIStaticModel 的分层 uiModel 皮肤 ——
            // 最底是原始裸模 + 原始头发，常驻不替换；上层依次覆盖
            // 衣服 / 头盔(斗笠) / 武器 / 面纱 / 护盾，每层一张 uiModel/{模型号}.png，
            // 按 uiModel/pos.json 的 x/y 站位点摆进同一 1025×1025 画布后叠加。
            // 容器原点 = 人物脚底中心 (159,350)，位于内框 (16,65)-(302,461) 的中下部；
            // 它是 CharacterPage 的第一个子节点，故立绘在装备格之下，不遮挡格子。
            var modelHost = new MirImageControl
            {
                Parent = CharacterPage,
                Location = new Point(159, 300),
                DrawImage = false,
                NotControl = true,
            };
            // 裸模与头发是常驻层，先预热它们的站立帧站位。
            Mir2OriginUIModel.EnsureFrame(Mir2OriginUIModel.MaleBody);
            Mir2OriginUIModel.EnsureFrame(Mir2OriginUIModel.FemaleBody);
            Mir2OriginUIModel.EnsureFrame(Mir2OriginUIModel.MaleHair);
            Mir2OriginUIModel.EnsureFrame(Mir2OriginUIModel.FemaleHair);

            _modelLayers = new MirImageControl[Mir2OriginUIModel.LayerCount];
            foreach (int layer in Mir2OriginUIModel.DrawOrder)
            {
                int captured = layer;
                var node = new MirImageControl
                {
                    Parent = modelHost,
                    NotControl = true,
                    Visible = false,
                    SizeScale = Mir2OriginUIModel.Scale,
                };
                // pos.json 可能比图片晚到、图片本身也异步解码；纹理就绪后重算一次坐标（居中回落需要真实尺寸）。
                node.RemoteImageLoaded += (o, e) => ApplyModelLayerPosition(captured, node);
                _modelLayers[layer] = node;
            }

            // 换装/卸装后每帧按 Grid[] 解算模型号；模型号不变不重发图片请求。
            // 用 BeforeDraw（子控件绘制之前）而非 AfterDraw，保证同帧生效。
            CharacterPage.BeforeDraw += (o, e) => RefreshModelLayers(actor);

            // 热区必须比装备格先加入 Controls：OnMouseMove 倒序命中，可见装备格才能盖过热区。
            CreateModelHotspots();

            int cellWidth = 60;
            int cellHeigth = 60;
            int nSpace = 11;

            Point GetGridPos(int x, int y)
            {
                Point p = new Point(y * (cellWidth + nSpace), x * (cellHeigth + nSpace));
                p.Offset(new Point(23, 60));
                return p;
            }

            // 装备槽底图：原始 RolePop 的 EquipItemRenderSkin.bgImg.source = "equip_grid"（60×60），
            // 取自 gameui4 图集 y_ngrid-sheet。逐槽垫在槽位下方（位置与下方 Grid[] 创建顺序/坐标一致），NotControl 不挡点击。
            var slotGridPoints = new Point[]
            {
                GetGridPos(0, 3), // 项链
                GetGridPos(1, 3), // 荣誉勋章
                GetGridPos(2, 0),   // 左手镯
                GetGridPos(2, 3), // 右手镯
                GetGridPos(3, 0),   // 左戒指
                GetGridPos(3, 3), // 右戒指
                GetGridPos(4, 0),   // 玉佩
                GetGridPos(4, 1), // 腰带
                GetGridPos(4, 2),  // 鞋子
                GetGridPos(4, 3), // 疗伤石
            };

            foreach (var gp in slotGridPoints)
            {
                new MirImageControl
                {
                    Parent = CharacterPage,
                    AtlasNameKey = "y_ngrid-sheet#equip_grid",
                    NotControl = true,
                    Location = gp,
                    Size = new Size(cellWidth, cellHeigth),
                    AutoSize = false,
                };
            }

            // 有格子的 10 个部位：顺序与上面 slotGridPoints 的注释一一对应。
            // 武器/衣服/头盔/斗笠/面纱没有槽位，改在角色立绘上显示并点击穿脱。
            EquipmentSlot[] visibleSlots =
            {
                EquipmentSlot.Necklace,      // 项链
                EquipmentSlot.XunZhang,      // 荣誉勋章
                EquipmentSlot.BraceletL,     // 左手镯
                EquipmentSlot.BraceletR,     // 右手镯
                EquipmentSlot.RingL,         // 左戒指
                EquipmentSlot.RingR,         // 右戒指
                EquipmentSlot.YuPei,         // 玉佩
                EquipmentSlot.Belt,          // 腰带
                EquipmentSlot.Boots,         // 鞋子
                EquipmentSlot.LiaoShang,     // 疗伤石
            };

            Grid = new MirItemCell[Enum.GetNames(typeof(EquipmentSlot)).Length];

            // 先把所有槽位建成隐藏格子：没有格子的部位也要有对象，
            // 否则 GameScene 的 Grid[p.To] 与 RefreshInterface 的 Grid[i].Item 会索引到 null。
            for (int i = 0; i < Grid.Length; i++)
            {
                Grid[i] = new MirItemCell
                {
                    ItemSlot = i,
                    GridType = gridType,
                    Parent = CharacterPage,
                    Location = new Point(0, 0),
                    // 装备槽按原始 equip_grid 尺寸 60×60 显示（覆盖 MirItemCell 默认 36×32）。
                    Size = new Size(60, 60),
                    Visible = false,
                };
            }

            // 有槽位的部位直接落在 equip_grid 底图坐标上，图标与底图严丝合缝。
            for (int i = 0; i < visibleSlots.Length; i++)
            {
                MirItemCell cell = Grid[(int)visibleSlots[i]];
                cell.Location = slotGridPoints[i];
                cell.Visible = true;
            }
        }

        /// <summary>角色立绘热区：一整块透明命中区覆盖立绘，按【各层精灵帧的实际绘制矩形】
        /// 判断鼠标落在哪件装备上；悬停显示该装备的物品提示框，左键/右键走现有格子的穿脱链路。
        ///
        /// 旧实现按固定小矩形估算部位，还把武器放在立绘右侧；而 model/{id}/...994.png 的武器帧
        /// （如木剑 9501 offX=542）实际画在立绘左侧，点上去既没有提示也卸不下来。
        /// 热区必须【先于】装备格创建：OnMouseMove 倒序命中，真实的可见装备格因此优先，
        /// 热区才能放宽到覆盖整个人物区而不挡格子。</summary>
        private void CreateModelHotspots()
        {
            var hotspot = new MirButton
            {
                Parent = CharacterPage,
                Location = new Point(0, 60),
                Size = new Size(302, 340),
                AutoSize = false,
                // DrawImage=false 只关绘制，不影响鼠标命中与 Click 事件。
                DrawImage = false,
                Sound = SoundList.ButtonA,
            };

            hotspot.MouseEnter += (o, e) => RefreshModelTooltip();
            hotspot.MouseMove += (o, e) => RefreshModelTooltip();
            hotspot.MouseLeave += (o, e) => ClearModelTooltip();

            hotspot.Click += (o, e) =>
            {
                MirItemCell cell = CurrentModelCell();
                if (cell == null) return;

                // 右键沿用其它装备格「右键卸下」的习惯，直接走 UseItem；
                // 否则手上还选着物品时 HandleModelSlotClick 会误判成「穿戴」而不卸下。
                if (e is MouseEventArgs me && me.Button == MouseButtons.Right)
                    cell.UseItem();
                else
                    cell.HandleModelSlotClick();
            };
        }

        /// <summary>悬停：把落点所属装备挂到 GameScene 的物品提示框，与背包/装备格共用 CreateItemLabel。</summary>
        private void RefreshModelTooltip()
        {
            if (GameScene.Scene == null) return;

            UserItem item = CurrentModelCell()?.Item;
            // 鼠标在立绘与可见装备格之间移动时，格子的 OnMouseLeave 会把提示框 Dispose 掉，
            // 所以缓存命中还要确认提示框确实还在，否则会出现「回到立绘后提示框不出现」。
            bool labelAlive = GameScene.Scene.ItemLabel != null && !GameScene.Scene.ItemLabel.IsDisposed;
            if (ReferenceEquals(item, _modelHoverItem))
            {
                if (item == null && !labelAlive) return;
                if (item != null && labelAlive) return;
            }
            _modelHoverItem = item;

            GameScene.Scene.DisposeItemLabel();
            GameScene.HoverItem = null;
            if (item != null) GameScene.Scene.CreateItemLabel(item);
        }

        private void ClearModelTooltip()
        {
            _modelHoverItem = null;
            if (GameScene.Scene == null) return;
            GameScene.Scene.DisposeItemLabel();
            GameScene.HoverItem = null;
        }

        private MirItemCell CurrentModelCell()
        {
            if (Grid == null) return null;
            int slot = (int)PickModelSlot(CMain.MPoint);
            return slot >= 0 && slot < Grid.Length ? Grid[slot] : null;
        }

        /// <summary>鼠标落点（UI 绝对坐标）→ 装备部位：优先按立绘层的真实绘制矩形从上层往下找；
        /// 帧图尚未加载完或落在裸模空白区时，退回按人物上中下三分的粗略划分。</summary>
        private EquipmentSlot PickModelSlot(Point p)
        {
            if (_modelLayers != null)
            {
                for (int i = Mir2OriginUIModel.DrawOrder.Length - 1; i >= 0; i--)
                {
                    int layer = Mir2OriginUIModel.DrawOrder[i];
                    if (!TryMapLayerToSlot(layer, out EquipmentSlot slot)) continue;

                    MirImageControl node = _modelLayers[layer];
                    if (node == null || !node.Visible) continue;

                    Point loc = node.DisplayLocationWithoutOffSet;
                    Size size = node.Size;
                    if (size.Width <= 0 || size.Height <= 0) continue;
                    if (p.X >= loc.X && p.X < loc.X + size.Width &&
                        p.Y >= loc.Y && p.Y < loc.Y + size.Height)
                        return slot;
                }
            }

            int x = p.X - CharacterPage.DisplayLocation.X;
            int y = p.Y - CharacterPage.DisplayLocation.Y;
            if (y < 120) return EquipmentSlot.DouLi;
            if (y < 165) return EquipmentSlot.Helmet;
            if (x < 120 && y < 300) return EquipmentSlot.Weapon;   // 武器帧在立绘左侧
            return EquipmentSlot.Armour;
        }

        /// <summary>立绘层 → 装备槽。裸模/头发/护盾没有装备槽，不参与命中。</summary>
        private bool TryMapLayerToSlot(int layer, out EquipmentSlot slot)
        {
            switch (layer)
            {
                case Mir2OriginUIModel.Armour: slot = EquipmentSlot.Armour; return true;
                case Mir2OriginUIModel.Weapon: slot = EquipmentSlot.Weapon; return true;
                case Mir2OriginUIModel.Miansha: slot = EquipmentSlot.MianSha; return true;
                case Mir2OriginUIModel.HeadGear:
                    // 头部覆盖层同时承载斗笠与头盔，斗笠优先（与 ResolvePlayerModels 一致）。
                    slot = Grid != null && Grid[(int)EquipmentSlot.DouLi]?.Item != null
                        ? EquipmentSlot.DouLi : EquipmentSlot.Helmet;
                    return true;
                default: slot = EquipmentSlot.Weapon; return false;
            }
        }

        /// <summary>按页游 UIModelUtil.setFirstPlayerModel 的规则，从装备槽解算各层模型号并刷新立绘节点。</summary>
        private void RefreshModelLayers(UserObject actor)
        {
            if (Grid == null || _modelLayers == null) return;
            // 版本表未就绪时 URL 会缺版本段，等下一帧再解算，避免错误的 ImageUrl 被缓存住。
            if (!Mir2OriginUIModel.PathsReady) return;

            int ItemId(EquipmentSlot slot)
            {
                int i = (int)slot;
                return i < Grid.Length && Grid[i] != null ? Grid[i].Item?.Info?.Index ?? 0 : 0;
            }

            // 斗笠/面纱现在是真实装备槽，优先用槽里的物品 Index；旧的外部注入字段作兜底。
            int douLi = ItemId(EquipmentSlot.DouLi);
            if (douLi == 0) douLi = DouLiItemId;

            int mianSha = ItemId(EquipmentSlot.MianSha);
            if (mianSha == 0) mianSha = MianShaItemId;

            int[] models = Mir2OriginUIModel.ResolvePlayerModels(
                actor.Gender == MirGender.Female,
                ItemId(EquipmentSlot.Armour),
                ItemId(EquipmentSlot.Weapon),
                ItemId(EquipmentSlot.Helmet),
                douLi, HuDunItemId, mianSha);

            // 每个用到的模型都要拉一次站立帧 json（offX/offY），每帧调用是幂等的。
            Mir2OriginUIModel.EnsureFrames(models);

            int posVersion = Mir2OriginUIModel.PosVersion;
            bool posChanged = posVersion != _modelPosVersion;
            _modelPosVersion = posVersion;

            foreach (int layer in Mir2OriginUIModel.DrawOrder)
            {
                MirImageControl node = _modelLayers[layer];
                if (node == null) continue;

                int model = models[layer];
                if (_modelIds[layer] != model)
                {
                    _modelIds[layer] = model;
                    node.Visible = model > 0;
                    // ImageUrl setter 自带去重（值相同不重发请求）；设 null 清空旧图。
                    node.ImageUrl = model > 0 ? Mir2OriginUIModel.Path(model) : null;
                    ApplyModelLayerPosition(layer, node);
                }
                else if (posChanged)
                    ApplyModelLayerPosition(layer, node);
            }
        }

        /// <summary>按 uiModel/pos.json 的站位点摆放单层；无站位数据时退化为「图片中心对齐容器脚底原点」。</summary>
        private void ApplyModelLayerPosition(int layer, MirImageControl node)
        {
            int model = _modelIds[layer];
            if (model <= 0 || node == null) return;

            if (Mir2OriginUIModel.TryGetOffset(model, out int x, out int y))
            {
                node.Location = new Point(x, y);
            }
            else
            {
                // 对应页游 UIModelSkin type!=1 分支：bitmap.x = -width>>1 / y = -height>>1。
                // Size 已含 SizeScale，纹理异步解码完成后由 RemoteImageLoaded 再重算一次。
                Size size = node.Size;
                node.Location = new Point(-size.Width / 2, -size.Height / 2);
            }
        }


        /// <summary>属性行：名称取自 0config.attribute_score[id].description（上下限合并项用
        /// CommonUtils.getSpecialAttrName 的全角空格写法），取值取自本服务端 UserObject。
        /// 对应页游 showDetailAttr() 里 createAttr(description + "  :", value, tips, type) 的一行。</summary>
        private sealed class AttrRow
        {
            public readonly string Name;
            public readonly Func<UserObject, string> Read;
            public MirLabel Value;

            public AttrRow(string name, Func<UserObject, string> read)
            {
                Name = name;
                Read = read;
            }
        }

        /// <summary>页游角色面板只用一个长属性页：前 6 行是 RolePop_ztSkin 的角色资料
        /// （职　　业 / 等　　级 / 封　　号 / 阵　　法 / 血魔之体 / 金刚之体），
        /// 后面是 showDetailAttr() 按 attribute_score.sorting 排的属性表。
        /// 服务端尚未下发的封号/阵法/血魔之体/金刚之体沿用页游的「未解锁」。</summary>
        private static AttrRow[] BuildAttributeRows()
        {
            var rows = new List<AttrRow>();

            void Add(string name, Func<UserObject, string> read) => rows.Add(new AttrRow(name, read));
            string Range(UserObject a, Stat min, Stat max) => a.Stats[min] + "﹣" + a.Stats[max];

            // —— 角色资料（RolePop_ztSkin 前 6 项）——
            Add("职　　业", a => ClassName(a.Class));
            Add("等　　级", a => a.Level + "级");
            Add("封　　号", a => "未解锁");
            Add("阵　　法", a => "未解锁");
            Add("血魔之体", a => "未解锁");
            Add("金刚之体", a => "未解锁");

            // —— 基础属性（attribute_score.sorting 顺序）——
            Add("生　　命", a => a.HP + "/" + a.Stats[Stat.HP]);
            Add("魔法值", a => a.MP + "/" + a.Stats[Stat.MP]);
            Add("攻　　击", a => Range(a, Stat.MinDC, Stat.MaxDC));
            Add("防　　御", a => Range(a, Stat.MinAC, Stat.MaxAC));
            Add("魔　　防", a => Range(a, Stat.MinMAC, Stat.MaxMAC));
            Add("准　　确", a => "+" + a.Stats[Stat.Accuracy]);
            Add("敏　　捷", a => "+" + a.Stats[Stat.Agility]);
            Add("魔　　法", a => Range(a, Stat.MinMC, Stat.MaxMC));
            Add("道　　术", a => Range(a, Stat.MinSC, Stat.MaxSC));
            Add("魔法躲避", a => "+" + a.Stats[Stat.MagicResist]);
            Add("暴击几率", a => a.Stats[Stat.CriticalRate] + "%");
            Add("暴击伤害", a => a.Stats[Stat.CriticalDamage].ToString());
            Add("攻击速度", a => a.Stats[Stat.AttackSpeed].ToString());
            Add("幸　　运", a => a.Stats[Stat.Luck].ToString());
            Add("神　　圣", a => "+" + a.Stats[Stat.Holy]);
            Add("冰　　冻", a => "+" + a.Stats[Stat.Freezing]);
            Add("中毒攻击", a => "+" + a.Stats[Stat.PoisonAttack]);
            Add("中毒抗性", a => "+" + a.Stats[Stat.PoisonResist]);
            Add("生命恢复", a => "+" + a.Stats[Stat.HealthRecovery]);
            Add("魔法恢复", a => "+" + a.Stats[Stat.SpellRecovery]);
            Add("中毒恢复", a => "+" + a.Stats[Stat.PoisonRecovery]);
            Add("伤害反弹", a => a.Stats[Stat.Reflect].ToString());
            Add("强　　壮", a => a.Stats[Stat.Strong].ToString());
            Add("背包负重", a => a.CurrentBagWeight + "/" + a.Stats[Stat.BagWeight]);
            Add("穿戴负重", a => a.CurrentWearWeight + "/" + a.Stats[Stat.WearWeight]);
            Add("手持负重", a => a.CurrentHandWeight + "/" + a.Stats[Stat.HandWeight]);
            Add("经验进度", a => string.Format("{0:0.##%}", a.Experience / (double)a.MaxExperience));

            // —— 倍率/加成项：页游属性页同样会列大量加成，靠滚动查看 ——
            Add("生命加成", a => a.Stats[Stat.HPRatePercent] + "%");
            Add("魔法加成", a => a.Stats[Stat.MPRatePercent] + "%");
            Add("攻击加成", a => a.Stats[Stat.MaxDCRatePercent] + "%");
            Add("防御加成", a => a.Stats[Stat.MaxACRatePercent] + "%");
            Add("魔防加成", a => a.Stats[Stat.MaxMACRatePercent] + "%");
            Add("法术加成", a => a.Stats[Stat.MaxMCRatePercent] + "%");
            Add("道术加成", a => a.Stats[Stat.MaxSCRatePercent] + "%");
            Add("攻速加成", a => a.Stats[Stat.AttackSpeedRatePercent] + "%");
            Add("生命汲取", a => a.Stats[Stat.HPDrainRatePercent] + "%");
            Add("经验倍率", a => a.Stats[Stat.ExpRatePercent] + "%");
            Add("物品爆率", a => a.Stats[Stat.ItemDropRatePercent] + "%");
            Add("金币爆率", a => a.Stats[Stat.GoldDropRatePercent] + "%");
            Add("挖矿倍率", a => a.Stats[Stat.MineRatePercent] + "%");
            Add("宝石倍率", a => a.Stats[Stat.GemRatePercent] + "%");
            Add("钓鱼倍率", a => a.Stats[Stat.FishRatePercent] + "%");
            Add("合成倍率", a => a.Stats[Stat.CraftRatePercent] + "%");
            Add("技能领悟", a => a.Stats[Stat.SkillGainMultiplier] + "%");
            Add("攻击附加值", a => a.Stats[Stat.AttackBonus].ToString());
            Add("伤害减免", a => a.Stats[Stat.DamageReductionPercent] + "%");
            Add("护盾加成", a => a.Stats[Stat.EnergyShieldPercent] + "%");
            Add("师徒伤害", a => a.Stats[Stat.MentorDamageRatePercent] + "%");
            Add("法力惩罚", a => a.Stats[Stat.ManaPenaltyPercent] + "%");

            return rows.ToArray();
        }

        private static string ClassName(MirClass c)
        {
            switch (c)
            {
                case MirClass.Warrior: return "战士";
                case MirClass.Wizard: return "法师";
                case MirClass.Taoist: return "道士";
                case MirClass.Assassin: return "刺客";
                case MirClass.Archer: return "弓手";
                default: return "—";
            }
        }

        private void CreateStatusPage(UserObject actor, MirGridType gridType)
        {
            // 状态/属性合并成一页。页游 NewheroPop_roleSkin 是 286×396 的 eui.Scroller(s1)，
            // viewport = grp_attr(eui.VerticalLayout gap=0)，showDetailAttr() 用 createAttr()
            // 每行 36px 往 grp_attr 里 addChild，属性名来自 attribute_score.description。
            // 这里用 MirScrollView 承载同样的数据驱动长列表，滚轮/拖拽滚动条/溢出裁剪由它接管。
            StatusPage = new MirImageControl
            {
                Parent = this,
                Location = new Point(8, 70),
                Size = new Size(286, 396),
                Visible = false,
                NotControl = true
                // 不能设 NotControl：MirControl.OnMouseMove 只遍历直接子控件，
                // 父级 NotControl=true 会让整棵子树收不到鼠标，滚动视图的滚轮/滚动条就失效。
            };

            _statusScroll = new MirScrollView
            {
                Parent = StatusPage,
                Location = new Point(0, 0),
                Size = new Size(286, 380),
                ShowScrollBar = true,
            };

            _statusRows = BuildAttributeRows();
            for (int i = 0; i < _statusRows.Length; i++)
            {
                int y = i * StatusRowHeight;
                // 仿 createAttr()：名称 x=2,width=103,textAlign=right；数值 x=110。
                var nameLabel = new MirLabel
                {
                    Text = _statusRows[i].Name + "：",
                    AutoSize = false,
                    Size = new Size(103, StatusRowHeight),
                    Location = new Point(2, y),
                    DrawFormat = new TextStyle(TextAnchor.MiddleRight),
                    ForeColour = Color.White,
                    OutLine = true,
                    NotControl = true,
                };
                var valueLabel = new MirLabel
                {
                    AutoSize = false,
                    Size = new Size(160, StatusRowHeight),
                    Location = new Point(110, y),
                    ForeColour = Color.White,
                    OutLine = true,
                    NotControl = true,
                };
                _statusRows[i].Value = valueLabel;
                _statusScroll.Add(nameLabel);
                _statusScroll.Add(valueLabel);
            }

            // 页游 refr() 打开面板时重刷一遍；这里逐行刷新，MirLabel.Text 自带去重，
            // 只有数值变化才重绘，MirScrollView 会自动重烘离屏纹理。
            StatusPage.BeforeDraw += (o, e) =>
            {
                foreach (AttrRow row in _statusRows)
                    row.Value.Text = row.Read(actor);
            };
        }





        public override void Show()
        {
            base.Show();
            Location = new Point(Settings.ScreenWidth / 2 - 600, Settings.ScreenHeight / 2 - 350);
        }

        public override void Hide()
        {
            GameScene.Scene.SocketDialog.Hide();
            base.Hide();
        }

        /// <summary>统一切页：先隐藏全部页，再显示目标页并点亮对应选中态。
        /// 「状态」与「属性」共用同一个滚动页，用滚动位置区分：状态停在顶部角色资料，属性滚到属性表起点。</summary>
        public void ShowPage(int index)
        {
            if (_pages == null || index < 0 || index >= TabCount) return;

            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = false;
            _pages[index].Visible = true;

            for (int i = 0; i < TabCount; i++)
                _tabSelectButtons[i].Visible = i == index;

            if (_statusScroll != null && _pages[index] == StatusPage)
            {
                _statusScroll.SetScrollOffset(new Point(0, 0));
            }
        }

        // 主界面按钮仍调用这个入口，保持原有语义。
        public void ShowCharacterPage()
        {
            ShowPage(0);
        }

        private void RefreshInterface()
        {
            NameLabel.Text = Actor.Name;

            for (int i = 0; i < Grid.Length; i++)
            {
                Grid[i].Item = Actor.Equipment != null && i < Actor.Equipment.Length ?
                    Actor.Equipment[i] : null;
            }
        }

        public MirItemCell GetCell(ulong id)
        {
            for (int i = 0; i < Grid.Length; i++)
            {
                if (Grid[i].Item != null && Grid[i].Item.UniqueID == id)
                {
                    return Grid[i];
                }
            }
            return null;
        }

    }
}
