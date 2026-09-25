using System;

namespace KFramework.MonoGame
{
    // 自绘文本框的“模型”：保存文本、光标、选择区间与基础外观。
    // 不依赖任何渲染后端；真正的绘制由本类的 TextBox.Renderer 部分负责。
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

    public sealed partial class TextBox : IDisposable
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
        private int selectionAnchor;
        // IME 组字预览（尚未提交到 text），仅用于绘制；对齐 UGUI 的 InputField.compositionString。
        private string compositionString = string.Empty;
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
                // 文本被外部设置（如打开时恢复存档）时，光标置于末尾并清除选区，对齐 WinForms TextBox 行为；
                // 否则会出现“开局长度为 0，恢复后光标仍停在开头”的问题。
                selectionStart = text.Length;
                selectionLength = 0;
                compositionString = string.Empty;
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>IME 组字预览文本（尚未提交到 Text）。</summary>
        public string CompositionString => compositionString;

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

        /// <summary>
        /// 退化兜底：当引擎自身的焦点链（<see cref="_active"/>）因某些原因未建立时，
        /// 由上层（shim）提供"当前激活的 TextBox"。控制键据此仍能被正确路由，
        /// 避免焦点链异常时整框键盘输入（删除 / 方向 / 选区）全部失效。
        /// </summary>
        public static Func<TextBox> ActiveTextBoxResolver;

        /// <summary>当前应接收按键的 TextBox：优先引擎焦点链，退化时回退到上层提供的激活框。</summary>
        private static TextBox ActiveOrResolved => _active ?? ActiveTextBoxResolver?.Invoke();

        /// <summary>当前由引擎接管的文本框（DOM 回传的锚点）。</summary>
        internal static TextBox ActiveTextBox => _active;

        // 全局键盘（Input_KeyBoard）兜底：本工程里 DOM <input> 覆盖层主要用于 IME 捕获，
        // 普通字符与控制键实际都经全局键盘进入引擎（原逻辑只转发了 Enter）。
        // 这里把回车 / 删除 / 方向 / Home / End 等控制键一并转发给当前激活的 TextBox，
        // 与 DOM 的 input_html_ime.OnKeyDown 走同一引擎入口（SimulateKeyDown），由 _active 互斥保证唯一目标。
        static TextBox()
        {
            Input_KeyBoard.KeyDown += OnGlobalKeyDown;
            // DOM 覆盖层经 Input_IME 转发的控制键 / 编辑结果，订阅其事件，与 [JSExport] 解耦。
            Input_IME.KeyDown += ProcessKey;
            Input_IME.DomValue += ProcessDomValue;
        }

        private static void OnGlobalKeyDown(Keys key)
        {
            TextBox active = ActiveOrResolved;
            if (active == null || active.isDisposed) return;

            bool ctrl = Input_KeyBoard.Ctrl, alt = Input_KeyBoard.Alt, shift = Input_KeyBoard.Shift;

            // Ctrl+A 全选：DOM 覆盖层未聚焦时的兜底（聚焦时由 TS keydown 已转发的 Ctrl+A 经 ProcessKey -> SimulateKeyDown 处理）。
            // 直接走引擎 SelectAll，使选区成为唯一真相源，引擎自绘高亮。
            if (ctrl && !alt && key == Keys.A)
            {
                active.SelectAll();
                active.SyncOverlay();
                return;
            }

            switch (key)
            {
                case Keys.Enter:
                case Keys.Escape:
                case Keys.Backspace:
                case Keys.Delete:
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                    active.SimulateKeyDown(key);
                    break;
            }

            // 兜底：当 DOM 覆盖层未真正取得键盘焦点时（普通字符的 keydown 被 DOM 的 stopPropagation 拦截，
            // 不会冒泡到 window），全局键盘仍能收到这些字符——此时由引擎把可打印字符补录进激活的 TextBox。
            // DOM 正常聚焦时该分支不会触发（stopPropagation 已拦截，不会到达本全局路径），故不会与 input 事件重复插入；
            // 而删除 / 方向等控制键仍走上面的 SimulateKeyDown，互不影响。IME 中文仍由 DOM 覆盖层经 input 事件回传。
            // 注意：Ctrl / Alt 组合键（如 Ctrl+A 全选、Ctrl+C/V/X 复制粘贴）不在此作为可打印字符插入，交由浏览器 / 上面的分支处理。
            if (!ctrl && !alt && TryGetPrintableChar(key, shift, out char c))
                active.InsertText(c.ToString());
        }

