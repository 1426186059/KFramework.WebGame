using Client.MirGraphics;
using KFramework.MonoGameExtend;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public sealed class MirInputBox : MirImageControl
    {
        // 背景：y_common-sheet 的 com_txt_bgi1（558x35 灰色渐变横条）。
        // 九宫格：左右各留 40px 保留两端渐隐，中间横向平铺、纵向拉伸到弹窗尺寸。
        private const string PanelAtlas = "y_common-sheet";
        private const string PanelFrame = "com_txt_bgi1";

        // 按钮：y_common-sheet 的 com_z_btn_1（正常）/ com_z_btn_1_1（按下），真实帧 106x40。
        private const string ButtonAtlas = "y_common-sheet";
        private const string ButtonUpFrame = "com_z_btn_1";
        private const string ButtonDownFrame = "com_z_btn_1_1";
        private const int ButtonWidth = 106, ButtonHeight = 40;

        public readonly MirLabel CaptionLabel;
        public readonly MirButton OKButton, CancelButton;
        public readonly MirTextBox InputTextBox;


        public MirInputBox(string message)
        {
            // 模态：显示期间所有鼠标事件被本输入框接管，背景界面点不动（详见 MirControl.Modal 注释）。
            Modal = true;
            Movable = false;

            AutoSize = false;
            Size = new Size(3000, 3000);
            Anchor = EAnchorType.MiddleCenter;
            AnchorPos = new Point(0, 0);
            ApplyAnchor();

            // 大背景：全屏半透明黑遮罩，颜色统一用 2026New/CommonColor，与 MirMessageBox 一致。
            BackColour = CommonColor.ModalBackdrop;
            DrawControlTexture = true;
            DrawImage = false;
            Border = false;

            var mBg = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/y_alert_bg.png",
                Location = new Point(0, 0),
                Size = new Size(450, 179),
            };
            mBg.Location = mBg.Center;

            CaptionLabel = new MirLabel
            {
                Parent = mBg,
                Alignment = KFramework.MonoGame.TextAnchor.MiddleCenter,
                Location = new Point(0, 20),
                Size = new Size(450, 30),
                Text = message,
            };

            InputTextBox = new MirTextBox
            {
                Parent = mBg,
                Border = true,
                BorderColour = Color.Lime,
                Size = new Size(300, 30),
                MaxLength = 50,
            };
            InputTextBox.Location = InputTextBox.Center;
            InputTextBox.SetFocus();
            InputTextBox.TextBox.KeyPress += MirInputBox_KeyPress;

            OKButton = new MirButton
            {
                Parent = mBg,
                AtlasNameKey = ButtonAtlas + "#" + ButtonUpFrame,
                PressedAtlasFrame = ButtonDownFrame,
                Text = "确定",
                Size = new Size(106, 40)
            };
            OKButton.CenterText = true;
            OKButton.FontColour = Color.White;
            OKButton.Location = new Point(OKButton.Center.X - 100, 120);

            CancelButton = new MirButton
            {
                Parent = mBg,
                Location = new Point(200, 150),
                AtlasNameKey = ButtonAtlas + "#" + ButtonUpFrame,
                PressedAtlasFrame = ButtonDownFrame,
                Text = "取消",
                Size = new Size(106, 40)
            };
            CancelButton.CenterText = true;
            CancelButton.FontColour = Color.White;
            CancelButton.Click += DisposeDialog;
            CancelButton.Location = new Point(CancelButton.Center.X + 100, 120);
        }

        void MirInputBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (OKButton != null && !OKButton.IsDisposed)
                    OKButton.InvokeMouseClick(EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (CancelButton != null && !CancelButton.IsDisposed)
                    CancelButton.InvokeMouseClick(EventArgs.Empty);
                e.Handled = true;
            }
        }
        void DisposeDialog(object sender, EventArgs e)
        {
            Dispose();
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            e.Handled = true;
        }
        public override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            e.Handled = true;
        }
        public override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (e.KeyCode == Keys.Escape)
            {
                if (CancelButton != null && !CancelButton.IsDisposed)
                    CancelButton.InvokeMouseClick(EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (OKButton != null && !OKButton.IsDisposed)
                    OKButton.InvokeMouseClick(EventArgs.Empty);

            }
            e.Handled = true;
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



        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            for (int i = 0; i < CMain.Instance.Controls.Count; i++)
            {
                TextBox T = (TextBox)CMain.Instance.Controls[i];
                if (T != null && T.Tag != null && T.Tag != null)
                    ((MirTextBox)T.Tag).DialogChanged();
            }
        }


    }
}
