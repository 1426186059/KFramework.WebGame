using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class MainDialog : MirImageControl
    {
        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public MirProgressBar ExperienceBar, SExpBar;
        public MirImageControl WeightBar, LeftCap, RightCap;
        public MirButton GameShopButton, InventoryButton, CharacterButton, SkillButton;
        public MirButton SettingButton, AuctionButton, RebirthButton;
        public MirControl HealthOrb;
        public MirLabel HealthLabel, ManaLabel, TopLabel, BottomLabel, LevelLabel, CharacterName, ExperienceLabel, SExpLabel, GoldLabel, WeightLabel, SpaceLabel, AModeLabel, PModeLabel;
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
                SizeScale = new KFramework.MonoGame.Vector2(1.1f, 1.1f),
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
                SizeScale = new KFramework.MonoGame.Vector2(1.1f, 1.1f),
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
                SizeScale = new KFramework.MonoGame.Vector2(1.1f, 1.1f),
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
                SizeScale = new KFramework.MonoGame.Vector2(1.1f, 1.1f),
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

            InventoryButton = CreateMainButton(
                "btn_gongneng_09", "btn_gongneng_10", "word_xitong_10",
                new Point(this.Size.Width - 205, 40),
                LanguageMgr.Get("InventoryKey", CMain.InputKeys.GetKey(KeybindOptions.Inventory)));
            InventoryButton.Click += (o, e) =>
            {
                if (GameScene.Scene.InventoryDialog.Visible)
                {
                    GameScene.Scene.InventoryDialog.Hide();
                }
                else
                {
                    GameScene.Scene.InventoryDialog.Show();
                }
            };

            CharacterButton = CreateMainButton(
                "btn_gongneng_07", "btn_gongneng_08", "word_xitong_09",
                new Point(this.Size.Width - 250, 60),
                LanguageMgr.Get("CharacterKey"));
            CharacterButton.Click += (o, e) =>
            {
                if (GameScene.Scene.CharacterDialog.Visible && GameScene.Scene.CharacterDialog.CharacterPage.Visible)
                {
                    GameScene.Scene.CharacterDialog.Hide();
                }
                else
                {
                    GameScene.Scene.CharacterDialog.Show();
                    GameScene.Scene.CharacterDialog.ShowCharacterPage();
                }
            };

            SkillButton = CreateMainButton(
                "btn_gongneng_11", "btn_gongneng_12", "word_xitong_14",
                new Point(this.Size.Width - 155, 50),
                LanguageMgr.Get("SkillsKey", CMain.InputKeys.GetKey(KeybindOptions.Skills)));
            SkillButton.Click += (o, e) =>
            {
                GameScene.Scene.CharacterSkillDialog.Toggle();
            };


            //设置
            SettingButton = CreateMainButton(
                "btn_gongneng_13", "btn_gongneng_14", "word_xitong_13",
                new Point(this.Size.Width - 115, 80), LanguageMgr.Get("SettingButtonHint"));
            SettingButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.OptionDialog.Visible) GameScene.Scene.OptionDialog.Show();
                else GameScene.Scene.OptionDialog.Hide();
            };

            //拍卖
            AuctionButton = CreateMainButton(
                "btn_gongneng_15", "btn_gongneng_16", "word_xitong_12",
                new Point(this.Size.Width - 110, 130), LanguageMgr.Get("AuctionButtonHint"));
            AuctionButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.MarketRefresh());
            };

            //转生。
            RebirthButton = CreateMainButton(
                "btn_gongneng_17", "btn_gongneng_18", "word_xitong_11",
                new Point(this.Size.Width - 120, 180), LanguageMgr.Get("RebirthButtonHint"));


            GameShopButton = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_19",
                PressedAtlasFrame = "btn_gongneng_20",
                Size = new Size(45, 45),
                Location = new Point(this.Size.Width - 170, 100),
                Parent = this,
                Sound = SoundList.ButtonC,
                Hint = LanguageMgr.Get("GameShopKey", CMain.InputKeys.GetKey(KeybindOptions.GameShop))
            };

            GameShopButton.Click += (o, e) =>
            {
                if (!GameScene.Scene.GameShopDialog.Visible) GameScene.Scene.GameShopDialog.Show();
                else GameScene.Scene.GameShopDialog.Hide();
            };

            this.RankBtn = this.RankBtnStyle(
                "word_xitong_03", 
                0, 
                LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                );

            this.GuildBtn = this.RankBtnStyle(
                 "word_xitong_02", 
                 1,
                 LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.HeChengBtn = this.RankBtnStyle(
                 "word_xitong_04",
                 2,
                 LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.QiangXingBtn = this.RankBtnStyle(
                 "word_xitong_05",
                 3,
                 LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );
                
            this.ZhuangBanBtn = this.RankBtnStyle(
                 "word_xitong_06",
                 4,
                 LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                 );

            this.RongLuBtn = this.RankBtnStyle(
                   "word_xitong_07",
                   5,
                   LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                   );

            this.SheJiaoBtn = this.RankBtnStyle(
                "word_xitong_08",
                6,
                LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                );

            this.TianShenBtn = this.RankBtnStyle(
                  "word_xitong_16",
                  7,
                  LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                  );

            this.ShenChongBtn = this.RankBtnStyle(
                  "word_xitong_17",
                  8,
                  LanguageMgr.Get("RankingKey", CMain.InputKeys.GetKey(KeybindOptions.Ranking))
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
                ForeColour = CommonColor.White,
                Animated = false,
                Visible = false,
                NotControl = true,                // 不拦截按钮点击
                AtlasNameKey = "resource/assets/gameui4/movieClip/autoFightEff.json",
            };

            AutoFightButton.Click += (o, e) =>
            {
                bool on = !GameScene.Scene.MapControl.AutoFight;
                GameScene.Scene.MapControl.AutoFight = on;
                if (on)
                {
                    // 自动战斗与自动寻路互斥：开启战斗就停掉大地图/点地面的寻路。
                    GameScene.Scene.MapControl.AutoPath = false;
                }
                MapControl.ResetAutoFightGroundHint();
                SetAutoFightSpin(on);
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
                // 返回选人界面：复用 GameScene.LogOut()（战斗冷却检查 + 二次确认 + 发 C.LogOut），
                // 服务端回 S.LogOutSuccess 后由 GameScene 切到 SelectScene。
                GameScene.Scene.LogOut();
            };



            var mLevel0 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_22",
                Location = new Point(this.Size.Width - 250, 115),
                Parent = this,
                NotControl = true,
            };

            var mLevel1 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_21",
                Location = new Point(this.Size.Width - 270, 110),
                Parent = this,
                NotControl = true,
            };

            LevelLabel = new MirLabel
            {
                Size = new Size(50, 22),
                Parent = this,
                Location = new Point(this.Size.Width - 240, 116),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                NotControl = true,
                OutLine = true,
                Font = FontConst.F9
            };

            // 原始传奇主界面 img_exp：图集 y_homeUI-sheet#btn_gongneng_25，宽 70，scaleX=经验比例。
            // 用 MirProgressBar 的 Fill 拉伸同一帧实现，轨道/左右端盖仍用上面的独立图片。
            ExperienceBar = new MirProgressBar
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_bg3",
                Location = new Point(this.Size.Width - 248, 155),
                Parent = this,
                NotControl = true,
                AutoSize = false,
                Size = new Size(78, 13),
                Minimum = 0,
                Maximum = 100,
            };
            ExperienceBar.Fill.AtlasNameKey = "y_homeUI-sheet#btn_gongneng_25";
            ExperienceBar.Fill.DrawControlTexture = false;

            var mProgressBar0 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_26",
                Location = new Point(this.Size.Width - 250, 150),
                Parent = this,
                NotControl = true,
            };

            var mProgressBar1 = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_24",
                Location = new Point(this.Size.Width - 270, 145),
                Parent = this,
                NotControl = true,
            };
            
            ExperienceLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(this.Size.Width - 240, 151),
                AutoSize = false,
                Size = new Size(60, 22),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                NotControl = true,
                Font = FontConst.F9
            };

            // 原始传奇 10倍经验点条：img_expMedal = btn_gongneng_28，宽 90；阈值 cm.global[34201]=40。
            SExpBar = new MirProgressBar
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_bg2",
                Location = new Point(this.Size.Width - 248, 190),
                Parent = this,
                NotControl = true,

                AutoSize = false,
                Size = new Size(98, 11),
                Minimum = 0,
                Maximum = 100,
            };
            SExpBar.Fill.AtlasNameKey = "y_homeUI-sheet#btn_gongneng_28";
            SExpBar.Fill.DrawControlTexture = false;
            
            var sexpRight = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_29",
                Location = new Point(this.Size.Width - 250, 185),
                Parent = this,
                NotControl = true,
            };

            var sexpLeft = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_gongneng_27",
                Location = new Point(this.Size.Width - 270, 180),
                Parent = this,
                NotControl = true,
            };

            SExpLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(this.Size.Width - 240, 186),
                AutoSize = false,
                Size = new Size(70, 22),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                NotControl = true,
                Font = FontConst.F9
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
                ForeColour = CommonColor.Yellow,
                OutLineColour = CommonColor.Black,
                Parent = this,
                Location = new Point(Settings.Resolution != 800 ? 899 : 675, Settings.Resolution != 800 ? -448 : -280),
                Visible = Settings.ModeView
            };

            PModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = CommonColor.Orange,
                OutLineColour = CommonColor.Black,
                Parent = this,
                Location = new Point(230, 125),
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
                ForeColour = CommonColor.White,
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
                ForeColour = CommonColor.White,
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

        private MirButton CreateMainButton(
            string frame, 
            string pressedFrame, 
            string labelFrame, 
            Point location, 
            string hint)
        {
            MirButton button = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = frame,
                PressedAtlasFrame = pressedFrame,
                Size = new Size(45, 45),
                Location = location,
                Parent = this,
                Sound = SoundList.ButtonA,
                Hint = hint,
            };

            if (!string.IsNullOrEmpty(labelFrame))
            {
                button.LabelImage.AtlasNameKey = "y_homeUI-sheet";
                button.LabelImage.AtlasFrame = labelFrame;
                button.LabelImage.Size = new Size(25, 14);
                button.LabelImage.Visible = true;
                button.LabelImage.Location = button.LabelImage.Bottom.Add(new Point(0, 0));
            }

            return button;
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
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_Peace");
                    break;
                case AttackMode.Group:
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_Group");
                    break;
                case AttackMode.Guild:
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_Guild");
                    break;
                case AttackMode.EnemyGuild:
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_EnemyGuild");
                    break;
                case AttackMode.RedBrown:
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_RedBrown");
                    break;
                case AttackMode.All:
                    AModeLabel.Text = LanguageMgr.Get("AttackMode_All");
                    break;
            }

            switch (GameScene.Scene.PMode)
            {
                case PetMode.Both:
                    PModeLabel.Text = LanguageMgr.Get("PetMode_Both");
                    break;
                case PetMode.MoveOnly:
                    PModeLabel.Text = LanguageMgr.Get("PetMode_MoveOnly");
                    break;
                case PetMode.AttackOnly:
                    PModeLabel.Text = LanguageMgr.Get("PetMode_AttackOnly");
                    break;
                case PetMode.None:
                    PModeLabel.Text = LanguageMgr.Get("PetMode_None");
                    break;
                case PetMode.FocusMasterTarget:
                    PModeLabel.Text = LanguageMgr.Get("PetMode_FocusMasterTarget");
                    break;
            }

            UpdateExperienceBar();
            UpdateSExpBar();
        }

        private void UpdateExperienceBar()
        {
            LevelLabel.Text = $"{User.Level}级";

            double expPercent = User.MaxExperience > 0
                ? Math.Min(1d, Math.Max(0d, User.Experience / (double)User.MaxExperience))
                : 0d;

            float percent = (float)expPercent * 100f;
            ExperienceBar.Value = percent;
            ExperienceLabel.Text = Math.Min(99, (int)Math.Round(percent)) + "%";
        }

        private void UpdateSExpBar()
        {
            const uint SExpMaxPoints = 40; // cm.global[34201]
            float sexpPercent = User.SExp >= SExpMaxPoints
                ? 100f
                : (float)User.SExp * 100f / SExpMaxPoints;
            SExpBar.Value = sexpPercent;
            SExpLabel.Text = User.SExp.ToString();
        }

        //血液动画
        private void HealthOrb_BeforeDraw(object sender, EventArgs e)
        {
            float fHPPercent = User.HP / (float)User.Stats[Stat.HP];
            this.mHPAni.FillAmount = fHPPercent;
            this.mHPAni1.Location = new Point(0, (int)(10 + 122 * this.mHPAni.SizeScale.Y * (1 - fHPPercent)));
            if (HPOnly) return;
            float fMPPercent = User.MP / (float)User.Stats[Stat.MP];
            this.mMPAni.FillAmount = fMPPercent;
            this.mMPAni1.Location = new Point(0, (int)(10 + 122 * this.mMPAni.SizeScale.Y * (1 - fMPPercent)));
        }

    }

}
