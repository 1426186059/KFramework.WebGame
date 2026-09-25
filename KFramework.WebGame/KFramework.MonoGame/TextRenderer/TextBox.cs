using System;

namespace KFramework.MonoGame
{
    // 自绘文本框的“模型”：保存文本、光标、选择区间与基础外观。
    // 不依赖任何渲染后端；真正的绘制由 TextBoxRenderer 负责。
    //
    // 说明：KeyEventArgs / KeyPressEventArgs / MouseEventArgs / BorderStyle / MouseButtons
    // 等支撑类型作为嵌套类型存在，避免污染 KFramework.MonoGame 命名空间顶层，
    // 防止与客户端 MirEngine 的同名 shim 类型产生歧义。
    public enum BorderStyle
    {
        None,
        FixedSingle,
        Fixed3D
    }

    public class TextBox : IDisposable
    {
        [Flags]
        public enum MouseButtons
        {
            None = 0,
            Left = 1,
            Right = 2,
            Middle = 4,
            XButton1 = 8,
            XButton2 = 16
        }

        public class KeyEventArgs : EventArgs
        {
            public Keys KeyCode { get; }
            public bool Handled { get; set; }
            public bool SuppressKeyPress { get; set; }
            public KeyEventArgs(Keys keyCode) { KeyCode = keyCode; }
        }

        public class KeyPressEventArgs : EventArgs
        {
            public char KeyChar { get; }
            public bool Handled { get; set; }
            public KeyPressEventArgs(char keyChar) { KeyChar = keyChar; }
        }

        public class MouseEventArgs : EventArgs
        {
            public MouseButtons Button { get; }
            public int Clicks { get; }
            public int X { get; }
            public int Y { get; }
            public int Delta { get; }
            public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
            {
                Button = button;
                Clicks = clicks;
                X = x;
                Y = y;
                Delta = delta;
            }
        }

        public delegate void KeyEventHandler(object sender, KeyEventArgs e);
        public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
        public delegate void MouseEventHandler(object sender, MouseEventArgs e);

        private string text = string.Empty;
        private string[] lines = new string[0];
        private int selectionStart;
        private int selectionLength;
        private int maxLength;
        private bool multiline;
        private bool useSystemPasswordChar;
        private bool isDisposed;
        private bool isFocused;
        private bool focused;
        private bool canFocus = true;
        private bool readOnly;
        private BorderStyle borderStyle = BorderStyle.None;
        private bool visible = true;
        private bool enabled = true;
        private bool acceptsReturn;
        private bool acceptsTab;
        private Color backColor = default;
        private Color foreColor = Color.White;
        private Point location = new Point(0, 0);
        private Size size = new Size(0, 0);
        private IFont font;
        private bool wordWrap;
        private bool scrollBars;

        public event EventHandler TextChanged;
        public event EventHandler GotFocus;
        public event EventHandler LostFocus;
        public event EventHandler VisibleChanged;
        public event EventHandler EnabledChanged;
        public event EventHandler ParentChanged;
        public event KeyEventHandler KeyDown;
        public event KeyPressEventHandler KeyPress;
        public event KeyEventHandler KeyUp;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseWheel;

