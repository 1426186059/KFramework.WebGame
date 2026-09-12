using System;
using System.Collections.Generic;

namespace MirEngine
{
    public enum BorderStyle
    {
        None,
        FixedSingle,
        Fixed3D
    }

    public class TextBox : IDisposable
    {
        private string text = string.Empty;
        private int selectionStart;
        private int selectionLength;
        private bool multiline;
        private Color backColor;
        private BorderStyle borderStyle;
        private Font font;
        private Color foreColor;
        private Point location;
        private Size size;
        private bool visible;
        private bool enabled = true; // 新增的Enabled属性
        private object tag;
        private object parent;
        private List<string> lines = new List<string>();
        private int maxLines = 10;
        private bool readOnly = false;
        private bool acceptsReturn = false;
        private IntPtr handle = IntPtr.Zero;
        private int maxLength = int.MaxValue;
        private bool useSystemPasswordChar = false;
        private bool isDisposed = false;
        private bool canFocus = true;
        private bool isFocused = false; // 表示当前是否已获得焦点
        private bool focused;

        // 当前聚焦的 shim TextBox（浏览器端无真实窗口，需 shim 自行维护焦点链）
        private static TextBox _active;

        // 属性
        public string[] Lines
        {
            get => lines.ToArray();
            set
            {
                if (value != null)
                {
                    lines = new List<string>(value);
                    ApplyMaxLines();
                }
            }
        }

        public Font Font
        {
            get => font;
            set
            {
                if (value != null)
                {
                    font = value;
                }
            }
        }

        public bool UseSystemPasswordChar
        {
            get => useSystemPasswordChar;
            set => useSystemPasswordChar = value;
        }

        public string Text
        {
            get => text;
            set
            {
                if (!readOnly)
                {
                    text = string.IsNullOrEmpty(value) ? string.Empty : (value.Length > maxLength ? value.Substring(0, maxLength) : value);
                    UpdateLines();
                    OnTextChanged(EventArgs.Empty);
                    selectionStart = text.Length; // 光标自动移到文本末尾
                }
            }
        }

        public int MaxLength
        {
            get => maxLength;
            set
            {
                if (maxLength != value)
                {
                    maxLength = value;
                    if (text.Length > maxLength)
                        text = text.Substring(0, maxLength);
                }
            }
        }

        public bool Multiline
        {
            get => multiline;
            set
            {
                multiline = value;
                UpdateLines();
            }
        }

        public int SelectionStart
        {
            get => selectionStart;
            set
            {
                selectionStart = Math.Clamp(value, 0, text.Length); // 确保光标位置在合法范围内
            }
        }

        public int SelectionLength
        {
            get => selectionLength;
            set
            {
                selectionLength = Math.Clamp(value, 0, text.Length - selectionStart); // 确保选择的长度不超出文本范围
            }
        }

        public Color BackColor
        {
            get => backColor;
            set => backColor = value;
        }

        public Color ForeColor
        {
            get => foreColor;
            set => foreColor = value;
        }

        public Point Location
        {
            get => location;
            set => location = value;
        }

