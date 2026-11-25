using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    public sealed class HelpDialog : MirImageControl
    {
        public List<HelpPage> Pages = new List<HelpPage>();

        public MirButton CloseButton, NextButton, PreviousButton;
        public MirLabel PageLabel;
        public HelpPage CurrentPage;

        public int CurrentPageNumber = 0;

        public HelpDialog()
        {
            Index = 920;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;

            Location = Center;

            MirImageControl TitleLabel = new MirImageControl
            {
                Index = 57,
                Library = Libraries.Title,
                Location = new Point(18, 9),
                Parent = this
            };

            PreviousButton = new MirButton
            {
                Index = 240,
                HoverIndex = 241,
                PressedIndex = 242,
                Library = Libraries.Prguse2,
                Parent = this,
                Size = new Size(16, 16),
                Location = new Point(210, 485),
                Sound = SoundList.ButtonA,
            };
            PreviousButton.Click += (o, e) =>
            {
                CurrentPageNumber--;

                if (CurrentPageNumber < 0) CurrentPageNumber = Pages.Count - 1;

                DisplayPage(CurrentPageNumber);
            };

            NextButton = new MirButton
            {
                Index = 243,
                HoverIndex = 244,
                PressedIndex = 245,
                Library = Libraries.Prguse2,
                Parent = this,
                Size = new Size(16, 16),
                Location = new Point(310, 485),
                Sound = SoundList.ButtonA,
            };
            NextButton.Click += (o, e) =>
            {
                CurrentPageNumber++;

                if (CurrentPageNumber > Pages.Count - 1) CurrentPageNumber = 0;

                DisplayPage(CurrentPageNumber);
            };

            PageLabel = new MirLabel
            {
                Text = "",
                Font = FontConst.F9,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                NotControl = true,
                Location = new Point(230, 480),
                Size = new Size(80, 20)
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(509, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            LoadImagePages();

            DisplayPage();
        }

        private void LoadImagePages()
        {
            Point location = new Point(12, 35);

            Dictionary<string, string> keybinds = new Dictionary<string, string>();

            List<HelpPage> imagePages = new List<HelpPage> { 
                new HelpPage(LanguageMgr.Get("ShortcutInformation"), -1, new ShortcutPage1 { Parent = this } ) { Parent = this, Location = location, Visible = false }, 
                new HelpPage(LanguageMgr.Get("ShortcutInformation"), -1, new ShortcutPage2 { Parent = this } ) { Parent = this, Location = location, Visible = false }, 
                new HelpPage(LanguageMgr.Get("ChatShortcuts"), -1, new ShortcutPage3 { Parent = this } ) { Parent = this, Location = location, Visible = false }, 
                new HelpPage(LanguageMgr.Get("Movements"), 0, null) { Parent = this, Location = location, Visible = false }, 
                new HelpPage(LanguageMgr.Get("Attacking"), 1, null) { Parent = this, Location = location, Visible = false }, 
                new HelpPage(LanguageMgr.Get("CollectingItems"), 2, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Health"), 3, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Skills"), 4, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Skills"), 5, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Mana"), 6, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Chatting"), 7, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Groups"), 8, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Durability"), 9, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Purchasing"), 10, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Selling"), 11, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Repairing"), 12, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Trading"), 13, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Inspecting"), 14, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 15, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 16, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 17, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 18, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 19, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Statistics"), 20, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Quests"), 21, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Quests"), 22, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Quests"), 23, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Quests"), 24, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Mounts"), 25, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Mounts"), 26, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Fishing"), 27, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("GemsAndOrbs"), 28, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Heroes"), 29, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Heroes"), 30, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Heroes"), 31, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Heroes"), 32, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Heroes"), 33, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("GuildBuffs"), 34, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("GuildBuffs"), 35, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("GuildBuffs"), 36, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Awakening"), 37, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Awakening"), 38, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Awakening"), 39, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Awakening"), 40, null) { Parent = this, Location = location, Visible = false },
                new HelpPage(LanguageMgr.Get("Awakening"), 41, null) { Parent = this, Location = location, Visible = false },
            };

            Pages.AddRange(imagePages);
        }


        public void DisplayPage(string pageName)
        {
            if (Pages.Count < 1) return;

            for (int i = 0; i < Pages.Count; i++)
            {
                if (Pages[i].Title.ToLower() != pageName.ToLower()) continue;

                DisplayPage(i);
                break;
            }
        }

        public void DisplayPage(int id = 0)
        {
            if (Pages.Count < 1) return;

            if (id > Pages.Count - 1) id = Pages.Count - 1;
            if (id < 0) id = 0;

            if (CurrentPage != null)
            {
                CurrentPage.Visible = false;
                if (CurrentPage.Page != null) CurrentPage.Page.Visible = false;
            }

            CurrentPage = Pages[id];

            if (CurrentPage == null) return;

            CurrentPage.Visible = true;
            if (CurrentPage.Page != null) CurrentPage.Page.Visible = true;
            CurrentPageNumber = id;

            CurrentPage.PageTitleLabel.Text = id + 1 + ". " + CurrentPage.Title;

            PageLabel.Text = string.Format("{0} / {1}", id + 1, Pages.Count);

            Show();
        }


        public void Toggle()
        {
            if (!Visible)
                Show();
            else
                Hide();
        }
    }

    public class ShortcutPage1 : ShortcutInfoPage
    {
        public ShortcutPage1()
        {
            Shortcuts = new List<ShortcutInfo>
            {
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Exit), LanguageMgr.Get("ExitGame")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Logout), LanguageMgr.Get("LogOut")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Bar1Skill1) + "-" + CMain.InputKeys.GetKey(KeybindOptions.Bar1Skill8), LanguageMgr.Get("SkillButtons")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Inventory), LanguageMgr.Get("InventoryWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Equipment), LanguageMgr.Get("StatusWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Skills), LanguageMgr.Get("SkillWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Group), LanguageMgr.Get("GroupWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Trade), LanguageMgr.Get("TradeWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Friends), LanguageMgr.Get("FriendWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Minimap), LanguageMgr.Get("MinimapWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Guilds), LanguageMgr.Get("GuildWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.GameShop), LanguageMgr.Get("GameshopWindowOpenClose")),
                //Shortcuts.Add(new ShortcutInfo("K", "Rental window (open / close)"));
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Relationship), LanguageMgr.Get("EngagementWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Belt), LanguageMgr.Get("BeltWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Options), LanguageMgr.Get("OptionWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Help), LanguageMgr.Get("HelpWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Mount), LanguageMgr.Get("MountDismountRide")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), LanguageMgr.Get("LockSpellOnTargetNotCursor"))
            };

            LoadKeyBinds();
        }
    }
    public class ShortcutPage2 : ShortcutInfoPage
    {
        public ShortcutPage2()
        {
            Shortcuts = new List<ShortcutInfo>
            {
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.ChangePetmode), LanguageMgr.Get("TogglePetAttackPet")),
                //Shortcuts.Add(new ShortcutInfo("Ctrl + F", "Change the font in the chat box"));
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.ChangeAttackmode), LanguageMgr.Get("TogglePlayerAttackMode")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.AttackmodePeace), LanguageMgr.Get("PeaceModeAttackMonstersOnly")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.AttackmodeGroup), LanguageMgr.Get("GroupModeAttackExceptMembers")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.AttackmodeGuild), LanguageMgr.Get("GuildModeAttackExceptMembers")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.AttackmodeRedbrown), LanguageMgr.Get("GoodEvilModeAttackPKAndMonsters")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.AttackmodeAll), LanguageMgr.Get("AllAttackModeAllSubjects")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Bigmap), LanguageMgr.Get("ShowFieldMap")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Skillbar), LanguageMgr.Get("ShowSkillBar")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Autorun), LanguageMgr.Get("AutoRunOnOff")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Cameramode), LanguageMgr.Get("ShowHideInterface")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Pickup), LanguageMgr.Get("HighlightPickupItems")),
                new ShortcutInfo(LanguageMgr.Get("CtrlRightClick"), LanguageMgr.Get("ShowOtherPlayersKits")),
                //Shortcuts.Add(new ShortcutInfo("F12", "Chat macros"));
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Screenshot), LanguageMgr.Get("ScreenCapture")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Fishing), LanguageMgr.Get("OpenCloseFishingWindow")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.Mentor), LanguageMgr.Get("MentorWindowOpenClose")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.CreaturePickup), LanguageMgr.Get("CreaturePickupMultiMouseTarget")),
                new ShortcutInfo(CMain.InputKeys.GetKey(KeybindOptions.CreatureAutoPickup), LanguageMgr.Get("CreaturePickupSingleMouseTarget"))
            };

            LoadKeyBinds();
        }
    }
    public class ShortcutPage3 : ShortcutInfoPage
    {
        public ShortcutPage3()
        {
            Shortcuts = new List<ShortcutInfo>
            {
                //Shortcuts.Add(new ShortcutInfo("` / Ctrl", "Change the skill bar"));
                new ShortcutInfo("/(username)", LanguageMgr.Get("CommandWhisperOthers")),
                new ShortcutInfo("!(text)", LanguageMgr.Get("CommandShoutNearby")),
                new ShortcutInfo("!~(text)", LanguageMgr.Get("CommandGuildChat"))
            };

            LoadKeyBinds();
        }
    }

    public class ShortcutInfo
    {
        public string Shortcut { get; set; }
        public string Information { get; set; }

        public ShortcutInfo(string shortcut, string info)
        {
            Shortcut = shortcut.Replace("\n", " + ");
            Information = info;
        }
    }

    public class ShortcutInfoPage : MirControl
    {
        protected List<ShortcutInfo> Shortcuts = new List<ShortcutInfo>();

        public ShortcutInfoPage()
        {
            Visible = false;

            MirLabel shortcutTitleLabel = new MirLabel
            {
                Text = LanguageMgr.Get("Shortcuts"),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.White,
                Font = FontConst.F10,
                Parent = this,
                AutoSize = true,
                Location = new Point(13, 75),
                Size = new Size(100, 30)
            };

            MirLabel infoTitleLabel = new MirLabel
            {
                Text = LanguageMgr.Get("Information"),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.White,
                Font = FontConst.F10,
                Parent = this,
                AutoSize = true,
                Location = new Point(114, 75),
                Size = new Size(405, 30)
            };
        }

        public void LoadKeyBinds()
        {
            if (Shortcuts == null) return;

            for (int i = 0; i < Shortcuts.Count; i++)
            {
                MirLabel shortcutLabel = new MirLabel
                {
                    Text = Shortcuts[i].Shortcut,
                    ForeColour = CommonColor.Yellow,
                    DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                    Font = FontConst.F9,
                    Parent = this,
                    AutoSize = true,
                    Location = new Point(18, 107 + (20 * i)),
                    Size = new Size(95, 23),
                };

                MirLabel informationLabel = new MirLabel
                {
                    Text = Shortcuts[i].Information,
                    DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                    ForeColour = CommonColor.White,
                    Font = FontConst.F9,
                    Parent = this,
                    AutoSize = true,
                    Location = new Point(119, 107 + (20 * i)),
                    Size = new Size(400, 23),
                };
            }  
        }
    }

    public class HelpPage : MirControl
    {
        public string Title;
        public int ImageID;
        public MirControl Page;

        public MirLabel PageTitleLabel;

        public HelpPage(string title, int imageID, MirControl page)
        {
            Title = title;
            ImageID = imageID;
            Page = page;

            NotControl = true;
            Size = new Size(508, 396 + 40);

            BeforeDraw += HelpPage_BeforeDraw;

            PageTitleLabel = new MirLabel
            {
                Text = Title,
                Font = FontConst.F10Bold,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Parent = this,
                Size = new Size(242, 30),
                Location = new Point(135, 4)
            };
        }

        void HelpPage_BeforeDraw(object sender, EventArgs e)
        {
            if (ImageID < 0) return;

            Libraries.Help.Draw(ImageID, new Point(DisplayLocation.X, DisplayLocation.Y + 40), CommonColor.White, false);
        }
    }
}
