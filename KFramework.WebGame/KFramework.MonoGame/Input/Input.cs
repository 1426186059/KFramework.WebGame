using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入总入口（协调层）。
    ///
    /// <para>一帧的事件由 <see cref="Input_GameFrameData"/> 一次取回（一推一拉：JS 推帧、C# 回头取），
    /// 再按统一事件类型分发给三个模块 —— 各模块只收语义化参数，不解析字节。</para>
    ///
    /// <para>具体状态与查询都在各自的封装里：<see cref="Input_KeyBoard"/> /
    /// <see cref="Input_Mouse"/> / <see cref="Input_Touch"/>。本类只做协调与组合查询。</para>
    /// </summary>
    internal static class Input
    {
        public static void Update()
        {
            Input_GameFrameData.Update();

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
