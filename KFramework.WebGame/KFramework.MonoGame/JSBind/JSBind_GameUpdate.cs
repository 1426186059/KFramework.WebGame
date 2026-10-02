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

        /// <summary>
        /// 由 wwwroot/main.js 的渲染循环每帧调用。
        /// <para>
        /// <b>只带时间戳</b>，不带事件数据：事件由 C# 在本帧的 Update 里用
        /// <see cref="JSBind_GameFrameData.TakeFrameData"/> 回头取（一推一拉）。
        /// 之所以不随帧一并送来：JS→C# 的 byte[] 每帧都要新建一个托管数组，
        /// 而 C#→JS 能走 MemoryView 零拷贝 —— 每帧零分配，比省那一次跨界值钱得多。
        /// </para>
        /// </summary>
        [JSExport]
        public static void Frame(double timestampMs)
        {
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
