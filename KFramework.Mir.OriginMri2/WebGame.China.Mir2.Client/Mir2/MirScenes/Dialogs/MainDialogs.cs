using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using System.Text.RegularExpressions;
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

        public MirImageControl ExperienceBar, WeightBar, LeftCap, RightCap;
        public MirButton GameShopButton, MenuButton, InventoryButton, CharacterButton, SkillButton, QuestButton, OptionButton;
        public MirControl HealthOrb;
        public MirLabel HealthLabel, ManaLabel, TopLabel, BottomLabel, LevelLabel, CharacterName, ExperienceLabel, GoldLabel, WeightLabel, SpaceLabel, AModeLabel, PModeLabel, SModeLabel;
        public HeroInfoPanel HeroInfoPanel;
        public HeroBehaviourPanel HeroBehaviourPanel;

        public MirButton HeroMenuButton, HeroSummonButton;

        MirAnimatedAtlasControl mHPAni, mHPAni1, mMPAni, mMPAni1;

        public MirButton RankBtn, GuildBtn, HeChengBtn, QiangXingBtn, ZhuangBanBtn, RongLuBtn, SheJiaoBtn, TianShenBtn, ShenChongBtn;

        public bool HPOnly
        {
            get { return User != null && User.Class == MirClass.Warrior && User.Level < 26; }
        }

        public MainDialog()
        {
            PixelDetect = true;

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

    public sealed class ChatDialog : MirImageControl
    {
        public List<ChatHistory> FullHistory = new List<ChatHistory>();
        public List<ChatHistory> History = new List<ChatHistory>();
        public List<MirLabel> ChatLines = new List<MirLabel>();
        private MirImageControl ChatHistoryPanel;

        public List<ChatItem> LinkedItems = new List<ChatItem>();
        public List<MirLabel> LinkedItemButtons = new List<MirLabel>();

        public MirButton HomeButton, UpButton, EndButton, DownButton, PositionBar, SendChatBtn;
        public MirImageControl CountBar;
        public MirTextBox ChatTextBox;
        public MirButton ChatInputBtn;
        public Font ChatFont = new Font(Settings.FontName, 8F);

        public int StartIndex, LineCount = 7, WindowSize;
        // 私聊目标角色ID:由好友/伴侣等入口设置;非私聊为 0。取代原先把名字塞进消息文本的做法。
        public uint LastPrivateChatTargetId = 0;

        // 下拉/快捷键选定的“发送频道”;与 CurrentViewChannel(视图过滤)解耦,避免打字时误改视图。
        public ChatChannelFilter CurrentSendChannel = ChatChannelFilter.All;

        // 下拉频道选择后,聊天面板只显示该频道(综合=全部);发送频道由 ToggleChatFilter 一并设置。
        public ChatChannelFilter CurrentViewChannel = ChatChannelFilter.All;

        // 频道下拉相关(从 ChatControlBar 合并)
        private MirDropDownBox2 mChatChannel;
        private MirImageControl mChatChannel_BtnJianTou;

        private readonly List<string> ChatChannelList = new List<string>()
        {
            "综合", "世界", "私聊", "行会", "队伍", "附近"
        };

        // 与 ChatChannelList 一一对应:下拉选中第 i 项时,映射到对应的频道过滤类型。
        private static readonly ChatChannelFilter[] ChannelFilterMap = new ChatChannelFilter[]
        {
            ChatChannelFilter.All,         // 综合
            ChatChannelFilter.World,       // 世界
            ChatChannelFilter.PrivateChat, // 私聊
            ChatChannelFilter.Guild,       // 行会
            ChatChannelFilter.Team,        // 队伍
            ChatChannelFilter.Nearby,      // 附近(本作无独立过滤,归并到无特定频道)
        };

        public ChatDialog()
        {
            //Library = Libraries.Prguse;
            //Location = new Point(GameScene.Scene.MainDialog.Location.X + 230, Settings.ScreenHeight - 97);
            PixelDetect = true;

            //AtlasNameKey = "y_homeUI-sheet";
            //AtlasFrame = "y_h_msbg";
            //AtlasFrame = "bg_liaotianzy_06";

            AutoSize = false;
            Size = new Size(600, 120);
            Anchor = EAnchorType.BottomCenter;
            AnchorPos = new Point(0, -6);
            ApplyAnchor();

            KeyDown += ChatPanel_KeyDown;
            MouseWheel += ChatPanel_MouseWheel;

            var mChatTextBoxBg = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "y_h_msbg",
                AutoSize = false,
                Size = new Size(470, 20),
                Parent = this,
            };
            mChatTextBoxBg.Location = mChatTextBoxBg.Bottom.Add(new Point(0, -5));

            var mChatTipLabel = new MirLabel
            {
                Parent = mChatTextBoxBg,
                Text = "请输入文字",
                Size = new Size(465, 20),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Location = new Point(5, 0),
                Opacity = 0.5f,
            };

            ChatTextBox = new MirTextBox
            {
                BackColour = Color.DarkGray,
                ForeColour = Color.Black,
                Parent = mChatTextBoxBg,
                Size = new Size(470, 20),
                Location = new Point(0, 0),
                MaxLength = Globals.MaxChatLength,
                Visible = false,
                Font = ChatFont,
            };
            ChatTextBox.TextBox.KeyPress += ChatTextBox_KeyPress;
            ChatTextBox.TextBox.KeyDown += ChatTextBox_KeyDown;
            ChatTextBox.TextBox.KeyUp += ChatTextBox_KeyUp;

            // 空按钮:覆盖在聊天输入框背景之上,点击聚焦并开始输入(类似按 Enter 打开聊天)
            ChatInputBtn = new MirButton
            {
                Parent = mChatTextBoxBg,
                Size = mChatTextBoxBg.Size,
                Location = new Point(0, 0),
                Sound = SoundList.ButtonA,
                Text = "",
                CenterText = true,
            };

            ChatInputBtn.Click += (o, e) =>
            {
                StartChatInput();
            };

            // 聊天文字容器:把聊天接收文字(ChatLines / 物品链接)归到其下,使其绘制在输入框与下拉列表之下,
            // 下拉展开时不再被聊天文字挡住。仅绘制、不拦截鼠标(点击/悬停穿透到下层控件)。
            ChatHistoryPanel = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0),
                Size = Size,
                NotControl = true,
            };

            // 频道下拉 + 发送按钮(从 ChatControlBar 合并进来)
            mChatChannel = new MirDropDownBox2()
            {
                BackColour = Color.FromArgb(255, 15, 15, 15),
                ForeColour = Color.White,
                Parent = this,
                Size = new Size(60, 30),
                Location = new Point(0, 90),
                Visible = true,
                Enabled = true,
                DropMode = 1,
            };
            mChatChannel.ValueChanged += (o, e) =>
            {
                // 写回选中项,按钮 Label 才显示当前频道
                mChatChannel.SelectedIndex = mChatChannel._WantedIndex;
                int idx = mChatChannel.SelectedIndex;
                if (idx >= 0 && idx < ChannelFilterMap.Length)
                    ToggleChatFilter(ChannelFilterMap[idx]);
            };
            mChatChannel.DropDownStateChanged += (s, e) =>
            {
                if (mChatChannel_BtnJianTou != null)
                {
                    // 展开时箭头朝上,收起时朝下(翻转是之前给 MirImageControl 加的 FlipY)
                    mChatChannel_BtnJianTou.FlipY = e.IsOpen;
                }
            };

            mChatChannel.Items = ChatChannelList;
            mChatChannel.MinimumOption = 0;
            mChatChannel.SelectedIndex = 0; // 默认显示第一个频道(综合)
            mChatChannel.DropDownButton.Library = null;
            mChatChannel.DropDownButton.Index = 0;
            mChatChannel.DropDownButton.AtlasNameKey = "y_homeUI-sheet";
            mChatChannel.DropDownButton.AtlasFrame = "btn_liaotian_11";
            mChatChannel.DropDownButton.PressedAtlasFrame = "btn_liaotian_12";
            mChatChannel.DropDownButton.Size = new Size(60, 19);
            mChatChannel.ButtonTextOffsetX = -10;
            mChatChannel_BtnJianTou = new MirImageControl
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_liaotian_13",
                Parent = mChatChannel.DropDownButton,
                Size = new Size(12, 9),
                NotControl = true,
            };
            mChatChannel_BtnJianTou.Location = mChatChannel_BtnJianTou.Right.Subtract(new Point(5, 0));

            SendChatBtn = new MirButton
            {
                AtlasNameKey = "y_homeUI-sheet",
                AtlasFrame = "btn_liaotian_11",
                PressedAtlasFrame = "btn_liaotian_12",
                Location = new Point(540, 95),
                Parent = this,
                Sound = SoundList.ButtonA,
                Text = "发送",
                CenterText = true,
            };

            SendChatBtn.Click += (o, e) =>
            {
                SendChatMessage();
            };

            ToggleChatFilter(ChatChannelFilter.All);



            HomeButton = new MirButton
            {
                Index = 2018,
                HoverIndex = 2019,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 618 : 394, 1),
                Parent = this,
                PressedIndex = 2020,
                Sound = SoundList.ButtonA,
            };
            HomeButton.Click += (o, e) =>
            {
                if (StartIndex == 0) return;
                StartIndex = 0;
                Update();
            };


            UpButton = new MirButton
            {
                Index = 2021,
                HoverIndex = 2022,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 618 : 394, 9),
                Parent = this,
                PressedIndex = 2023,
                Sound = SoundList.ButtonA,
            };
            UpButton.Click += (o, e) =>
            {
                if (StartIndex == 0) return;
                StartIndex--;
                Update();
            };


            EndButton = new MirButton
            {
                Index = 2027,
                HoverIndex = 2028,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 618 : 394, 45),
                Parent = this,
                PressedIndex = 2029,
                Sound = SoundList.ButtonA,
            };
            EndButton.Click += (o, e) =>
            {
                if (StartIndex == History.Count - 1) return;
                StartIndex = History.Count - 1;
                Update();
            };

            DownButton = new MirButton
            {
                Index = 2024,
                HoverIndex = 2025,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 618 : 394, 39),
                Parent = this,
                PressedIndex = 2026,
                Sound = SoundList.ButtonA,
            };
            DownButton.Click += (o, e) =>
            {
                if (StartIndex == History.Count - 1) return;
                StartIndex++;
                Update();
            };



            CountBar = new MirImageControl
            {
                Index = 2012,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 622 : 398, 16),
                Parent = this,
            };

            PositionBar = new MirButton
            {
                Index = 2015,
                HoverIndex = 2016,
                Library = Libraries.Prguse,
                Location = new Point(Settings.Resolution != 800 ? 619 : 395, 16),
                Parent = this,
                PressedIndex = 2017,
                Movable = true,
                Sound = SoundList.None,
            };
            PositionBar.OnMoving += PositionBar_OnMoving;
        }

        public void SetChatText(string newText)
        {
            string newMsg = ChatTextBox.Text += newText;

            if (newMsg.Length > Globals.MaxChatLength) return;

            ChatTextBox.Text = newMsg;
            ChatTextBox.SetFocus();
            ChatTextBox.Visible = true;
            ChatTextBox.TextBox.SelectionLength = 0;
            ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
        }

        private void ChatTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    //KFramework.MonoGame.PrintTool.Log($"[Chat] ChatTextBox_KeyPress Enter Visible={ChatTextBox.Visible} Active={CMain.Instance?.ActiveControl?.GetType().Name}");
                    e.Handled = true;
                    SendChatMessage();
                    break;
                case Keys.Escape:
                    e.Handled = true;
                    ChatTextBox.Visible = false;
                    ChatTextBox.Text = string.Empty;
                    LinkedItems.Clear();
                    break;
            }
        }

        public void SendChatMessage()
        {
            string msg = ChatTextBox.Text;

            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.ToUpper() == "@LEVELEFFECT")
                    Settings.LevelEffect = !Settings.LevelEffect;

                if (msg.ToUpper() == "@TARGETDEAD")
                    Settings.TargetDead = !Settings.TargetDead;

                ChatType type = ViewChannelToType(CurrentSendChannel);
                uint targetId = 0;

                if (type == ChatType.PrivateChat)
                {
                    targetId = LastPrivateChatTargetId;
                }
                else
                {
                    // 非私聊:剥掉可能残留的旧版路由前缀(!/!!/!~),避免服务端重复解析;@ 命令前缀保留。
                    msg = StripLegacyPrefix(msg);
                    LastPrivateChatTargetId = 0;
                }

                Network.Enqueue(new C.Chat
                {
                    Message = msg,
                    LinkedItems = new List<ChatItem>(LinkedItems),
                    Type = type,
                    PrivateChatTargetId = targetId,
                });
            }

            // 无论是否发送,都收起输入框(空内容时等同于按 Enter 关闭输入),与 Enter 键行为一致
            ChatTextBox.Visible = false;
            ChatTextBox.Text = string.Empty;
            LinkedItems.Clear();
        }

        // 聚焦聊天输入框并开始输入(供"开始聊天"按钮与 Enter 打开聊天共用)
        public void StartChatInput()
        {
            ChatTextBox.SetFocus();
            // 频道已由下拉/快捷键显式选定(CurrentSendChannel),无需再往输入框塞前缀文本。
            ChatTextBox.Visible = true;
            ChatTextBox.TextBox.SelectionLength = 0;
            ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
        }

        void PositionBar_OnMoving(object sender, MouseEventArgs e)
        {
            int x = Settings.Resolution != 800 ? 619 : 395;
            int y = PositionBar.Location.Y;
            if (y >= 16 + CountBar.Size.Height - PositionBar.Size.Height) y = 16 + CountBar.Size.Height - PositionBar.Size.Height;
            if (y < 16) y = 16;

            int h = CountBar.Size.Height - PositionBar.Size.Height;
            h = (int)((y - 16) / (h / (float)(History.Count - 1)));

            if (h != StartIndex)
            {
                StartIndex = h;
                Update();
            }

            PositionBar.Location = new Point(x, y);
        }

        public void ReceiveChat(string text, ChatType type)
        {
            Color foreColour, backColour;

            switch (type)
            {
                case ChatType.World:
                    backColour = Color.Yellow;
                    foreColour = Color.Black;
                    break;
                case ChatType.System:
                    backColour = Color.Red;
                    foreColour = Color.White;
                    break;
                case ChatType.Team:
                    backColour = Color.White;
                    foreColour = Color.Brown;
                    break;
                case ChatType.PrivateChat:
                    foreColour = Color.CornflowerBlue;
                    backColour = Color.White;
                    break;
                case ChatType.Guild:
                    backColour = Color.White;
                    foreColour = Color.Green;
                    break;
                default:
                    backColour = Color.White;
                    foreColour = Color.Black;
                    break;
            }

            List<string> chat = new List<string>();

            int chatWidth = Settings.Resolution != 800 ? 614 : 390;
            int index = 0;

            for (int i = 1; i < text.Length; i++)
            {
                if (i - index < 0) continue;

                if (TextRenderer.MeasureText(text.Substring(index, i - index), ChatFont, new Size(int.MaxValue, int.MaxValue)).Width > chatWidth)
                {
                    int offset = i - index;
                    int newIndex = i - 1;

                    var itemLinkMatches = RegexFunctions.ChatItemLinks.Matches(text.Substring(index)).Cast<Match>();

                    if (itemLinkMatches.Any())
                    {
                        var match = itemLinkMatches.SingleOrDefault(x => (x.Index < (i - index)) && (x.Index + x.Length > offset - 1));

                        if (match != null)
                        {
                            offset = match.Index;
                            newIndex = match.Index;
                        }
                    }

                    chat.Add(text.Substring(index, offset - 1));
                    index = newIndex;
                }
            }

            chat.Add(text.Substring(index, text.Length - index));

            // 在每条消息首行前加频道前缀,如【世界】【私聊】,便于区分来源
            string channelPrefix = GetChannelPrefix(type);
            if (chat.Count > 0 && !string.IsNullOrEmpty(channelPrefix))
                chat[0] = channelPrefix + chat[0];

            if (StartIndex == History.Count - LineCount)
                StartIndex += chat.Count;

            for (int i = 0; i < chat.Count; i++)
                FullHistory.Add(new ChatHistory { Text = chat[i], BackColour = backColour, ForeColour = foreColour, Type = type });

            Update();
        }

        // 频道 → 显示前缀
        private static string GetChannelPrefix(ChatType type)
        {
            switch (type)
            {
                case ChatType.World: return "【世界】";
                case ChatType.PrivateChat: return "【私聊】";
                case ChatType.Guild: return "【行会】";
                case ChatType.Team: return "【队伍】";
                case ChatType.Nearby: return "【附近】";
                case ChatType.System: return "【系统】";
                default: return "";
            }
        }

        // 视图频道 → 对应的消息类型(All 不会走到这里)
        private static ChatType ViewChannelToType(ChatChannelFilter filter)
        {
            switch (filter)
            {
                case ChatChannelFilter.World: return ChatType.World;
                case ChatChannelFilter.PrivateChat: return ChatType.PrivateChat;
                case ChatChannelFilter.Guild: return ChatType.Guild;
                case ChatChannelFilter.Team: return ChatType.Team;
                case ChatChannelFilter.Nearby: return ChatType.Nearby;
                case ChatChannelFilter.System: return ChatType.System;
                default: return ChatType.Nearby;
            }
        }

        // 剥掉非私聊消息里可能残留的旧版路由前缀(!/!!/!~);注意只判断 '!' 开头,不误伤 '@' 命令前缀。
        private static string StripLegacyPrefix(string msg)
        {
            if (msg.StartsWith("!~")) return msg.Substring(2).TrimStart();
            if (msg.StartsWith("!!")) return msg.Substring(2).TrimStart();
            if (msg.StartsWith("!")) return msg.Substring(1).TrimStart();
            return msg;
        }

        public override void Update()
        {
            base.Update();
            History = new List<ChatHistory>();

            for (int i = 0; i < FullHistory.Count; i++)
            {
                switch (FullHistory[i].Type)
                {
                    case ChatType.Nearby:
                        if (Settings.FilterNormalChat) continue;
                        break;
                    case ChatType.PrivateChat:
                        if (Settings.FilterWhisperChat) continue;
                        break;
                    case ChatType.World:
                        if (Settings.FilterShoutChat) continue;
                        break;
                    case ChatType.System:
                        if (Settings.FilterSystemChat) continue;
                        break;
                    case ChatType.Team:
                        if (Settings.FilterGroupChat) continue;
                        break;
                    case ChatType.Guild:
                        if (Settings.FilterGuildChat) continue;
                        break;
                }

                // 下拉频道视图过滤:选定具体频道时只显示该频道(综合=全部)
                if (CurrentViewChannel != ChatChannelFilter.All && FullHistory[i].Type != ViewChannelToType(CurrentViewChannel))
                    continue;

                History.Add(FullHistory[i]);
            }

            for (int i = 0; i < ChatLines.Count; i++)
                ChatLines[i].Dispose();

            for (int i = 0; i < LinkedItemButtons.Count; i++)
                LinkedItemButtons[i].Dispose();

            ChatLines.Clear();
            LinkedItemButtons.Clear();

            if (StartIndex >= History.Count) StartIndex = History.Count - 1;
            if (StartIndex < 0) StartIndex = 0;

            if (History.Count > 1)
            {
                int h = CountBar.Size.Height - PositionBar.Size.Height;
                h = (int)((h / (float)(History.Count - 1)) * StartIndex);
                PositionBar.Location = new Point(Settings.Resolution != 800 ? 619 : 395, 16 + h);
            }

            int y = 1;

            for (int i = StartIndex; i < History.Count; i++)
            {
                MirLabel temp = new MirLabel
                {
                    AutoSize = true,
                    BackColour = History[i].BackColour,
                    ForeColour = History[i].ForeColour,
                    Location = new Point(1, y),
                    OutLine = false,
                    Parent = ChatHistoryPanel,
                    Text = History[i].Text,
                    Font = ChatFont,
                };
                temp.MouseWheel += ChatPanel_MouseWheel;
                ChatLines.Add(temp);

                temp.Click += (o, e) =>
                {
                    if (!(o is MirLabel l)) return;

                    string[] parts = l.Text.Split(':', ' ');
                    if (parts.Length == 0) return;

                    string name = Regex.Replace(parts[0], "[^A-Za-z0-9]", "");

                    ChatTextBox.SetFocus();
                    ChatTextBox.Text = string.Format("/{0} ", name);
                    ChatTextBox.Visible = true;
                    ChatTextBox.TextBox.SelectionLength = 0;
                    ChatTextBox.TextBox.SelectionStart = ChatTextBox.Text.Length;
                };

                string currentLine = History[i].Text;

                int oldLength = currentLine.Length;

                Capture capture = null;

                foreach (Match match in RegexFunctions.ChatItemLinks.Matches(currentLine).Cast<Match>().OrderBy(o => o.Index).ToList())
                {
                    try
                    {
                        int offSet = oldLength - currentLine.Length;

                        capture = match.Groups[1].Captures[0];
                        string[] values = capture.Value.Split('/');
                        currentLine = currentLine.Remove(capture.Index - 1 - offSet, capture.Length + 2).Insert(capture.Index - 1 - offSet, values[0]);
                        string text = currentLine.Substring(0, capture.Index - 1 - offSet) + " ";
                        Size size = TextRenderer.MeasureText(text, temp.Font, new Size(temp.Size.Width, int.MaxValue));

                        ChatLink(values[0], ulong.Parse(values[1]), temp.Location.Add(new Point(size.Width - 10, 0)));
                    }
                    catch(Exception ex)
                    {
                        //Temporary debug to catch unknown error
                        CMain.SaveError(ex.ToString());
                        CMain.SaveError(currentLine);
                        CMain.SaveError(capture.Value);
                        throw;
                    }
                }

                temp.Text = currentLine;

                y += 13;
                if (i - StartIndex == LineCount - 1) break;
            }

        }

        private void ChatLink(string name, ulong uniqueID, Point p)
        {
            UserItem item = GameScene.ChatItemList.FirstOrDefault(x => x.UniqueID == uniqueID);

            if (item != null)
            {
                MirLabel temp = new MirLabel
                {
                    AutoSize = true,
                    Visible = true,
                    Parent = ChatHistoryPanel,
                    Location = p,
                    Text = name,
                    ForeColour = Color.Blue,
                    Sound = SoundList.ButtonC,
                    Font = ChatFont,
                    OutLine = false,
                };

                temp.MouseEnter += (o, e) => temp.ForeColour = Color.Red;
                temp.MouseLeave += (o, e) =>
                {
                    GameScene.Scene.DisposeItemLabel();
                    temp.ForeColour = Color.Blue;
                };
                temp.MouseDown += (o, e) => temp.ForeColour = Color.Blue;
                temp.MouseUp += (o, e) => temp.ForeColour = Color.Red;

                temp.Click += (o, e) =>
                {
                    GameScene.Scene.CreateItemLabel(item);
                };

                LinkedItemButtons.Add(temp);
            }
        }


        private void ChatPanel_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.ArrowUp:
                    if (StartIndex == 0) return;
                    StartIndex--;
                    break;
                case Keys.Home:
                    if (StartIndex == 0) return;
                    StartIndex = 0;
                    break;
                case Keys.ArrowDown:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex++;
                    break;
                case Keys.End:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex = History.Count - 1;
                    break;
                case Keys.PageUp:
                    if (StartIndex == 0) return;
                    StartIndex -= LineCount;
                    break;
                case Keys.PageDown:
                    if (StartIndex == History.Count - 1) return;
                    StartIndex += LineCount;
                    break;
                default:
                    return;
            }
            Update();
            e.Handled = true;
        }
        private void ChatPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (StartIndex == 0 && count >= 0) return;
            if (StartIndex == History.Count - 1 && count <= 0) return;

            StartIndex -= count;
            Update();
        }
        private void ChatTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            CMain.Shift = e.Shift;
            CMain.Alt = e.Alt;
            CMain.Ctrl = e.Control;

            switch (e.KeyCode)
            {
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                case Keys.Tab:
                    CMain.CMain_KeyUp(sender, e);
                    break;

            }
        }
        private void ChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            CMain.Shift = e.Shift;
            CMain.Alt = e.Alt;
            CMain.Ctrl = e.Control;

            switch (e.KeyCode)
            {
                case Keys.F1:
                case Keys.F2:
                case Keys.F3:
                case Keys.F4:
                case Keys.F5:
                case Keys.F6:
                case Keys.F7:
                case Keys.F8:
                case Keys.F9:
                case Keys.F10:
                case Keys.F11:
                case Keys.F12:
                case Keys.Tab:
                    CMain.CMain_KeyDown(sender, e);
                    break;

            }
        }

        public void ToggleChatFilter(ChatChannelFilter filter)
        {
            // 下拉选定频道:既作为聊天面板视图过滤(只看该频道),也作为发送频道(显式下发给服务端,取代文本前缀)。
            CurrentViewChannel = filter;
            CurrentSendChannel = filter;

            // 重新渲染聊天面板以应用视图过滤
            Update();
        }

        public class ChatHistory
        {
            public string Text;
            public Color ForeColour, BackColour;
            public ChatType Type;
        }
    }
    /// <summary>
    /// 技能栏单列（8 格）：封装一列的格子、按键标注、冷却与施法/刷新/冷却处理。
    /// _keyOffset=0 → 主键 1..8（Bar1Skill1..8）；=8 → 副键 9..16（Bar2Skill1..8）。
    /// 布局按原始传奇 HomeUIRightCtrlSkin：52×52 格、格距 52、底图 y_h_grid(0.9)、右侧按键文字。
    /// </summary>
    public sealed class SkillBarColumn : MirControl
    {
        private const string HomeSheet = "y_homeUI-sheet";

        private const int CellBgSize = 50;
        private const int CellSize = 44;
        public const int ColumnWidth = CellBgSize;
        public const int ColumnHeight = CellBgSize * 8;
        public const int SlotPerColumn = 8;
        private const int CellOffset = (CellBgSize - CellSize) / 2;

        private readonly int _keyOffset;

        // CDN SkillCdEff movieClip（36 帧半黑圆遮罩），两列共享一次加载。
        private static Task<Mir2AtlasSheet> _cdSheetTask;
        private static List<string> _cdFrames;   // SkillCdEff 时间线帧名
        private const string CdJsonKey = "resource/assets/gameui4/movieClip/SkillCdEff.json";
        private const string CdPngKey = "resource/assets/gameui4/movieClip/SkillCdEff.png";

        public MirImageControl[] Cells = new MirImageControl[SlotPerColumn];
        public MirLabel[] KeyNameLabels = new MirLabel[SlotPerColumn];
        public MirAnimatedAtlasControl[] CoolDowns = new MirAnimatedAtlasControl[SlotPerColumn];
        public MirLabel[] CdTexts = new MirLabel[SlotPerColumn];   // JS 的 cdSkillTxt：居中剩余秒数

        public bool HasSkill = false;

        public SkillBarColumn(int keyOffset)
        {
            _keyOffset = keyOffset;
            Size = new Size(ColumnWidth, ColumnHeight);
            Visible = true;

            for (int row = 0; row < SlotPerColumn; row++)
            {
                int y = row * CellBgSize;
                int spellSlot = keyOffset + row + 1;   // 循环体内局部拷贝，闭包捕获每格自己的值

                var grid = new MirImageControl
                {
                    Parent = this,
                    AtlasNameKey = HomeSheet + "#y_h_grid",
                    Location = new Point(0, y),
                    AutoSize = false,
                    Size = new Size(CellBgSize, CellBgSize),
                    NotControl = true,
                };

                Cells[row] = new MirImageControl
                {
                    Parent = this,
                    Location = new Point(CellOffset, y + CellOffset),
                    AutoSize = false,
                    Size = new Size(CellSize, CellSize),
                };
                Cells[row].Click += (o, e) => GameScene.Scene.UseSpell(spellSlot);
                CoolDowns[row] = new MirAnimatedAtlasControl
                {
                    Parent = this,
                    Location = new Point(0, y + 1),
                    AutoSize = false,
                    Size = new Size(CellSize, CellSize),
                    NotControl = true,
                    Visible = false,
                };

                CdTexts[row] = new MirLabel
                {
                    Text = "",
                    Font = new Font(Settings.FontName, 12F),
                    ForeColour = Color.White,
                    OutLine = true,
                    OutLineColour = Color.Black,
                    Parent = this,
                    Location = new Point(0, y),
                    Size = new Size(CellBgSize, CellBgSize),
                    NotControl = true,
                    Visible = false,
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter),           
                };

                KeyNameLabels[row] = new MirLabel
                {
                    Text = keyOffset == 0 ? (row + 1).ToString() : ("F" + (row + 1)),
                    Font = new Font(Settings.FontName, 10F),
                    ForeColour = Color.White,
                    Parent = this,
                    Location = new Point(CellOffset, y + CellOffset),
                    Size = new Size(CellSize, CellSize),
                    NotControl = true,
                    DrawFormat = new TextStyle(TextAnchor.LowerRight)
                };
            }

            _ = LoadCdSheetAsync();
        }

        /// <summary>异步加载 CDN 的 SkillCdEff movieClip，装配到本列 8 个 CD 控件。两列共享同一片。</summary>
        private async Task LoadCdSheetAsync()
        {
            try
            {
                if (_cdSheetTask == null)
                {
                    string jsonPath = Mir2OriginResource.ResolveManifestAssetPath(CdJsonKey);
                    string pngPath = Mir2OriginResource.ResolveManifestAssetPath(CdPngKey);
                    _cdSheetTask = Mir2AtlasLibrary.GetSheetByPathsAsync(jsonPath, pngPath);
                }
                Mir2AtlasSheet sheet = await _cdSheetTask;
                if (sheet == null) return;

                _cdFrames = sheet.MovieClip?.PlayOrder;
                foreach (var cd in CoolDowns)
                {
                    if (cd == null || cd.IsDisposed) continue;
                    cd.SetMovieClip(sheet);
                }
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[SkillBarColumn] SkillCdEff 加载失败：" + ex);
                _cdSheetTask = null;   // 允许下次重试
            }
        }

        public void UpdateUI()
        {
            HasSkill = false;
            foreach (var m in GameScene.User.Magics)
            {
                if (m.Key >= _keyOffset + 1 && m.Key <= _keyOffset + SlotPerColumn)
                    HasSkill = true;
            }

            for (int row = 0; row < SlotPerColumn; row++)
            {
                int slot = _keyOffset + row + 1;
                Cells[row].Index = -1;
                Cells[row].ImageUrl = null;   // 清掉上一轮 CDN 图标，避免换绑/解绑后残留

                var keybind = (KeybindOptions)((int)KeybindOptions.Bar1Skill1 + (slot - 1));
                string key = CMain.InputKeys.GetKey(keybind);

                foreach (var m in GameScene.User.Magics)
                {
                    if (m.Key != slot) continue;
                    HasSkill = true;
                    ClientMagic magic = MapObject.User.GetMagic(m.Spell);
                    if (magic == null) continue;
                    if (SkillDescriptionHelper.IsPassive(magic.Spell)) continue;   // 被动技能不占快捷键栏

                    // 原始传奇技能图标：ResUrl.url(icon,9)=icon/skills/{OriginIcon}.png，纯 CDN 无本地回落。
                    Cells[row].ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon);
                    Cells[row].ForeColour = Color.White;
                    // 技能描述 + 按键/MP/CD 摘要
                    Cells[row].Hint = SkillDescriptionHelper.Get(magic) + "\n\n" +
                        GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SkillMpCooldownKey), magic.Name,
                        (magic.BaseCost + (magic.LevelCost * magic.Level)), Functions.PrintTimeSpanFromMilliSeconds(magic.Delay), key);
                }
            }
        }

        public void ProcessSkillDelay()
        {
            for (int row = 0; row < SlotPerColumn; row++)
            {
                int slot = _keyOffset + row + 1;
                foreach (var magic in GameScene.User.Magics)
                {
                    if (magic.Key != slot) continue;

                    long timeLeft = magic.CastTime + magic.Delay - CMain.Time;

                    if (timeLeft <= 0)
                    {
                        CoolDowns[row].Visible = false;
                        CdTexts[row].Visible = false;
                        continue;
                    }

                    // SkillCdEff 时间线：首帧=全黑、末帧=不黑。按已过去的时间比例取帧，
                    // 使 CD 开始时全黑、结束时完全透出（此前用剩余比例，方向反了）。
                    if (_cdFrames != null && _cdFrames.Count > 0)
                    {
                        double cdSeconds = magic.Delay / 1000.0;
                        if (cdSeconds <= 0) cdSeconds = 1;
                        double elapsedRatio = 1.0 - (timeLeft / 1000.0) / cdSeconds;
                        int frame = (int)(_cdFrames.Count * elapsedRatio);
                        if (frame < 0) frame = 0;
                        if (frame >= _cdFrames.Count) frame = _cdFrames.Count - 1;
                        CoolDowns[row].AtlasFrame = _cdFrames[frame];
                    }
                    CoolDowns[row].Visible = true;

                    // 居中剩余秒数（JS setCdTxt：toFixed(1)，15号白字黑描边）
                    CdTexts[row].Text = (timeLeft / 1000.0).ToString("0.0");
                    CdTexts[row].Visible = true;
                }
            }
        }
    }

    /// <summary>
    /// PC 页游技能快捷栏：右上角锚定，内含两列 SkillBarColumn（主键 1..8 / 副键 F1..F8）。
    /// 按原始传奇 HomeUIRightCtrlSkin（106×464）布局移植。
    /// </summary>
    public sealed class SkillBarDialog : MirImageControl
    {
        public SkillBarColumn Column1, Column2;   // 主键列(左) / 副键列(右)
        public bool AltBind;

        private bool _showSecondColumn = Settings.SkillBarTwoRows;
        /// <summary>是否显示第二列（副键 F1..F8）。关闭时容器收窄为单列并重新锚定，并持久化到 Settings。</summary>
        public bool ShowSecondColumn
        {
            get { return _showSecondColumn; }
            set
            {
                if (_showSecondColumn == value) return;
                _showSecondColumn = value;
                Settings.SetSkillBarTwoRows(value);   // 写字段并落库，下次登录保留

                if (value && Column2 == null)   // 从单列切回双列时补建
                    Column2 = new SkillBarColumn(SkillBarColumn.SlotPerColumn) { Parent = this, Location = new Point(0, 0) };
                if (Column2 != null) Column2.Visible = value;

                // 双列：主键在右(x=ColumnWidth)、副键在左(x=0)；单列：主键在 x=0
                Column1.Location = new Point(value ? SkillBarColumn.ColumnWidth : 0, 0);
                Size = new Size(SkillBarColumn.ColumnWidth * (value ? 2 : 1), SkillBarColumn.ColumnHeight);
                ApplyAnchor();
                UpdateUI();
            }
        }

        private int ColumnCount => _showSecondColumn ? 2 : 1;

        public SkillBarDialog()
        {
            AutoSize = false;
            Size = new Size(SkillBarColumn.ColumnWidth * ColumnCount, SkillBarColumn.ColumnHeight);
            Sort = true;
            Visible = true;
            Movable = false;

            Anchor = EAnchorType.TopRight;
            AnchorPos = new Point(0, 200);
            ApplyAnchor();

            if (_showSecondColumn)
            {
                Column1 = new SkillBarColumn(0) { Parent = this, Location = new Point(SkillBarColumn.ColumnWidth, 0) };
                Column2 = new SkillBarColumn(SkillBarColumn.SlotPerColumn) { Parent = this, Location = new Point(0, 0) };
            }
            else
            {
                Column1 = new SkillBarColumn(0) { Parent = this, Location = new Point(0, 0) };
            }
        }

        public bool HasSkill { get { return Column1.HasSkill || (_showSecondColumn && Column2.HasSkill); } }

        public void UpdateUI()
        {
            Column1.UpdateUI();
            if (_showSecondColumn) Column2.UpdateUI();
        }

        public void Process()
        {
            Column1.ProcessSkillDelay();
            if (_showSecondColumn) Column2.ProcessSkillDelay();
        }

        public override void Show()
        {
            if (Visible) return;
            if (!HasSkill) return;
            Settings.SkillBar = true;
            Visible = true;
            UpdateUI();
        }

        public override void Hide()
        {
            if (!Visible) return;
            Settings.SkillBar = false;
            Visible = false;
        }
    }

    public sealed class MiniMapDialog : MirImageControl
    {
        public MirImageControl LightSetting, NewMail;
        public MirButton ToggleButton, BigMapButton, MailButton;
        public MirLabel LocationLabel, MapNameLabel;
        private float _fade = 1F;
        private bool _bigMode = true;

        // 小地图纹理：从 CDN 按地图图像 id 取（与提取工具一致），替代老 MMap 库 + 服务端 MiniMap 索引
        private KFramework.MonoGame.Texture2D? _miniMapTexture;
        private int _miniMapLoadedImg = -1;

        public MirLabel AModeLabel, PModeLabel;

        public List<MirLabel> QuestIcons = new List<MirLabel>();

        public MiniMapDialog()
        {
            Index = 2090;
            Library = Libraries.Prguse;
            Location = new Point(Settings.ScreenWidth - 126, 0);
            PixelDetect = true;

            BeforeDraw += MiniMap_BeforeDraw;
            AfterDraw += MiniMapDialog_AfterDraw;

            // 走 MirImageControl.ImageUrl 标准远程通道：加载完成后把纹理收进 _miniMapTexture，
            // 同时清掉 Texture（否则 DrawControl 会把整张小地图盖在边框/控件上，而小地图的视口裁剪在 BeforeDraw 手绘）。
            RemoteImageLoaded += (s, e) => { _miniMapTexture = Texture; Texture = null; };

            MapNameLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Size = new Size(120, 18),
                Location = new Point(2, 2),
                NotControl = true,
            };

            LocationLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Size = new Size(56, 18),
                Location = new Point(46, 131),
                NotControl = true,
            };

            MailButton = new MirButton
            {
                Index = 2099,
                HoverIndex = 2100,
                PressedIndex = 2101,
                Parent = this,
                Location = new Point(4, 131),
                Library = Libraries.Prguse,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Mail)
            };
            MailButton.Click += (o, e) => GameScene.Scene.MailListDialog.Toggle();

            NewMail = new MirImageControl
            {
                Index = 544,
                Location = new Point(5, 132),
                Parent = this,
                Library = Libraries.Prguse,
                Visible = false,
                NotControl = true
            };

            BigMapButton = new MirButton
            {
                Index = 2096,
                HoverIndex = 2097,
                PressedIndex = 2098,
                Parent = this,
                Location = new Point(25, 131),
                Library = Libraries.Prguse,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BigMapKey), CMain.InputKeys.GetKey(KeybindOptions.Bigmap))
            };
            BigMapButton.Click += (o, e) => GameScene.Scene.BigMapDialog.Toggle();

            ToggleButton = new MirButton
            {
                Index = 2102,
                HoverIndex = 2103,
                PressedIndex = 2104,
                Parent = this,
                Location = new Point(109, 3),
                Library = Libraries.Prguse,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MiniMapKey), CMain.InputKeys.GetKey(KeybindOptions.Minimap))
            };
            ToggleButton.Click += (o, e) => Toggle();

            LightSetting = new MirImageControl
            {
                Index = 2093,
                Library = Libraries.Prguse,
                Parent = this,
                Location = new Point(102, 131),
            };


            AModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Yellow,
                OutLineColour = Color.Black,
                Parent = this,
                Location = new Point(115, 125)
            };

            PModeLabel = new MirLabel
            {
                AutoSize = true,
                ForeColour = Color.Yellow,
                OutLineColour = Color.Black,
                Parent = this,
                Location = new Point(230, 125),
                Visible = false
            };
        }

        private void MiniMapDialog_AfterDraw(object sender, EventArgs e)
        {

        }

        private void MiniMap_BeforeDraw(object sender, EventArgs e)
        {

            foreach (var icon in QuestIcons)
                icon.Dispose();

            QuestIcons.Clear();

            MapControl map = GameScene.Scene.MapControl;
            if (map == null) return;

            // 小地图改用 CDN：按地图图像 id 取 assets/resource/minimap/{img}.jpg（与提取工具同款），
            // 不再依赖老 MMap 库 + 服务端 MiniMap 索引（原始传世这批图没有对应帧，且服务端 MiniMap 多为 0）。
            if (map.MapImg != _miniMapLoadedImg)
            {
                _miniMapLoadedImg = map.MapImg;
                _miniMapTexture = null;
                if (map.MapImg > 0) LoadMiniMapTexture(map.MapImg);
            }

            // 小模式(Index 2091)或未取到小地图时，仅显示外框
            if (Index != 2090 || _miniMapTexture == null)
                return;

            Rectangle viewRect = new Rectangle(0, 0, 120, 108);
            Point drawLocation = Location;
            drawLocation.Offset(3, 22);

            Size miniMapSize = new Size(_miniMapTexture.Width, _miniMapTexture.Height);
            float scaleX = miniMapSize.Width / (float)map.Width;
            float scaleY = miniMapSize.Height / (float)map.Height;

            viewRect.Location = new Point(
                (int)(scaleX * MapObject.User.CurrentLocation.X) - viewRect.Width / 2,
                (int)(scaleY * MapObject.User.CurrentLocation.Y) - viewRect.Height / 2);

            //   viewRect.Location = viewRect.Location.Subtract(1, 1);
            if (viewRect.Right >= miniMapSize.Width)
                viewRect.X = miniMapSize.Width - viewRect.Width;
            if (viewRect.Bottom >= miniMapSize.Height)
                viewRect.Y = miniMapSize.Height - viewRect.Height;

            if (viewRect.X < 0) viewRect.X = 0;
            if (viewRect.Y < 0) viewRect.Y = 0;

            DXManager.Draw(_miniMapTexture, viewRect, new Vector3(drawLocation.X, drawLocation.Y, 0), Color.FromArgb((int)(255 * _fade), 255, 255, 255));


            int startPointX = (int)(viewRect.X / scaleX);
            int startPointY = (int)(viewRect.Y / scaleY);

            foreach (var ob in MapControl.Objects.Values)
            {
                if (ob.Race == ObjectType.Item || ob.Dead || ob.Race == ObjectType.Spell || ob.Sneaking) continue;
                float x = ((ob.CurrentLocation.X - startPointX) * scaleX) + drawLocation.X;
                float y = ((ob.CurrentLocation.Y - startPointY) * scaleY) + drawLocation.Y;

                Color colour;

                if ((GroupDialog.GroupList.Contains(ob.Name) && MapObject.User != ob) || ob.Name.EndsWith(string.Format("({0})", MapObject.User.Name)))
                    colour = Color.FromArgb(0, 0, 255);
                else
                    if (ob is PlayerObject)
                {
                    colour = Color.FromArgb(255, 255, 255);
                }
                else if (ob is NPCObject || ob.AI == 6)
                {
                    colour = Color.FromArgb(0, 255, 50);
                }
                else
                    colour = Color.FromArgb(255, 0, 0);

                DXManager.Draw(DXManager.RadarTexture, new Rectangle(0, 0, 4, 4), new Vector3((float)(x - 0.5), (float)(y - 0.5), 0.0F), colour);


                if (ob is NPCObject npc && npc.GetAvailableQuests(true).Any())
                {
                    string text = "";
                    Color color = Color.Empty;

                    switch (npc.QuestIcon)
                    {
                        case QuestIcon.ExclamationBlue:
                            color = Color.DodgerBlue;
                            text = "!";
                            break;
                        case QuestIcon.ExclamationYellow:
                            color = Color.Yellow;
                            text = "!";
                            break;
                        case QuestIcon.ExclamationGreen:
                            color = Color.Green;
                            text = "!";
                            break;
                        case QuestIcon.QuestionBlue:
                            color = Color.DodgerBlue;
                            text = "?";
                            break;
                        case QuestIcon.QuestionWhite:
                            color = Color.White;
                            text = "?";
                            break;
                        case QuestIcon.QuestionYellow:
                            color = Color.Yellow;
                            text = "?";
                            break;
                        case QuestIcon.QuestionGreen:
                            color = Color.Green;
                            text = "?";
                            break;
                    }

                    QuestIcons.Add(new MirLabel
                    {
                        AutoSize = true,
                        Parent = GameScene.Scene.MiniMapDialog,
                        Font = new Font(Settings.FontName, 9f, KFramework.MonoGame.FontStyle.Bold),
                        DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                        Text = text,
                        ForeColour = color,
                        Location = new Point((int)(x - Settings.ScreenWidth + GameScene.Scene.MiniMapDialog.Size.Width) - 6, (int)(y) - 10),
                        NotControl = true,
                        Visible = true,
                        Modal = true
                    });
                }


            }
        }

        private async void LoadMiniMapTexture(int img)
        {
            // 走 MirImageControl.ImageUrl 标准远程通道（带取消/重试安全），
            // 加载完成由构造函数里的 RemoteImageLoaded 收进 _miniMapTexture；失败则保持 null（外框照常显示）。
            ImageUrl = Mir2OriginResource.MinimapPath(img);
        }

        public void Toggle()
        {
            if (_fade == 0F)
            {
                _bigMode = true;
                SetBigMode();
                _fade = 1F;
            }
            //else if(_fade == 1F)
            //{
            //    _bigMode = true;
            //    SetBigMode();
            //    _fade = 0.8F;
            //}
            else
            {
                _bigMode = false;
                SetSmallMode();
                _fade = 0;
            }

            Redraw();
        }

        private void SetSmallMode()
        {
            Index = 2091;
            int y = Size.Height - 23;
            MailButton.Location = new Point(4, y);
            NewMail.Location = new Point(5, y + 1);
            BigMapButton.Location = new Point(25, y);
            LocationLabel.Location = new Point(46, y);
            LightSetting.Location = new Point(102, y);

            GameScene.Scene.DuraStatusPanel.Location = new Point(GameScene.Scene.MiniMapDialog.Location.X + 86,
            GameScene.Scene.MiniMapDialog.Size.Height);
        }

        private void SetBigMode()
        {
            Index = 2090;
            int y = Size.Height - 23;
            MailButton.Location = new Point(4, y);
            NewMail.Location = new Point(5, y + 1);
            BigMapButton.Location = new Point(25, y);
            LocationLabel.Location = new Point(46, y);
            LightSetting.Location = new Point(102, y);

            GameScene.Scene.DuraStatusPanel.Location = new Point(GameScene.Scene.MiniMapDialog.Location.X + 86,
            GameScene.Scene.MiniMapDialog.Size.Height);
        }

        public void Process()
        {
            MapControl map = GameScene.Scene.MapControl;
            if (map == null) return;

            MapNameLabel.Text = map.Title;
            LocationLabel.Text = Functions.PointToString(MapObject.User.CurrentLocation);

            GameScene.Scene.MainDialog.SModeLabel.Location = new Point((GameScene.Scene.MiniMapDialog.Location.X - 3) - GameScene.Scene.MainDialog.Location.X,
            (GameScene.Scene.MiniMapDialog.Size.Height + 150) - Settings.ScreenHeight);
            GameScene.Scene.MainDialog.AModeLabel.Location = new Point((GameScene.Scene.MiniMapDialog.Location.X - 3) - GameScene.Scene.MainDialog.Location.X,
            (GameScene.Scene.MiniMapDialog.Size.Height + 165) - Settings.ScreenHeight);
            GameScene.Scene.MainDialog.PModeLabel.Location = new Point((GameScene.Scene.MiniMapDialog.Location.X - 3) - GameScene.Scene.MainDialog.Location.X,
            (GameScene.Scene.MiniMapDialog.Size.Height + 180) - Settings.ScreenHeight);

            if (GameScene.Scene.NewMail)
            {
                double time = (CMain.Time) / 100D;

                if (Math.Round(time) % 10 < 5 || GameScene.Scene.NewMailCounter >= 10)
                {
                    NewMail.Visible = true;
                }
                else
                {
                    if (NewMail.Visible)
                    {
                        GameScene.Scene.NewMailCounter++;
                    }

                    NewMail.Visible = false;
                }
            }
            else
            {
                NewMail.Visible = false;
            }
        }
    }
    public sealed class InspectDialog : MirImageControl
    {
        public static UserItem[] Items = new UserItem[Enum.GetNames(typeof(EquipmentSlot)).Length];
        public static uint InspectID;

        public string Name;
        public string GuildName;
        public string GuildRank;
        public MirClass Class;
        public MirGender Gender;
        public byte Hair;
        public ushort Level;
        public string LoverName;
        public bool AllowObserve;

        public MirButton CloseButton, GroupButton, FriendButton, MailButton, TradeButton, LoverButton, ObserveButton;
        public MirImageControl CharacterPage, ClassImage;
        public MirLabel NameLabel;
        public MirLabel GuildLabel, LoverLabel;



        public MirItemCell
            WeaponCell,
            ArmorCell,
            HelmetCell,
            TorchCell,
            NecklaceCell,
            BraceletLCell,
            BraceletRCell,
            RingLCell,
            RingRCell,
            AmuletCell,
            BeltCell,
            BootsCell,
            StoneCell,
            MountCell;

        public InspectDialog()
        {
            Index = 430;
            Library = Libraries.Prguse;
            Location = new Point(536, 0);
            Movable = true;
            Sort = true;

            CharacterPage = new MirImageControl
            {
                Index = 340,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(8, 70),
            };
            CharacterPage.AfterDraw += (o, e) =>
            {
                if (Libraries.StateItems == null) return;

                ItemInfo RealItem = null;

                if (ArmorCell.Item != null)
                {
                    RealItem = Functions.GetRealItem(ArmorCell.Item.Info, Level, Class, GameScene.ItemInfoList);
                    Libraries.StateItems.Draw((int)RealItem.Image, new Point(DisplayLocation.X + 0, DisplayLocation.Y + -20), Color.White, true, 1F);

                    if (RealItem.Effect > 0)
                    {
                        int wingOffset = RealItem.Effect == 1 ? 2 : 4;

                        int genderOffset = MapObject.User.Gender == MirGender.Male ? 0 : 1;

                        Libraries.Prguse2.DrawBlend(1200 + wingOffset + genderOffset, new Point(DisplayLocation.X, DisplayLocation.Y - 20), Color.White, true, 1F);
                    }
                }

                if (WeaponCell.Item != null)
                {
                    RealItem = Functions.GetRealItem(WeaponCell.Item.Info, Level, Class, GameScene.ItemInfoList);
                    Libraries.StateItems.Draw((int)RealItem.Image, new Point(DisplayLocation.X, DisplayLocation.Y - 20),
                    Color.White, true, 1F);

                }

                if (HelmetCell.Item != null)
                    Libraries.StateItems.Draw((int)HelmetCell.Item.Info.Image, new Point(DisplayLocation.X, DisplayLocation.Y - 20), Color.White, true, 1F);
                else
                {
                    int hair = 441 + Hair + (Class == MirClass.Assassin ? 20 : 0) + (Gender == MirGender.Male ? 0 : 40);

                    int offSetX = Class == MirClass.Assassin ? (Gender == MirGender.Male ? 6 : 4) : 0;
                    int offSetY = Class == MirClass.Assassin ? (Gender == MirGender.Male ? 25 : 18) : 0;

                    Libraries.Prguse.Draw(hair, new Point(DisplayLocation.X + offSetX, DisplayLocation.Y + offSetY - 20), Color.White, true, 1F);
                }
            };


            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(241, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();



            GroupButton = new MirButton
            {
                HoverIndex = 432,
                Index = 431,
                Location = new Point(55, 357),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 433,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.InviteToGroup),
            };
            GroupButton.Click += (o, e) =>
            {

                if (GroupDialog.GroupList.Count >= Globals.MaxGroup)
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.GroupHasMaxMembers), ChatType.System);
                    return;
                }
                if (GroupDialog.GroupList.Count > 0 && GroupDialog.GroupList[0] != MapObject.User.Name)
                {

                    GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.YouNotGroupLeader), ChatType.System);
                }

                Network.Enqueue(new C.AddMember { Name = Name });
                return;
            };

            FriendButton = new MirButton
            {
                HoverIndex = 435,
                Index = 434,
                Location = new Point(85, 357),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 436,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AddToFriendsList),
            };
            FriendButton.Click += (o, e) =>
            {
                Network.Enqueue(new C.AddFriend { Name = Name, Blocked = false });
            };

            MailButton = new MirButton
            {
                HoverIndex = 438,
                Index = 437,
                Location = new Point(115, 357),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 439,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SendMail),
            };
            MailButton.Click += (o, e) => GameScene.Scene.MailComposeLetterDialog.ComposeMail(Name);

            TradeButton = new MirButton
            {
                HoverIndex = 524,
                Index = 523,
                Location = new Point(145, 357),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 525,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Trade),
            };
            TradeButton.Click += (o, e) => Network.Enqueue(new C.TradeRequest());

            ObserveButton = new MirButton
            {
                Index = 854,
                HoverIndex = 855,
                PressedIndex = 856,
                Location = new Point(175, 357),
                Library = Libraries.Title,
                Parent = this,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Observe),
            };
            ObserveButton.Click += (o, e) =>
            {
                if (AllowObserve)
                {
                    Network.Enqueue(new C.Observe { Name = Name });
                }
                else
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PlayerDisabledObservation), ChatType.System);
                }
            };

            NameLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Location = new Point(50, 12),
                Size = new Size(190, 20),
                NotControl = true
            };
            NameLabel.Click += (o, e) =>
            {
                GameScene.Scene.ChatDialog.ChatTextBox.SetFocus();
                GameScene.Scene.ChatDialog.ChatTextBox.Text = string.Format("/{0} ", Name);
                GameScene.Scene.ChatDialog.ChatTextBox.Visible = true;
                GameScene.Scene.ChatDialog.ChatTextBox.TextBox.SelectionLength = 0;
                GameScene.Scene.ChatDialog.ChatTextBox.TextBox.SelectionStart = Name.Length + 2;

            };
            LoverButton = new MirButton
            {
                Index = 604,
                Location = new Point(17, 17),
                Library = Libraries.Prguse,
                Parent = this,
                Sound = SoundList.None
            };

            GuildLabel = new MirLabel
            {
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Location = new Point(50, 33),
                Size = new Size(190, 30),
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


            WeaponCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Weapon,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(123, 7),
            };

            ArmorCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Armour,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(163, 7),
            };

            HelmetCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Helmet,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 7),
            };


            TorchCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Torch,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 134),
            };

            NecklaceCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Necklace,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 98),
            };

            BraceletLCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.BraceletL,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(8, 170),
            };
            BraceletRCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.BraceletR,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 170),
            };
            RingLCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.RingL,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(8, 206),
            };
            RingRCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.RingR,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 206),
            };

            AmuletCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Amulet,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(8, 242),
            };

            BootsCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Boots,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(48, 242),
            };
            BeltCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Belt,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(88, 242),
            };

            StoneCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Stone,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(128, 242),
            };

            MountCell = new MirItemCell
            {
                ItemSlot = (int)EquipmentSlot.Mount,
                GridType = MirGridType.Inspect,
                Parent = CharacterPage,
                Location = new Point(203, 62),
            };
        }

        public void RefreshInferface(bool IsHero)
        {
            int offSet = Gender == MirGender.Male ? 0 : 1;

            CharacterPage.Index = 340 + offSet;

            switch (Class)
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

            NameLabel.Text = Name;
            GuildLabel.Text = GuildName + " " + GuildRank;
            if (LoverName != "")
            {
                LoverButton.Visible = true;
                LoverButton.Hint = LoverName;
            }
            else
                LoverButton.Visible = false;


            for (int i = 0; i < Items.Length; i++)
            {
                if (Items[i] == null) continue;
                GameScene.Bind(Items[i]);
            }

            //ObserveButton.Visible = AllowObserve; //Use this to allow observe button to be shown only if the player has Observe Enabled, pressing this wont bypass Observe being false.

            ObserveButton.Visible = !IsHero;

            TradeButton.Visible = !IsHero;
            MailButton.Visible = !IsHero;
            FriendButton.Visible = !IsHero;
            GroupButton.Visible = !IsHero;

        }


    }
    public sealed class OptionDialog : MirImageControl
    {
        public MirButton SkillModeOn, SkillModeOff;
        public MirButton SkillBarOn, SkillBarOff;
        public MirButton EffectOn, EffectOff;
        public MirButton DropViewOn, DropViewOff;
        public MirButton NameViewOn, NameViewOff;
        public MirButton HPViewOn, HPViewOff;
        public MirButton NewMoveOn, NewMoveOff;
        public MirButton ObserveOn, ObserveOff;
        public MirImageControl SoundBar, MusicSoundBar;
        public MirImageControl VolumeBar, MusicVolumeBar;

        public MirButton CloseButton;


        public OptionDialog()
        {
            Index = 411;
            Library = Libraries.Title;
            Movable = true;
            Sort = true;

            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);

            BeforeDraw += OptionPanel_BeforeDraw;

            CloseButton = new MirButton
            {
                Index = 360,
                HoverIndex = 361,
                Library = Libraries.Prguse2,
                Location = new Point(Size.Width - 26, 5),
                Parent = this,
                Sound = SoundList.ButtonA,
                PressedIndex = 362,
            };
            CloseButton.Click += (o, e) => Hide();

            //tilde option
            SkillModeOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 68),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 451,
            };
            SkillModeOn.Click += (o, e) =>
            {
                GameScene.Scene.ChangeSkillMode(false);
            };

            //ctrl option
            SkillModeOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 68),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 454
            };
            SkillModeOff.Click += (o, e) =>
            {
                GameScene.Scene.ChangeSkillMode(true);
            };

            SkillBarOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 93),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 457,
            };
            SkillBarOn.Click += (o, e) => Settings.SkillBar = true;

            SkillBarOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 93),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 460
            };
            SkillBarOff.Click += (o, e) => Settings.SkillBar = false;

            EffectOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 118),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 457,
            };
            EffectOn.Click += (o, e) => Settings.Effect = true;

            EffectOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 118),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 460
            };
            EffectOff.Click += (o, e) => Settings.Effect = false;

            DropViewOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 143),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 457,
            };
            DropViewOn.Click += (o, e) => Settings.DropView = true;

            DropViewOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 143),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 460
            };
            DropViewOff.Click += (o, e) => Settings.DropView = false;

            NameViewOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 168),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 457,
            };
            NameViewOn.Click += (o, e) => Settings.NameView = true;

            NameViewOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 168),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 460
            };
            NameViewOff.Click += (o, e) => Settings.NameView = false;

            HPViewOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 193),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 463,
            };
            HPViewOn.Click += (o, e) =>
            {
                Settings.HPView = true;
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.HpMpMode1), ChatType.System);
            };

            HPViewOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 193),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 466
            };
            HPViewOff.Click += (o, e) =>
            {
                Settings.HPView = false;
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.HpMpMode2), ChatType.System);
            };

            SoundBar = new MirImageControl
            {
                Index = 468,
                Library = Libraries.Prguse2,
                Location = new Point(159, 225),
                Parent = this,
                DrawImage = false,
            };
            SoundBar.MouseDown += SoundBar_MouseMove;
            SoundBar.MouseMove += SoundBar_MouseMove;
            SoundBar.BeforeDraw += SoundBar_BeforeDraw;

            VolumeBar = new MirImageControl
            {
                Index = 20,
                Library = Libraries.Prguse,
                Location = new Point(155, 218),
                Parent = this,
                NotControl = true,
            };

            MusicSoundBar = new MirImageControl
            {
                Index = 468,
                Library = Libraries.Prguse2,
                Location = new Point(159, 251),
                Parent = this,
                DrawImage = false
            };
            MusicSoundBar.MouseDown += MusicSoundBar_MouseMove;
            MusicSoundBar.MouseMove += MusicSoundBar_MouseMove;
            MusicSoundBar.BeforeDraw += MusicSoundBar_BeforeDraw;

            MusicVolumeBar = new MirImageControl
            {
                Index = 20,
                Library = Libraries.Prguse,
                Location = new Point(155, 244),
                Parent = this,
                NotControl = true,
            };

            NewMoveOn = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(159, 296),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 853,
            };
            NewMoveOn.Click += (o, e) =>
            {
                Settings.NewMove = true;
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.NewMovementStyle), ChatType.System);
            };

            NewMoveOff = new MirButton
            {
                Library = Libraries.Title,
                Location = new Point(201, 296),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 850
            };
            NewMoveOff.Click += (o, e) =>
            {
                Settings.NewMove = false;
                GameScene.Scene.ChatDialog.ReceiveChat(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.OldMovementStyle), ChatType.System);
            };

            ObserveOn = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(159, 271),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 457,
            };
            ObserveOn.Click += (o, e) => ToggleObserve(true);

            ObserveOff = new MirButton
            {
                Library = Libraries.Prguse2,
                Location = new Point(201, 271),
                Parent = this,
                Sound = SoundList.ButtonA,
                Size = new Size(36, 17),
                PressedIndex = 460
            };
            ObserveOff.Click += (o, e) => ToggleObserve(false);
        }

        private void ToggleObserve(bool allow)
        {
            if (GameScene.AllowObserve == allow) return;

            Network.Enqueue(new C.Chat
            {
                Message = "@ALLOWOBSERVE",
            });
        }

        public void ToggleSkillButtons(bool Ctrl)
        {
            foreach (KeyBind KeyCheck in CMain.InputKeys.Keylist)
            {
                if (KeyCheck.Key == Keys.None)
                    continue;
                if ((KeyCheck.function < KeybindOptions.Bar1Skill1) || (KeyCheck.function > KeybindOptions.Bar2Skill8)) continue;
                //need to test this 
                if ((KeyCheck.RequireCtrl != 1) && (KeyCheck.RequireTilde != 1)) continue;
                KeyCheck.RequireCtrl = (byte)(Ctrl ? 1 : 0);
                KeyCheck.RequireTilde = (byte)(Ctrl ? 0 : 1);
            }
        }

        private void SoundBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || SoundBar != ActiveControl) return;

            Point p = e.Location.Subtract(SoundBar.DisplayLocation);

            byte volume = (byte)(p.X / (double)SoundBar.Size.Width * 100);
            Settings.Volume = volume;

            double percent = Settings.Volume / 100D;

            SoundBar.Hint = $"{Settings.Volume}%";

            if (percent > 1) percent = 1;

            VolumeBar.Location = percent > 0 ? new Point(159 + (int)((SoundBar.Size.Width - 2) * percent), 218) : new Point(159, 218);
        }

        private void SoundBar_BeforeDraw(object sender, EventArgs e)
        {
            if (SoundBar.Library == null) return;

            double percent = Settings.Volume / 100D;

            SoundBar.Hint = $"{Settings.Volume}%";

            if (percent > 1) percent = 1;
            if (percent > 0)
            {
                Rectangle section = new Rectangle(0, 0, (int)((SoundBar.Size.Width - 2) * percent), SoundBar.Size.Height);

                SoundBar.Library.Draw(SoundBar.Index, section, SoundBar.DisplayLocation, Color.White, false);
                VolumeBar.Location = new Point(159 + section.Width, 218);
            }
            else
                VolumeBar.Location = new Point(159, 218);
        }

        private void MusicSoundBar_BeforeDraw(object sender, EventArgs e)
        {
            if (MusicSoundBar.Library == null) return;

            double percent = Settings.MusicVolume / 100D;

            MusicSoundBar.Hint = $"{Settings.MusicVolume}%";

            if (percent > 1) percent = 1;
            if (percent > 0)
            {
                Rectangle section = new Rectangle(0, 0, (int)((MusicSoundBar.Size.Width - 2) * percent), MusicSoundBar.Size.Height);

                MusicSoundBar.Library.Draw(MusicSoundBar.Index, section, MusicSoundBar.DisplayLocation, Color.White, false);
                MusicVolumeBar.Location = new Point(159 + section.Width, 244);
            }
            else
                MusicVolumeBar.Location = new Point(159, 244);
        }

        private void MusicSoundBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || MusicSoundBar != ActiveControl) return;

            Point p = e.Location.Subtract(MusicSoundBar.DisplayLocation);

            byte volume = (byte)(p.X / (double)MusicSoundBar.Size.Width * 100);
            Settings.MusicVolume = volume;

            double percent = Settings.MusicVolume / 100D;

            MusicSoundBar.Hint = $"{Settings.MusicVolume}%";

            if (percent > 1) percent = 1;

            MusicVolumeBar.Location = percent > 0 ? new Point(159 + (int)((MusicSoundBar.Size.Width - 2) * percent), 244) : new Point(159, 244);
        }

        private void OptionPanel_BeforeDraw(object sender, EventArgs e)
        {
            if (Settings.SkillMode)
            {
                SkillModeOn.Index = 452;
                SkillModeOff.Index = 453;
            }
            else
            {
                SkillModeOn.Index = 450;
                SkillModeOff.Index = 455;
            }

            if (Settings.SkillBar)
            {
                SkillBarOn.Index = 458;
                SkillBarOff.Index = 459;
            }
            else
            {
                SkillBarOn.Index = 456;
                SkillBarOff.Index = 461;
            }

            if (Settings.Effect)
            {
                EffectOn.Index = 458;
                EffectOff.Index = 459;
            }
            else
            {
                EffectOn.Index = 456;
                EffectOff.Index = 461;
            }

            if (Settings.DropView)
            {
                DropViewOn.Index = 458;
                DropViewOff.Index = 459;
            }
            else
            {
                DropViewOn.Index = 456;
                DropViewOff.Index = 461;
            }

            if (Settings.NameView)
            {
                NameViewOn.Index = 458;
                NameViewOff.Index = 459;
            }
            else
            {
                NameViewOn.Index = 456;
                NameViewOff.Index = 461;
            }

            if (Settings.HPView)
            {
                HPViewOn.Index = 464;
                HPViewOff.Index = 465;
            }
            else
            {
                HPViewOn.Index = 462;
                HPViewOff.Index = 467;
            }

            if (Settings.NewMove)
            {
                NewMoveOn.Index = 853;
                NewMoveOff.Index = 848;
            }
            else
            {
                NewMoveOn.Index = 851;
                NewMoveOff.Index = 850;
            }

            if (GameScene.AllowObserve)
            {
                ObserveOn.Index = 458;
                ObserveOff.Index = 459;
            }
            else
            {
                ObserveOn.Index = 456;
                ObserveOff.Index = 461;
            }
        }

    }
    public sealed class MenuDialog : MirImageControl
    {
        public MirButton ExitButton,
                         LogOutButton,
                         HelpButton,
                         KeyboardLayoutButton,
                         RankingButton,
                         CraftingButton,
                         IntelligentCreatureButton,
                         RideButton,
                         FishingButton,
                         FriendButton,
                         MentorButton,
                         RelationshipButton,
                         GroupButton,
                         GuildButton;

        public MenuDialog()
        {
            Index = 567;
            Parent = GameScene.Scene;
            Library = Libraries.Title;
            Location = new Point(Settings.ScreenWidth - Size.Width, GameScene.Scene.MainDialog.Location.Y - this.Size.Height + 15);
            Sort = true;
            Visible = false;
            Movable = true;

            ExitButton = new MirButton
            {
                HoverIndex = 634,
                Index = 633,
                Parent = this,
                Library = Libraries.Title,
                Location = new Point(3, 12),
                PressedIndex = 635,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ExitKey), CMain.InputKeys.GetKey(KeybindOptions.Exit))
            };
            ExitButton.Click += (o, e) => GameScene.Scene.QuitGame();

            LogOutButton = new MirButton
            {
                HoverIndex = 637,
                Index = 636,
                Parent = this,
                Library = Libraries.Title,
                Location = new Point(3, 31),
                PressedIndex = 638,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.LogOutKey), CMain.InputKeys.GetKey(KeybindOptions.Logout))
            };
            LogOutButton.Click += (o, e) => GameScene.Scene.LogOut();


            HelpButton = new MirButton
            {
                Index = 1970,
                HoverIndex = 1971,
                PressedIndex = 1972,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 50),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HelpKey), CMain.InputKeys.GetKey(KeybindOptions.Help))
            };
            HelpButton.Click += (o, e) =>
            {
                if (GameScene.Scene.HelpDialog.Visible)
                    GameScene.Scene.HelpDialog.Hide();
                else GameScene.Scene.HelpDialog.Show();
            };

            KeyboardLayoutButton = new MirButton
            {
                Index = 1973,
                HoverIndex = 1974,
                PressedIndex = 1975,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 69),
                Visible = true,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.KeyboardKeybind), CMain.InputKeys.GetKey(KeybindOptions.Keybind))
            };
            KeyboardLayoutButton.Click += (o, e) =>
            {
                if (GameScene.Scene.KeyboardLayoutDialog.Visible)
                    GameScene.Scene.KeyboardLayoutDialog.Hide();
                else GameScene.Scene.KeyboardLayoutDialog.Show();
            };

            RankingButton = new MirButton
            {
                Index = 2000,
                HoverIndex = 2001,
                PressedIndex = 2002,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 88),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RankingKey), CMain.InputKeys.GetKey(KeybindOptions.Ranking))
                //Visible = false
            };
            RankingButton.Click += (o, e) =>
            {
                if (GameScene.Scene.RankingDialog.Visible)
                    GameScene.Scene.RankingDialog.Hide();
                else GameScene.Scene.RankingDialog.Show();
            };

            CraftingButton = new MirButton
            {
                Index = 2000,
                HoverIndex = 2001,
                PressedIndex = 2002,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 107),
                Visible = false
            };
            CraftingButton.Click += (o, e) =>
            {

            };

            IntelligentCreatureButton = new MirButton
            {
                Index = 431,
                HoverIndex = 432,
                PressedIndex = 433,
                Parent = this,
                Library = Libraries.Prguse2,
                Location = new Point(3, 126),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CreaturesKey), CMain.InputKeys.GetKey(KeybindOptions.Creature))
            };
            IntelligentCreatureButton.Click += (o, e) =>
            {
                if (GameScene.Scene.IntelligentCreatureDialog.Visible)
                    GameScene.Scene.IntelligentCreatureDialog.Hide();
                else GameScene.Scene.IntelligentCreatureDialog.Show();
            };
            RideButton = new MirButton
            {
                Index = 1976,
                HoverIndex = 1977,
                PressedIndex = 1978,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 145),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MountKey), CMain.InputKeys.GetKey(KeybindOptions.MountWindow))
            };
            RideButton.Click += (o, e) =>
            {
                if (GameScene.Scene.MountDialog.Visible)
                    GameScene.Scene.MountDialog.Hide();
                else GameScene.Scene.MountDialog.Show();
            };

            FishingButton = new MirButton
            {
                Index = 1979,
                HoverIndex = 1980,
                PressedIndex = 1981,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 164),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FishingKey), CMain.InputKeys.GetKey(KeybindOptions.Fishing))
            };
            FishingButton.Click += (o, e) =>
            {
                if (GameScene.Scene.FishingDialog.Visible)
                    GameScene.Scene.FishingDialog.Hide();
                else GameScene.Scene.FishingDialog.Show();
            };

            FriendButton = new MirButton
            {
                Index = 1982,
                HoverIndex = 1983,
                PressedIndex = 1984,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 183),
                Visible = true,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FriendsKey), CMain.InputKeys.GetKey(KeybindOptions.Friends))
            };
            FriendButton.Click += (o, e) =>
            {
                if (GameScene.Scene.FriendDialog.Visible)
                    GameScene.Scene.FriendDialog.Hide();
                else GameScene.Scene.FriendDialog.Show();
            };

            MentorButton = new MirButton
            {
                Index = 1985,
                HoverIndex = 1986,
                PressedIndex = 1987,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 202),
                Visible = true,
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MentorKey), CMain.InputKeys.GetKey(KeybindOptions.Mentor))
            };
            MentorButton.Click += (o, e) =>
            {
                if (GameScene.Scene.MentorDialog.Visible)
                    GameScene.Scene.MentorDialog.Hide();
                else GameScene.Scene.MentorDialog.Show();
            };


            RelationshipButton = new MirButton  /* lover button */
            {
                Index = 1988,
                HoverIndex = 1989,
                PressedIndex = 1990,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 221),
                Visible = true,
                Hint = string.Format(   GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.RelationshipKey), CMain.InputKeys.GetKey(KeybindOptions.Relationship))
            };
            RelationshipButton.Click += (o, e) =>
            {
                if (GameScene.Scene.RelationshipDialog.Visible)
                    GameScene.Scene.RelationshipDialog.Hide();
                else GameScene.Scene.RelationshipDialog.Show();
            };

            GroupButton = new MirButton
            {
                Index = 1991,
                HoverIndex = 1992,
                PressedIndex = 1993,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 240),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.GroupsKey), CMain.InputKeys.GetKey(KeybindOptions.Group))
            };
            GroupButton.Click += (o, e) =>
            {
                if (GameScene.Scene.GroupDialog.Visible)
                    GameScene.Scene.GroupDialog.Hide();
                else GameScene.Scene.GroupDialog.Show();
            };

            GuildButton = new MirButton
            {
                Index = 1994,
                HoverIndex = 1995,
                PressedIndex = 1996,
                Parent = this,
                Library = Libraries.Prguse,
                Location = new Point(3, 259),
                Hint = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.GuildKey), CMain.InputKeys.GetKey(KeybindOptions.Guilds))
            };
            GuildButton.Click += (o, e) =>
            {
                if (GameScene.Scene.GuildDialog.Visible)
                    GameScene.Scene.GuildDialog.Hide();
                else GameScene.Scene.GuildDialog.Show();
            };

        }


    }
    public sealed class MagicButton : MirControl
    {
        public MirImageControl LevelImage, ExpImage;
        public MirButton SkillButton;
        public MirLabel LevelLabel, NameLabel, ExpLabel, KeyLabel;
        public ClientMagic Magic;
        public MirImageControl CoolDown;
        public bool HeroMagic;

        string[] Prefixes = new string[] { "", "CTRL", "Shift" };

        public MagicButton()
        {
            Size = new Size(231, 33);

            SkillButton = new MirButton
            {
                Parent = this,
                Location = new Point(36, 0),
                Size = new Size(32, 32),   // CDN Texture 通道按 Size 绘制
                Sound = SoundList.ButtonA,
            };
            SkillButton.Click += (o, e) =>
            {
                if (HeroMagic)
                {
                    if (GameScene.Hero == null || GameScene.Hero.Dead)
                        return;
                    new AssignKeyPanel(Magic, 17, new string[]
                        {
                            "Shift" + Environment.NewLine + "F1",
                            "Shift" + Environment.NewLine + "F2",
                            "Shift" + Environment.NewLine + "F3",
                            "Shift" + Environment.NewLine + "F4",
                            "Shift" + Environment.NewLine + "F5",
                            "Shift" + Environment.NewLine + "F6",
                            "Shift" + Environment.NewLine + "F7",
                            "Shift" + Environment.NewLine + "F8"
                        })
                    { Actor = GameScene.Hero };
                }
                else
                {
                    new AssignKeyPanel(Magic, 1, new string[]
                        {
                            "F1",
                            "F2",
                            "F3",
                            "F4",
                            "F5",
                            "F6",
                            "F7",
                            "F8",
                            "Ctrl" + Environment.NewLine + "F1",
                            "Ctrl" + Environment.NewLine + "F2",
                            "Ctrl" + Environment.NewLine + "F3",
                            "Ctrl" + Environment.NewLine + "F4",
                            "Ctrl" + Environment.NewLine + "F5",
                            "Ctrl" + Environment.NewLine + "F6",
                            "Ctrl" + Environment.NewLine + "F7",
                            "Ctrl" + Environment.NewLine + "F8"
                        })
                    { Actor = GameScene.User };
                }
            };

            LevelImage = new MirImageControl
            {
                Index = 516,
                Library = Libraries.Title,
                Location = new Point(73, 7),
                Parent = this,
                NotControl = true,
            };

            ExpImage = new MirImageControl
            {
                Index = 517,
                Library = Libraries.Title,
                Location = new Point(73, 19),
                Parent = this,
                NotControl = true,
            };

            LevelLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(88, 2),
                NotControl = true,
            };

            NameLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(109, 2),
                NotControl = true,
            };

            ExpLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(109, 15),
                NotControl = true,
            };

            KeyLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(2, 2),
                NotControl = true,
            };

            CoolDown = new MirImageControl
            {
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(36, 0),
                Opacity = 0.6F,
                NotControl = true,
                UseOffSet = true,
            };
        }

        public void Update(ClientMagic magic)
        {
            Magic = magic;

            NameLabel.Text = Magic.Name;

            LevelLabel.Text = Magic.Level.ToString();
            switch (Magic.Level)
            {
                case 0:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need1);
                    break;
                case 1:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need2);
                    break;
                case 2:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need3);
                    break;
                case 3:
                    ExpLabel.Text = "-";
                    break;
            }

            KeyLabel.Text = Magic.Key == 0 ? string.Empty : string.Format("{0}{1}F{2}",
                Prefixes[(Magic.Key - 1) / 8],
                Magic.Key > 8 ? Environment.NewLine : string.Empty,
                (Magic.Key - 1) % 8 + 1);

            SkillButton.Hint = SkillDescriptionHelper.Get(magic);


            // 原始传奇技能图标：按 OriginIcon 从 CDN 加载，纯 CDN 无本地回落。
            SkillButton.ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(Magic.OriginIcon);
            SkillButton.ForeColour = Color.White;

            SetDelay();
        }

        public void SetDelay()
        {
            if (Magic == null) return;

            int totalFrames = 34;

            long timeLeft = Magic.CastTime + Magic.Delay - CMain.Time;

            if (timeLeft < 100)
            {
                CoolDown.Visible = false;
                return;
            }

            int delayPerFrame = (int)(Magic.Delay / totalFrames);
            int startFrame = totalFrames - (int)(timeLeft / delayPerFrame);

            if ((CMain.Time <= Magic.CastTime + Magic.Delay))
            {
                CoolDown.Visible = true;
                CoolDown.Index = 1290 + startFrame;
            }
        }
    }
    /// <summary>
    /// 技能数字键面板（主角/英雄通用）。按原始传奇 JS SkillHotKeySkin(516×217) 布局移植：
    /// 技能图标 + "X快捷键设置为" 描述 + 16 个数字键网格(0~15) + 清空/确定按钮 + 双排技能栏勾选。
    /// 图集全部走 CDN（y_skillhotkey-sheet），键号用 word_kjjsz_{n}，选中框 y_select。
    /// 绑定沿用现有 C.MagicKey(Spell,Key) 协议，key = 键号 + KeyOffset（主角 offset=1 → F1..F16 槽位）。
    /// </summary>
    public sealed class AssignKeyPanel : MirImageControl
    {
        private const string Sheet = "y_skillhotkey-sheet";
        private const string BgUrl = "resource/assets/gameui4/jpg/bg_kjjsz_01.png";
        private const int PanelW = 520, PanelH = 210;

        public MirButton SaveButton, NoneButton;
        public UserObject Actor;
        public MirLabel TitleLabel;
        public MirImageControl MagicImage;
        public MirButton[] FKeys;
        public MirImageControl[] SelectFrames;   // 每个数字键的选中框 y_select

        public ClientMagic Magic;
        public byte Key;
        public byte KeyOffset;

        public AssignKeyPanel(ClientMagic magic, byte keyOffset, string[] keyStrings)
        {
            Magic = magic;
            Key = magic.Key;
            KeyOffset = keyOffset;

            Modal = true;
            AutoSize = false;
            Size = new Size(PanelW, PanelH);
            Location = Center;
            Parent = GameScene.Scene;
            Visible = true;

            // 背景：bg_kjjsz_01.png（_Image1）+ 内框 bg_kjjsz_03（_Image3 @ 86,20）
            var bg = new MirImageControl
            {
                Parent = this,
                ImageKey = BgUrl,
                Location = new Point(0, 0),
                AutoSize = false,
                Size = new Size(PanelW, PanelH),
                NotControl = true,
                DrawMode = MirImageDrawMode.NineGrid,
                BorderTop = 20, BorderBottom = 20, BorderLeft = 20, BorderRight = 20,
            };

            var MagicImageBg = new MirImageControl
            {
                Location = new Point(92, 26),
                Parent = this,
                Size = new Size(50, 50),
                NotControl = true,
                AtlasNameKey = "y_skillhotkey-sheet#bg_kjjsz_03",
                ForeColour = Color.White,
            };

            // 技能图标 38×38 @ (92,26)
            MagicImage = new MirImageControl
            {
                Location = new Point(97, 31),
                Parent = this,
                AutoSize = false,
                Size = new Size(40, 40),
                NotControl = true,
                ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon),
                ForeColour = Color.White,
            };

            // 描述文字 "X快捷键设置为" @ (150,38)
            TitleLabel = new MirLabel
            {
                Location = new Point(150, 38),
                Parent = this,
                Size = new Size(230, 32),
                DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                ForeColour = Color.White,
                Text = magic.Name + "快捷键设置为"
            };

            // 双排技能栏开关：两图同位置互斥显隐；图集帧用 AtlasNameKey（ImageKey 只支持单图），
            // 不能设 NotControl（否则点不到，Click 不触发）。
            bool secondOn = GameScene.Scene != null && GameScene.Scene.SkillBarDialogs != null
                && GameScene.Scene.SkillBarDialogs.ShowSecondColumn;
            var mUnSelectSecondBG = new MirImageControl
            {
                Location = new Point(250, 31),
                Parent = this,
                AutoSize = false,
                Size = new Size(24, 24),
                AtlasNameKey = "y_common-sheet#hide_unselect",
                ForeColour = Color.White,
                Visible = !secondOn,
            };
            var mSelectSecondBG = new MirImageControl
            {
                Location = new Point(250, 31),
                Parent = this,
                AutoSize = false,
                Size = new Size(24, 24),
                AtlasNameKey = "y_common-sheet#hide_select",
                ForeColour = Color.White,
                Visible = secondOn,
            };
            mUnSelectSecondBG.Click += (object sender, EventArgs e) =>
            {
                if (GameScene.Scene == null || GameScene.Scene.SkillBarDialogs == null) return;
                GameScene.Scene.SkillBarDialogs.ShowSecondColumn = true;
                mUnSelectSecondBG.Visible = false;
                mSelectSecondBG.Visible = true;
            };
            mSelectSecondBG.Click += (object sender, EventArgs e) =>
            {
                if (GameScene.Scene == null || GameScene.Scene.SkillBarDialogs == null) return;
                GameScene.Scene.SkillBarDialogs.ShowSecondColumn = false;
                mSelectSecondBG.Visible = false;
                mUnSelectSecondBG.Visible = true;
            };

            // 描述文字 "X快捷键设置为" @ (150,38)
            var mDes = new MirLabel
            {
                Location = new Point(300, 38),
                Parent = this,
                Size = new Size(230, 32),
                DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                ForeColour = Color.White,
                Text = "双排技能栏"
            };

            // 16 个数字键网格：8 列 × 2 行，起点 (33,91)，每格 44×42，横间距 0、纵间距 12（JS TileLayout）
            FKeys = new MirButton[16];
            SelectFrames = new MirImageControl[16];
            for (byte i = 0; i < FKeys.Length; i++)
            {
                int col = i % 8, row = i / 8;
                FKeys[i] = new MirButton
                {
                    Parent = this,
                    AtlasNameKey = Sheet + "#btn_kjjsz_01",
                    PressedAtlasFrame = "btn_kjjsz_02",
                    Location = new Point(33 + 44 * col, 91 + (42 + 12) * row),
                    Size = new Size(50, 50),
                    Sound = SoundList.ButtonA,
                };
                // 数字图 word_kjjsz_{n}
                var digit = new MirImageControl
                {
                    Parent = FKeys[i],
                    AtlasNameKey = Sheet + "#word_kjjsz_" + i,
                    Size = new Size(44, 42),
                    NotControl = true,
                    Anchor =  EAnchorType.MiddleCenter,
                    AnchorPos = new Point(0, 0)
                };
                digit.ApplyAnchor();

                // 选中框 y_select（来自 y_common-sheet）
                SelectFrames[i] = new MirImageControl
                {
                    Parent = FKeys[i],
                    AtlasNameKey = "y_common-sheet#y_select",
                    Location = new Point(2, 2),
                    AutoSize = false,
                    Size = new Size(45, 45),
                    NotControl = true,
                    Visible = true,
                };

                int ii = i;
                FKeys[i].Click += (o, e) =>
                {
                    Key = (byte)(ii + KeyOffset);
                };
            }

            // 清空按钮 @ (397,86) / 确定按钮 @ (397,141)，各 85×46
            NoneButton = new MirButton
            {
                Parent = this,
                AtlasNameKey = Sheet + "#btn_kjjsz_01",
                PressedAtlasFrame = "btn_kjjsz_02",
                Location = new Point(397, 86),
                Size = new Size(85, 46),
                Sound = SoundList.ButtonA,
            };
            var clearWord = new MirImageControl
            {
                Parent = NoneButton, AtlasNameKey = Sheet + "#word_kjjsz_18",
                Location = new Point(0, 0), Size = new Size(85, 46), NotControl = true,
            };
            NoneButton.Click += (o, e) => { Key = 0; SaveAndClose(); };

            SaveButton = new MirButton
            {
                Parent = this,
                AtlasNameKey = Sheet + "#btn_kjjsz_01",
                PressedAtlasFrame = "btn_kjjsz_02",
                Location = new Point(397, 141),
                Size = new Size(85, 46),
                Sound = SoundList.ButtonA,
            };
            var okWord = new MirImageControl
            {
                Parent = SaveButton, AtlasNameKey = Sheet + "#word_kjjsz_17",
                Location = new Point(0, 0), Size = new Size(85, 46), NotControl = true,
            };
            SaveButton.Click += (o, e) => SaveAndClose();

            BeforeDraw += AssignKeyPanel_BeforeDraw;

            // 透明关闭：本面板 Modal=true，点击面板矩形外（被模态拦截的区域）也会派发到本控件。
            // 判定点击落在面板之外即关闭；面板内（含背景空白）不关，避免误触 UI。
            Click += (o, e) =>
            {
                if (!DisplayRectangle.Contains(CMain.MPoint))
                {
                    Dispose();
                }
            };

        }

        private void SaveAndClose()
        {
            if (Actor != null)
            {
                for (int i = 0; i < Actor.Magics.Count; i++)
                {
                    if (Actor.Magics[i].Key == Key)
                    {
                        Actor.Magics[i].Key = 0;
                    }
                }

                Network.Enqueue(new C.MagicKey { Spell = Magic.Spell, Key = Key, OldKey = Magic.Key });
                Magic.Key = Key;
                GameScene.Scene.SkillBarDialogs.UpdateUI();
            }
            Dispose();
        }

        private void AssignKeyPanel_BeforeDraw(object sender, EventArgs e)
        {
            int key = Key - KeyOffset;
            for (int i = 0; i < FKeys.Length; i++)
            {
                SelectFrames[i].Visible = (i == key);
            }
        }

    }

    public sealed class DuraStatusDialog : MirImageControl
    {
        public MirButton Character;

        public DuraStatusDialog()
        {
            Size = new Size(40, 19);
            Location = new Point((GameScene.Scene.MiniMapDialog.Location.X + 86), GameScene.Scene.MiniMapDialog.Size.Height);

            Character = new MirButton()
            {
                Index = 2113,
                Library = Libraries.Prguse,
                Parent = this,
                Size = new Size(20, 19),
                Location = new Point(20, 0),
                HoverIndex = 2111,
                PressedIndex = 2112,
                Sound = SoundList.ButtonA,
                Hint = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DuraPanel)
            };
            Character.Click += (o, e) =>
            {
                if (GameScene.Scene.CharacterDuraPanel.Visible == true)
                {
                    GameScene.Scene.CharacterDuraPanel.Hide();
                    Settings.DuraView = false;
                }
                else
                {
                    GameScene.Scene.CharacterDuraPanel.Show();
                    Settings.DuraView = true;
                }
            };
        }

    }
    public sealed class CharacterDuraPanel : MirImageControl
    {
        public MirImageControl GrayBackground, Background, Helmet, Armour, Belt, Boots, Weapon, Necklace, RightBracelet, LeftBracelet, RightRing, LeftRing, Torch, Stone, Amulet, Mount, Item1, Item2;

        public CharacterDuraPanel()
        {
            Index = 2105;
            Library = Libraries.Prguse;
            Movable = false;
            Location = new Point(Settings.ScreenWidth - 61, 200);

            GrayBackground = new MirImageControl()
            {
                Index = 2161,
                Library = Libraries.Prguse,
                Parent = this,
                Size = new Size(56, 80),
                Location = new Point(3, 3),
                Opacity = 0.4F
            };
            Background = new MirImageControl()
            {
                Index = 2162,
                Library = Libraries.Prguse,
                Parent = this,
                Size = new Size(56, 80),
                Location = new Point(3, 3),
            };


            Helmet = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(24, 3) };
            Belt = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 7), Location = new Point(23, 23) };
            Armour = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(28, 32), Location = new Point(16, 11) };
            Boots = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(24, 9), Location = new Point(17, 43) };
            Weapon = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 33), Location = new Point(4, 5) };
            Necklace = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(3, 67) };
            LeftBracelet = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 8), Location = new Point(3, 43) };
            RightBracelet = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 8), Location = new Point(43, 43) };
            LeftRing = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(3, 54) };
            RightRing = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(43, 54) };
            Torch = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(8, 32), Location = new Point(44, 5) };
            Stone = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(30, 54) };
            Amulet = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(16, 54) };
            Mount = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(12, 12), Location = new Point(43, 68) };
            Item1 = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(8, 12), Location = new Point(19, 67) };
            Item2 = new MirImageControl() { Index = -1, Library = Libraries.Prguse, Parent = Background, Size = new Size(8, 12), Location = new Point(31, 67) };

        }

        public void GetCharacterDura()
        {
            if (GameScene.Scene.CharacterDialog.Grid[0].Item == null) { Weapon.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[1].Item == null) { Armour.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[2].Item == null) { Helmet.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[3].Item == null) { Torch.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[4].Item == null) { Necklace.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[5].Item == null) { LeftBracelet.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[6].Item == null) { RightBracelet.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[7].Item == null) { LeftRing.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[8].Item == null) { RightRing.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[9].Item == null) { Amulet.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[10].Item == null) { Belt.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[11].Item == null) { Boots.Index = -1; }
            if (GameScene.Scene.CharacterDialog.Grid[12].Item == null) { Stone.Index = -1; }

            for (int i = 0; i < MapObject.User.Equipment.Length; i++)
            {
                if (MapObject.User.Equipment[i] == null) continue;
                UpdateCharacterDura(MapObject.User.Equipment[i]);
            }
        }
        public void UpdateCharacterDura(UserItem item)
        {
            int Warning = item.MaxDura / 2;
            int Danger = item.MaxDura / 5;
            ushort AmuletWarning = (ushort)(item.Info.StackSize / 2);
            ushort AmuletDanger = (ushort)(item.Info.StackSize / 5);

            switch (item.Info.Type)
            {
                case ItemType.Amulet: //Based on stacks of 5000
                    if (item.Count > AmuletWarning)
                        Amulet.Index = 2134;
                    if (item.Count <= AmuletWarning)
                        Amulet.Index = 2135;
                    if (item.Count <= AmuletDanger)
                        Amulet.Index = 2136;
                    if (item.Count == 0)
                        Amulet.Index = -1;
                    break;
                case ItemType.Armour:
                    if (item.CurrentDura > Warning)
                        Armour.Index = 2149;
                    if (item.CurrentDura <= Warning)
                        Armour.Index = 2150;
                    if (item.CurrentDura <= Danger)
                        Armour.Index = 2151;
                    if (item.CurrentDura == 0)
                        Armour.Index = -1;
                    break;
                case ItemType.Belt:
                    if (item.CurrentDura > Warning)
                        Belt.Index = 2158;
                    if (item.CurrentDura <= Warning)
                        Belt.Index = 2159;
                    if (item.CurrentDura <= Danger)
                        Belt.Index = 2160;
                    if (item.CurrentDura == 0)
                        Belt.Index = -1;
                    break;
                case ItemType.Boots:
                    if (item.CurrentDura > Warning)
                        Boots.Index = 2152;
                    if (item.CurrentDura <= Warning)
                        Boots.Index = 2153;
                    if (item.CurrentDura <= Danger)
                        Boots.Index = 2154;
                    if (item.CurrentDura == 0)
                        Boots.Index = -1;
                    break;
                case ItemType.Bracelet:
                    if (GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.BraceletR].Item != null && item.UniqueID == GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.BraceletR].Item.UniqueID)
                    {
                        if (item.CurrentDura > Warning)
                            RightBracelet.Index = 2143;
                        if (item.CurrentDura <= Warning)
                            RightBracelet.Index = 2144;
                        if (item.CurrentDura <= Danger)
                            RightBracelet.Index = 2145;
                        if (item.CurrentDura == 0)
                            RightBracelet.Index = -1;
                    }
                    else if (GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.BraceletL].Item != null && item.UniqueID == GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.BraceletL].Item.UniqueID)
                    {
                        if (item.CurrentDura > Warning)
                            LeftBracelet.Index = 2143;
                        if (item.CurrentDura <= Warning)
                            LeftBracelet.Index = 2144;
                        if (item.CurrentDura <= Danger)
                            LeftBracelet.Index = 2145;
                        if (item.CurrentDura == 0)
                            LeftBracelet.Index = -1;
                    }
                    break;
                case ItemType.Helmet:
                    if (item.CurrentDura > Warning)
                        Helmet.Index = 2155;
                    if (item.CurrentDura <= Warning)
                        Helmet.Index = 2156;
                    if (item.CurrentDura <= Danger)
                        Helmet.Index = 2157;
                    if (item.CurrentDura == 0)
                        Helmet.Index = -1;
                    break;
                case ItemType.Necklace:
                    if (item.CurrentDura > Warning)
                        Necklace.Index = 2122;
                    if (item.CurrentDura <= Warning)
                        Necklace.Index = 2123;
                    if (item.CurrentDura <= Danger)
                        Necklace.Index = 2124;
                    if (item.CurrentDura == 0)
                        Necklace.Index = -1;
                    break;
                case ItemType.Ring:
                    if (GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.RingR].Item != null && item.UniqueID == GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.RingR].Item.UniqueID)
                    {
                        if (item.CurrentDura > Warning)
                            RightRing.Index = 2131;
                        if (item.CurrentDura <= Warning)
                            RightRing.Index = 2132;
                        if (item.CurrentDura <= Danger)
                            RightRing.Index = 2133;
                        if (item.CurrentDura == 0)
                            RightRing.Index = -1;
                    }
                    else if (GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.RingL].Item != null && item.UniqueID == GameScene.Scene.CharacterDialog.Grid[(byte)EquipmentSlot.RingL].Item.UniqueID)
                    {
                        if (item.CurrentDura > Warning)
                            LeftRing.Index = 2131;
                        if (item.CurrentDura <= Warning)
                            LeftRing.Index = 2132;
                        if (item.CurrentDura <= Danger)
                            LeftRing.Index = 2133;
                        if (item.CurrentDura == 0)
                            LeftRing.Index = -1;
                    }
                    break;
                case ItemType.Stone:
                    if (item.CurrentDura == 0)
                        Stone.Index = 2137;
                    break;
                case ItemType.Mount:
                    if (item.CurrentDura > Warning)
                        Mount.Index = 2140;
                    if (item.CurrentDura <= Warning)
                        Mount.Index = 2141;
                    if (item.CurrentDura <= Danger)
                        Mount.Index = 2142;
                    if (item.CurrentDura == 0)
                        Mount.Index = -1;
                    break;
                case ItemType.Torch:
                    if (item.CurrentDura > Warning)
                        Torch.Index = 2146;
                    if (item.CurrentDura <= Warning)
                        Torch.Index = 2147;
                    if (item.CurrentDura <= Danger)
                        Torch.Index = 2148;
                    if (item.CurrentDura == 0)
                        Torch.Index = -1;
                    break;
                case ItemType.Weapon:
                    if (item.CurrentDura > Warning)
                        Weapon.Index = 2125;
                    if (item.CurrentDura <= Warning)
                        Weapon.Index = 2126;
                    if (item.CurrentDura <= Danger)
                        Weapon.Index = 2127;
                    if (item.CurrentDura == 0)
                        Weapon.Index = -1;
                    break;
            }
        }

        public override void Hide()
        {
            if (!Visible) return;
            Visible = false;
            GameScene.Scene.DuraStatusPanel.Character.Index = 2113;
        }
        public override void Show()
        {
            if (Visible) return;
            Visible = true;
            GameScene.Scene.DuraStatusPanel.Character.Index = 2110;

            GetCharacterDura();
        }
    }

    // 技能描述（从原 MagicButton.Update 的 switch 提取，供技能栏/技能面板共用）
    public static class SkillDescriptionHelper
    {
        /// <summary>被动技能：不能也不该绑定技能快捷键（与 GameScene.UseSpell 的被动 return 列表一致）。</summary>
        public static bool IsPassive(Spell spell)
        {
            switch (spell)
            {
                case Spell.Fencing:
                case Spell.FatalSword:
                case Spell.MPEater:
                case Spell.Hemorrhage:
                case Spell.SpiritSword:
                case Spell.Slaying:
                case Spell.Focus:
                case Spell.Meditation:
                    return true;
                default:
                    return false;
            }
        }

        public static string Get(ClientMagic magic)
        {
                switch (magic.Spell)
                {  //Warrior
                    case Spell.Fencing:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FencingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.Slaying:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SlayingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.Thrusting:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ThrustingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.Rage:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RageSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ProtectionField:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ProtectionFieldSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.HalfMoon:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HalfMoonSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FlamingSword:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FlamingSwordSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ShoulderDash:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ShoulderDashSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.CrossHalfMoon:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CrossHalfMoonSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.TwinDrakeBlade:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TwinDrakeBladeSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Entrapment:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.EntrapmentSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.LionRoar:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.LionRoarSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.CounterAttack:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CounterAttackSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ImmortalSkin:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ImmortalSkinSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Fury:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FurySkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SlashingBurst:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SlashingBurstSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.BladeAvalanche:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BladeAvalancheSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Wizard
                    case Spell.FireBall:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FireballSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ThunderBolt:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ThunderboltSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.GreatFireBall:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.GreatFireballSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Repulsion:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RepulsionSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.HellFire:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HellfireSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Lightning:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.LightningSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ElectricShock:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ElectricShockSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Teleport:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TeleportSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FireWall:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FireWallSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FireBang:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FireBangSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ThunderStorm:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ThunderstormSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MagicShield:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MagicShieldSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.TurnUndead:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TurnUndeadSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.IceStorm:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.IceStormSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FlameDisruptor:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FlameDisruptorSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FrostCrunch:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FrostCrunchSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Mirroring:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MirroringSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FlameField:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FlameFieldSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Vampirism:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.VampirismSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Blizzard:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BlizzardSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MeteorStrike:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MeteorStrikeSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.IceThrust:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.IceThrustSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MagicBooster:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MagicBoosterSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FastMove:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FastMoveSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.StormEscape:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.StormEscapeSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Blink:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BlinkSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Taoist
                    case Spell.SpiritSword:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SpiritSwordSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Healing:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HealingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Poisoning:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PoisoningSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SoulFireBall:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SoulFireBallSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SoulShield:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SoulShieldSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.BlessedArmour:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BlessedArmourSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.TrapHexagon:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TrapHexagonSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonSkeleton:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonSkeletonSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Hiding:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HidingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MassHiding:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MassHidingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Revelation:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RevelationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MassHealing:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MassHealingSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonShinsu:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonShinsuSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.UltimateEnhancer:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.UltimateEnhancerSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.EnergyRepulsor:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.EnergyRepulsorSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Purification:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PurificationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonHolyDeva:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonHolyDevaSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Curse:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CurseSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Hallucination:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HallucinationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Reincarnation:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ReincarnationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.PoisonCloud:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PoisonCloudSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.EnergyShield:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.EnergyShieldSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Plague:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PlagueSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.HealingCircle:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HealingCircleSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.PetEnhancer:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PetEnhancerSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Assassin
                    case Spell.FatalSword:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FatalSwordPassiveSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.DoubleSlash:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DoubleSlashToggleSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Haste:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HasteBuffSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FlashDash:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FlashDashSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.HeavenlySword:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HeavenlySwordSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.FireBurst:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FireBurstSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Trap:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TrapSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MoonLight:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MoonlightSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MPEater:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MPEaterSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SwiftFeet:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SwiftFeetSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.LightBody:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.LightBodySkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.PoisonSword:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PoisonSwordSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.DarkBody:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DarkBodySkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.CrescentSlash:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CrescentSlashSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Hemorrhage:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.HemorrhageSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.MoonMist:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MoonMistSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Archer
                    case Spell.Focus:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.FocusSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.StraightShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.StraightShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.DoubleShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DoubleShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ExplosiveTrap:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ExplosiveTrapSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.DelayedExplosion:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DelayedExplosionSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Meditation:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.MeditationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.BackStep:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BackStepSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ElementalShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ElementalShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Concentration:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ConcentrationSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.Stonetrap:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.StoneTrapSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.ElementalBarrier:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ElementalBarrierSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonVampire:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonVampireSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.VampireShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.VampireShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonToad:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonToadSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.PoisonShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PoisonShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.CrippleShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CrippleShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.SummonSnakes:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SummonSnakesSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.NapalmShot:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.NapalmShotSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.OneWithNature:
                        return GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.OneWithNatureSkillDescription), magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);


                    default:
                        break;
                }
                return string.Empty;
        }
    }
}
