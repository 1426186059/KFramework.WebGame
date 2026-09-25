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
            Input_IME.Poll();
        }

        /// <summary>每个固定步结束后由 <see cref="Game"/> 调用：清空键盘/鼠标的按下、抬起边沿，
        /// 使一次按键 / 一次点击只被识别一次。
        ///
        /// <para>固定步长下，一个渲染帧可能跑多个 Update 步，但 <see cref="Poll"/> 每帧只调用一次。
        /// 若不在每个步后清理边沿，<c>GetKeyDown</c> / <c>GetButtonDown</c> 会在该帧的所有步里都返回
        /// true，导致"按一次"的逻辑（如跳跃）被重复触发，结果随帧时序抖动（忽高忽低）。
        /// 边沿在下一帧 <see cref="Poll"/> 时重新产生。</para>
        ///
        /// <para>触摸边沿由 per-frame 的帧列表承载，不在本方法内消费（另行处理）。</para>
        /// </summary>
        public static void ConsumeStepEdges()
        {
            Input_KeyBoard.ConsumeEdges();
            Input_Mouse.ConsumeEdges();
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

        /// <summary>释放三个输入模块（键盘 / 鼠标 / 触摸）的底层资源。幂等，可安全重复调用。</summary>
        public static void Dispose()
        {
            Input_KeyBoard.Instance.Dispose();
            Input_Mouse.Instance.Dispose();
            Input_Touch.Instance.Dispose();
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
