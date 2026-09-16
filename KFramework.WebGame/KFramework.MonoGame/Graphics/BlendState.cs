namespace KFramework.MonoGame
{
    /// <summary>混合模式。</summary>
    public sealed class BlendState
    {
        public readonly int SourceBlend;
        public readonly int DestinationBlend;
        public readonly int SourceAlphaBlend;
        public readonly int DestinationAlphaBlend;

        private BlendState(int src, int dst, int srcA, int dstA)
        {
            SourceBlend = src; DestinationBlend = dst;
            SourceAlphaBlend = srcA; DestinationAlphaBlend = dstA;
        }

        public static readonly BlendState AlphaBlend =
            new(JSBind_GL.ONE, JSBind_GL.ONE_MINUS_SRC_ALPHA, JSBind_GL.ONE, JSBind_GL.ONE_MINUS_SRC_ALPHA);

        public static readonly BlendState NonPremultiplied =
            new(JSBind_GL.SRC_ALPHA, JSBind_GL.ONE_MINUS_SRC_ALPHA, JSBind_GL.SRC_ALPHA, JSBind_GL.ONE_MINUS_SRC_ALPHA);

        /// <summary>叠加发光，用于粒子、爆炸、激光。</summary>
        public static readonly BlendState Additive =
            new(JSBind_GL.SRC_ALPHA, JSBind_GL.ONE, JSBind_GL.SRC_ALPHA, JSBind_GL.ONE);

        public static readonly BlendState Opaque =
            new(JSBind_GL.ONE, JSBind_GL.ZERO, JSBind_GL.ONE, JSBind_GL.ZERO);
    }
}
