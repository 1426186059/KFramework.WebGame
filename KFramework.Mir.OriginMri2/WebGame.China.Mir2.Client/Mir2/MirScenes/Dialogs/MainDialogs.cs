using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    public sealed class MainDialog : MirImageControl
    {
        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public MirImageControl ExperienceBar, WeightBar, LeftCap, RightCap;
        public MirButton GameShopButton, MenuButton, InventoryButton, CharacterButton, SkillButton, QuestButton, OptionButton;
        public MirControl HealthOrb;
        public MirLabel HealthLabel, ManaLabel, TopLabel, BottomLabel, LevelLabel, CharacterName, ExperienceLabel, GoldLabel, WeightLabel, SpaceLabel, AModeLabel, PModeLabel, SModeLabel;
        public HeroInfoPanel HeroInfoPanel;
        public HeroBehaviourPanel HeroBehaviourPanel;

        public MirButton HeroMenuButton, HeroSummonButton;

        MirAnimatedAtlasControl mHPAni, mHPAni1, mMPAni, mMPAni1;

        // 原始传奇主界面「自动战斗/自动寻路」序列帧提示（auto_eff/zdzhandou、zdxunlu）。
        // 位置：原始传奇 PC 布局 x=屏宽/2-110、y=屏高-290；MainDialog 为底部居中(1151×237)，故相对=(465,-53)。
        private MirAnimatedAtlasControl mAutoFightEff, mAutoPathEff;
        private bool mLastAutoFight, mLastAutoPath;
        private const string AutoFightSheetKey = "resource/assets/auto_eff/zdzhandou.json";
        private const string AutoPathSheetKey = "resource/assets/auto_eff/zdxunlu.json";

        public MirButton RankBtn, GuildBtn, HeChengBtn, QiangXingBtn, ZhuangBanBtn, RongLuBtn, SheJiaoBtn, TianShenBtn, ShenChongBtn;
        public MirButton AutoFightButton;   // 自动战斗开关按钮（原始传奇 btn_auto）
        private MirAnimatedAtlasControl mAutoFightSpin; // 按钮上的转圈特效（原始传奇 autoFightEff，x=-6,y=-7，ADD）

        public bool HPOnly
        {
            get { return User != null && User.Class == MirClass.Warrior && User.Level < 26; }
        }

        public MainDialog()
        {
            PixelDetect = true;
            RaycastTarget_OnlyMe = false; // 按「自身 + 整棵子树」整体判定命中（子控件探出本容器矩形时仍算命中）
            HitSelf = false;

            Anchor = EAnchorType.BottomCenter;
            AnchorPos = new Point(0, 0);
            ApplyAnchor();
            this.Size = new Size(1151, 237);

            HealthOrb = new MirControl
            {
                Parent = this,
                Location = new Point(168, 90),
                NotControl = true,
            };
            HealthOrb.BeforeDraw += HealthOrb_BeforeDraw;

            this.mHPAni = new MirAnimatedAtlasControl
            {
                Parent = HealthOrb,
                Location = new Point(0, 0),
                Animated = true,
                Loop = true,
                AnimationDelay = 120,
                Pivot = new PointF(1, 0f),
                AtlasNameKey = "y_homeUI-sheet",
                SizeScale = 1.1f,
                FillOrigin = MirFillOrigin.Bottom,
            };
            this.mHPAni.SetFrames("y_h_hp_", 0, 9);

            this.mHPAni1 = new MirAnimatedAtlasControl
            {
                Parent = mHPAni,
                Location = new Point(0, 0),
                Animated = true,
                Loop = true,
                AnimationDelay = 120,
                Pivot = new PointF(1, 1f),
                AtlasNameKey = "y_homeUI-sheet",
                SizeScale = 1.1f,
            };
            this.mHPAni1.SetFrames("y_h_pl_", 0, 9);

            this.mMPAni = new MirAnimatedAtlasControl
            {
                Parent = HealthOrb,
                Location = new Point(0, 0),
                Animated = true,
                Loop = true,
                AnimationDelay = 120,
                AtlasNameKey = "y_homeUI-sheet",
                SizeScale = 1.1f,
                FillOrigin = MirFillOrigin.Bottom,
            };
            this.mMPAni.SetFrames("y_h_mp_", 0, 9);

            this.mMPAni1 = new MirAnimatedAtlasControl
            {
                Parent = mMPAni,
                Location = new Point(0, 0),
                Animated = true,
                Loop = true,
                AnimationDelay = 120,
                AtlasNameKey = "y_homeUI-sheet",
                SizeScale = 1.1f,
                Pivot = new PointF(0, 1f),
            };
            this.mMPAni1.SetFrames("y_h_mpl_", 0, 9);

            var mChatBg = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_msbg",
                Parent = this,

                AutoSize = false,
                Size = new Size(650, 130),
                Anchor = EAnchorType.BottomCenter,
                AnchorPos = new Point(0, -10),
            };
            mChatBg.ApplyAnchor();

            var mMainUI = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "bg_zhukuang_1_tx",
                Location = new Point(0, 0),
                Parent = this,
                PixelDetect = true,
            };

            InventoryButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_09",
                PressedAtlasFrame = "btn_gongneng_10",

                Size = new Size(45, 45),
                Location = new Point(this.Size.Width - 200, 30),
                Parent = this,
                PressedIndex = 1905,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.InventoryKey), CMain.InputKeys.GetKey(KeybindOptions.Inventory))
            };

            InventoryButton.LabelImage.AtlasNameKey = "y_homeUI-sheet";
            InventoryButton.LabelImage.AtlasFrame = "word_xitong_10";
            InventoryButton.LabelImage.Size = new Size(25, 14);
            InventoryButton.LabelImage.Location =
                InventoryButton.LabelImage.Bottom.Add(new Point(0, 0));
            InventoryButton.LabelImage.Visible = true;

            InventoryButton.Click += (o, e) =>
            {
                if (GameScene.Scene.InventoryDialog.Visible)
                    GameScene.Scene.InventoryDialog.Hide();
                else
                    GameScene.Scene.InventoryDialog.Show();
            };

            CharacterButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_07",
                PressedAtlasFrame = "btn_gongneng_08",
                Size = new Size(45, 45),
                Location = new Point(this.Size.Width - 250, 55),
                Parent = this,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.CharacterKey)
            };
            CharacterButton.LabelImage.AtlasNameKey = "y_homeUI-sheet";
            CharacterButton.LabelImage.AtlasFrame = "word_xitong_09";
            CharacterButton.LabelImage.Size = new Size(25, 14);
            CharacterButton.LabelImage.Location =
                CharacterButton.LabelImage.Bottom.Add(new Point(0, 0));
            CharacterButton.LabelImage.Visible = true;

            CharacterButton.Click += (o, e) =>
            {
                if (GameScene.Scene.CharacterDialog.Visible && GameScene.Scene.CharacterDialog.CharacterPage.Visible)
                    GameScene.Scene.CharacterDialog.Hide();
                else
                {
                    GameScene.Scene.CharacterDialog.Show();
                    GameScene.Scene.CharacterDialog.ShowCharacterPage();
                }
            };

            SkillButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_11",
                PressedAtlasFrame = "btn_gongneng_12",
                Size = new Size(45, 45),
                Location = new Point(this.Size.Width - 150, 50),
                Parent = this,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SkillsKey), CMain.InputKeys.GetKey(KeybindOptions.Skills))
            };
            SkillButton.LabelImage.AtlasNameKey = "y_homeUI-sheet";
            SkillButton.LabelImage.AtlasFrame = "word_xitong_14";
            SkillButton.LabelImage.Size = new Size(25, 14);
            SkillButton.LabelImage.Location =
                SkillButton.LabelImage.Bottom.Add(new Point(0, 0));
            SkillButton.LabelImage.Visible = true;

            SkillButton.Click += (o, e) =>
            {
                GameScene.Scene.CharacterSkillDialog.Toggle();
            };

            QuestButton = new MirButton
            {
                HoverIndex = 1910,
                Index = 1909,
                Library = Libraries.Prguse,
                Location = new Point(this.Size.Width - 120, 100),
                Parent = this,
                PressedIndex = 1911,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.QuestsKey), CMain.InputKeys.GetKey(KeybindOptions.Quests))
            };
            QuestButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.QuestLogDialog.Visible)
                    GameScene.Scene.QuestLogDialog.Show();
                else GameScene.Scene.QuestLogDialog.Hide();
            };

            OptionButton = new MirButton
            {
                HoverIndex = 1913,
                Index = 1912,
                Library = Libraries.Prguse,
                Location = new Point(this.Size.Width - 100, 130),
                Parent = this,
                PressedIndex = 1914,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.OptionsKey), CMain.InputKeys.GetKey(KeybindOptions.Options))
            };
            OptionButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.OptionDialog.Visible)
                    GameScene.Scene.OptionDialog.Show();
                else GameScene.Scene.OptionDialog.Hide();
            };

            MenuButton = new MirButton
            {
                HoverIndex = 1961,
                Index = 1960,
                Library = Libraries.Prguse,
                Location = new Point(this.Size.Width - 100, 160),
                Parent = this,
                PressedIndex = 1962,
                Sound = SoundList.ButtonC,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Menu)
            };
            MenuButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.MenuDialog.Visible) GameScene.Scene.MenuDialog.Show();
                else GameScene.Scene.MenuDialog.Hide();
            };

            GameShopButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_19",
                PressedAtlasFrame = "btn_gongneng_20",
                Size = new Size(45, 45),
                Location = new Point(this.Size.Width - 170, 100),
                Parent = this,
                Sound = SoundList.ButtonC,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.GameShopKey), CMain.InputKeys.GetKey(KeybindOptions.GameShop))
            };

            GameShopButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.GameShopDialog.Visible) GameScene.Scene.GameShopDialog.Show();
                else GameScene.Scene.GameShopDialog.Hide();
            };

            this.RankBtn = this.RankBtnStyle(
                "word_xitong_03", 
                0, 
                GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                );

            this.GuildBtn = this.RankBtnStyle(
                 "word_xitong_02", 
                 1,
                 GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.HeChengBtn = this.RankBtnStyle(
                 "word_xitong_04",
                 2,
                 GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.QiangXingBtn = this.RankBtnStyle(
                 "word_xitong_05",
                 3,
                 GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );
                
            this.ZhuangBanBtn = this.RankBtnStyle(
                 "word_xitong_06",
                 4,
                 GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.RongLuBtn = this.RankBtnStyle(
                   "word_xitong_07",
                   5,
                   GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                   );

            this.SheJiaoBtn = this.RankBtnStyle(
                "word_xitong_08",
                6,
                GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                );

            this.TianShenBtn = this.RankBtnStyle(
                  "word_xitong_16",
                  7,
                  GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                  );

            this.ShenChongBtn = this.RankBtnStyle(
                  "word_xitong_17",
                  8,
                  GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                  );

            // 自动战斗开关按钮（原始传奇 btn_auto）：左侧空位，点击切换 MapControl.AutoFight。
            AutoFightButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_btn6",
                Size = new Size(59, 59),
                Location = new Point(900, -40),
                Parent = this,
                Sound = SoundList.ButtonC,
            };

            mAutoFightSpin = new MirAnimatedAtlasControl
            {
                Parent = AutoFightButton,
                Location = new Point(-6, -8),   // 让约 70×65 的转圈居中覆盖在 34×18 的按钮上
                Loop = true,
                ForeColour = Color.White,
                Animated = false,
                Visible = false,
                NotControl = true,                // 不拦截按钮点击
                AtlasNameKey = "resource/assets/gameui4/movieClip/autoFightEff.json",
            };

            AutoFightButton.Click += (o, e) =>
            {
                GameScene.Scene.MapControl.AutoFight = !GameScene.Scene.MapControl.AutoFight;
                bool on = GameScene.Scene.MapControl.AutoFight;
                MapControl.ResetAutoFightGroundHint();
                SetAutoFightSpin(on);
                GameScene.Scene.ChatDialog.ReceiveChat(on ? "自动战斗：开启" : "自动战斗：关闭", ChatType.System);
            };

            var quitBtn = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_05",
                PressedAtlasFrame = "btn_gongneng_06",
                Location = new Point(830, 88),
                Parent = this,
                Sound = SoundList.ButtonC,
                Hint = Hint
            };
            quitBtn.Click += (o, e) =>
            {

            };

            var mLevel0 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_22",
                Location = new Point(this.Size.Width - 225, 135),
                Parent = this,
                NotControl = true,
            };

            var mLevel1 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_21",
                Location = new Point(this.Size.Width - 250, 130),
                Parent = this,
                NotControl = true,
            };

            LevelLabel = new MirLabel
            {
                Size = new Size(50, 22),
                Parent = this,
                Location = new Point(this.Size.Width - 218, 135),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                NotControl = true,
            };

            var mProgressBar00 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_bg3",
                Location = new Point(this.Size.Width - 225, 168),
                Parent = this,
                NotControl = true,
            };

            var mProgressBar0 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_26",
                Location = new Point(this.Size.Width - 225, 165),
                Parent = this,
                NotControl = true,
            };

            var mProgressBar1 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_24",
                Location = new Point(this.Size.Width - 250, 160),
                Parent = this,
                NotControl = true,
            };

            ExperienceBar = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_25",
                Location = new Point(this.Size.Width - 250, 165),
                Parent = this,
                DrawImage = false,
                NotControl = true,
            };
            ExperienceBar.BeforeDraw += ExperienceBar_BeforeDraw;

            ExperienceLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(this.Size.Width - 250, 165),
                AutoSize = false,
                Size = new Size(88, 22),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                NotControl = true,
            };

            SpaceLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(this.Size.Width - 30, 101),
                Size = new Size(26, 14),
            };

            AModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Yellow,
                OutLineColour = Color.Black,
                Parent = this,
                Location = new Point(Settings.Resolution != 800 ? 899 : 675, Settings.Resolution != 800 ? -448 : -280),
                Visible = Settings.ModeView
            };

            PModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Orange,
                OutLineColour = Color.Black,
                Parent = this,
                Location = new Point(230, 125),
                Visible = Settings.ModeView
            };

            SModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.LimeGreen,
                OutLineColour = Color.Black,
                Parent = this,
                Location = new Point(899, -463),
                Visible = Settings.ModeView
            };

            // 原始传奇主界面自动状态序列帧：位置 x=屏宽/2-110、y=屏高-290（PC）。
            // MainDialog 底部居中(1151×237)，故相对坐标 x≈465、y=-53（血条上方）。
            // 直接设 AtlasNameKey 让控件自动异步加载图集（同血条动画），无需手动 GetSheetAsync。
            // 帧序：原始传奇 auto_eff mc 时间线 00000_png..00008_png，frameRate=9 → 约111ms/帧。
            mAutoFightEff = new MirAnimatedAtlasControl
            {
                Parent = this,
                Location = new Point(465, -53),
                Loop = true,
                Blending = true,               // 原始传奇 blendMode = ADD
                ForeColour = Color.White,
                Animated = false,
                Visible = false,
                NotControl = true,
                AtlasNameKey = AutoFightSheetKey,
            };
            mAutoPathEff = new MirAnimatedAtlasControl
            {
                Parent = this,
                Location = new Point(465, -53),
                Loop = true,
                Blending = true,
                ForeColour = Color.White,
                Animated = false,
                Visible = false,
                NotControl = true,
                AtlasNameKey = AutoPathSheetKey,
            };
        }

        private static void SetAutoEff(MirAnimatedAtlasControl clip, bool on)
        {
            if (clip == null) return;
            if (on)
            {
                if (!clip.Animated)
                {
                    clip.OffSet = 0;
                    clip.Animated = true;   // Loop=true，自动循环
                }
                clip.Visible = true;
            }
            else
            {
                clip.Animated = false;
                clip.Visible = false;
            }
        }

        private void SetAutoFightSpin(bool on)
        {
            SetAutoEff(mAutoFightSpin, on);
        }

        private MirButton RankBtnStyle(
            string LabelImageAtlasFrame,
            int nIndex, string Hint)
        {
            var rankBtn = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_01",
                PressedAtlasFrame = "btn_gongneng_02",
                Size = new Size(34, 18),
                Location = new Point(350 + 50 * nIndex, 88),
                Parent = this,
                Sound = SoundList.ButtonC,
                Hint = Hint
            };

            rankBtn.LabelImage.Visible = true;
            rankBtn.LabelImage.AtlasNameKey = "y_homeUI-sheet";
            rankBtn.LabelImage.AtlasFrame = LabelImageAtlasFrame;
            rankBtn.LabelImage.Size = new Size(25, 14);
            rankBtn.LabelImage.Location = rankBtn.LabelImage.Center;
            rankBtn.Click += (o, e) =>
            {
                
            };

            return rankBtn;
        }

        private void MainDialog_AtlasLoaded(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public void Process()
        {
            // 原始传奇主界面自动状态提示：自动战斗/自动寻路分别跟随 MapControl 上的开关。
            bool autoFight = GameScene.Scene.MapControl != null && GameScene.Scene.MapControl.AutoFight;
            bool autoPath = GameScene.Scene.MapControl != null && GameScene.Scene.MapControl.AutoPath;
            if (autoFight != mLastAutoFight || autoPath != mLastAutoPath)
            {
                mLastAutoFight = autoFight;
                mLastAutoPath = autoPath;
                bool showFight = autoFight;
                bool showPath = autoPath && !showFight;
                SetAutoEff(mAutoFightEff, showFight);
                SetAutoFightSpin(autoFight);   // 按钮上的转圈（原始传奇 autoFightEff）
                SetAutoEff(mAutoPathEff, showPath);
            }

            switch (GameScene.Scene.AMode)
            {
                case AttackMode.Peace:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_Peace);
                    break;
                case AttackMode.Group:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_Group);
                    break;
                case AttackMode.Guild:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_Guild);
                    break;
                case AttackMode.EnemyGuild:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_EnemyGuild);
                    break;
                case AttackMode.RedBrown:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_RedBrown);
                    break;
                case AttackMode.All:
                    AModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttackMode_All);
                    break;
            }

            switch (GameScene.Scene.PMode)
            {
                case PetMode.Both:
                    PModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PetMode_Both);
                    break;
                case PetMode.MoveOnly:
                    PModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PetMode_MoveOnly);
                    break;
                case PetMode.AttackOnly:
                    PModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PetMode_AttackOnly);
                    break;
                case PetMode.None:
                    PModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PetMode_None);
                    break;
                case PetMode.FocusMasterTarget:
                    PModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PetMode_FocusMasterTarget);
                    break;
            }

            switch (Settings.SkillMode)
            {
                case true:
                    SModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SkillModeTilde);
                    break;
                case false:
                    SModeLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SkillModeCtrl);
                    break;
            }

            LevelLabel.Text = User.Level.ToString();
            ExperienceLabel.Text = string.Format("{0:#0.##%}", User.Experience / (double)User.MaxExperience);
            
        }

        private void Label_SizeChanged(object sender, EventArgs e)
        {
            if (!(sender is MirLabel l)) return;

            l.Location = new Point(50 - (l.Size.Width / 2), l.Location.Y);
        }

        private void HealthOrb_BeforeDraw(object sender, EventArgs e)
        {
            float fHPPercent = User.HP / (float)User.Stats[Stat.HP];
            this.mHPAni.FillAmount = fHPPercent;
            this.mHPAni1.Location = new Point(0, (int)(10 + 122 * this.mHPAni.SizeScale * (1 - fHPPercent)));
            if (HPOnly) return;
            float fMPPercent = User.MP / (float)User.Stats[Stat.MP];
            this.mMPAni.FillAmount = fMPPercent;
            this.mMPAni1.Location = new Point(0, (int)(10 + 122 * this.mMPAni.SizeScale * (1 - fMPPercent)));
        }

        private void ExperienceBar_BeforeDraw(object sender, EventArgs e)
        {
            if (ExperienceBar.Library == null) return;

            double percent = MapObject.User.Experience / (double)MapObject.User.MaxExperience;
            if (percent > 1) percent = 1;
            if (percent <= 0) return;

            Rectangle section = new Rectangle(0, 0, (int)((ExperienceBar.Size.Width - 3) * percent), ExperienceBar.Size.Height);

            ExperienceBar.Library.Draw(ExperienceBar.Index, section, ExperienceBar.DisplayLocation, Color.White, false);
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
                WeightBar.Index = 76;
            }
            else if (percent <= 0.75)
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 473;
            }
            else
            {
                WeightBar.Library = Libraries.UI_32bit;
                WeightBar.Index = 472;
            }

            Rectangle section = new Rectangle(0, 0, (int)((WeightBar.Size.Width - 2) * percent), WeightBar.Size.Height);

            WeightBar.Library.Draw(WeightBar.Index, section, WeightBar.DisplayLocation, Color.White, false);
        }
    }







}
