namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染目标附带的深度 / 模板缓冲格式（照 MonoGame 的 DepthFormat）。
    /// </summary>
    public enum DepthFormat
    {
        /// <summary>不附带深度缓冲（纯颜色渲染目标，2D 离屏常用）。</summary>
        None,

        /// <summary>16 位深度。</summary>
        Depth16,

        /// <summary>24 位深度。</summary>
        Depth24,

        /// <summary>24 位深度 + 8 位模板（WebGL2 用 DEPTH24_STENCIL8，depth 与 stencil 共用同一个 renderbuffer）。</summary>
        Depth24Stencil8,
    }
}
