namespace KFramework.MonoGame
{
    /// 通用混合公式
    /// FinalRGB = (SrcRGB × Fsrc_c) OP_c (DstRGB × Fdst_c)
    /// FinalA   = (SrcA   × Fsrc_a) OP_a (DstA   × Fdst_a)
    public sealed class BlendState
    {
        /// <summary>是否开启混合。false（如 Opaque）时后端直接关闭混合单元，整像素覆盖。</summary>
        public readonly bool Enabled;
        public readonly BlendMode SourceColorBlendFactor;
        public readonly BlendMode DestinationColorBlendFactor;
        public readonly BlendOp ColorBlendOperation;

        public readonly BlendMode SourceAlphaBlendFactor;
        public readonly BlendMode DestinationAlphaBlendFactor;
        public readonly BlendOp AlphaBlendOperation;

        public BlendState(BlendMode srcColor, BlendMode dstColor, BlendMode srcAlpha, BlendMode dstAlpha, bool enabled = true)
            : this(srcColor, dstColor, BlendOp.Add, srcAlpha, dstAlpha, BlendOp.Add, enabled)
        {
        }

        /// <summary>完整指定：颜色 / Alpha 各自的因子与运算。</summary>
        public BlendState(BlendMode srcColor, BlendMode dstColor, BlendOp colorOp,
                          BlendMode srcAlpha, BlendMode dstAlpha, BlendOp alphaOp,
                          bool enabled)
        {
            Enabled = enabled;
            SourceColorBlendFactor = srcColor; DestinationColorBlendFactor = dstColor;
            ColorBlendOperation = colorOp;
            SourceAlphaBlendFactor = srcAlpha; DestinationAlphaBlendFactor = dstAlpha;
            AlphaBlendOperation = alphaOp;
        }

        public static readonly BlendState NoneBlend =
            new(BlendMode.Zero,
                BlendMode.Zero,
                BlendMode.Zero,
                BlendMode.Zero,
                enabled: false);

        /// <summary>
        /// 不预乘 alpha 的标准混合
        /// 得在 Shader 片元着色器里  rgb *= alpha 之后再输出，才能正确混合。
        public static readonly BlendState AlphaBlend =
            new(BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);

        // 这个存在严重的一个问题，就是一个 半透明的图 与 完全不透明的图 混合，会漏底。
        // 比如 0.5A 得图 和 1.0A 得图 混合，烘出来得 颜色是 0.5² + 1×(1-0.5) = 0.75，层下就漏底了。
        [Obsolete("Use NonPremultiplied instead.")]
        public static readonly BlendState Old_NonPremultiplied =
            new(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

        //这个是Old_NonPremultiplied 得改进版, 解决了上面漏底问题
        public static readonly BlendState NonPremultiplied =
            new(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);

        /// <summary>
        /// 叠加发光，用于粒子、爆炸、激光。
        /// <para>公式：Final = S×Sa + D；FinalA = Sa×Sa + Da。</para>
        /// </summary>
        public static readonly BlendState Additive =
            new(BlendMode.SrcAlpha, BlendMode.One, BlendMode.SrcAlpha, BlendMode.One);

        /// <summary>
        /// 完全不混合：整像素覆盖（等价于关闭 BLEND）。
        /// <para>公式（混合关闭时）：Final = S；FinalA = Sa（目标被整像素替换）。</para>
        /// </summary>
        public static readonly BlendState Opaque =
            new(BlendMode.One, BlendMode.Zero, BlendOp.Add, BlendMode.One, BlendMode.Zero, BlendOp.Add, enabled: false);

        /// <summary>
        /// 乘法混合：Final = Dest × SrcColor。传奇 / 传奇昼夜系统用它把「暗度色 + 发光体」的光照图
        /// 逐像素乘回主画面实现压暗（原版 D3D9 固定管线：SourceBlend=Zero, DestinationBlend=SourceColor）。
        /// <para>公式：Final = D × S；FinalA = Da × Sa。</para>
        /// </summary>
        public static readonly BlendState Multiply =
            new(BlendMode.Zero, BlendMode.SrcColor, BlendMode.Zero, BlendMode.SrcColor);

        /// <summary>
        /// 全加法发光：Final = Dest + Src（忽略 alpha 权重），用于把火把 / 法术等发光体累积进光照 RT。
        /// （与 <see cref="Additive"/> 的区别：Additive 按源 alpha 加权，本状态直接整亮度相加。）
        /// <para>公式：Final = S + D；FinalA = Sa + Da（各自相加，超过 1 截断）。</para>
        /// </summary>
        public static readonly BlendState AdditiveFull =
            new(BlendMode.One, BlendMode.One, BlendMode.One, BlendMode.One);
    }
}
