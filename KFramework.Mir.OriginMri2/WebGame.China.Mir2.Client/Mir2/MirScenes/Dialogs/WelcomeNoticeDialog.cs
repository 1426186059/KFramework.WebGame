using Client.MirControls;
using Client.MirSounds;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 新角色首次进游戏弹出的欢迎界面 —— 对齐《原始传奇》页游 GameMemoryDialog（皮肤 GameMemorySkin）。
    /// 原版：370×519，背景 game_start_bg（gamestart-sheet），顶部“玛法2001纪元”、底部“寻找共同梦想！”，
    /// 中间是模拟老玩家的聊天（由服务端 Notice.txt 下发），“确定”按钮 + 5 秒倒计时自动关闭，无标题栏。
    /// 数据来源：服务端 S.UpdateNotice（WelcomeNotice.txt 的 TITLE 留空、正文为聊天行）→ GameScene.ShowWelcomeNotice → Update()。
    /// 富文本：Notice.Message 中 {文字/#RRGGBB} 着色，解析逻辑沿用经典 Mir2。
    /// </summary>
    public sealed class WelcomeNoticeDialog : MirImageControl
    {
        public static Regex C = new Regex(@"{((.*?)\/(.*?))}");
        public static Regex L = new Regex(@"\(((.*?)\/(.*?))\)");

        public MirButton CloseButton, UpButton, DownButton, PositionBar, OkButton;
        public MirLabel[] TextLabel;
        public List<MirLabel> TextButtons;
        public MirImageControl TitleLabel;
        public MirLabel NameLabel;

        public Notice Notice = new Notice();

        // —— 原始传奇 GameMemorySkin 几何：370×519，背景 game_start_bg（gamestart-sheet）——
        private const string WelcomeSheet = "gamestart-sheet";
        private const int PanelW = 370, PanelH = 519;
        private const int ChatX = 81, ChatY = 164, ChatW = 240, LineH = 22;

        Font font = new Font(Settings.FontName, 11F);

        public List<string> CurrentLines = new List<string>();
        private int _index = 0;
        public int MaximumLines = 9;                    // 聊天区约 9 行

        public WelcomeNoticeDialog()
        {
            AtlasNameKey = WelcomeSheet + "#game_start_bg";   // 欢迎界面背景（gamestart-sheet 图集帧）
            AutoSize = false;
            Size = new Size(PanelW, PanelH);
            TextLabel = new MirLabel[32];
            TextButtons = new List<MirLabel>();
            Sort = true;
            Modal = true;
            Anchor = EAnchorType.MiddleCenter;
            AnchorPos = new Point(0, -100);
            ApplyAnchor();

            Font bigFont = new Font(Settings.FontName, 14F);

            // 顶部“玛法2001纪元”（原版 txt_date x80,y118）—— 无标题栏，直接画在面板上。
            new MirLabel
            {
                Parent = this, Text = "玛法2001纪元", Font = bigFont,
                ForeColour = CommonColor.FromArgb(255, 255, 228, 100), OutLine = true,
                Location = new Point(80, 118), AutoSize = true, NotControl = true
            };

            // 底部“寻找共同梦想！”（原版 txt_date1 x136,y394）
            new MirLabel
            {
                Parent = this, Text = "寻找共同梦想！", Font = bigFont,
                ForeColour = CommonColor.FromArgb(255, 40, 239, 1), OutLine = true,
                Location = new Point(136, 394), AutoSize = true, NotControl = true
            };

            NameLabel = new MirLabel { Parent = this, Visible = false };  // 兼容旧字段，原版无标题
            UpButton = new MirButton { Parent = this, Visible = false };
            DownButton = new MirButton { Parent = this, Visible = false };
            PositionBar = new MirButton { Parent = this, Visible = false };

            // 右上角 X 关闭按钮 —— 与背包 InventoryDialog 的 CloseButton 同款（y_common-sheet#com_btn_close0）。
            // 放在面板右上角内侧，点击关闭；与底部“关闭”按钮并存，两种方式都能关。
            CloseButton = new MirButton
            {
                Location = new Point(PanelW, 3),
                Parent = this,
                Sound = SoundList.ButtonA,
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                Size = new Size(26, 42),
            };
            CloseButton.Click += (o, e) => Hide();

            // “关闭”按钮（原版 btn_ok x127,y438，改为手动关闭）——用现役图集按钮，
            // 不再用已废弃的 Library/Index（MirMessageBox 同款：y_common-sheet#com_z_btn_1）。
            OkButton = new MirButton
            {
                Parent = this,
                Location = new Point(132, 438),
                Size = new Size(106, 40),
                AtlasNameKey = "y_common-sheet#com_z_btn_1",
                HoverAtlasFrame = "com_z_btn_1_1",
                PressedAtlasFrame = "com_z_btn_1_1",
                BackColour = CommonColor.FromArgb(0, 0, 0, 0),
                Border = false,
                Text = "关闭",
                CenterText = true,
                FontColour = CommonColor.White,
                Sound = SoundList.ButtonA,
            };
            OkButton.Click += (o, e) => Hide();
        }

        public void Update(Notice notice)
        {
            this.Notice = notice;

            List<string> temp = new List<string>();

            if (string.IsNullOrWhiteSpace(notice.Message))
            {
                return;
            }

            string[] lines = notice.Message.Split(new string[] { "\r\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line)) temp.Add(line);
            }

            NewText(temp);
            Visible = true;
        }

        public void NewText(List<string> lines, bool resetIndex = true)
        {
            if (resetIndex)
            {
                _index = 0;
                CurrentLines = lines;
            }

            for (int i = 0; i < TextButtons.Count; i++)
                TextButtons[i].Dispose();

            for (int i = 0; i < TextLabel.Length; i++)
            {
                if (TextLabel[i] != null) TextLabel[i].Text = "";
            }

            TextButtons.Clear();

            // 原版欢迎界面无标题栏，Notice.Title 忽略（服务端 NOTICE.TXT 里 TITLE 留空）。

            int lastLine = lines.Count > MaximumLines ? MaximumLines : lines.Count;

            for (int i = _index; i < lastLine; i++)
            {
                TextLabel[i] = new MirLabel
                {
                    Font = font,
                    DrawFormat = new TextStyle(TextAnchor.UpperLeft, HorizontalWrapMode.Wrap),
                    Visible = true,
                    Parent = this,
                    Size = new Size(ChatW, LineH),
                    Location = new Point(ChatX, ChatY + (i - _index) * LineH),
                    NotControl = true
                };

                if (i >= lines.Count)
                {
                    TextLabel[i].Text = string.Empty;
                    continue;
                }

                string currentLine = lines[i];

                List<Match> matchList = C.Matches(currentLine).Cast<Match>().ToList();
                matchList.AddRange(L.Matches(currentLine).Cast<Match>());

                int oldLength = currentLine.Length;

                foreach (Match match in matchList.OrderBy(o => o.Index).ToList())
                {
                    int offSet = oldLength - currentLine.Length;

                    Capture capture = match.Groups[1].Captures[0];
                    string txt = match.Groups[2].Captures[0].Value;
                    string action = match.Groups[3].Captures[0].Value;

                    currentLine = currentLine.Remove(capture.Index - 1 - offSet, capture.Length + 2).Insert(capture.Index - 1 - offSet, txt);

                    string text = currentLine.Substring(0, capture.Index - 1 - offSet) + " ";
                    Size size = TextRenderer.MeasureText(text, TextLabel[i].Font, TextLabel[i].Size, new TextStyle(TextAnchor.UpperLeft));

                    if (L.Match(match.Value).Success)
                    {
                        NewLink(txt, action, TextLabel[i].Location.Add(new Point(size.Width - 5, 0)));
                    }

                    if (C.Match(match.Value).Success)
                    {
                        NewColour(txt, action, TextLabel[i].Location.Add(new Point(size.Width - 5, 0)));
                    }
                }

                TextLabel[i].Text = currentLine;
            }
            OkButton.BringToFront();
        }

        private void NewLink(string text, string link, Point p)
        {
            MirLabel temp = new MirLabel
            {
                AutoSize = true,
                Visible = true,
                Parent = this,
                Location = p,
                Text = text,
                ForeColour = CommonColor.Yellow,
                Sound = SoundList.ButtonC,
                Font = font
            };


            temp.MouseEnter += (o, e) => temp.ForeColour = CommonColor.Red;
            temp.MouseLeave += (o, e) => temp.ForeColour = CommonColor.Yellow;
            temp.MouseDown += (o, e) => temp.ForeColour = CommonColor.Yellow;
            temp.MouseUp += (o, e) => temp.ForeColour = CommonColor.Red;

            temp.Click += (o, e) =>
            {
                if (link.StartsWith("http://", true, CultureInfo.InvariantCulture))
                {
                    BrowserHelper.OpenDefaultBrowser(link);
                }
            };
            TextButtons.Add(temp);
        }

        private void NewColour(string text, string colour, Point p)
        {
            Color textColour = ColorTranslator.FromHtml(colour);

            MirLabel temp = new MirLabel
            {
                AutoSize = true,
                Visible = true,
                Parent = this,
                Location = p,
                Text = text,
                ForeColour = textColour,
                Font = font
            };
            TextButtons.Add(temp);
        }
    }
}
