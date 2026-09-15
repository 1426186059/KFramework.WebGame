using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入总入口（协调层）。
    ///
    /// <para>键盘 / 鼠标 / 触摸三个模块各自 poll 自己的事件队列（各一次跨界调用）。
    /// 一帧共 3 次跨界，对输入这种低频操作完全可以接受，换来的是三个模块彼此独立。</para>
    ///
    /// <para>具体状态与查询都在各自的封装里：<see cref="Input_KeyBoard"/> /
    /// <see cref="Input_Mouse"/> / <see cref="Input_Touch"/>。本类只做协调与组合查询。</para>
    /// </summary>
    public static class Input
    {
        /// <summary>是否移动端平台（纯 .NET 判断，不跨界）。</summary>
        public static bool IsMobileDevice => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>每帧由 <see cref="Game"/> 调用一次：三个模块各自取回自己的事件。</summary>
        public static void Poll()
        {
            Input_KeyBoard.Poll();
            Input_Mouse.Poll();
            Input_Touch.Poll();
        }

        /// <summary>清空三个模块的状态。</summary>
        public static void Reset()
        {
            Input_KeyBoard.Reset();
            Input_Mouse.Reset();
            Input_Touch.Reset();
        }

        /// <summary>解绑三个模块在 JS 侧的监听（切场景 / 销毁时调用）。</summary>
        public static void Unbind()
        {
            Input_KeyBoard.Unbind();
            Input_Mouse.Unbind();
            Input_Touch.Unbind();
        }

        /// <summary>触屏或鼠标左键当前是否按住（移动端虚拟摇杆等统一处理）。</summary>
        public static bool IsPointerDown
            => Input_Mouse.GetButton(MouseButton.Left) || Input_Touch.Touches.Count > 0;

        // ===== 组合快照（兼容旧用法） =====

        public static KeyboardState GetKeyboardState()
            => new(Input_KeyBoard.Held, Input_KeyBoard.Pressed, Input_KeyBoard.Released);

        public static MouseState GetMouseState()
            => new(Input_Mouse.X, Input_Mouse.Y, Input_Mouse.Buttons,
                   Input_Mouse.ScrollDelta, Input_Mouse.PreviousButtons);

        public static TouchCollection GetTouchState() => new(Input_Touch.Touches);
    }
}
