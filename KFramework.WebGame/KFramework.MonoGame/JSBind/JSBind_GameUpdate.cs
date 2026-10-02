using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 游戏帧循环（Game Update）的 C#/JS 绑定。
    /// <list type="bullet">
    ///   <item><description><see cref="Frame"/> 为 [JSExport]，由 TS 主循环（src/game_update.ts 编译出的 game_update.js）
    ///     每帧回调，转发到 <see cref="Game.TickFrame"/>。</description></item>
    ///   <item><description>StartRenderLoop / SetFrameInterval / GetFrameInterval 为 [JSImport]，映射到
    ///     src/game_update.ts 的 "game_update" 模块（独立于 platform），驱动 requestAnimationFrame 主循环与限帧。</description></item>
    /// </list>
    /// 依赖 KFramework.TSEngine 项目。
    /// </summary>
    public static partial class JSBind_GameUpdate
    {
        /// <summary>当前在跑的游戏实例；JS 每帧回调 Frame 时据此转发到 TickFrame。</summary>
        internal static Game? Current;

        /// <summary>由 wwwroot/main.js 的渲染循环调用。</summary>
        /// <summary>
        /// 由 wwwroot/main.js 的渲染循环每帧调用。
        /// <para>
        /// <paramref name="events"/> 是本帧所有输入模块的事件流（由 game_frame_take_js_data 汇总，
        /// 布局见 <see cref="GameFrameData"/>）。数据随帧回调<b>一并送入</b>，于是每帧只需
        /// 【一次】跨界（JS→C#），不必再让 C# 回头去 JS 取 —— 原先是两次。
        /// </para>
        /// </summary>
        [JSExport]
        public static void Frame(double timestampMs, byte[]? events)
        {
            GameFrameData.Receive(events);
            Current?.TickFrame(timestampMs);
        }

        /// <summary>启动 requestAnimationFrame 主循环，之后每帧回调 <c>JSBind_GameUpdate.Frame</c>。</summary>
        [JSImport("startRenderLoop", "game_update")]
        public static partial void StartRenderLoop();

        /// <summary>
        /// 设置呈现间隔：每 N 个垂直同步（rAF）回调一帧（N ≥ 1）。
        /// 对应 MonoGame 的 swapInterval，浏览器里由主循环跳帧实现。
        /// </summary>
        [JSImport("setFrameInterval", "game_update")]
        public static partial void SetFrameInterval(int interval);

        /// <summary>当前呈现间隔（1 = 每个垂直同步都画）。</summary>
        [JSImport("getFrameInterval", "game_update")]
        public static partial int GetFrameInterval();
    }
}
