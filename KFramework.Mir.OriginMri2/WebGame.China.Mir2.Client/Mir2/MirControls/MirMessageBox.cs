using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public enum MirMessageBoxButtons { OK, OKCancel, YesNo, YesNoCancel, Cancel }

    public sealed class MirMessageBox : MirImageControl
    {
        // 消息框背景：manifest key = y_alert_bg.png（450x179 深色描边面板）。
        // 走 manifest：ImageKey -> SetManifestImageAsync -> Mir2AtlasManifest.ResolvePath，
        // CDN 上自动解析成 resource/87/87240d4f_66458.png（v=87240d4f, s=66458）；
        // 切本地镜像时按 key 原路径取，不写死 CDN 哈希路径，两种取数来源才能统一切换。
        private const string PanelImageKey = "resource/assets/gameui4/jpg/y_alert_bg.png";
        private static readonly Size PanelSize = new Size(450, 179);
        private static readonly Color BackdropColour = Color.FromArgb(160, 0, 0, 0);

        private const int ButtonWidth = 88, ButtonHeight = 34, ButtonGap = 22;

        public MirLabel Label;
        public MirButton OKButton, CancelButton, NoButton, YesButton;
        public MirMessageBoxButtons Buttons;
        public bool AllowKeyPress = true;

        // 两层结构：this = 全屏半透明黑遮罩；_panel = 遮罩上方居中的 PNG 背景，文字/按钮都挂在 _panel 上。
        private readonly MirImageControl _panel;

        public MirMessageBox(string message, MirMessageBoxButtons b = MirMessageBoxButtons.OK, bool allowKeys = true)
        {
            Buttons = b;
            AllowKeyPress = allowKeys;
            Modal = true;
            Movable = false;

            // 第一层：全屏半透明黑遮罩，压暗游戏画面并拦截穿透点击。
            AutoSize = false;
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            Location = new Point(0, 0);
            BackColour = BackdropColour;
            DrawControlTexture = true;
            Border = false;
            ForeColour = Color.White;
            DrawImage = false;   // 遮罩只画纯色，不走 Texture / 图集 / Library 分支

            // 第二层：居中显示的 PNG 背景（Anchor 保持 None，异步加载完成后的 ApplyAnchor 不会挪动它）。
            _panel = new MirImageControl
            {
                Parent = this,
                AutoSize = false,
                Size = PanelSize,
                Location = new Point((Settings.ScreenWidth - PanelSize.Width) / 2,
                                     (Settings.ScreenHeight - PanelSize.Height) / 2),
                ImageKey = PanelImageKey,
                BackColour = Color.FromArgb(0, 0, 0, 0),
                Border = false,
            };

            Label = new MirLabel
            {
                AutoSize = false,
                Location = new Point(24, 26),
                Size = new Size(PanelSize.Width - 48, 78),
                Parent = _panel,
                Text = message,
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };

            // 按钮统一居中排在面板底部。原 Libraries.Title 图集在浏览器端不可用（会画成灰块），
            // 这里改成带中文文字的描边按钮，后续要换 FGUI 贴图只改 MakeButton 一处。
            string[] captions;
            switch (Buttons)
            {
                case MirMessageBoxButtons.OK: captions = new[] { "确定" }; break;
                case MirMessageBoxButtons.OKCancel: captions = new[] { "确定", "取消" }; break;
                case MirMessageBoxButtons.YesNo: captions = new[] { "是", "否" }; break;
                case MirMessageBoxButtons.YesNoCancel: captions = new[] { "是", "否", "取消" }; break;
                case MirMessageBoxButtons.Cancel: captions = new[] { "取消" }; break;
                default: captions = new[] { "确定" }; break;
            }

            int totalW = captions.Length * ButtonWidth + (captions.Length - 1) * ButtonGap;
            int x0 = (PanelSize.Width - totalW) / 2;
            int y = PanelSize.Height - ButtonHeight - 20;
            MirButton[] made = new MirButton[captions.Length];
            for (int i = 0; i < captions.Length; i++)
                made[i] = MakeButton(captions[i], x0 + i * (ButtonWidth + ButtonGap), y);

            switch (Buttons)
            {
                case MirMessageBoxButtons.OK:
                    OKButton = made[0]; break;
                case MirMessageBoxButtons.OKCancel:
                    OKButton = made[0]; CancelButton = made[1]; break;
                case MirMessageBoxButtons.YesNo:
                    YesButton = made[0]; NoButton = made[1]; break;
                case MirMessageBoxButtons.YesNoCancel:
                    YesButton = made[0]; NoButton = made[1]; CancelButton = made[2]; break;
                case MirMessageBoxButtons.Cancel:
                    CancelButton = made[0]; break;
            }

            foreach (MirButton btn in made)
                btn.Click += (o, e) => Dispose();
        }

        private MirButton MakeButton(string caption, int x, int y)
        {
            MirButton btn = new MirButton
            {
                Parent = _panel,
                AutoSize = false,
                Size = new Size(ButtonWidth, ButtonHeight),
                Location = new Point(x, y),
                BackColour = Color.FromArgb(210, 42, 34, 28),
                Border = true,
                BorderColour = Color.FromArgb(210, 176, 142, 86),
                Text = caption,
            };
            btn.CenterText = true;
            btn.FontColour = Color.White;
            return btn;
        }

        public override void Show()
        {
            if (Parent != null) return;

            Parent = MirScene.ActiveScene;

            Highlight();

            for (int i = 0; i < CMain.Instance.Controls.Count; i++)
            {
                MirTextBox T = CMain.Instance.Controls[i] as MirTextBox;
                if (T != null)
                    T.DialogChanged();
            }
        }


        public override void OnKeyDown(KeyEventArgs e)
        {
            if (AllowKeyPress)
            {
                base.OnKeyDown(e);
                e.Handled = true;
            }
        }
        public override void OnKeyUp(KeyEventArgs e)
        {
            if (AllowKeyPress)
            {
                base.OnKeyUp(e);
                e.Handled = true;
            }
        }
        public override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (AllowKeyPress)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    switch (Buttons)
                    {
                        case MirMessageBoxButtons.OK:
                            if (OKButton != null && !OKButton.IsDisposed) OKButton.InvokeMouseClick(null);
                            break;
                        case MirMessageBoxButtons.OKCancel:
                        case MirMessageBoxButtons.YesNoCancel:
                            if (CancelButton != null && !CancelButton.IsDisposed) CancelButton.InvokeMouseClick(null);
                            break;
                        case MirMessageBoxButtons.YesNo:
                            if (NoButton != null && !NoButton.IsDisposed) NoButton.InvokeMouseClick(null);
                            break;
                    }
                }

                else if (e.KeyCode == Keys.Enter)
                {
                    switch (Buttons)
                    {
                        case MirMessageBoxButtons.OK:
                        case MirMessageBoxButtons.OKCancel:
                            if (OKButton != null && !OKButton.IsDisposed) OKButton.InvokeMouseClick(null);
                            break;
                        case MirMessageBoxButtons.YesNoCancel:
                        case MirMessageBoxButtons.YesNo:
                            if (YesButton != null && !YesButton.IsDisposed) YesButton.InvokeMouseClick(null);
                            break;

                    }
                }
                e.Handled = true;
            }
        }


        public static void Show(string message, bool close = false)
        {
            MirMessageBox box = new MirMessageBox(message);

            if (close) box.OKButton.Click += (o, e) => CMain.Instance.Close();

            box.Show();
        }


        protected override void Dispose(bool disposing)
        {

            base.Dispose(disposing);

            if (!disposing) return;

            Label = null;
            OKButton = null;
            CancelButton = null;
            NoButton = null;
            YesButton = null;
            Buttons = 0;

            for (int i = 0; i < CMain.Instance.Controls.Count; i++)
            {
                TextBox T = (TextBox) CMain.Instance.Controls[i];
                if (T != null && T.Tag != null)
                    ((MirTextBox) T.Tag).DialogChanged();
            }
        }

    }
}
