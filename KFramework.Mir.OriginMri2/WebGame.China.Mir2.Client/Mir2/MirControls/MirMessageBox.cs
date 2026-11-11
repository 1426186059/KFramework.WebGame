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

        // AlertDialogSkin / Button23Skin（gameEui.json）：按钮 106x40，y=122。
        // JS showAlert()：单按钮 horizontalCenter=0；双按钮 agree +105 / cancel -105。
        private const int ButtonWidth = 106, ButtonHeight = 40, ButtonY = 122;
        private const int TwinOffset = 105, TripleOffset = 140;

        // 按钮贴图在 y_common-sheet：up=com_z_btn_1，down/hover=com_z_btn_1_1（均 106x40）。
        private const string ButtonAtlas = "y_common-sheet";
        private const string ButtonUpFrame = "com_z_btn_1";
        private const string ButtonDownFrame = "com_z_btn_1_1";

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
            // 模态：显示期间所有鼠标事件被本消息框接管，背景界面点不动（详见 MirControl.Modal 注释）。
            Modal = true;
            Movable = false;

            // 第一层：全屏半透明黑遮罩，压暗游戏画面并拦截穿透点击。
            AutoSize = false;
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            Location = new Point(0, 0);
            BackColour = CommonColor.ModalBackdrop;
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

            // 按钮按 AlertDialogSkin（gameEui.json）还原：Button23Skin + y_common-sheet 的两态贴图，
            // 文字叠在图上；位置与数量由 JS showAlert() 的规则决定。
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

            // 1 个居中；2 个左右 ±105（确定/是在右，取消/否在左，与 JS 一致）；3 个等距铺开。
            int[] offsets;
            if (captions.Length == 1) offsets = new[] { 0 };
            else if (captions.Length == 2) offsets = new[] { TwinOffset, -TwinOffset };
            else offsets = new[] { TripleOffset, 0, -TripleOffset };

            int centerX = PanelSize.Width / 2;
            MirButton[] made = new MirButton[captions.Length];
            for (int i = 0; i < captions.Length; i++)
                made[i] = MakeButton(captions[i], centerX + offsets[i] - ButtonWidth / 2, ButtonY);

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
            // Button23Skin：icon 用 com_z_btn_1，down 态切 com_z_btn_1_1；图片不含文字，
            // 文字由 MirButton 内建 label 居中叠加（对应 EXML 的 labelDisplay）。
            MirButton btn = new MirButton
            {
                Parent = _panel,
                AutoSize = false,
                Size = new Size(ButtonWidth, ButtonHeight),
                Location = new Point(x, y),
                AtlasNameKey = ButtonAtlas + "#" + ButtonUpFrame,
                HoverAtlasFrame = ButtonDownFrame,
                PressedAtlasFrame = ButtonDownFrame,
                BackColour = Color.FromArgb(0, 0, 0, 0),
                Border = false,
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
