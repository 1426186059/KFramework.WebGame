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

        

        // 当前由浏览器原生 <input> 覆盖层接管的文本框（全局唯一，输入焦点互斥）。
        private static MirTextBox _current;
        // 原生输入覆盖层是否正接管本框（仅作 IME/键盘捕获代理，DOM 透明，不再影响自身绘制）。
        private bool _nativeActive;

        // 文字与光标一律由引擎自绘；浏览器 DOM <input> 仅作 IME / 键盘捕获（TextInputHtmlIme）。
        //   Rendered（默认）：DOM <input> 透明，仅作 IME / 键盘捕获代理，文字与光标由引擎在 canvas 自绘。
        //   Browser          ：由 DOM 直接显示文字与光标（浏览器原生光标 / 选区 / IME 候选窗），引擎画空串。
        // 默认 Rendered：与 JS 侧 input_overlay.js 的 _transparentInput 默认值及设计意图一致。
        // Browser 模式下若 DOM 覆盖层未正常弹出/聚焦，输入框会完全空白（无文字无光标）。
        // 转发到基础库静态开关：基础库内部据此自动切换 HTML（DOM 显示）与自绘两种光标实现。

        // 光标：闪烁由引擎 TextBoxRenderer.DrawTextBox 内部自驱（传入 focused），本控件聚焦时每帧使纹理失效以驱动重绘。

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

        // 回车确认：DOM 的非组字态 Enter 会冒泡到全局键盘（组字选词 Enter 已在 Web 端被 isComposing 吞掉），
        // 这里在"本框是激活捕获目标"时，把它模拟进隐藏的 WinForms TextBox，
        // 使原消费方（LoginScene / MirInputBox 等）通过 KeyPress / OnKeyDown 拿到的 Enter 与原版一致。
        static MirTextBox()
        {
            KFramework.MonoGame.Input_KeyBoard.KeyDown += OnGlobalKeyDown;
        }

        private static void OnGlobalKeyDown(KFramework.MonoGame.Keys key)
        {
            if (key == KFramework.MonoGame.Keys.Enter && _current != null && _current._nativeActive)
                _current.TextBox.SimulateKeyDown(Keys.Return);
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

            // 文本框隐藏（如所在对话框关闭）时，收起原生输入覆盖层。
            if (!Visible && _current == this)
            {
                _current = null;
                KFramework.MonoGame.Input_IME.Close();
            }
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
                Location = HiddenTextBoxLocation,
                Size = Size,
                Visible = Visible,
                Tag = this,
            };

            TextBox.VisibleChanged += TextBox_VisibleChanged;
            TextBox.ParentChanged += TextBox_VisibleChanged;
            TextBox.KeyUp += TextBoxOnKeyUp;  
            TextBox.KeyPress += TextBox_KeyPress;

            TextBox.KeyPress += TextBox_NeedRedraw;
            TextBox.KeyUp += TextBox_NeedRedraw;
            TextBox.TextChanged += TextBox_NeedRedraw;
            TextBox.MouseDown += TextBox_NeedRedraw;
            TextBox.MouseUp += TextBox_NeedRedraw;
            TextBox.LostFocus += TextBox_NeedRedraw;
            TextBox.GotFocus += TextBox_NeedRedraw;
            TextBox.MouseWheel += TextBox_NeedRedraw;

            TextBox.GotFocus += OnGotFocus;
            TextBox.LostFocus += OnLostFocus;

            Shown += MirTextBox_Shown;
            TextBox.MouseMove += CMain.CMain_MouseMove;
        }

        // 原生输入覆盖层现在为纯 Pull 模型（见 KFramework.MonoGame.Input_IME）：
        // 引擎每帧经 Input.Poll → Input_IME.Poll 拉取文本到 Input_IME.Text，本类只在 DrawControl 中读取它。
        // 回车确认不经由 Input_IME：DOM 的非组字态 Enter 冒泡到全局键盘 Input_KeyBoard，
        // 由本类的静态 OnGlobalKeyDown 在"本框是激活捕获目标"时模拟进隐藏的 WinForms TextBox，
        // 从而走原消费方（LoginScene / MirInputBox 等）的 KeyPress / OnKeyDown 链路——与原版 WinForms 一致。

        private void OnGotFocus(object sender, EventArgs e)
        {
            // 焦点互斥：先把上一个文本框的原生输入关掉，再接管自己。
            // 直接走 NativeBlur（只依赖 _nativeActive / BrowserInputOverlay，跨 WinForm 与 Web 两端均可编译），
            // 避免引用 shim 专有的 IsFocused/Focused/Blur 等仅在浏览器端存在、桌面端 System.Windows.Forms.TextBox 没有的成员。
            if (_current == this) return;
            if (_current != null && !_current.TextBox.IsDisposed)
                _current.NativeBlur();

            _current = this;
            ShowNativeInput();
        }

        private void OnLostFocus(object sender, EventArgs e)
        {
            if (_current == this)
            {
                // 文本已由 OnInput 实时同步回本框 TextBox.Text，切勿在此用共享单例 <input> 的当前值回写，
                // 否则该 <input> 可能已被复用成对方框的值/空串，从而把本框文本误清空（导致 OK 按钮校验失败无法点击）。
                _current = null;
                KFramework.MonoGame.Input_IME.Close();
            }
            // 无论 GotFocus / LostFocus 谁先到达（切换时新框可能已先接管 _current），本框既已失去焦点
            // 就必须恢复自身绘制：否则 _nativeActive 会一直残留，DrawControl 恒画空串，
            // 表现为"切到密码框后账号文字被隐藏"（内容其实还在，切回来又显示）。
            _nativeActive = false;
            TextureValid = false;
            Redraw();
        }

        // 让原生 <input> 覆盖层显示在文本框对应的屏幕位置并接管输入。
        private void ShowNativeInput()
        {
            if (TextBox == null || TextBox.IsDisposed) return;

            _nativeActive = true;
            int fore = (TextBox.ForeColor != Color.Empty ? TextBox.ForeColor : Color.White).ToArgb();
            // 覆盖层坐标系是"后备缓冲(绘制)像素"（见 JSBind_InputOverlay.Show 注释），而 DisplayLocation/
            // Size 是逻辑坐标(1024x768)。渲染端 UI 以 s = 视口高/768 按高缩放铺进后备缓冲，这里必须把
            // 逻辑坐标×s 换算成后备缓冲像素，DOM 覆盖层(及其光标)才能与画布文本框严格对齐；否则光标/输入框错位。
            // 字号同样按 s 缩放：交给引擎 TextInputOverlay 生成 CSS 字体串并换算 fontPx，
            // 保证 DOM 输入框字形(字重/族/字号)与画布 SpriteFont 渲染一致，切换不跳变。
            var vp = DXManager.GDevice.Viewport;
            float sScale = vp.Height > 0 ? (float)vp.Height / Client.Settings.ScreenHeight : 1f;

            // transparent = Rendered 模式（DOM <input> 仅作 IME / 键盘捕获代理，文字与光标由引擎自绘）。
            // 与原版一致：文本框获焦即由 Input_IME.Open 接管输入（失焦由 OnLostFocus / NativeBlur 调 Close），
            // IME 开 / 关不挂在绘制类 TextBoxRenderer 上。
            KFramework.MonoGame.Input_IME.Open(
                DisplayLocation.X * sScale, DisplayLocation.Y * sScale,
                Size.Width * sScale, Size.Height * sScale,
                MirEngine.FontFactory.GetPixelSize(TextBox.Font, sScale),
                fore, TextBox.Text ?? string.Empty, TextBox.UseSystemPasswordChar, TextBox.MaxLength, TextBox.Multiline,
                MirEngine.FontFactory.BuildCssFont(TextBox.Font, sScale));
            TextureValid = false;
            Redraw();
        }

        private void NativeBlur()
        {
            if (_current == this)
            {
                // 不在此回写文本：共享单例 <input> 的当前值可能是别的框的，回写会误清空本框（见 OnLostFocus 注释）。
                _current = null;
                KFramework.MonoGame.Input_IME.Close();
            }
            _nativeActive = false;
            TextureValid = false;
            Redraw();
        }

        private void TextBox_NeedRedraw(object sender, EventArgs e)
        {
            TextureValid = false;
            Redraw();
        }

        // 每帧由基类 Draw() 调用；在此推进光标闪烁（与 Web_Mir3 DXTextBox 一致：
        // 聚焦时到点翻转并置 TextureValid=false 触发重绘；失焦时关掉光标）。
        protected internal override void DrawControl()
        {
            // Pull 模型：每帧从引擎 Input_IME 拉取（引擎已通过 Input.Poll 刷新），仅作用于当前激活框；
            // 不再订阅任何 Input_IME 事件。文本变化会触发 TextChanged → 登录校验 / 重绘。
            // 回车确认不在这里处理：DOM 的非组字态 Enter 会冒泡到全局键盘 Input_KeyBoard，
            // 由下方静态 OnGlobalKeyDown 在"本框是激活捕获目标"时模拟进 WinForms TextBox，
            // 从而走原消费方（LoginScene / MirInputBox 等）的 KeyPress / OnKeyDown 链路——与原版一致。

            // 光标闪烁由引擎 TextBoxRenderer.DrawTextBox 内部自驱；聚焦时每帧令纹理失效，
            // 使 DrawTextBox 每帧执行、引擎据此推进闪烁节拍。失焦时 OnLostFocus 已重绘一次熄灭光标。
            if (TextBox != null && TextBox.Focused)
                TextureValid = false;

            if (_current == this && KFramework.MonoGame.Input_IME.Active)
            {
                string t = KFramework.MonoGame.Input_IME.Text;
                if (TextBox.Text != t) TextBox.Text = t;
            }

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
            // DOM 覆盖层不透明（TransparentDomInput=false）且本框正由 DOM 接管时，文字与光标一律交给浏览器
            // 原生 DOM 显示（浏览器光标、选区、IME 候选窗都更贴合系统），引擎只把纹理清成透明，避免与 DOM 文字重影。
            // 仅在 DOM 透明（文字由引擎自绘）或本框未接管（失焦/隐藏）时，才在 canvas 上绘制文字与光标。
            string drawText = TextBox.Text ?? "";
            TextRenderer.Clear(ControlTexture, back);
            // Browser 模式下文字与光标都交给 DOM，引擎只保留背景（已 Clear）。
            
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
                        TextBox.SelectionStart, !TextBox.Multiline, TextBox.Focused);
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

        void TextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (e.KeyChar == (char)Keys.Escape)
            {
                CMain.Instance.ActiveControl = null;
                e.Handled = true;
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

            if (_current == this)
            {
                _current = null;
                KFramework.MonoGame.Input_IME.Close();
            }

            if (!TextBox.IsDisposed)
                TextBox.Dispose();
        }


    }
}
