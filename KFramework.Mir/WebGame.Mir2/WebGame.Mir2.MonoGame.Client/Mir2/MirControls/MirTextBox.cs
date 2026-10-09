using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public sealed class MirTextBox : MirControl
    {
        // 每个文本框持有自己独立的 TextBox 实例：整合后的光标状态（TextBox.Caret 部分）即“每框一支光标”，互不串闪烁相位 / 可见性。
        private readonly KFramework.MonoGame.TextBox _caretBox = new KFramework.MonoGame.TextBox();

        protected override void OnBackColourChanged()
        {
            base.OnBackColourChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.BackColor = BackColour;
        }



        protected override void OnEnabledChanged()
        {
            base.OnEnabledChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.Enabled = Enabled;
        }



        protected override void OnForeColourChanged()
        {
            base.OnForeColourChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.ForeColor = ForeColour;
        }



        protected override void OnLocationChanged()
        {
            base.OnLocationChanged();
            ApplyNativeTextBoxState();
            TextureValid = false;
            Redraw();
        }



        public int MaxLength
        {
            get
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    return TextBox.MaxLength;
                return -1;
            }
            set
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    TextBox.MaxLength = value;
            }
        }



        protected override void OnParentChanged()
        {
            base.OnParentChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                ApplyNativeTextBoxState();
        }



        public bool Password
        {
            get
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    return TextBox.UseSystemPasswordChar;
                return false;
            }
            set
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    TextBox.UseSystemPasswordChar = value;
            }
        }



        public Font Font
        {
            get
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    return TextBox.Font;
                return null;
            }
            set
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    TextBox.Font = ScaleFont(value);
            }
        }



        protected override void OnSizeChanged()
        {
            TextBox.Size = Size;

            DisposeTexture();

            _size = Size;

            if (TextBox != null && !TextBox.IsDisposed)
                base.OnSizeChanged();
        }

        public bool CanLoseFocus;
        public readonly TextBox TextBox;

        private static Point HiddenTextBoxLocation
        {
            get { return new Point(-32000, -32000); }
        }

        private void ApplyNativeTextBoxState()
        {
            if (TextBox == null || TextBox.IsDisposed) return;
            TextBox.Location = HiddenTextBoxLocation;
            TextBox.Visible = Visible && TextBox.Parent != null;
        }


        public string Text
        {
            get
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    return TextBox.Text;
                return null;
            }
            set
            {
                if (TextBox != null && !TextBox.IsDisposed)
                {
                    TextBox.Text = value;
                    TextBox_NeedRedraw(this, EventArgs.Empty);
                }
            }
        }
        public string[] MultiText
        {
            get
            {
                if (TextBox != null && !TextBox.IsDisposed)
                    return TextBox.Lines;
                return null;
            }
            set
            {
                if (TextBox != null && !TextBox.IsDisposed)
                {
                    TextBox.Lines = value;
                    TextBox_NeedRedraw(this, EventArgs.Empty);
                }
            }
        }



        public override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                base.Visible = value;
                OnVisibleChanged();
            }
        }

        protected override void OnVisibleChanged()
        {
            base.OnVisibleChanged();

            // 登录框等"创建即可见"的文本框，Parent 由 MirTextBox_Shown 在首帧 Draw 时挂上；
            // 但聊天框这类初始 Visible=false 的框从未被绘制，Shown 不会触发，Parent 一直是 null，
            // 于是 ApplyNativeTextBoxState 里 TextBox.Visible = Visible && Parent != null 恒为 false，
            // SetFocus() 转入的延迟链（等待 VisibleChanged）永远等不到，表现为"按 Enter 唤起了却无法输入"。
            // 故在此补齐 Parent（与 MirTextBox_Shown 一致），使隐藏后再显示也能立即进入可聚焦状态。
            if (Visible && TextBox != null && !TextBox.IsDisposed && TextBox.Parent == null && CMain.Instance != null)
                TextBox.Parent = CMain.Instance;

            ApplyNativeTextBoxState();

            // 文本框隐藏（如所在对话框关闭）时，收回原生输入覆盖层与焦点（引擎 Blur 收起 IME）。
            if (!Visible)
                TextBox.LoseFocus();
        }
        private void TextBox_VisibleChanged(object sender, EventArgs e)
        {
            DialogChanged();

            if (TextBox.Visible && TextBox.CanFocus)
                if (CMain.Instance.ActiveControl == null || CMain.Instance.ActiveControl == CMain.Instance)
                    CMain.Instance.ActiveControl = TextBox;

            if (!TextBox.Visible)
            {
                // 关闭（隐藏）时把激活框交还窗体：无论 ActiveControl 之前指向什么（本框/其它文本框/其它控件），
                // 只要不是窗体自身（CMain.Instance）就清回窗体。否则引擎 ActiveTextBoxResolver 会持续把 Enter
                // 转发给残留的隐藏框，导致“第三次按 Enter 唤不醒聊天框”。改用类型无关判法，避免漏掉同名非 MG.TextBox。
                var ac = CMain.Instance.ActiveControl;
                if (ac != null && ac != CMain.Instance)
                    CMain.Instance.ActiveControl = CMain.Instance;
            }
        }
        private void SetFocus(object sender, EventArgs e)
        {
            if (TextBox.Visible)
                TextBox.VisibleChanged -= SetFocus;
            if (TextBox.Parent != null)
                TextBox.ParentChanged -= SetFocus;

            if (TextBox.CanFocus) TextBox.Focus();
            else if (TextBox.Visible && TextBox.Parent != null)
                CMain.Instance.ActiveControl = TextBox;


        }



        public override void MultiLine()
        {
            TextBox.Multiline = true;
            TextBox.Size = Size;

            DisposeTexture();
            Redraw();
        }


        public MirTextBox()
        {
            BackColour = Color.Black;

            DrawControlTexture = true;
            TextureValid = false;

            TextBox = new TextBox
            {
                BackColor = BackColour,
                Font = new Font(Settings.FontName, 10F * 96f / FontDpiX),
                ForeColor = ForeColour,
                Location = DisplayLocation,
                Size = Size,
                Visible = Visible,
                Tag = this,
            };

            TextBox.VisibleChanged += TextBox_VisibleChanged;
            TextBox.ParentChanged += TextBox_VisibleChanged;
            TextBox.KeyPress += TextBox_KeyPress;
            TextBox.KeyUp += TextBoxOnKeyUp;
            TextBox.KeyPress += TextBox_NeedRedraw;
            TextBox.KeyUp += TextBox_NeedRedraw;
            TextBox.TextChanged += TextBox_NeedRedraw;
            TextBox.MouseDown += TextBox_NeedRedraw;
            TextBox.MouseUp += TextBox_NeedRedraw;
            TextBox.LostFocus += TextBox_NeedRedraw;
            TextBox.GotFocus += TextBox_NeedRedraw;
            TextBox.MouseWheel += TextBox_NeedRedraw;

            Shown += MirTextBox_Shown;
            TextBox.MouseMove += CMain.CMain_MouseMove;
        }
        
        private void TextBox_NeedRedraw(object sender, EventArgs e)
        {
            TextureValid = false;
            Redraw();
        }
        
        protected internal override void DrawControl()
        {
            base.DrawControl();
            if (TextBox.IsFocused)
            {
                TextureValid = false;
                Redraw();
            }
        }

        protected override void CreateTexture()
        {
            if (Size.IsEmpty)
                return;

            if (TextureSize != Size)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);

                ControlTexture = DXManager.CreateRenderTarget(Size.Width, Size.Height);
                TextureSize = Size;
            }

            Font font = TextBox.Font ?? new Font(Settings.FontName, Settings.FontSize);
            int fore = (TextBox.ForeColor != Color.Empty ? TextBox.ForeColor : Color.White).ToArgb();
            int back = (TextBox.BackColor != Color.Empty && TextBox.BackColor.A > 0) ? TextBox.BackColor.ToArgb() : 0;
            int selBack = Color.FromArgb(128, 51, 153, 255).ToArgb();
            string drawText = TextBox.Text ?? "";
            if (TextBox.UseSystemPasswordChar) drawText = new string('●', drawText.Length);
            string drawComp = TextBox.CompositionString ?? "";
            if (TextBox.UseSystemPasswordChar) drawComp = new string('●', drawComp.Length);
            TextRenderer.Clear(ControlTexture, back);
            
            {
                // 渲染目标与批次由本控件绑定，文本 + 光标交给基础库 TextBox（DrawTextBox，实例自身持有光标状态）。
                var saved = DXManager.CurrentSurface;
                DXManager.SetSurface(new SlimDX.Direct3D9.Surface(ControlTexture));
                try
                {
                    // 用主批（SpriteBatch）绘制：以新式材质 API 开批（NonPremultiplied + 纹理 PointClamp），
                    // 文本 + 光标交给基础库 TextBox（DrawTextBox，实例自身持有光标状态）。
                    DXManager.BeginBatch(DXManager.GetMaterial(KFramework.MonoGame.BlendState.NonPremultiplied));
                    _caretBox.DrawTextBox(
                        DXManager.Batch, DXManager.GDevice, font, drawText,
                        new KFramework.MonoGame.Rectangle(0, 0, Size.Width, Size.Height),
                        KFramework.MonoGame.Color.FromArgb((uint)fore),
                        TextBox.SelectionStart, !TextBox.Multiline, TextBox.Focused,
                        KFramework.MonoGame.TextBox.DefaultPadLeft, drawComp,
                        TextBox.SelectionStart, TextBox.SelectionLength);
                    DXManager.EndBatch();
                }
                finally
                {
                    DXManager.SetSurface(saved);
                }
            }

            TextureValid = true;
        }

        public override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || TextBox == null || TextBox.IsDisposed || !TextBox.Visible) return;

            if (e.Button == MouseButtons.Left)
            {
                Point localPoint = new Point(e.X - DisplayLocation.X, e.Y - DisplayLocation.Y);
                int charIndex = TextBox.GetCharIndexFromPosition(localPoint);
                TextBox.SelectionStart = Math.Max(0, Math.Min(charIndex, TextBox.TextLength));
                TextBox.SelectionLength = 0;
            }

            SetFocus();

            if (TextBox.CanFocus)
                CMain.Instance.ActiveControl = TextBox;
        }

        void TextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (e.KeyCode == Keys.Escape)
            {
                CMain.Instance.ActiveControl = null;
                e.Handled = true;
            }
        }


        private void TextBoxOnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.PrintScreen:
                    CMain.CMain_KeyUp(sender, e);
                    break;

            }
        }

        void MirTextBox_Shown(object sender, EventArgs e)
        {
            TextBox.Parent = CMain.Instance;
            ApplyNativeTextBoxState();
            CMain.Ctrl = false;
            CMain.Shift = false;
            CMain.Alt = false;
            CMain.Tilde = false;

            TextureValid = false;
            SetFocus();
        }

        public void SetFocus()
        {
            if (!TextBox.Visible)
                TextBox.VisibleChanged += SetFocus;
            else if (TextBox.Parent == null)
                TextBox.ParentChanged += SetFocus;
            else
                TextBox.Focus();
        }

        public void DialogChanged()
        {
            MirMessageBox box1 = null;
            MirInputBox box2 = null;
            MirAmountBox box3 = null;

            if (MirScene.ActiveScene != null && MirScene.ActiveScene.Controls.Count > 0)
            {
                box1 = (MirMessageBox) MirScene.ActiveScene.Controls.FirstOrDefault(ob => ob is MirMessageBox);
                box2 = (MirInputBox) MirScene.ActiveScene.Controls.FirstOrDefault(O => O is MirInputBox);
                box3 = (MirAmountBox) MirScene.ActiveScene.Controls.FirstOrDefault(ob => ob is MirAmountBox);
            }


            if ((box1 != null && box1 != Parent) || (box2 != null && box2 != Parent)  || (box3 != null && box3 != Parent))
                TextBox.Visible = false;
            else
                ApplyNativeTextBoxState();
        }



        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            // 若本框正接管输入，失焦以收起 IME（覆盖其他清理）。
            if (!TextBox.IsDisposed)
                TextBox.LoseFocus();
            if (!TextBox.IsDisposed)
                TextBox.Dispose();
        }


    }
}