        public Size Size
        {
            get => size;
            set => size = value;
        }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible != value)
                {
                    visible = value;
                    OnVisibleChanged(EventArgs.Empty);
                }
            }
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled != value)
                {
                    enabled = value;
                    OnEnabledChanged(EventArgs.Empty);
                }
            }
        }

        public BorderStyle BorderStyle
        {
            get => borderStyle;
            set => borderStyle = value;
        }

        public bool AcceptsReturn
        {
            get => acceptsReturn;
            set => acceptsReturn = value;
        }

        public bool AcceptsTab { get; set; }
        public Cursor Cursor { get; set; }
        public int TextLength => (text ?? string.Empty).Length;
        public IntPtr Handle => handle;

        public bool ReadOnly
        {
            get => readOnly;
            set => readOnly = value;
        }

        public object Tag
        {
            get => tag;
            set => tag = value;
        }

        public object Parent
        {
            get => parent;
            set
            {
                if (parent != value)
                {
                    parent = value;
                    OnParentChanged(EventArgs.Empty);
                }
            }
        }

        public bool CanFocus
        {
            get => canFocus;
            set => canFocus = value;
        }

        public bool IsDisposed
        {
            get => isDisposed;
            private set => isDisposed = value;
        }

        public bool IsFocused
        {
            get => isFocused;
            set
            {
                if (isFocused != value)
                {
                    isFocused = value;
                    if (isFocused)
                        OnGotFocus(EventArgs.Empty);
                    else
                        OnLostFocus(EventArgs.Empty);
                }
            }
        }


        // 新增 Focused 属性
        public bool Focused
        {
            get => focused;
            set
            {
                focused = value;
                // 在这里可以触发 GotFocus 和 LostFocus 事件
                if (focused)
                {
                    OnGotFocus(EventArgs.Empty);
                }
                else
                {
                    OnLostFocus(EventArgs.Empty);
                }
            }
        }

        public void AppendText(string value)
        {
            if (!readOnly)
            {
                text += value;
                UpdateLines();
                selectionStart = text.Length; // 将光标移到文本末尾
            }
        }

        public void Select(int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
        }

        public void SelectAll()
        {
            SelectionStart = 0;
            SelectionLength = text.Length;
        }

        public void Copy()
        {
            if (selectionLength > 0)
            {
                Console.WriteLine(text.Substring(selectionStart, selectionLength));
            }
        }

        public void SimulateKeyDown(Keys keyCode)
        {
            var args = new KeyEventArgs(keyCode);
            KeyDown?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;

            switch (keyCode)
            {
                case Keys.Back:
                    if (selectionLength > 0)
                    {
                        text = text.Remove(selectionStart, selectionLength);
                        selectionLength = 0;
                    }
                    else if (selectionStart > 0)
                    {
                        selectionStart--;
                        text = text.Remove(selectionStart, 1);
                    }
                    break;
                case Keys.Delete:
                    if (selectionLength > 0)
                        text = text.Remove(selectionStart, selectionLength);
                    else if (selectionStart < text.Length)
                        text = text.Remove(selectionStart, 1);
                    selectionLength = 0;
                    break;
                case Keys.Left:
                    if (selectionLength > 0) selectionLength = 0;
                    else selectionStart = Math.Max(0, selectionStart - 1);
                    break;
                case Keys.Right:
                    if (selectionLength > 0)
                    {
                        selectionStart = Math.Min(text.Length, selectionStart + selectionLength);
                        selectionLength = 0;
                    }
                    else selectionStart = Math.Min(text.Length, selectionStart + 1);
                    break;
                case Keys.Home:
                    selectionStart = 0;
                    selectionLength = 0;
                    break;
                case Keys.End:
                    selectionStart = text.Length;
                    selectionLength = 0;
                    break;
            }

            if (keyCode == Keys.Back || keyCode == Keys.Delete)
            {
                UpdateLines();
                OnTextChanged(EventArgs.Empty);
            }

            // 回车键需补发 KeyPress（WinForms 中 Enter 会触发 KeyPress，登录框等据此响应确认），
            // 否则原生覆盖层下按回车无法登录/确认。
            if (keyCode == Keys.Return)
                KeyPress?.Invoke(this, new KeyPressEventArgs((char)Keys.Return));
        }

        public void SimulateKeyPress(char keyChar)
        {
            var args = new KeyPressEventArgs(keyChar);
            KeyPress?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;

            InsertText(keyChar.ToString());
        }

        // 在光标处插入文本（替换选区），并受 MaxLength 约束。
        private void InsertText(string s)
        {
            if (string.IsNullOrEmpty(s)) return;

            int remaining = maxLength - (text.Length - selectionLength);
            int len = Math.Min(s.Length, Math.Max(0, remaining));
            if (len <= 0) return;
            s = s.Substring(0, len);

            text = text.Remove(selectionStart, selectionLength).Insert(selectionStart, s);
            selectionStart += s.Length;
            selectionLength = 0;

            UpdateLines();
            OnTextChanged(EventArgs.Empty);
        }

        public void SimulateMouseMove(int x, int y, MouseButtons button = MouseButtons.None)
        {
            var args = new MouseEventArgs(button, 0, x, y, 0);
            MouseMove?.Invoke(this, args);
        }

        public void SimulateMouseDown(int x, int y, MouseButtons button = MouseButtons.Left)
        {
            var args = new MouseEventArgs(button, 1, x, y, 0);
            MouseDown?.Invoke(this, args);
        }

        public void SimulateMouseUp(int x, int y, MouseButtons button = MouseButtons.Left)
        {
            var args = new MouseEventArgs(button, 1, x, y, 0);
            MouseUp?.Invoke(this, args);
        }

        public void Focus()
        {
            if (!CanFocus || !Enabled) return;
            if (_active == this) return;

            // 与 WinForms 一致：先把焦点从之前聚焦的框转移走（触发其 LostFocus），再聚焦本框。
            // MirTextBox 依赖 GotFocus/LostFocus 显示与收起原生 <input> 覆盖层，
            // 不触发事件会导致切换输入框后覆盖层不显示、无法继续输入。
            if (_active != null)
            {
                _active.IsFocused = false;
                _active.Focused = false;
                _active.LostFocus?.Invoke(_active, EventArgs.Empty);
            }

            _active = this;
            IsFocused = true;
            Focused = true;
            GotFocus?.Invoke(this, EventArgs.Empty);
        }

        // 主动失焦（供原生输入覆盖层在切换/失焦时调用），触发 LostFocus 事件。
        public void Blur()
        {
            if (_active == this) _active = null;
            if (!IsFocused && !Focused) return;

            IsFocused = false;
            Focused = false;
            LostFocus?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                isDisposed = true;
            }
        }

        // 供宿主工程（Web_Mir2 / Web_Mir3）的 TextBox 子类重写：浏览器端无真实窗口，
        // 这些方法仅保留 WinForms 签名以兼容原有的 override 代码。
        protected virtual void OnMouseClick(MouseEventArgs e) { }
        protected virtual void OnMouseUp(MouseEventArgs e) { }
        protected virtual void OnKeyDown(KeyEventArgs e) { }
        protected virtual void OnKeyUp(KeyEventArgs e) { }
        protected virtual void OnPreviewKeyDown(PreviewKeyDownEventArgs e) { }
        protected virtual void OnSizeChanged(EventArgs e) { }
        protected virtual void WndProc(ref Message m) { }

        public void Invalidate() { }
        public Graphics CreateGraphics() => null;
        public event EventHandler<PreviewKeyDownEventArgs> PreviewKeyDown;

        // 事件
        public event EventHandler TextChanged;
        public event EventHandler GotFocus;
        public event EventHandler LostFocus;
        public event EventHandler VisibleChanged;
        public event EventHandler EnabledChanged;
        public event EventHandler ParentChanged;
        public event KeyPressEventHandler KeyPress;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MouseUp;
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;
        public event MouseEventHandler MouseWheel;

        // 方法
        private void UpdateLines()
        {
            if (multiline)
            {
                lines = new List<string>(text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                ApplyMaxLines();
            }
            else
            {
                lines = new List<string> { text };
            }
        }

        private void ApplyMaxLines()
        {
            if (lines.Count > maxLines)
            {
                lines.RemoveRange(maxLines, lines.Count - maxLines);
            }
        }

        protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);
        protected virtual void OnVisibleChanged(EventArgs e) => VisibleChanged?.Invoke(this, e);
        protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);
        protected virtual void OnParentChanged(EventArgs e) => ParentChanged?.Invoke(this, e);
        protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);
        protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);

        // 获取光标位置的 x 和 y 坐标（像素），基于字符索引
        public Point GetPositionFromCharIndex(int index)
        {
            if (index < 0 || index > text.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            // 遍历每一行，找到该索引对应的行号和在该行内的位置
            int currentIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int lineLength = lines[i].Length;
                if (currentIndex + lineLength >= index)
                {
                    int charIndexInLine = index - currentIndex;
                    // 假设每个字符的宽度为一定值，根据Font的大小或实际情况可调整
                    int x = charIndexInLine * (int)font.Size; // 简化版计算字符的x位置
                    int y = i * (int)font.Size; // 行高按字体大小来定
                    return new Point(x, y);
                }
                currentIndex += lineLength + 1; // 加 1 是因为 \n
            }

            // 如果索引超出，返回最末位置
            return new Point(0, lines.Count * (int)font.Size);
        }

        // 根据字符索引返回它所处的行号
        public int GetLineFromCharIndex(int index)
        {
            if (index < 0 || index > text.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            int currentIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                currentIndex += lines[i].Length + 1; // 加 1 是因为换行符
                if (currentIndex > index)
                    return i;
            }

            return lines.Count - 1; // 如果超出文本长度，返回最后一行
        }

        // 以下为 Web_Mir2 工程用到、原仅定义在其本地 shim 副本中的成员；统一到本权威 shim 后补齐，
        // 使 Web_Mir2 不再需要自带重复的 TextBox 定义。
        public int GetCharIndexFromPosition(Point pt) => 0;
        public int GetFirstCharIndexFromLine(int line) => 0;
        public void ScrollToCaret() { }
    }
}
