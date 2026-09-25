using Client.MirGraphics;
using MirEngine;
using SlimDX;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public sealed class MirTextBox : MirControl
    {

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

        

        // 文字与光标一律由引擎自绘；浏览器 DOM <input> 仅作 IME / 键盘捕获（TextInputHtmlIme）。
        //   Rendered（默认）：DOM <input> 透明，仅作 IME / 键盘捕获代理，文字与光标由引擎在 canvas 自绘。
        //   Browser          ：由 DOM 直接显示文字与光标（浏览器原生光标 / 选区 / IME 候选窗），引擎画空串。
        // 默认 Rendered：与 JS 侧 input_overlay.js 的 _transparentInput 默认值及设计意图一致。
        // Browser 模式下若 DOM 覆盖层未正常弹出/聚焦，输入框会完全空白（无文字无光标）。
        // 转发到基础库静态开关：基础库内部据此自动切换 HTML（DOM 显示）与自绘两种光标实现。

        // 光标：闪烁由引擎 TextBoxRenderer.DrawTextBox 内部自驱（传入 focused），本控件聚焦时每帧使纹理失效以驱动重绘。

        public bool CanLoseFocus;
        public readonly TextBox TextBox;

        private void ApplyNativeTextBoxState()
        {
            if (TextBox == null || TextBox.IsDisposed) return;

            // 把 shim TextBox 摆到真实显示位置：引擎 Focus() 直接用其 Location/Size 定位原生 <input> 覆盖层
            // （本工程为恒等变换，逻辑坐标即后备缓冲像素；与 WinForms 把控件放在真实位置同理）。
            TextBox.Location = DisplayLocation;
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
                if (CMain.Instance.ActiveControl == TextBox)
                    CMain.Instance.Focus();
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
                BorderStyle = BorderStyle.None,
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

        // 原生输入覆盖层为纯 Pull 模型（见 KFramework.MonoGame.Input_IME）：
        // 引擎每帧经 Input.Poll → Input_IME.Poll 拉取文本到 Input_IME.Text，本类只在 DrawControl 中读取它。
        // 覆盖层的开 / 关、焦点互斥、回车转发全部由引擎 KFramework.MonoGame.TextBox 在 Focus()/Blur() 内负责
        // （与原版 WinForms 一致：上层只管设置 Location/Size/Font/Visible 并调用 Focus()）。

        private void TextBox_NeedRedraw(object sender, EventArgs e)
        {
            TextureValid = false;
            Redraw();
        }

        // 每帧由基类 Draw() 调用；在此推进光标闪烁（与 Web_Mir3 DXTextBox 一致：
        // 聚焦时到点翻转并置 TextureValid=false 触发重绘；失焦时关掉光标）。
        protected internal override void DrawControl()
        {
            // 引擎 TextBox 持有 text / 光标 / 选区 / IME 预览，是文本与光标的唯一真相源（对齐 UGUI InputField）。
            // DOM 仅转发控制键 / 回传原生编辑结果，引擎不再从 DOM 读取文本或光标。
            // 聚焦时每帧令纹理失效，使引擎 TextBoxRenderer 每帧执行并推进光标闪烁节拍。
            if (TextBox != null && TextBox.Focused && KFramework.MonoGame.Input_IME.Active)
                TextureValid = false;

            base.DrawControl();
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

            Font font = TextBox.Font ?? new Font("Arial", 10f);
            int fore = (TextBox.ForeColor != Color.Empty ? TextBox.ForeColor : Color.White).ToArgb();
            // 文本框纹理作为面板上的透明叠层：无背景色时清成透明（alpha 0），避免盖住面板里的输入框底。
            int back = (TextBox.BackColor != Color.Empty && TextBox.BackColor.A > 0) ? TextBox.BackColor.ToArgb() : 0;
            int selBack = Color.FromArgb(128, 51, 153, 255).ToArgb();
            // 文字与光标一律由引擎在 canvas 自绘；DOM 覆盖层仅作 IME / 键盘捕获代理（透明、pointer-events:none）。
            string drawText = TextBox.Text ?? "";
            if (TextBox.UseSystemPasswordChar) drawText = new string('●', drawText.Length);
            string drawComp = TextBox.CompositionString ?? "";
            if (TextBox.UseSystemPasswordChar) drawComp = new string('●', drawComp.Length);
            TextRenderer.Clear(ControlTexture, back);
            
            {
                // 渲染目标与批次由本控件绑定，文本 + 光标交给基础库 TextBoxRenderer。
                var saved = DXManager.CurrentSurface;
                DXManager.SetSurface(new SlimDX.Direct3D9.Surface(ControlTexture));
                try
                {
                    var batch = DXManager.Batch;
                    batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                                KFramework.MonoGame.BlendState.NonPremultiplied,
                                KFramework.MonoGame.SamplerState.PointClamp);
                    KFramework.MonoGame.TextBoxRenderer.DrawTextBox(
                        batch, DXManager.GDevice, font, drawText,
                        new KFramework.MonoGame.Rectangle(0, 0, Size.Width, Size.Height),
                        KFramework.MonoGame.Color.FromArgb((uint)fore),
                        TextBox.SelectionStart, !TextBox.Multiline, TextBox.Focused,
                        KFramework.MonoGame.TextBoxRenderer.DefaultPadLeft, drawComp,
                        TextBox.SelectionStart, TextBox.SelectionLength);
                    batch.End();
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

            if (e.KeyChar == (char)Keys.Escape)
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
            {
                TextBox.VisibleChanged += SetFocus;
                return;
            }
            if (TextBox.Parent == null)
            {
                TextBox.ParentChanged += SetFocus;
                return;
            }

            // 焦点互斥、GotFocus/LostFocus 事件分发、以及原生 <input> 覆盖层的开 / 关与回车转发，
            // 全部由引擎 KFramework.MonoGame.TextBox.Focus()/Blur() 负责（与原版 WinForms 一致）。
            // 本方法只负责请求聚焦。
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
