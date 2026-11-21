using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 公告页面（游戏内公告面板）。
    /// 对应《原始传奇》页游：福利面板 FuliPanel 的“公告”子页 FuliPopAnnouncement（皮肤 FuliPopAnnouncementSkin），
    /// 外层弹窗为 PopCommonSkin（背景 common_bg.png + 标题栏 + 关闭按钮）。
    /// 数据来源：服务端 S.UpdateNotice → GameScene.ShowNotice → NoticeDialog.Update(p.Notice)，
    /// 而不是像原版那样从 lang 表 Lang.getId(1000) 取静态 HTML —— 数据源保持服务端下发，仅 UI 外观对齐原版。
    /// 富文本：Notice.Message 中 {文字/#RRGGBB} 着色、(文字/http链接) 超链接，解析逻辑沿用经典 Mir2。
    /// </summary>
    public sealed class NoticeDialog : MirImageControl
    {
        public static Regex C = new Regex(@"{((.*?)\/(.*?))}");
        public static Regex L = new Regex(@"\(((.*?)\/(.*?))\)");

        public MirButton CloseButton, UpButton, DownButton, PositionBar, OkButton;
        public MirLabel[] TextLabel;
        public List<MirLabel> TextButtons;
        public MirImageControl TitleLabel;
        public MirLabel NameLabel;

        public Notice Notice = new Notice();

        // —— 原始传奇公告页皮肤几何（PopCommonSkin 818×536 + FuliPopAnnouncementSkin 606×446）——
        // 背景走 manifest 哈希路径，避免 404；按钮帧用 gameui4 现成的 com_btn_close0 替代线上 com_btn_close。
        private const string NoticeSheet = "y_common-sheet";
        private const string NoticeBgUrl = "resource/assets/gameui4/jpg/common_bg.png";
        private const int PanelW = 818, PanelH = 536;
        private const int TextX = 40, TextY = 70;       // 正文区左上（对齐原版 group_page x=29,y=52 + scroller y=22）
        private const int TextW = 600;                  // 原版 scroller/viewport 宽 570，这里略放宽
        private const int LineH = 22;                   // 原版 style46 行距 12 + 字高，约 22
        private const int ScrollX = 786;                // 右侧滚动条（原版 barGro x=588 相对内容页 606 宽 → 靠右）
        private const int ScrollTop = 70, ScrollBottom = 452;
        private const int BarMinY = 96, BarMaxY = 430;
        private const int BarTravel = BarMaxY - BarMinY;

        Font font = new Font(Settings.FontName, 11F);

        public List<string> CurrentLines = new List<string>();
        private int _index = 0;
        public int MaximumLines = 17;                   // (ScrollBottom-ScrollTop)/LineH ≈ 17 行

        public NoticeDialog()
        {
            _ = SetManifestImageAsync(NoticeBgUrl);    // 背景走 manifest 解析（CDN 哈希布局），避免 404
            Size = new Size(PanelW, PanelH);
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 3);
            TextLabel = new MirLabel[64];
            TextButtons = new List<MirLabel>();

            MouseWheel += LoginNoticeDialog_MouseWheel;

            Sort = true;

            // 标题栏（原版 PopCommonSkin.titleGro/txt_title：x=14,y=13，style233 居中标题）
            NameLabel = new MirLabel
            {
                Text = "",
                Parent = this,
                Font = new Font(Settings.FontName, 12F, KFramework.MonoGame.FontStyle.Bold),
                ForeColour = Color.BurlyWood,
                Location = new Point(34, 20),
                AutoSize = true
            };

            // 关闭按钮（原版 PopCommonSkin.btn_close x=768,y=2；本工程用 com_btn_close0 替代，放右上角内角并置顶）
            CloseButton = new MirButton
            {
                AtlasNameKey = NoticeSheet + "#com_btn_close0",
                Parent = this,
                Location = new Point(PanelW - 30, 6),
                Size = new Size(26, 26),
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            // 原始传奇公告页无确定按钮（仅右上关闭），保留 OkButton 字段但不显示，避免破坏外部引用。
            OkButton = new MirButton
            {
                Enabled = false,
                Visible = false,
                Parent = this,
            };
            OkButton.Click += (o, e) => Hide();

            UpButton = new MirButton
            {
                Index = 470,
                HoverIndex = 471,
                PressedIndex = 472,
                Library = Libraries.Prguse2,
                Parent = this,
                Size = new Size(16, 14),
                Location = new Point(ScrollX, ScrollTop - 16),
                Sound = SoundList.ButtonA,
                Visible = false
            };
            UpButton.Click += (o, e) =>
            {
                if (_index <= 0) return;

                _index--;

                NewText(CurrentLines, false);
                UpdatePositionBar();
            };

            DownButton = new MirButton
            {
                Index = 473,
                HoverIndex = 474,
                Library = Libraries.Prguse2,
                PressedIndex = 475,
                Parent = this,
                Size = new Size(16, 14),
                Location = new Point(ScrollX, ScrollBottom),
                Sound = SoundList.ButtonA,
                Visible = true
            };
            DownButton.Click += (o, e) =>
            {
                if (_index + MaximumLines >= CurrentLines.Count) return;

                _index++;

                NewText(CurrentLines, false);
                UpdatePositionBar();
            };

            PositionBar = new MirButton
            {
                Index = 205,
                HoverIndex = 206,
                PressedIndex = 206,
                Library = Libraries.Prguse2,
                Location = new Point(ScrollX, BarMinY),
                Parent = this,
                Movable = true,
                Sound = SoundList.None,
                Visible = true
            };
            PositionBar.OnMoving += PositionBar_OnMoving;
        }

        private void PositionBar_OnMoving(object sender, MouseEventArgs e)
        {
            int x = ScrollX;
            int y = PositionBar.Location.Y;

            if (y >= BarMaxY) y = BarMaxY;
            if (y <= BarMinY) y = BarMinY;

            int location = y - BarMinY;
            int interval = Math.Max(1, BarTravel / (CurrentLines.Count - MaximumLines));

            double yPoint = location / interval;

            _index = Convert.ToInt16(Math.Floor(yPoint));

            NewText(CurrentLines, false);

            PositionBar.Location = new Point(x, y);
        }

        private void LoginNoticeDialog_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (_index == 0 && count >= 0) return;
            if (_index == CurrentLines.Count - 1 && count <= 0) return;
            if (CurrentLines.Count <= MaximumLines) return;

            _index -= count;

            if (_index < 0) _index = 0;
            if (_index + MaximumLines > CurrentLines.Count - 1) _index = CurrentLines.Count - MaximumLines;

            NewText(CurrentLines, false);

            UpdatePositionBar();
        }

        private void UpdatePositionBar()
        {
            if (CurrentLines.Count <= MaximumLines) return;

            int interval = Math.Max(1, BarTravel / (CurrentLines.Count - MaximumLines));

            int x = ScrollX;
            int y = BarMinY + (_index * interval);

            if (y >= BarMaxY) y = BarMaxY;
            if (y <= BarMinY) y = BarMinY;

            PositionBar.Location = new Point(x, y);
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
                temp.Add(line);
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
                UpdatePositionBar();
            }

            if (resetIndex)
            {
                if (Index != -1)
                {
                    MaximumLines = 17;
                    if (lines.Count > MaximumLines)
                    {
                        UpButton.Visible = true;
                        DownButton.Visible = true;
                        PositionBar.Visible = true;
                    }
                    else
                    {
                        UpButton.Visible = false;
                        DownButton.Visible = false;
                        PositionBar.Visible = false;
                    }
                }
                else
                {
                    MaximumLines = 17;
                    UpButton.Visible = false;
                    DownButton.Visible = false;
                    PositionBar.Visible = false;
                }
            }

            for (int i = 0; i < TextButtons.Count; i++)
                TextButtons[i].Dispose();

            for (int i = 0; i < TextLabel.Length; i++)
            {
                if (TextLabel[i] != null) TextLabel[i].Text = "";
            }

            TextButtons.Clear();

            if (!string.IsNullOrEmpty(Notice.Title))
            {
                NameLabel.Text = Notice.Title;
            }

            int lastLine = lines.Count > MaximumLines ? ((MaximumLines + _index) > lines.Count ? lines.Count : (MaximumLines + _index)) : lines.Count;

            for (int i = _index; i < lastLine; i++)
            {
                TextLabel[i] = new MirLabel
                {
                    Font = font,
                    DrawFormat = new TextStyle(TextAnchor.UpperLeft, HorizontalWrapMode.Wrap),
                    Visible = true,
                    Parent = this,
                    Size = new Size(TextW, LineH),
                    Location = new Point(TextX, TextY + (i - _index) * LineH),
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
                TextLabel[i].MouseWheel += LoginNoticeDialog_MouseWheel;

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
                ForeColour = Color.Yellow,
                Sound = SoundList.ButtonC,
                Font = font
            };


            temp.MouseEnter += (o, e) => temp.ForeColour = Color.Red;
            temp.MouseLeave += (o, e) => temp.ForeColour = Color.Yellow;
            temp.MouseDown += (o, e) => temp.ForeColour = Color.Yellow;
            temp.MouseUp += (o, e) => temp.ForeColour = Color.Red;

            temp.Click += (o, e) =>
            {
                if (link.StartsWith("http://", true, CultureInfo.InvariantCulture))
                {
                    BrowserHelper.OpenDefaultBrowser(link);
                }
            };
            temp.MouseWheel += LoginNoticeDialog_MouseWheel;

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
            temp.MouseWheel += LoginNoticeDialog_MouseWheel;

            TextButtons.Add(temp);
        }
    }
}
