using System;
using System.Collections.Generic;

namespace MirEngine
{
    // 原 Web_Mir2.Engine/MirEngine/Shims/Forms/* 中需要的部分（TextBox 原样；TextRenderer 改为无 Browser* 依赖的桩）

    [Flags]
    public enum TextFormatFlags
    {
        DirectionRightToLeft = 0x1,
        DirectionVertical = 0x2,
        DisplayControlText = 0x4,
        NoPadding = 0x8,
        NoClipping = 0x10,
        ExternalLeading = 0x20,
        WordBreak = 0x40,
        SingleLine = 0x80,
        ExpandTabs = 0x100,
        TabStop = 0x200,
        NoPrefix = 0x400,
        Internal = 0x800,
        TextBoxControl = 0x1000,
        PathEllipsis = 0x2000,
        EndEllipsis = 0x4000,
        ModifyString = 0x8000,
        Right = 0x10000,
        Left = 0x20000,
        Center = 0x40000,
        Top = 0x80000,
        Bottom = 0x100000,
        VerticalCenter = 0x200000,
        WordEllipsis = 0x400000,
        HidePrefix = 0x800000,
        PrefixOnly = 0x1000000,
        PreserveGraphicsClipping = 0x2000000,
        PreserveGraphicsTranslateTransform = 0x4000000,
        NoWrap = 0x8000000,
        LeftAndRightPadding = 0x10000000,
        RightToLeft = 0x20000000,
        HorizontalCenter = 0x400000,
        LinkMeasureFlags = 0x2000000,
        Default = 0x0
    }

    public struct Message
    {
        public IntPtr HWnd;
        public int Msg;
        public IntPtr WParam;
        public IntPtr LParam;
        public IntPtr Result;
        public static Message Create(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
            => new Message { HWnd = hWnd, Msg = msg, WParam = wparam, LParam = lparam };
    }

    public class Cursor
    {
        public string Name;
        public static Cursor Current;
        public Cursor() { }
        public Cursor(string name) { Name = name; }
        public static implicit operator Cursor(string s) => new Cursor(s);
    }

    public static class Cursors
    {
        public static Cursor AppStarting = new Cursor("wait");
        public static Cursor Arrow = new Cursor("default");
        public static Cursor Cross = new Cursor("crosshair");
        public static Cursor Default = new Cursor("default");
        public static Cursor Hand = new Cursor("pointer");
        public static Cursor Help = new Cursor("help");
        public static Cursor HSplit = new Cursor("row-resize");
        public static Cursor IBeam = new Cursor("text");
        public static Cursor No = new Cursor("not-allowed");
        public static Cursor NoMove2D = new Cursor("not-allowed");
        public static Cursor SizeAll = new Cursor("move");
        public static Cursor SizeNESW = new Cursor("nesw-resize");
        public static Cursor SizeNS = new Cursor("ns-resize");
        public static Cursor SizeNWSE = new Cursor("nwse-resize");
        public static Cursor SizeWE = new Cursor("ew-resize");
        public static Cursor UpArrow = new Cursor("n-resize");
        public static Cursor VSplit = new Cursor("col-resize");
        public static Cursor WaitCursor = new Cursor("wait");
    }

    public class Timer
    {
        public event EventHandler Tick;
        public int Interval;
        public bool Enabled;
        public void Start() { Enabled = true; }
        public void Stop() { Enabled = false; }
        public void Dispose() { }
    }

    public static class Application
    {
        public static void Run() { }
        public static void Run(object f) { }
        public static void Exit() { }
        public static void DoEvents() { }
        public static void SetSuspendPower(bool enabled) { }
        public static event EventHandler ApplicationExit;
        public static event EventHandler ThreadException;
        public static event EventHandler Idle;
        public static string ProductVersion => "1.0.0.0";
        public static string ExecutablePath => "";
        public static string StartupPath => "/";
        public static void EnableVisualStyles() { }
        public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }
        public static void Restart() { }
    }

