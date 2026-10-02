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
            // 键盘 / 鼠标 / 触摸的事件由 GameFrameData 每帧【一次】跨界取回，再按模块分发到
            // 各自的 Consume —— 原先三者各 poll 一次（一帧 3 次跨界），现在合并为 1 次。
            // 各模块依旧独立：监听、状态、payload 格式都没动，只是"取数据"的通道统一了。
            GameFrameData.Update();

            // IME 走 [JSExport] 由 JS 主动推给 C#（OnDomValue / OnKeyDown），
            // 本来就不占这条拉取流，仍走自己的 Update。
            Input_IME.Update();
        }
        
        public static void LateUpdate()
        {
            Input_KeyBoard.LateUpdate();
            Input_Mouse.LateUpdate();
        }
    }
}
