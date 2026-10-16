using System;

namespace MirEngine
{
    // Mir2 客户端使用的 WinForms 兼容鼠标/键事件参数。
    // Keys 类型由 GlobalUsings 的 `global using Keys = KFramework.MonoGame.Keys;` 提供；
    // 修饰键不再走 65536 高位掩码，改为独立 bool（来源：MG.Input_KeyBoard.Shift/Ctrl/Alt）。
    // KeyEventHandler / MouseEventHandler / KeyPressEventHandler 委托由本工程 WinFormsExtra.cs 统一提供。

    public class MouseEventArgs : EventArgs
    {
        public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
        {
            Button = button; Clicks = clicks; X = x; Y = y; Delta = delta;
        }
        public MouseButtons Button { get; }
        public int Clicks { get; }
        public int X { get; }
        public int Y { get; }
        public int Delta { get; }
        public Point Location => new Point(X, Y);
    }

    public class KeyEventArgs : EventArgs
    {
        public KeyEventArgs(Keys keyCode, bool shift = false, bool control = false, bool alt = false)
        {
            KeyCode = keyCode; Shift = shift; Control = control; Alt = alt;
        }
        public Keys KeyCode { get; }
        public bool Shift { get; }
        public bool Control { get; }
        public bool Alt { get; }
        public Keys KeyData => KeyCode;
        public bool Handled { get; set; }
        public bool SuppressKeyPress { get; set; }
    }

    public class KeyPressEventArgs : EventArgs
    {
        public KeyPressEventArgs(Keys keyCode, bool shift = false, bool control = false, bool alt = false)
        {
            KeyCode = keyCode; Shift = shift; Control = control; Alt = alt;
        }
        public Keys KeyCode { get; }
        public bool Shift { get; }
        public bool Control { get; }
        public bool Alt { get; }
        public bool Handled { get; set; }
    }

    public class PreviewKeyDownEventArgs : EventArgs
    {
        public PreviewKeyDownEventArgs(Keys keyData, bool shift = false, bool control = false, bool alt = false)
        {
            KeyData = keyData; Shift = shift; Control = control; Alt = alt;
        }
        public Keys KeyData { get; }
        public bool Shift { get; }
        public bool Control { get; }
        public bool Alt { get; }
        public bool IsInputKey { get; set; }
    }
}
