using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class RelationshipDialog : MirImageControl
    {
        public MirImageControl TitleLabel;
        public MirButton CloseButton, AllowButton, RequestButton, DivorceButton, MailButton, WhisperButton;
        public MirLabel LoverNameLabel, LoverDateLabel, LoverOnlineLabel, LoverLengthLabel;


        public string LoverName = "";
        public DateTime Date;
        public string MapName = "";
        public short MarriedDays = 0;


        public RelationshipDialog()
        {
            Index = 583;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Location = Center;

            TitleLabel = new MirImageControl
            {
                Index = 52,
                Library = Libraries.Title,
                Location = new Point(18, 8),
                Parent = this
            };

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                Location = new Point(260, 3),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 362,
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            AllowButton = new MirButton
            {
                HoverIndex = 611,
                Index = 610,
                Location = new Point(50, 164),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 612,
                Sound = SoundList.ButtonA,
                Hint = LanguageMgr.Get("SwitchMarriage")};
            AllowButton.Click += (o, e) => Network.Enqueue(new C.ChangeMarriage());

            RequestButton = new MirButton
            {
                HoverIndex = 601,
                Index = 600,
                Location = new Point(85, 164),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 602,
                Sound = SoundList.ButtonA,
                Hint = LanguageMgr.Get("RequestMarriage")};
            RequestButton.Click += (o, e) =>
            {
                if (LoverName != "")
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(LanguageMgr.Get("YouAreAlreadyMarried"), ChatType.System);
                    return;
                }

                Network.Enqueue(new C.MarriageRequest());
            };

            DivorceButton = new MirButton
            {
                HoverIndex = 617,
                Index = 616,
                Location = new Point(120, 164),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 618,
                Sound = SoundList.ButtonA,
                Hint = LanguageMgr.Get("RequestDivorce")};
            DivorceButton.Click += (o, e) =>
            {
                if (LoverName == "")
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(LanguageMgr.Get("YouAreNotMarried"), ChatType.System);
                    return;
                }

                Network.Enqueue(new C.DivorceRequest());
            };

            MailButton = new MirButton
            {
                HoverIndex = 438,
                Index = 437,
                Location = new Point(155, 164),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 439,
                Sound = SoundList.ButtonA,
                Hint = LanguageMgr.Get("MailLover")};
            MailButton.Click += (o, e) =>
            {
                if (LoverName == "")
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(LanguageMgr.Get("YouAreNotMarried"), ChatType.System);
                    return;
                }

                GameScene.Scene.MailComposeLetterDialog.ComposeMail(LoverName);
            };

            WhisperButton = new MirButton
            {
                HoverIndex = 567,
                Index = 566,
                Location = new Point(190, 164),
                Library = Libraries.Prguse,
                Parent = this,
                PressedIndex = 568,
                Sound = SoundList.ButtonA,
                Hint = LanguageMgr.Get("WhisperLover")};
            WhisperButton.Click += (o, e) =>
            {
                if (LoverName == "")
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(LanguageMgr.Get("YouAreNotMarried"), ChatType.System);
                    return;
                }

                if (MapName == "")
                {
                    GameScene.Scene.ChatDialog.ReceiveChat(LanguageMgr.Get("LoverIsNotOnline"), ChatType.System);
                    return;
                }

                GameScene.Scene.ChatDialog.ChatTextBox.SetFocus();
                GameScene.Scene.ChatDialog.ChatTextBox.Text = ":)";
                GameScene.Scene.ChatDialog.ChatTextBox.Visible = true;
                GameScene.Scene.ChatDialog.ChatTextBox.TextBox.SelectionLength = 0;
                GameScene.Scene.ChatDialog.ChatTextBox.TextBox.SelectionStart = GameScene.Scene.ChatDialog.ChatTextBox.Text.Length;
            };

            LoverNameLabel = new MirLabel
            {
                Location = new Point(30, 40),
                Size = new Size(200, 30),
                BackColour = CommonColor.Empty,
                ForeColour = CommonColor.LightGray,
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Parent = this,
                NotControl = true,
                Font = new Font(Settings.FontName, 10F),
            };

            LoverDateLabel = new MirLabel
            {
                Location = new Point(30, 65),
                Size = new Size(200, 30),
                BackColour = CommonColor.Empty,
                ForeColour = CommonColor.LightGray,
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Parent = this,
                NotControl = true,
                Font = new Font(Settings.FontName, 10F),
            };

            LoverLengthLabel = new MirLabel
            {
                Location = new Point(30, 90),
                Size = new Size(200, 30),
                BackColour = CommonColor.Empty,
                ForeColour = CommonColor.LightGray,
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Parent = this,
                NotControl = true,
                Font = new Font(Settings.FontName, 10F),
            };

            LoverOnlineLabel = new MirLabel
            {
                Location = new Point(30, 115),
                Size = new Size(200, 30),
                BackColour = CommonColor.Empty,
                ForeColour = CommonColor.LightGray,
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                Parent = this,
                NotControl = true,
                Font = new Font(Settings.FontName, 10F),
            };
        }

        public void UpdateInterface()
        {
            LoverNameLabel.Text = LanguageMgr.Get("LoverName", LoverName);

            if (MapName != "")
            {
                LoverOnlineLabel.Text = LanguageMgr.Get("LoverLocation", MapName);
            }
            else
                LoverOnlineLabel.Text = LanguageMgr.Get("LoverLocationOffline");

            if ((LoverName == "") && (Date != default))
            {
                if (Date < new DateTime(2000))
                {
                    LoverDateLabel.Text = LanguageMgr.Get("LoverDate");
                    LoverLengthLabel.Text = LanguageMgr.Get("LoverLength");
                }
                else
                {
                    LoverDateLabel.Text = LanguageMgr.Get("DivorcedDate", Date.ToShortDateString());
                    LoverLengthLabel.Text = LanguageMgr.Get("TimeSinceDays", MarriedDays);
                }


                LoverOnlineLabel.Text = LanguageMgr.Get("LoverLocationTitle");
                AllowButton.Hint = LanguageMgr.Get("SwitchMarriage");
            }
            else
            {
                LoverDateLabel.Text = LanguageMgr.Get("MarriageDate", Date == DateTime.MinValue ? string.Empty : Date.ToShortDateString());
                LoverLengthLabel.Text = LanguageMgr.Get("LengthDays", MarriedDays);
                AllowButton.Hint = LanguageMgr.Get("AllowBlockRecall");
            }
        }
    }
}