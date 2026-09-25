using System;
using Client;
using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace MirEngine
{
    // 原 Web_Mir2.Engine/MirEngine/Shims/Forms/* 中需要的部分（TextRenderer 已移除，文本度量统一改用 TextRenderer.MeasureText）

    // TextFormatFlags 已上移到引擎通用库：KFramework.MonoGame.TextFormatFlags
    // （见 Shims/GlobalUsings.cs 的全局别名，位值与此处原定义完全一致）。

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

    public enum BorderStyle
    {
        None,
        FixedSingle,
        Fixed3D
    }

    // 轻量适配层：把引擎自带的 KFramework.MonoGame.TextBox（位于 KFramework.MonoGame.TextRenderer）
    // 暴露成本工程既有的 WinForms 风格 API（MirEngine.Color / Font / Point / Size / Keys / 事件参数），
    // 所有调用方（MirTextBox 及各个对话框处理器）按原签名直接使用，无需改动。
    // 引擎侧保持自洽（只用引擎类型），类型桥接集中在本适配器内完成。
    public class TextBox : IDisposable
    {
        private readonly KFramework.MonoGame.TextBox _inner = new KFramework.MonoGame.TextBox();

        public string Text { get => _inner.Text; set => _inner.Text = value; }
        public string[] Lines { get => _inner.Lines; set => _inner.Lines = value; }
        public int MaxLength { get => _inner.MaxLength; set => _inner.MaxLength = value; }
        public bool Multiline { get => _inner.Multiline; set => _inner.Multiline = value; }
        public int SelectionStart { get => _inner.SelectionStart; set => _inner.SelectionStart = value; }
        public int SelectionLength { get => _inner.SelectionLength; set => _inner.SelectionLength = value; }
        public string CompositionString { get => _inner.CompositionString; }
        public bool UseSystemPasswordChar { get => _inner.UseSystemPasswordChar; set => _inner.UseSystemPasswordChar = value; }
        public bool Visible { get => _inner.Visible; set => _inner.Visible = value; }
        public bool Enabled { get => _inner.Enabled; set => _inner.Enabled = value; }
        public bool AcceptsReturn { get => _inner.AcceptsReturn; set => _inner.AcceptsReturn = value; }
        public bool AcceptsTab { get => _inner.AcceptsTab; set => _inner.AcceptsTab = value; }
        public int TextLength => _inner.TextLength;
        public IntPtr Handle { get => _inner.Handle; set => _inner.Handle = value; }
        public bool ReadOnly { get => _inner.ReadOnly; set => _inner.ReadOnly = value; }
        public object Tag { get => _inner.Tag; set => _inner.Tag = value; }
        public object Parent { get => _inner.Parent; set => _inner.Parent = value; }
        public bool CanFocus { get => _inner.CanFocus; set => _inner.CanFocus = value; }
        public bool IsDisposed => _inner.IsDisposed;
        public bool IsFocused { get => _inner.IsFocused; set => _inner.IsFocused = value; }
        public bool Focused { get => _inner.Focused; set => _inner.Focused = value; }

        public Color BackColor
        {
            get => new Color(_inner.BackColor.A, _inner.BackColor.R, _inner.BackColor.G, _inner.BackColor.B);
            set => _inner.BackColor = value;
        }

        public Color ForeColor
        {
            get => new Color(_inner.ForeColor.A, _inner.ForeColor.R, _inner.ForeColor.G, _inner.ForeColor.B);
            set => _inner.ForeColor = value;
        }

        public Point Location { get => _inner.Location; set => _inner.Location = value; }
        public Size Size { get => _inner.Size; set => _inner.Size = value; }

        public Font Font
        {
            get => (Font)_inner.Font;
            set => _inner.Font = value;
        }

        public BorderStyle BorderStyle
        {
            get => (BorderStyle)(int)_inner.BorderStyle;
            set => _inner.BorderStyle = (KFramework.MonoGame.BorderStyle)(int)value;
        }

        public void AppendText(string value) => _inner.AppendText(value);

        public void SimulateKeyDown(Keys keyCode) => _inner.SimulateKeyDown(ToEngineKeys(keyCode));
        public void SimulateKeyPress(char keyChar) => _inner.SimulateKeyPress(keyChar);

        public void Focus()
        {
            // 按当前视口计算逻辑→后备缓冲像素缩放（本工程恒等变换下为 1，但保留公式以兼容真实缩放部署）。
            var vp = DXManager.GDevice.Viewport;
            float scale = vp.Height > 0 && Client.Settings.ScreenHeight > 0
                ? (float)vp.Height / Client.Settings.ScreenHeight
                : 1f;
            _inner.OverlayScale = scale;
            if (Font != null)
            {
                _inner.OverlayFontCss = FontFactory.BuildCssFont(Font, scale);
                _inner.OverlayFontPx = (float)FontFactory.GetPixelSize(Font, scale);
            }
            _inner.Focus();
        }
        public void LoseFocus() => _inner.Blur();
        public void Dispose() => _inner.Dispose();

        public Point GetPositionFromCharIndex(int index) => _inner.GetPositionFromCharIndex(index);
        public int GetLineFromCharIndex(int index) => _inner.GetLineFromCharIndex(index);
        public int GetFirstCharIndexFromLine(int line) => _inner.GetFirstCharIndexFromLine(line);
        public int GetCharIndexFromPosition(Point pt) => _inner.GetCharIndexFromPosition(pt);
        public void ScrollToCaret() => _inner.ScrollToCaret();

        public event EventHandler TextChanged { add => _inner.TextChanged += value; remove => _inner.TextChanged -= value; }
        public event EventHandler GotFocus { add => _inner.GotFocus += value; remove => _inner.GotFocus -= value; }
        public event EventHandler LostFocus { add => _inner.LostFocus += value; remove => _inner.LostFocus -= value; }
        public event EventHandler VisibleChanged { add => _inner.VisibleChanged += value; remove => _inner.VisibleChanged -= value; }
        public event EventHandler EnabledChanged { add => _inner.EnabledChanged += value; remove => _inner.EnabledChanged -= value; }
        public event EventHandler ParentChanged { add => _inner.ParentChanged += value; remove => _inner.ParentChanged -= value; }

        public event KeyPressEventHandler KeyPress;
        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;
        public event MouseEventHandler MouseMove;
        public event MouseEventHandler MouseDown;
        public event MouseEventHandler MouseUp;
        public event MouseEventHandler MouseWheel;

        public TextBox()
        {
            _inner.KeyPress += (s, e) => KeyPress?.Invoke(this, new KeyPressEventArgs(e.KeyChar) { Handled = e.Handled });
            _inner.KeyDown += (s, e) => KeyDown?.Invoke(this, new KeyEventArgs(ToMirEngineKeys(e.KeyCode)) { Handled = e.Handled, SuppressKeyPress = e.SuppressKeyPress });
            _inner.KeyUp += (s, e) => KeyUp?.Invoke(this, new KeyEventArgs(ToMirEngineKeys(e.KeyCode)) { Handled = e.Handled, SuppressKeyPress = e.SuppressKeyPress });
            _inner.MouseMove += (s, e) => MouseMove?.Invoke(this, new MouseEventArgs(ToMirEngineButton(e.Button), e.Clicks, e.X, e.Y, e.Delta));
            _inner.MouseDown += (s, e) => MouseDown?.Invoke(this, new MouseEventArgs(ToMirEngineButton(e.Button), e.Clicks, e.X, e.Y, e.Delta));
            _inner.MouseUp += (s, e) => MouseUp?.Invoke(this, new MouseEventArgs(ToMirEngineButton(e.Button), e.Clicks, e.X, e.Y, e.Delta));
            _inner.MouseWheel += (s, e) => MouseWheel?.Invoke(this, new MouseEventArgs(ToMirEngineButton(e.Button), e.Clicks, e.X, e.Y, e.Delta));
        }

        private static KFramework.MonoGame.Keys ToEngineKeys(Keys k)
        {
            switch (k)
            {
                case Keys.Back: return KFramework.MonoGame.Keys.Backspace;
                case Keys.Return: return KFramework.MonoGame.Keys.Enter;
                default: return (KFramework.MonoGame.Keys)(int)k;
            }
        }

        private static Keys ToMirEngineKeys(KFramework.MonoGame.Keys k)
        {
            switch (k)
            {
                case KFramework.MonoGame.Keys.Backspace: return Keys.Back;
                case KFramework.MonoGame.Keys.Enter: return Keys.Return;
                default: return (Keys)(int)k;
            }
        }

        private static MouseButtons ToMirEngineButton(KFramework.MonoGame.TextBox.MouseButtons b)
            => (MouseButtons)(int)b;
    }
}
