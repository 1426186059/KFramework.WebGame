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

        /// <summary>取回键盘 / 鼠标 / 触摸 / IME 的事件并更新状态。引擎 <see cref="Game"/> 不再代劳，
        /// 需由各外部游戏在自己的 <c>Update</c> 里自行驱动（通常每个渲染帧 / 每个固定步调用一次）。</summary>
        public static void Update()
        {
            Input_KeyBoard.Update();
            Input_Mouse.Update();
            Input_Touch.Update();
            Input_IME.Update();
        }

        /// <summary>激活全部输入设备（键盘重新绑定 JS 监听；鼠标 / 触摸 / IME 在脚本加载时即自动绑定，此处为空操作）。</summary>
        public static void Activate()
        {
            Input_KeyBoard.Activate();
            Input_Mouse.Activate();
            Input_Touch.Activate();
            Input_IME.Activate();
        }

        public static void Deactivate()
        {
            Input_KeyBoard.Deactivate();
            Input_Mouse.Deactivate();
            Input_Touch.Deactivate();
            Input_IME.Deactivate();
        }

        /// <summary>清空键盘/鼠标的按下、抬起边沿，使一次按键 / 一次点击只被识别一次。
        /// 由游戏在每个固定步结束后自行调用（引擎 <see cref="Game"/> 不再代劳）。
        ///
        /// <para>固定步长下，一个渲染帧可能跑多个 Update 步，但 <see cref="Update"/> 每帧只调用一次。
        /// 若不在每个步后清理边沿，<c>GetKeyDown</c> / <c>GetButtonDown</c> 会在该帧的所有步里都返回
        /// true，导致"按一次"的逻辑（如跳跃）被重复触发，结果随帧时序抖动（忽高忽低）。
        /// 边沿在下一帧 <see cref="Update"/> 时重新产生。</para>
        ///
        /// <para>触摸边沿由 per-frame 的帧列表承载，不在本方法内消费（另行处理）。</para>
        /// </summary>
        public static void LateUpdate()
        {
            Input_KeyBoard.LateUpdate();
            Input_Mouse.LateUpdate();
        }
    }
}
