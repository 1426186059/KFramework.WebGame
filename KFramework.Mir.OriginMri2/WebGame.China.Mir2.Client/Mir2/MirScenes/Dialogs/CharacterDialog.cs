using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    public sealed class CharacterDialog : MirImageControl
    {
        public MirButton CloseButton, CharacterButton, StatusButton, StateButton, SkillButton;
        public MirImageControl CharacterPage, StatusPage, StatePage, SkillPage, ClassImage;

        public MirLabel NameLabel, GuildLabel, LoverLabel;
        public MirLabel ACLabel, MACLabel, DCLabel, MCLabel, SCLabel, HealthLabel, ManaLabel;
        public MirLabel CritRLabel, CritDLabel, LuckLabel, AttkSpdLabel, AccLabel, AgilLabel;
        public MirLabel ExpPLabel, BagWLabel, WearWLabel, HandWLabel, MagicRLabel, PoisonRecLabel, HealthRLabel, ManaRLabel, PoisonResLabel, HolyTLabel, FreezeLabel, PoisonAtkLabel;
        public MirLabel HeadingLabel, StatLabel;
        public MirButton NextButton, BackButton;

        public MirItemCell[] Grid;
        private MirGridType GridType;
        public MagicButton[] Magics;

        public int StartIndex;
        private UserObject Actor;

        // 角色面板人物立绘分层（页游 UIStaticModel 的 Body/Head/Weapon/Shield/Miansha）。
        private MirImageControl[] _modelLayers;
        private readonly int[] _modelIds = new int[Mir2OriginUIModel.LayerCount];
        private int _modelPosVersion = -1;

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
            CreateStatePage(actor, gridType);
            CreateSkillPage(actor, gridType);

            BeforeDraw += (o, e) => RefreshInterface();
        }

        private void CreateBackground()
        {
            var originBackground = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg.png",
                NotControl = true,
                Size = new Size(390, 525)
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
                DrawFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
                Parent = this,
                Location = new Point(0, 12),
                Size = new Size(264, 20),
                NotControl = true,
            };
            GuildLabel = new MirLabel
            {
                DrawFormat = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter,
                Parent = this,
                Location = new Point(0, 33),
                Size = new Size(264, 30),
                NotControl = true,
            };
            ClassImage = new MirImageControl
            {
                Index = 100,
                Library = Libraries.Prguse,
                Location = new Point(15, 33),
                Parent = this,
                NotControl = true,
            };
        }


        private void CreateTabButtons()
        {
            CharacterButton = new MirButton
            {
                Index = 500,
                Library = Libraries.Title,
                Location = new Point(8, 70),
                Parent = this,
                PressedIndex = 500,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA,
            };
            CharacterButton.Click += (o, e) => ShowCharacterPage();

            StateButton = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(132, 70),
                Parent = this,
                PressedIndex = 502,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA
            };
            StateButton.Click += (o, e) => ShowStatePage();

            SkillButton = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(194, 70),
                Parent = this,
                PressedIndex = 503,
                Size = new Size(64, 20),
                Sound = SoundList.ButtonA
            };
            SkillButton.Click += (o, e) => ShowSkillPage();

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

            StatusButton = new MirButton
            {
                Location = new Point(318, 105),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_page0",
                Size = new Size(27, 79)
            };
            StatusButton.Click += (o, e) => ShowStatusPage();
            StatusButton.Label.Text = "仓库";
            StatusButton.Label.OutLine = true;
            StatusButton.Label.ForeColour = Color.Gray;
            StatusButton.Label.Location = new Point(0, 10);
        }


        private void CreateCharacterPage(UserObject actor, MirGridType gridType)
        {
            CharacterPage = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0),
                DrawImage = false,
            };

            // —— 人物立绘：对齐页游 UIStaticModel 的分层 uiModel 皮肤 ——
            // 身体/头发(或头盔、斗笠)/武器/面纱/盾牌各是一张 uiModel/{模型号}.png，
            // 尺寸各不相同，按 uiModel/pos.json 的 x/y 站位点摆进同一 1025×1025 画布后叠加。
            // 容器原点 = 人物脚底中心 (159,400)，位于内框 (16,65)-(302,461) 的中下部；
            // 它是 CharacterPage 的第一个子节点，故立绘在装备格之下，不遮挡格子。
            var modelHost = new MirImageControl
            {
                Parent = CharacterPage,
                Location = new Point(159, 350),
                DrawImage = false,
                NotControl = true,
            };
            Mir2OriginUIModel.EnsurePos();

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

            int cellWidth = 60;
            int cellHeigth = 60;
            int nSpace = 11;

            Point GetGridPos(int x, int y)
            {
                Point p = new Point(y * (cellWidth + nSpace), x * (cellHeigth + nSpace));
                p.Offset(new Point(23, 110));
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

            Grid = new MirItemCell[Enum.GetNames(typeof(EquipmentSlot)).Length];

            Grid[(int)EquipmentSlot.Weapon] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Weapon,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(184, 10),
            };

            Grid[(int)EquipmentSlot.Armour] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Armour,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(244, 10),
            };

            Grid[(int)EquipmentSlot.Helmet] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Helmet,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 10),
            };

            Grid[(int)EquipmentSlot.Torch] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Torch,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 201),
            };

            Grid[(int)EquipmentSlot.Necklace] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Necklace,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 147),
            };

            Grid[(int)EquipmentSlot.BraceletL] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.BraceletL,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(12, 255),
            };

            Grid[(int)EquipmentSlot.BraceletR] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.BraceletR,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 255),
            };

            Grid[(int)EquipmentSlot.RingL] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.RingL,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(12, 309),
            };

            Grid[(int)EquipmentSlot.RingR] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.RingR,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 309),
            };

            Grid[(int)EquipmentSlot.Amulet] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Amulet,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(12, 363),
            };

            Grid[(int)EquipmentSlot.Boots] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Boots,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(72, 363),
            };

            Grid[(int)EquipmentSlot.Belt] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Belt,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(132, 363),
            };

            Grid[(int)EquipmentSlot.Stone] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Stone,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(192, 363),
            };

            Grid[(int)EquipmentSlot.Mount] = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Mount,
                GridType = gridType,
                Parent = CharacterPage,
                Location = new Point(304, 93),
            };

            // 装备槽按原始 equip_grid 尺寸 60×60 显示（覆盖 MirItemCell 默认 36×32）。
            foreach (var cell in Grid)
            {
                cell.Size = new Size(60, 60);
            }

        }

        /// <summary>按页游 UIModelUtil.setFirstPlayerModel 的规则，从装备槽解算各层模型号并刷新立绘节点。</summary>
        private void RefreshModelLayers(UserObject actor)
        {
            if (Grid == null || _modelLayers == null) return;
            Mir2OriginUIModel.EnsurePos();   // 幂等；面板构造过早导致首次加载失败时，这里会自动重试

            int ItemId(EquipmentSlot slot)
            {
                int i = (int)slot;
                return i < Grid.Length && Grid[i] != null ? Grid[i].Item?.Info?.Index ?? 0 : 0;
            }

            int[] models = Mir2OriginUIModel.ResolvePlayerModels(
                actor.Gender == MirGender.Female,
                ItemId(EquipmentSlot.Armour),
                ItemId(EquipmentSlot.Weapon),
                ItemId(EquipmentSlot.Helmet),
                DouLiItemId, HuDunItemId, MianShaItemId);

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


        private void CreateStatusPage(UserObject actor, MirGridType gridType)
        {
            // 页父节点（空容器）：本页所有 UI 都作为子节点挂到 StatusPage 下，切页只需显隐它。
            StatusPage = new MirImageControl
            {
                Parent = this,
                Location = new Point(8, 90),
                Visible = false,
                NotControl = true,
            };
            // 背景图作为子节点（原来 Index=506 那张页签底图）。
            new MirImageControl
            {
                Parent = StatusPage,
                Index = 506,
                Library = Libraries.Title,
                NotControl = true,
                Location = new Point(0, 0),
            };
            StatusPage.BeforeDraw += (o, e) =>
            {
                ACLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinAC], actor.Stats[Stat.MaxAC]);
                MACLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinMAC], actor.Stats[Stat.MaxMAC]);
                DCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinDC], actor.Stats[Stat.MaxDC]);
                MCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinMC], actor.Stats[Stat.MaxMC]);
                SCLabel.Text = string.Format("{0}-{1}", actor.Stats[Stat.MinSC], actor.Stats[Stat.MaxSC]);
                HealthLabel.Text = string.Format("{0}/{1}", actor.HP, actor.Stats[Stat.HP]);
                ManaLabel.Text = string.Format("{0}/{1}", actor.MP, actor.Stats[Stat.MP]);
                CritRLabel.Text = string.Format("{0}%", actor.Stats[Stat.CriticalRate]);
                CritDLabel.Text = string.Format("{0}", actor.Stats[Stat.CriticalDamage]);
                AttkSpdLabel.Text = string.Format("{0}", actor.Stats[Stat.AttackSpeed]);
                AccLabel.Text = string.Format("+{0}", actor.Stats[Stat.Accuracy]);
                AgilLabel.Text = string.Format("+{0}", actor.Stats[Stat.Agility]);
                LuckLabel.Text = string.Format("{0}", actor.Stats[Stat.Luck]);
            };

            HealthLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 20),
                NotControl = true,
                Text = "0-0",
            };

            ManaLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 38),
                NotControl = true,
                Text = "0-0",
            };

            ACLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 56),
                NotControl = true,
                Text = "0-0",
            };

            MACLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 74),
                NotControl = true,
                Text = "0-0",
            };
            DCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 92),
                NotControl = true,
                Text = "0-0"
            };
            MCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 110),
                NotControl = true,
                Text = "0/0"
            };
            SCLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 128),
                NotControl = true,
                Text = "0/0"
            };
            //Breezer - New Labels
            CritRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 146),
                NotControl = true
            };
            CritDLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 164),
                NotControl = true
            };
            AttkSpdLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 182),
                NotControl = true
            };
            AccLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 200),
                NotControl = true
            };
            AgilLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 218),
                NotControl = true
            };
            LuckLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatusPage,
                Location = new Point(126, 236),
                NotControl = true
            };
        }


        private void CreateStatePage(UserObject actor, MirGridType gridType)
        {
            StatePage = new MirImageControl
            {
                Parent = this,
                Location = new Point(8, 90),
                Visible = false,
                NotControl = true,
            };
            new MirImageControl
            {
                Parent = StatePage,
                Index = 507,
                Library = Libraries.Title,
                NotControl = true,
                Location = new Point(0, 0),
            };
            StatePage.BeforeDraw += (o, e) =>
            {
                ExpPLabel.Text = string.Format("{0:0.##%}", actor.Experience / (double)actor.MaxExperience);
                BagWLabel.Text = string.Format("{0}/{1}", actor.CurrentBagWeight, actor.Stats[Stat.BagWeight]);
                WearWLabel.Text = string.Format("{0}/{1}", actor.CurrentWearWeight, actor.Stats[Stat.WearWeight]);
                HandWLabel.Text = string.Format("{0}/{1}", actor.CurrentHandWeight, actor.Stats[Stat.HandWeight]);
                MagicRLabel.Text = string.Format("+{0}", actor.Stats[Stat.MagicResist]);
                PoisonResLabel.Text = string.Format("+{0}", actor.Stats[Stat.PoisonResist]);
                HealthRLabel.Text = string.Format("+{0}", actor.Stats[Stat.HealthRecovery]);
                ManaRLabel.Text = string.Format("+{0}", actor.Stats[Stat.SpellRecovery]);
                PoisonRecLabel.Text = string.Format("+{0}", actor.Stats[Stat.PoisonRecovery]);
                HolyTLabel.Text = string.Format("+{0}", actor.Stats[Stat.Holy]);
                FreezeLabel.Text = string.Format("+{0}", actor.Stats[Stat.Freezing]);
                PoisonAtkLabel.Text = string.Format("+{0}", actor.Stats[Stat.PoisonAttack]);
            };

            ExpPLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 20),
                NotControl = true,
                Text = "0-0",
            };

            BagWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 38),
                NotControl = true,
                Text = "0-0",
            };

            WearWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 56),
                NotControl = true,
                Text = "0-0",
            };

            HandWLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 74),
                NotControl = true,
                Text = "0-0",
            };
            MagicRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 92),
                NotControl = true,
                Text = "0-0"
            };
            PoisonResLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 110),
                NotControl = true,
                Text = "0/0"
            };
            HealthRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 128),
                NotControl = true,
                Text = "0/0"
            };
            //Breezer
            ManaRLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 146),
                NotControl = true
            };
            PoisonRecLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 164),
                NotControl = true
            };
            HolyTLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 182),
                NotControl = true
            };
            FreezeLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 200),
                NotControl = true
            };
            PoisonAtkLabel = new MirLabel
            {
                AutoSize = true,
                Parent = StatePage,
                Location = new Point(126, 218),
                NotControl = true
            };
        }


        private void CreateSkillPage(UserObject actor, MirGridType gridType)
        {
            SkillPage = new MirImageControl
            {
                Parent = this,
                Location = new Point(8, 90),
                Visible = false,
                NotControl = true,
            };
            new MirImageControl
            {
                Parent = SkillPage,
                Index = 508,
                Library = Libraries.Title,
                NotControl = true,
                Location = new Point(0, 0),
            };

            Magics = new MagicButton[7];

            for (int i = 0; i < Magics.Length; i++)
                Magics[i] = new MagicButton
                {
                    Parent = SkillPage,
                    Visible = false,
                    Location = new Point(8, 8 + i * 33),
                    HeroMagic = gridType == MirGridType.HeroEquipment
                };

            NextButton = new MirButton
            {
                Index = 396,
                Location = new Point(140, 250),
                Library = Libraries.Prguse,
                Parent = SkillPage,
                PressedIndex = 397,
                Sound = SoundList.ButtonA,
            };
            NextButton.Click += (o, e) =>
            {
                if (StartIndex + 7 >= actor.Magics.Count) return;

                StartIndex += 7;
                RefreshInterface();
            };

            BackButton = new MirButton
            {
                Index = 398,
                Location = new Point(90, 250),
                Library = Libraries.Prguse,
                Parent = SkillPage,
                PressedIndex = 399,
                Sound = SoundList.ButtonA,
            };
            BackButton.Click += (o, e) =>
            {
                if (StartIndex - 7 < 0) return;

                StartIndex -= 7;
                RefreshInterface();
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

        public void ShowCharacterPage()
        {
            CharacterPage.Visible = true;
            StatusPage.Visible = false;
            StatePage.Visible = false;
            SkillPage.Visible = false;
            CharacterButton.Index = 500;
            StatusButton.Index = -1;
            StateButton.Index = -1;
            SkillButton.Index = -1;
        }

        private void ShowStatusPage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = true;
            StatePage.Visible = false;
            SkillPage.Visible = false;
            CharacterButton.Index = -1;
            StatusButton.Index = 501;
            StateButton.Index = -1;
            SkillButton.Index = -1;
        }

        private void ShowStatePage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = false;
            StatePage.Visible = true;
            SkillPage.Visible = false;
            CharacterButton.Index = -1;
            StatusButton.Index = -1;
            StateButton.Index = 502;
            SkillButton.Index = -1;
        }

        public void ShowSkillPage()
        {
            CharacterPage.Visible = false;
            StatusPage.Visible = false;
            StatePage.Visible = false;
            SkillPage.Visible = true;
            CharacterButton.Index = -1;
            StatusButton.Index = -1;
            StateButton.Index = -1;
            SkillButton.Index = 503;
            //StartIndex = 0;
        }

        private void RefreshInterface()
        {
            switch (Actor.Class)
            {
                case MirClass.Warrior:
                    ClassImage.Index = 100;// + offSet * 5;
                    break;
                case MirClass.Wizard:
                    ClassImage.Index = 101;// + offSet * 5;
                    break;
                case MirClass.Taoist:
                    ClassImage.Index = 102;// + offSet * 5;
                    break;
                case MirClass.Assassin:
                    ClassImage.Index = 103;// + offSet * 5;
                    break;
                case MirClass.Archer:
                    ClassImage.Index = 104;// + offSet * 5;
                    break;
            }

            NameLabel.Text = Actor.Name;
            GuildLabel.Text = Actor.GuildName + " " + Actor.GuildRankName;

            for (int i = 0; i < Magics.Length; i++)
            {
                if (i + StartIndex >= Actor.Magics.Count)
                {
                    Magics[i].Visible = false;
                    continue;
                }

                Magics[i].Visible = true;
                Magics[i].Update(Actor.Magics[i + StartIndex]);
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
