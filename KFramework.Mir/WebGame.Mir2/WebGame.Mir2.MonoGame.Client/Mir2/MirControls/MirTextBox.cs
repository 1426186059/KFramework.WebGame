using Client.MirGraphics;
using MirEngine;
using SlimDX;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public sealed class MirTextBox : MirControl
    {
        #region Back Color

        protected override void OnBackColourChanged()
        {
            base.OnBackColourChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.BackColor = BackColour;
        }

        #endregion

        #region Enabled

        protected override void OnEnabledChanged()
        {
            base.OnEnabledChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.Enabled = Enabled;
        }

        #endregion

        #region Fore Color

        protected override void OnForeColourChanged()
        {
            base.OnForeColourChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                TextBox.ForeColor = ForeColour;
        }

        #endregion

        #region Location

        protected override void OnLocationChanged()
        {
            base.OnLocationChanged();
            ApplyNativeTextBoxState();

            // 文本框随对话框重新居中/移动时，原生输入覆盖层同步跟随（坐标按全屏拉伸缩放反算）。
            if (_current == this)
                BrowserInputOverlay.Reposition(DisplayLocation.X * CMain.ScaleX, DisplayLocation.Y * CMain.ScaleY,
                    Size.Width * CMain.ScaleX, Size.Height * CMain.ScaleY);

            TextureValid = false;
            Redraw();
        }

        #endregion

        #region Max Length

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

        #endregion

        #region Parent

        protected override void OnParentChanged()
        {
            base.OnParentChanged();
            if (TextBox != null && !TextBox.IsDisposed)
                ApplyNativeTextBoxState();
        }

        #endregion

        #region Password

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

        #endregion

        #region Font

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

        #endregion

        #region Size

        protected override void OnSizeChanged()
        {
            TextBox.Size = Size;

            DisposeTexture();

            _size = Size;

            if (TextBox != null && !TextBox.IsDisposed)
                base.OnSizeChanged();
        }

        #endregion
        
        #region TextBox

        // 当前由浏览器原生 <input> 覆盖层接管的文本框（全局唯一，输入焦点互斥）。
        private static MirTextBox _current;
        // 原生输入覆盖层是否正接管本框（仅作 IME/键盘捕获代理，DOM 透明，不再影响自身绘制）。
        private bool _nativeActive;

        // 原生输入覆盖层是否透明：true=文字/光标由引擎在 canvas 自绘（避免与 DOM 重影）；
        // false=由 DOM 直接显示文字/光标（使用浏览器原生光标 / 选区 / IME 候选窗）。
        // 默认 false：让浏览器托管光标，体验与系统一致。运行时可切换以对比两种渲染方式。
        public static bool TransparentDomInput { get; set; } = false;

        // 光标闪烁（对齐 Web_Mir3 DXTextBox）：聚焦时按固定间隔切换 _caretVisible，切换即触发纹理重绘。
        private bool _caretVisible;
        private long _caretToggle;
        private const long CaretBlinkInterval = 530;

        public bool CanLoseFocus;
        public readonly TextBox TextBox;
        private Pen CaretPen;

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

        #endregion

        #region Label

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

        #endregion

        #region Visible

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

            ApplyNativeTextBoxState();

            // 文本框隐藏（如所在对话框关闭）时，收起原生输入覆盖层。
            if (!Visible && _current == this)
            {
                _current = null;
                BrowserInputOverlay.Hide();
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

        #endregion

        #region MultiLine

        public override void MultiLine()
        {
            TextBox.Multiline = true;
            TextBox.Size = Size;

            DisposeTexture();
            Redraw();
        }

        #endregion

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
                Cursor = CMain.Cursors[(byte)MouseCursor.TextPrompt]
            };

            CaretPen = new Pen(ForeColour, 1);

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

        // 原生输入覆盖层事件路由：仅作用于当前获得焦点的文本框。
        static MirTextBox()
        {
            BrowserInputOverlay.ValueChanged += (s, v) =>
            {
                if (_current != null && !_current.TextBox.IsDisposed)
                    _current.TextBox.Text = v; // 赋值会触发 TextChanged（shim TextBox 的 Text setter 已调用 OnTextChanged），登录校验据此生效
            };
            BrowserInputOverlay.Enter += (s, e) =>
            {
                // 浏览器端：DOM <input> 收到回车后，通过 shim 的 SimulateKeyDown 触发 TextBox.KeyDown 事件，
                // 让游戏侧（登录/确认）像真实按键一样响应。SimulateKeyDown 是普通方法（非事件），可在此类外部调用。
                if (_current != null && !_current.TextBox.IsDisposed)
                    _current.TextBox.SimulateKeyDown(Keys.Return);
            };
            BrowserInputOverlay.Blur += (s, e) =>
            {
                // 浏览器原生 blur：多为点击了 canvas 上的另一个文本框（游戏侧 OnGotFocus 会重新 Show 它）。
                // 若此时直接 Hide，会把刚显示的输入框收掉、光标消失（切到密码后无光标）。
                // 故：仅重新 Show 当前框以保留光标/IME；文本已由 OnInput 实时同步，不要回写，避免误清空对方框。
                if (_current != null && !_current.TextBox.IsDisposed)
                    _current.ShowNativeInput();
            };
        }

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
                BrowserInputOverlay.Hide();
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
            // 字号取 FontToCss 解析出的 px（与离屏纹理渲染口径一致，Point 单位已乘 4/3），
            // 字体族用 TextBox.Font.Name（与 RT 同字体），保证 DOM 叠层与失焦时 RT 渲染完全一致，切换不跳变。
            double fontPx = 10d;
            string fontCss = BrowserCanvas.FontToCss(TextBox.Font);
            int pxIdx = fontCss.IndexOf("px");
            if (pxIdx > 0) {
                int s = pxIdx;
                while (s > 0 && (char.IsDigit(fontCss[s - 1]) || fontCss[s - 1] == '.')) s--;
                if (double.TryParse(fontCss.Substring(s, pxIdx - s), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p))
                    fontPx = p;
            }
            string fontFamily = TextBox.Font != null ? TextBox.Font.Name : "Arial";
            // 场景纹理被拉伸铺满画布，DOM 覆盖层坐标/字号需按同一缩放从逻辑(1024x768)空间
            // 反算到画布后备缓冲像素，否则输入框/光标位置错配（见 CMain.ScaleX/ScaleY）。
            double sx = CMain.ScaleX, sy = CMain.ScaleY;
            BrowserInputOverlay.Show(DisplayLocation.X * sx, DisplayLocation.Y * sy,
                Size.Width * sx, Size.Height * sy,
                fontPx * sx, fore, TextBox.Text ?? string.Empty, TextBox.UseSystemPasswordChar, TextBox.MaxLength, TextBox.Multiline, fontFamily, TransparentDomInput);
            TextureValid = false;
            Redraw();
        }

        private void NativeBlur()
        {
            if (_current == this)
            {
                // 不在此回写文本：共享单例 <input> 的当前值可能是别的框的，回写会误清空本框（见 OnLostFocus 注释）。
                _current = null;
                BrowserInputOverlay.Hide();
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

        // 每帧由基类 Draw() 调用；在此驱动光标闪烁（与 Web_Mir3 DXTextBox 一致：
        // 聚焦时到点翻转 _caretVisible 并置 TextureValid=false 触发重绘；失焦时关掉光标）。
        protected internal override void DrawControl()
        {
            // DOM 不透明显示文字/光标时，由浏览器托管光标，引擎不再自绘，故跳过闪烁逻辑。
            bool domShowsText = _nativeActive && !TransparentDomInput;
            if (domShowsText)
            {
                base.DrawControl();
                return;
            }
            if (TextBox != null && TextBox.Focused)
            {
                if (CMain.Time - _caretToggle >= CaretBlinkInterval)
                {
                    _caretToggle = CMain.Time;
                    _caretVisible = !_caretVisible;
                    TextureValid = false;
                    System.Console.WriteLine($"[DrawControl] blink flip caretVisible={_caretVisible}");
                }
            }
            else if (_caretVisible)
            {
                _caretVisible = false;
                TextureValid = false;
                System.Console.WriteLine("[DrawControl] lost focus, caret off");
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

            string css = TextBox.Font == null ? "10px sans-serif" : BrowserCanvas.FontToCss(TextBox.Font);
            int fore = (TextBox.ForeColor != Color.Empty ? TextBox.ForeColor : Color.White).ToArgb();
            // 文本框纹理作为面板上的透明叠层：无背景色时清成透明（alpha 0），避免盖住面板里的输入框底。
            int back = (TextBox.BackColor != Color.Empty && TextBox.BackColor.A > 0) ? TextBox.BackColor.ToArgb() : 0;
            int selBack = Color.FromArgb(128, 51, 153, 255).ToArgb();
            // DOM 覆盖层不透明（TransparentDomInput=false）且本框正由 DOM 接管时，文字与光标一律交给浏览器
            // 原生 DOM 显示（浏览器光标、选区、IME 候选窗都更贴合系统），引擎只把纹理清成透明，避免与 DOM 文字重影。
            // 仅在 DOM 透明（文字由引擎自绘）或本框未接管（失焦/隐藏）时，才在 canvas 上绘制文字与光标。
            bool domShowsText = _nativeActive && !TransparentDomInput;
            string drawText = TextBox.Text ?? "";
            System.Console.WriteLine($"[CreateTexture] drawText='{drawText}' ctrlRTnull={ControlTexture?.RenderTarget == null} focused={TextBox.Focused} caretVisible={_caretVisible} size={Size.Width}x{Size.Height} domShowsText={domShowsText}");
            if (domShowsText)
                BrowserCanvas.DrawTextBox(ControlTexture, Size.Width, Size.Height, "",
                    css, fore, back, selBack, fore, 0, 0, 0, false, !TextBox.Multiline);
            else
                BrowserCanvas.DrawTextBox(ControlTexture, Size.Width, Size.Height, drawText,
                    css, fore, back, selBack, fore, TextBox.SelectionStart, TextBox.SelectionLength, TextBox.SelectionStart, TextBox.Focused && _caretVisible, !TextBox.Multiline);

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

        private Point GetCaretPosition()
        {
            Point result = TextBox.GetPositionFromCharIndex(TextBox.SelectionStart);

            if (result.X == 0 && TextBox.Text.Length > 0)
            {
                result = TextBox.GetPositionFromCharIndex(TextBox.Text.Length - 1);
                int s = result.X / TextBox.Text.Length;
                result.X = (int)(result.X + (s * 1.46));
                result.Y = TextBox.GetLineFromCharIndex(TextBox.SelectionStart) * TextBox.Font.Height;
            }

            return result;
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


        #region Disposable

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            if (_current == this)
            {
                _current = null;
                BrowserInputOverlay.Hide();
            }

            if (!TextBox.IsDisposed)
                TextBox.Dispose();
        }


        #endregion
    }
}
