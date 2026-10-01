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
    internal static class Input
    {
        public static void Update()
        {
            Input_KeyBoard.Update();
            Input_Mouse.Update();
            Input_Touch.Update();
            Input_IME.Update();
        }
        
        public static void LateUpdate()
        {
            Input_KeyBoard.LateUpdate();
            Input_Mouse.LateUpdate();
        }
    }
}