        /// <summary>把物理按键映射为可打印字符（基本 ASCII：字母 / 数字 / 空格 / 常用标点）。
        /// 仅作为 DOM 覆盖层未聚焦时的兜底；中文等 IME 组字仍由 DOM input 事件回传。</summary>
        private static bool TryGetPrintableChar(Keys key, bool shift, out char c)
        {
            c = '\0';
            int k = (int)key;

            // 字母 A-Z（keyCode 65-90）
            if (k >= 65 && k <= 90)
            {
                c = shift ? (char)k : char.ToLowerInvariant((char)k);
                return true;
            }
            // 数字 0-9（keyCode 48-57，含 Shift 上档符号，US 布局）
            if (k >= 48 && k <= 57)
            {
                if (shift)
                {
                    char[] shifted = { ')', '!', '@', '#', '$', '%', '^', '&', '*', '(' };
                    c = shifted[k - 48];
                }
                else
                {
                    c = (char)k;
                }
                return true;
            }
            if (k == 32) { c = ' '; return true; }

            // 常用标点（browser keyCode → 字符，US 布局）
            switch (k)
            {
                case 190: c = shift ? '>' : '.'; return true;   // OEM_PERIOD
                case 188: c = shift ? '<' : ','; return true;   // OEM_COMMA
                case 191: c = shift ? '?' : '/'; return true;   // OEM_2 (/?)
                case 189: c = shift ? '_' : '-'; return true;   // OEM_MINUS
                case 187: c = shift ? '+' : '='; return true;   // OEM_PLUS
                case 186: c = shift ? ':' : ';'; return true;   // OEM_1
                case 222: c = shift ? '"' : '\''; return true;  // OEM_7
                case 219: c = shift ? '{' : '['; return true;   // OEM_4
                case 221: c = shift ? '}' : ']'; return true;   // OEM_6
                case 220: c = shift ? '|' : '\\'; return true;  // OEM_5
                case 192: c = shift ? '~' : '`'; return true;   // OEM_3
            }
            return false;
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
                    OverlayFontPx, (int)fc.PackedValue, text,
                    useSystemPasswordChar, maxLength, multiline, OverlayFontCss);
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