        public string Text
        {
            get => text;
            set
            {
                if (text == value) return;
                text = value ?? string.Empty;
                UpdateLines();
                selectionStart = Math.Min(selectionStart, text.Length);
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string[] Lines
        {
            get => lines;
            set
            {
                lines = value ?? new string[0];
                Text = string.Join(Environment.NewLine, lines);
            }
        }

        public int MaxLength { get => maxLength; set => maxLength = value; }
        public bool Multiline { get => multiline; set => multiline = value; }
        public bool UseSystemPasswordChar { get => useSystemPasswordChar; set => useSystemPasswordChar = value; }
        public bool WordWrap { get => wordWrap; set => wordWrap = value; }
        public bool ScrollBars { get => scrollBars; set => scrollBars = value; }

        public int SelectionStart
        {
            get => selectionStart;
            set => selectionStart = Math.Max(0, Math.Min(value, text.Length));
        }

        public int SelectionLength
        {
            get => selectionLength;
            set => selectionLength = Math.Max(0, Math.Min(value, text.Length - selectionStart));
        }

        public int TextLength => text.Length;

        public Color BackColor { get => backColor; set => backColor = value; }
        public Color ForeColor { get => foreColor; set => foreColor = value; }
        public Point Location { get => location; set => location = value; }
        public Size Size { get => size; set => size = value; }
        public bool Visible { get => visible; set { if (visible != value) { visible = value; VisibleChanged?.Invoke(this, EventArgs.Empty); } } }
        public bool Enabled { get => enabled; set { if (enabled != value) { enabled = value; EnabledChanged?.Invoke(this, EventArgs.Empty); } } }
        public BorderStyle BorderStyle { get => borderStyle; set => borderStyle = value; }
        public bool AcceptsReturn { get => acceptsReturn; set => acceptsReturn = value; }
        public bool AcceptsTab { get => acceptsTab; set => acceptsTab = value; }

        public IntPtr Handle { get; set; }
        public bool ReadOnly { get => readOnly; set => readOnly = value; }
        public object Tag { get; set; }
        public object Parent { get => parent; set { if (parent != value) { parent = value; ParentChanged?.Invoke(this, EventArgs.Empty); } } }
        private object parent;

        public bool CanFocus { get => canFocus; set => canFocus = value; }
        public bool IsDisposed => isDisposed;
        public bool IsFocused { get => isFocused; set => isFocused = value; }
        public bool Focused { get => focused; set => focused = value; }
        public IFont Font { get => font; set { if (value != null) font = value; } }

        // 原生输入覆盖层（DOM <input>）所需的、由 shim 在聚焦前按当前视口/字体填写的元数据。
        // 坐标 = 逻辑 Location/Size × OverlayScale（本工程当前为恒等变换，Scale=1）；
        // 这样上层只需像 WinForms 一样设置 Location/Size/Font/Visible，覆盖层的开关与焦点互斥全部由本引擎负责。
        public float OverlayScale { get; set; } = 1f;
        public float OverlayFontPx { get; set; } = 10f;
        public string OverlayFontCss { get; set; } = "10px sans-serif";

        public void AppendText(string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            Text = text + value;
        }

        // 当前聚焦的 TextBox（浏览器 / MonoGame 无真实窗口，需 shim 自行维护焦点链以实现互斥）。
        // 与 Web_Mir2.Engine 的 MirEngine.TextBox 行为对齐：聚焦时先对旧框触发 LostFocus，再对新框触发 GotFocus，
        // 上层无需再自行维护 _current / ReleaseFocus 之类的互斥逻辑。
        private static TextBox _active;

        // 回车确认：DOM 的非组字态 Enter 冒泡到全局键盘（组字选词 Enter 已在 Web 端被 isComposing 吞掉），
        // 这里在"本框是激活捕获目标"时把它模拟进本框，使原消费方（LoginScene / MirInputBox 等）
        // 通过 KeyPress / OnKeyDown 拿到的 Enter 与原版 WinForms 一致——焦点互斥同样由 _active 维护。
        static TextBox()
        {
            Input_KeyBoard.KeyDown += OnGlobalKeyDown;
        }

        private static void OnGlobalKeyDown(Keys key)
        {
            if (key == Keys.Enter && _active != null && !_active.isDisposed)
                _active.SimulateKeyDown(Keys.Enter);
        }

        public void Focus()
        {
            if (_active == this) return;

            // 互斥：先把上一个聚焦的框失焦（触发其 LostFocus 并收起覆盖层）。
            if (_active != null && !_active.isDisposed)
                _active.Blur();

            _active = this;
            focused = true;
            isFocused = true;
            GotFocus?.Invoke(this, EventArgs.Empty);

            // 接管原生输入：坐标用逻辑 Location/Size × OverlayScale 换算到后备缓冲像素。
            if (visible)
            {
                Color fc = (foreColor.A == 0) ? Color.White : foreColor;
                Input_IME.Open(
                    location.X * OverlayScale, location.Y * OverlayScale,
                    size.Width * OverlayScale, size.Height * OverlayScale,
                    overlayFontPx, (int)fc.PackedValue, text,
                    useSystemPasswordChar, maxLength, multiline, overlayFontCss);
            }
        }

        public void Blur()
        {
            if (_active == this) _active = null;
            if (!focused && !isFocused) return;
            focused = false;
            isFocused = false;
            LostFocus?.Invoke(this, EventArgs.Empty);
            Input_IME.Close();
        }

        public void Dispose()
        {
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        public Point GetPositionFromCharIndex(int index)
        {
            int lineIndex = GetLineFromCharIndex(index);
            if (lineIndex < 0 || lineIndex >= lines.Length) return Location;
            int col = index - GetFirstCharIndexFromLine(lineIndex);
            IFont f = font;
            string lineText = lines[lineIndex];
            int x = 0;
            if (f != null && col > 0 && col <= lineText.Length)
                x = TextRenderer.MeasureText(lineText.Substring(0, col), f).Width;
            int lineHeight = (int)(f?.LineSpacing ?? 0f);
            int y = lineIndex * lineHeight;
            return new Point(x, y);
        }

        public int GetLineFromCharIndex(int index)
        {
            int remaining = index;
            for (int i = 0; i < lines.Length; i++)
            {
                int lineLen = lines[i].Length + 1;
                if (remaining < lineLen) return i;
                remaining -= lineLen;
            }
            return lines.Length - 1;
        }

        public int GetFirstCharIndexFromLine(int line)
        {
            if (line < 0 || line >= lines.Length) return 0;
            int idx = 0;
            for (int i = 0; i < line; i++) idx += lines[i].Length + 1;
            return idx;
        }

        public int GetCharIndexFromPosition(Point pt)
        {
            int bestIndex = 0;
            int bestDist = int.MaxValue;
            IFont f = font;
            int lineHeight = (int)(f?.LineSpacing ?? 1f);
            for (int line = 0; line < lines.Length; line++)
            {
                int y = line * lineHeight;
                int dy = Math.Abs(y - pt.Y);
                string lineText = lines[line];
                int baseIndex = GetFirstCharIndexFromLine(line);
                if (f == null) continue;
                for (int c = 0; c <= lineText.Length; c++)
                {
                    int x = c > 0 ? TextRenderer.MeasureText(lineText.Substring(0, c), f).Width : 0;
                    int dx = x - pt.X;
                    int dist = dx * dx + dy * dy;
                    if (dist < bestDist) { bestDist = dist; bestIndex = baseIndex + c; }
                }
            }
            return bestIndex;
        }

        public void ScrollToCaret() { }

        public void SimulateKeyDown(Keys keyCode)
        {
            var args = new KeyEventArgs(keyCode);
            KeyDown?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;
            switch (keyCode)
            {
                case Keys.Backspace:
                    if (selectionLength > 0) { text = text.Remove(selectionStart, selectionLength); selectionLength = 0; }
                    else if (selectionStart > 0) { selectionStart--; text = text.Remove(selectionStart, 1); }
                    break;
                case Keys.Left:
                    if (selectionLength > 0) selectionLength = 0;
                    else selectionStart = Math.Max(0, selectionStart - 1);
                    break;
                case Keys.Right:
                    if (selectionLength > 0) { selectionStart = Math.Min(text.Length, selectionStart + selectionLength); selectionLength = 0; }
                    else selectionStart = Math.Min(text.Length, selectionStart + 1);
                    break;
            }
            if (keyCode == Keys.Backspace) { UpdateLines(); TextChanged?.Invoke(this, EventArgs.Empty); }
            if (keyCode == Keys.Enter) KeyPress?.Invoke(this, new KeyPressEventArgs((char)Keys.Enter));
        }

        public void SimulateKeyPress(char keyChar)
        {
            if (keyChar == '\b' || keyChar == '\r' || keyChar == '\n') return;
            var args = new KeyPressEventArgs(keyChar);
            KeyPress?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;
            if (keyChar == '\t' && !AcceptsTab) return;
            if (maxLength > 0 && text.Length - selectionLength >= maxLength) return;
            string insert = useSystemPasswordChar ? new string('*', 1) : keyChar.ToString();
            if (selectionLength > 0) text = text.Remove(selectionStart, selectionLength);
            text = text.Insert(selectionStart, insert);
            selectionStart += insert.Length;
            selectionLength = 0;
            UpdateLines();
            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateLines()
        {
            if (string.IsNullOrEmpty(text)) { lines = new string[0]; return; }
            lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }
    }
}