    // 文本度量/绘制桩：KFramework.MonoGame 迁移后真实文字绘制需走其文本/纹理通道，这里先保证编译。
    public static class TextRenderer
    {
        public static Size MeasureText(string text, Font font) => Size.Empty;
        public static Size MeasureText(string text, Font font, Size proposedSize) => Size.Empty;
        public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags) => Size.Empty;
        public static void DrawText(Graphics g, string text, Font font, Point pt, Color foreColor) { }
        public static void DrawText(Graphics g, string text, Font font, Point pt, Color foreColor, Color backColor) { }
        public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color foreColor) { }
        public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags) { }
        public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, TextFormatFlags flags) { }

        // 带 Graphics 参数的重载（原 Web_Mir2.Engine 的 TextRenderer 首参为 Graphics；CMain.Graphics 为 GraphicsStub，故用 object 兼容）。
        public static Size MeasureText(object g, string text, Font font) => Size.Empty;
        public static Size MeasureText(object g, string text, Font font, Size size, TextFormatFlags flags) => Size.Empty;
    }

    public static class SystemInformation
    {
        public static int MouseButtons => 2;
        public static bool MouseWheelPresent => true;
        public static int BorderSize => 1;
        public static int Border3DSizeWidth => 2;
        public static int Border3DSizeHeight => 2;
        public static int FrameBorderSize => 1;
        public static int CaptionHeight => 22;
        public static int MenuHeight => 19;
        public static int ToolWindowCaptionHeight => 16;
        public static int DoubleClickTime => 500;
        public static int MouseWheelScrollDelta => 120;
    }

    // ===== TextBox（原 Forms/TextBox.cs 原样；其引用类型均已在本工程 Shims 提供）=====
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
        private bool enabled = true;
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
        private bool isFocused = false;
        private bool focused;

        private static TextBox _active;

        public string[] Lines
        {
            get => lines.ToArray();
            set { if (value != null) { lines = new List<string>(value); ApplyMaxLines(); } }
        }
        public Font Font
        {
            get => font;
            set { if (value != null) font = value; }
        }
        public bool UseSystemPasswordChar { get => useSystemPasswordChar; set => useSystemPasswordChar = value; }
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
                    selectionStart = text.Length;
                }
            }
        }
        public int MaxLength { get => maxLength; set { if (maxLength != value) { maxLength = value; if (text.Length > maxLength) text = text.Substring(0, maxLength); } } }
        public bool Multiline { get => multiline; set { multiline = value; UpdateLines(); } }
        public int SelectionStart { get => selectionStart; set => selectionStart = Math.Clamp(value, 0, text.Length); }
        public int SelectionLength { get => selectionLength; set => selectionLength = Math.Clamp(value, 0, text.Length - selectionStart); }
        public Color BackColor { get => backColor; set => backColor = value; }
        public Color ForeColor { get => foreColor; set => foreColor = value; }
        public Point Location { get => location; set => location = value; }
        public Size Size { get => size; set => size = value; }
        public bool Visible
        {
            get => visible;
            set { if (visible != value) { visible = value; OnVisibleChanged(EventArgs.Empty); } }
        }
        public bool Enabled
        {
            get => enabled;
            set { if (enabled != value) { enabled = value; OnEnabledChanged(EventArgs.Empty); } }
        }
        public BorderStyle BorderStyle { get => borderStyle; set => borderStyle = value; }
        public bool AcceptsReturn { get => acceptsReturn; set => acceptsReturn = value; }
        public bool AcceptsTab { get; set; }
        public Cursor Cursor { get; set; }
        public int TextLength => (text ?? string.Empty).Length;
        public IntPtr Handle => handle;
        public bool ReadOnly { get => readOnly; set => readOnly = value; }
        public object Tag { get => tag; set => tag = value; }
        public object Parent
        {
            get => parent;
            set { if (parent != value) { parent = value; OnParentChanged(EventArgs.Empty); } }
        }
        public bool CanFocus { get => canFocus; set => canFocus = value; }
        public bool IsDisposed { get => isDisposed; private set => isDisposed = value; }
        public bool IsFocused
        {
            get => isFocused;
            set
            {
                if (isFocused != value)
                {
                    isFocused = value;
                    if (isFocused) OnGotFocus(EventArgs.Empty); else OnLostFocus(EventArgs.Empty);
                }
            }
        }
        public bool Focused
        {
            get => focused;
            set
            {
                focused = value;
                if (focused) OnGotFocus(EventArgs.Empty); else OnLostFocus(EventArgs.Empty);
            }
        }

        public void AppendText(string value)
        {
            if (!readOnly) { text += value; UpdateLines(); selectionStart = text.Length; }
        }
        public void Select(int start, int length) { SelectionStart = start; SelectionLength = length; }
        public void SelectAll() { SelectionStart = 0; SelectionLength = text.Length; }
        public void Copy() { if (selectionLength > 0) Console.WriteLine(text.Substring(selectionStart, selectionLength)); }

        public void SimulateKeyDown(Keys keyCode)
        {
            var args = new KeyEventArgs(keyCode);
            KeyDown?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;
            switch (keyCode)
            {
                case Keys.Back:
                    if (selectionLength > 0) { text = text.Remove(selectionStart, selectionLength); selectionLength = 0; }
                    else if (selectionStart > 0) { selectionStart--; text = text.Remove(selectionStart, 1); }
                    break;
                case Keys.Delete:
                    if (selectionLength > 0) text = text.Remove(selectionStart, selectionLength);
                    else if (selectionStart < text.Length) text = text.Remove(selectionStart, 1);
                    selectionLength = 0;
                    break;
                case Keys.Left:
                    if (selectionLength > 0) selectionLength = 0;
                    else selectionStart = Math.Max(0, selectionStart - 1);
                    break;
                case Keys.Right:
                    if (selectionLength > 0) { selectionStart = Math.Min(text.Length, selectionStart + selectionLength); selectionLength = 0; }
                    else selectionStart = Math.Min(text.Length, selectionStart + 1);
                    break;
                case Keys.Home: selectionStart = 0; selectionLength = 0; break;
                case Keys.End: selectionStart = text.Length; selectionLength = 0; break;
            }
            if (keyCode == Keys.Back || keyCode == Keys.Delete) { UpdateLines(); OnTextChanged(EventArgs.Empty); }
            if (keyCode == Keys.Return) KeyPress?.Invoke(this, new KeyPressEventArgs((char)Keys.Return));
        }

        public void SimulateKeyPress(char keyChar)
        {
            var args = new KeyPressEventArgs(keyChar);
            KeyPress?.Invoke(this, args);
            if (args.Handled) return;
            if (!Enabled || ReadOnly) return;
            InsertText(keyChar.ToString());
        }

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

        public void SimulateMouseMove(int x, int y, MouseButtons button = MouseButtons.None) => MouseMove?.Invoke(this, new MouseEventArgs(button, 0, x, y, 0));
        public void SimulateMouseDown(int x, int y, MouseButtons button = MouseButtons.Left) => MouseDown?.Invoke(this, new MouseEventArgs(button, 1, x, y, 0));
        public void SimulateMouseUp(int x, int y, MouseButtons button = MouseButtons.Left) => MouseUp?.Invoke(this, new MouseEventArgs(button, 1, x, y, 0));

        public void Focus()
        {
            if (!CanFocus || !Enabled) return;
            if (_active == this) return;
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

        public void Blur()
        {
            if (_active == this) _active = null;
            if (!IsFocused && !Focused) return;
            IsFocused = false;
            Focused = false;
            LostFocus?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing) { if (disposing) isDisposed = true; }

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

        private void UpdateLines()
        {
            if (multiline) { lines = new List<string>(text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)); ApplyMaxLines(); }
            else lines = new List<string> { text };
        }
        private void ApplyMaxLines() { if (lines.Count > maxLines) lines.RemoveRange(maxLines, lines.Count - maxLines); }

        protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);
        protected virtual void OnVisibleChanged(EventArgs e) => VisibleChanged?.Invoke(this, e);
        protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);
        protected virtual void OnParentChanged(EventArgs e) => ParentChanged?.Invoke(this, e);
        protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);
        protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);

        public Point GetPositionFromCharIndex(int index)
        {
            if (index < 0 || index > text.Length) throw new ArgumentOutOfRangeException(nameof(index));
            int currentIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                int lineLength = lines[i].Length;
                if (currentIndex + lineLength >= index)
                {
                    int charIndexInLine = index - currentIndex;
                    int x = charIndexInLine * (int)font.Size;
                    int y = i * (int)font.Size;
                    return new Point(x, y);
                }
                currentIndex += lineLength + 1;
            }
            return new Point(0, lines.Count * (int)font.Size);
        }

        public int GetLineFromCharIndex(int index)
        {
            if (index < 0 || index > text.Length) throw new ArgumentOutOfRangeException(nameof(index));
            int currentIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                currentIndex += lines[i].Length + 1;
                if (currentIndex > index) return i;
            }
            return lines.Count - 1;
        }

        public int GetCharIndexFromPosition(Point pt) => 0;
        public int GetFirstCharIndexFromLine(int line) => 0;
        public void ScrollToCaret() { }
    }
}