        /// <summary>
        /// 由 DOM 转发来的控制键（对齐 UGUI InputField.KeyPressed）。引擎自行维护 text / 光标 / 选区——
        /// 这是唯一真相源，绝不反向读取 DOM 的光标位置。
        /// </summary>
        public void SimulateKeyDown(Keys keyCode, bool shift = false, bool ctrl = false, bool alt = false)
        {
            var args = new KeyEventArgs(keyCode);
            KeyDown?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;

            if (ctrl && !alt)
            {
                if (keyCode == Keys.A) { SelectAll(); SyncOverlay(); return; }
                // C / V / X 依赖剪贴板，由浏览器侧处理，这里不拦截。
            }

            switch (keyCode)
            {
                case Keys.Backspace:
                    if (selectionLength > 0) { text = text.Remove(selectionStart, selectionLength); selectionLength = 0; }
                    else if (selectionStart > 0) { selectionStart--; text = text.Remove(selectionStart, 1); }
                    break;
                case Keys.Delete:
                    if (selectionLength > 0) { text = text.Remove(selectionStart, selectionLength); selectionLength = 0; }
                    else if (selectionStart < text.Length) { text = text.Remove(selectionStart, 1); }
                    break;
                case Keys.Left:
                    if (shift) { if (selectionLength == 0) selectionAnchor = selectionStart; selectionStart = Math.Max(0, selectionStart - 1); selectionLength = Math.Abs(selectionStart - selectionAnchor); }
                    else { selectionStart = Math.Max(0, selectionStart - 1); selectionLength = 0; }
                    break;
                case Keys.Right:
                    if (shift) { if (selectionLength == 0) selectionAnchor = selectionStart; selectionStart = Math.Min(text.Length, selectionStart + 1); selectionLength = Math.Abs(selectionStart - selectionAnchor); }
                    else { selectionStart = Math.Min(text.Length, selectionStart + 1); selectionLength = 0; }
                    break;
                case Keys.Home:
                    selectionStart = 0; selectionLength = 0; break;
                case Keys.End:
                    selectionStart = text.Length; selectionLength = 0; break;
                case Keys.Escape:
                    KeyPress?.Invoke(this, new KeyPressEventArgs((char)Keys.Escape));
                    SyncOverlay();
                    return;
                case Keys.Enter:
                    KeyPress?.Invoke(this, new KeyPressEventArgs((char)Keys.Enter));
                    SyncOverlay();
                    return;
            }
            if (keyCode == Keys.Backspace || keyCode == Keys.Delete ||
                keyCode == Keys.Left || keyCode == Keys.Right ||
                keyCode == Keys.Up || keyCode == Keys.Down ||
                keyCode == Keys.Home || keyCode == Keys.End)
            {
                UpdateLines();
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
            SyncOverlay();
        }

        /// <summary>提交文本（IME 组字结束 / 粘贴 / 程序化输入），在光标处插入并推进光标。</summary>
        public void InsertText(string value)
        {
            if (string.IsNullOrEmpty(value) || !Enabled || ReadOnly) return;
            if (maxLength > 0 && text.Length - selectionLength >= maxLength) return;
            if (selectionLength > 0) text = text.Remove(selectionStart, selectionLength);
            text = text.Insert(selectionStart, value);
            selectionStart += value.Length;
            selectionLength = 0;
            compositionString = string.Empty;
            UpdateLines();
            TextChanged?.Invoke(this, EventArgs.Empty);
            SyncOverlay();
        }

        /// <summary>设置 IME 组字预览（尚未提交）。</summary>
        public void SetComposition(string value)
        {
            compositionString = value ?? string.Empty;
            SyncOverlay();
        }

        /// <summary>提交 IME 组字结果。</summary>
        public void CommitComposition(string value)
        {
            InsertText(value);
            compositionString = string.Empty;
        }

        public void SelectAll()
        {
            selectionStart = 0;
            selectionLength = text.Length;
        }

        /// <summary>
        /// DOM 把原生编辑结果回传：以引擎自身光标为锚点合并差异（保证引擎是文本唯一真相源）。
        /// composing=true 时 value 含组字预览，仅更新预览不落库。
        /// </summary>
        public void SyncFromDom(string domValue, int selStart, int selEnd, bool composing)
        {
            if (domValue == null) domValue = string.Empty;
            selStart = Math.Max(0, Math.Min(domValue.Length, selStart));
            selEnd = Math.Max(0, Math.Min(domValue.Length, selEnd));
            if (composing)
            {
                compositionString = domValue.StartsWith(text) ? domValue.Substring(text.Length) : domValue;
            }
            else
            {
                compositionString = string.Empty;
                // 直接采纳 DOM 的文本与光标：DOM 是原生编辑的权威来源，按此镜像可保证光标停在正确位置（如末尾）。
                text = domValue;
                selectionStart = selStart;
                selectionLength = Math.Max(0, selEnd - selStart);
                UpdateLines();
                TextChanged?.Invoke(this, EventArgs.Empty);
            }
            SyncOverlay();
        }

        /// <summary>
        /// 把引擎的 text / 光标区间写回 DOM 覆盖层：对齐 IME 候选窗位置，并在引擎改字（Backspace / 程序化输入）后修正镜像。
        /// 不走 <see cref="Input_IME"/>（它是共享的、非 TextBox 专属），直接经 JSBind 操作覆盖层。
        /// 组字进行中由 DOM 自行管理，不回写以免覆盖候选串。
        /// </summary>
        private void SyncOverlay()
        {
            if (!Input_IME.Active) return;
            if (!string.IsNullOrEmpty(compositionString)) return;
            JSBind_InputHtmlIme.SetValue(text ?? string.Empty);
            JSBind_InputHtmlIme.SetSelectionRange(selectionStart, selectionStart + selectionLength);
        }

        // ---- DOM -> 引擎 的桥接入口（供 [JSExport] 调用） ----
        internal static void ProcessKey(string key, bool ctrl, bool shift, bool alt)
        {
            Keys k = ToKey(key);
            TextBox active = ActiveOrResolved;
            if (k != Keys.None && active != null) active.SimulateKeyDown(k, shift, ctrl, alt);
        }
        internal static void ProcessDomValue(string value, int selStart, int selEnd, bool composing)
        {
            TextBox active = ActiveOrResolved;
            active?.SyncFromDom(value, selStart, selEnd, composing);
        }

        private static Keys ToKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return Keys.None;
            switch (key)
            {
                case "ArrowLeft": return Keys.Left;
                case "ArrowRight": return Keys.Right;
                case "ArrowUp": return Keys.Up;
                case "ArrowDown": return Keys.Down;
                case "Backspace": return Keys.Backspace;
                case "Delete": return Keys.Delete;
                case "Home": return Keys.Home;
                case "End": return Keys.End;
                case "Enter": return Keys.Enter;
                case "Return": return Keys.Enter;
                case "Escape": return Keys.Escape;
                case "Tab": return Keys.Tab;
                case " ": return Keys.Space;
            }
            if (Enum.TryParse<Keys>(key, true, out Keys p)) return p;
            if (key.Length == 1) return (Keys)char.ToUpperInvariant(key[0]);
            return Keys.None;
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
