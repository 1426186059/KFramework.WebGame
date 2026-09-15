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
}
