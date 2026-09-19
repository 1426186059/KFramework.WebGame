namespace KFramework.MonoGame
{
    /// <summary>
    /// <see cref="PresentInterval"/> 的转换工具（照 MonoGame 的 <c>GraphicsExtensions.GetSwapInterval</c>）。
    /// </summary>
    public static class PresentIntervalExtensions
    {
        /// <summary>
        /// 转成「垂直同步间隔」（EXT_swap_control 语义，与 MonoGame 完全一致）。
        /// </summary>
        /// <param name="interval">呈现间隔。</param>
        /// <returns>Immediate = 0，One = 1，Two = 2，Default = -1（自适应 vsync）。</returns>
        public static int ToSwapInterval(this PresentInterval interval) => interval switch
        {
            PresentInterval.Immediate => 0,
            PresentInterval.One => 1,
            PresentInterval.Two => 2,
            _ => -1,
        };

        /// <summary>
        /// 转成浏览器主循环的「每几个垂直同步画一帧」（≥ 1）。
        /// </summary>
        /// <remarks>
        /// 浏览器里 rAF 本身就是垂直同步，没有「不等垂直同步」这一说（Immediate / 自适应都等价于 1），
        /// 所以 0 与 -1 一律按 1 处理，Two 才是真正的限帧（半刷新率）。
        /// </remarks>
        public static int ToFramesPerPresent(this PresentInterval interval)
        {
            int swap = interval.ToSwapInterval();
            return swap < 1 ? 1 : swap;
        }
    }
}
