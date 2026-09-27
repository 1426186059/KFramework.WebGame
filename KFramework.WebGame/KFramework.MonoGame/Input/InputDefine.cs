namespace KFramework.MonoGame
{
    /// <summary>鼠标按键</summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2,
    }

    /// <summary>按键的瞬时状态</summary>
    public enum KPressState
    {
        /// <summary>未按下</summary>
        None,
        /// <summary>本帧刚按下</summary>
        Down,
        /// <summary>持续按住</summary>
        Held,
        /// <summary>本帧刚抬起</summary>
        Up,
    }

    public enum Keys : byte
    {
        None = 0,

        Backspace = 8,
        Tab = 9,
        Enter = 13,
        Shift = 16,
        Control = 17,
        Alt = 18,
        Escape = 27,

        Space = 32,

        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,

        Delete = 46,
        Home = 36,
        End = 35,

        D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52,
        D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,

        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73,
        J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
        S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,

        // 左右修饰键：浏览器的 KeyboardEvent.keyCode 不区分左右（Shift 恒为 16），
        // 因此这里作为同名别名存在，便于沿用 XNA/MonoGame 命名的代码直接编译。
        LeftShift = Shift,
        RightShift = Shift,
        LeftControl = Control,
        RightControl = Control,
        LeftAlt = Alt,
        RightAlt = Alt,
    }
}
