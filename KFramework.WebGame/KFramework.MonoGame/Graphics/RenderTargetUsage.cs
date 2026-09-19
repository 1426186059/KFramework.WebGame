namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染目标的内容保留策略（照 MonoGame 的 RenderTargetUsage）。
    /// <para>
    /// 影响的是「每次 SetRenderTarget 绑定时是否自动清屏」：XNA 时代因为 Xbox 的硬件限制，
    /// 非 PreserveContents 的目标在绑定时会被清掉；WebGL 天然保留内容，因此 DiscardContents
    /// 需要在绑定后显式 <see cref="GraphicsDevice.Clear"/>，PreserveContents 则什么都不做。
    /// </para>
    /// </summary>
    public enum RenderTargetUsage
    {
        /// <summary>每次绑定都清空（XNA 兼容行为，也是默认值）。</summary>
        DiscardContents,

        /// <summary>绑定时保留上次绘制的内容（用于累积绘制 / 分帧叠加）。</summary>
        PreserveContents,

        /// <summary>由平台自行决定；本后端等同 <see cref="PreserveContents"/>。</summary>
        PlatformContents,
    }
}
