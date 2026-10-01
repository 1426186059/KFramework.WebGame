namespace KFramework.MonoGame
{
    /// <summary>混合模式。</summary>
    public sealed class BlendState
    {
        public readonly int SourceBlend;
        public readonly int DestinationBlend;
        public readonly int SourceAlphaBlend;
        public readonly int DestinationAlphaBlend;

        /// <summary>构造自定义混合状态（把混合参数直接传进来）。</summary>
        /// <param name="src">颜色源因子（如 <see cref="JSBind_WEBGL20"/>.SRC_ALPHA / ZERO / SRC_COLOR）。</param>
        /// <param name="dst">颜色目标因子（如 ONE / ONE_MINUS_SRC_ALPHA / SRC_COLOR）。</param>
        /// <param name="srcA">Alpha 源因子。</param>
        /// <param name="dstA">Alpha 目标因子。</param>
        public BlendState(int src, int dst, int srcA, int dstA)
        {
            SourceBlend = src; DestinationBlend = dst;
            SourceAlphaBlend = srcA; DestinationAlphaBlend = dstA;
        }

        public static readonly BlendState AlphaBlend =
            new(JSBind_WEBGL20.ONE, JSBind_WEBGL20.ONE_MINUS_SRC_ALPHA, JSBind_WEBGL20.ONE, JSBind_WEBGL20.ONE_MINUS_SRC_ALPHA);

        public static readonly BlendState NonPremultiplied =
            new(JSBind_WEBGL20.SRC_ALPHA, JSBind_WEBGL20.ONE_MINUS_SRC_ALPHA, JSBind_WEBGL20.SRC_ALPHA, JSBind_WEBGL20.ONE_MINUS_SRC_ALPHA);

        /// <summary>叠加发光，用于粒子、爆炸、激光。</summary>
        public static readonly BlendState Additive =
            new(JSBind_WEBGL20.SRC_ALPHA, JSBind_WEBGL20.ONE, JSBind_WEBGL20.SRC_ALPHA, JSBind_WEBGL20.ONE);

        public static readonly BlendState Opaque =
            new(JSBind_WEBGL20.ONE, JSBind_WEBGL20.ZERO, JSBind_WEBGL20.ONE, JSBind_WEBGL20.ZERO);

        /// <summary>
        /// 乘法混合：Final = Dest × SrcColor。传奇 / 传奇昼夜系统用它把「暗度色 + 发光体」的光照图
        /// 逐像素乘回主画面实现压暗（原版 D3D9 固定管线：SourceBlend=Zero, DestinationBlend=SourceColor）。
        /// </summary>
        public static readonly BlendState Multiply =
            new(JSBind_WEBGL20.ZERO, JSBind_WEBGL20.SRC_COLOR, JSBind_WEBGL20.ZERO, JSBind_WEBGL20.SRC_COLOR);

        /// <summary>
        /// 全加法发光：Final = Dest + Src（忽略 alpha 权重），用于把火把 / 法术等发光体累积进光照 RT。
        /// （与 <see cref="Additive"/> 的区别：Additive 按源 alpha 加权，本状态直接整亮度相加。）
        /// </summary>
        public static readonly BlendState AdditiveFull =
            new(JSBind_WEBGL20.ONE, JSBind_WEBGL20.ONE, JSBind_WEBGL20.ONE, JSBind_WEBGL20.ONE);
    }
}
