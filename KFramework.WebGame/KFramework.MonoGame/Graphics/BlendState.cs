namespace KFramework.MonoGame
{
    /// <summary>
    /// 混合状态。<b>后端无关</b>：字段是 <see cref="BlendMode"/> / <see cref="BlendOp"/> 中立枚举
    /// （照 Unity 的命名与数值），不含任何 WebGL / WebGPU 常量，因此同一套状态能被两个后端共用。
    /// <para>
    /// 形状照 Unity 的 <c>UnityEngine.Rendering.BlendState</c>：颜色与 Alpha 各有一组
    /// 「源因子 / 目标因子 / 运算」，可分别指定（Unity 的 ShaderLab 同理：<c>Blend</c> 与 <c>BlendOp</c>）。
    /// </para>
    /// </summary>
    public sealed class BlendState
    {
        public readonly BlendMode SourceColorBlendFactor;
        public readonly BlendMode DestinationColorBlendFactor;
        public readonly BlendOp ColorBlendOperation;

        public readonly BlendMode SourceAlphaBlendFactor;
        public readonly BlendMode DestinationAlphaBlendFactor;
        public readonly BlendOp AlphaBlendOperation;

        /// <summary>
        /// 只指定四个因子，颜色与 Alpha 的运算都取 <see cref="BlendOp.Add"/>（绝大多数情况）。
        /// </summary>
        public BlendState(BlendMode srcColor, BlendMode dstColor, BlendMode srcAlpha, BlendMode dstAlpha)
            : this(srcColor, dstColor, BlendOp.Add, srcAlpha, dstAlpha, BlendOp.Add)
        {
        }

        /// <summary>完整指定：颜色 / Alpha 各自的因子与运算。</summary>
        public BlendState(BlendMode srcColor, BlendMode dstColor, BlendOp colorOp,
                          BlendMode srcAlpha, BlendMode dstAlpha, BlendOp alphaOp)
        {
            SourceColorBlendFactor = srcColor; DestinationColorBlendFactor = dstColor;
            ColorBlendOperation = colorOp;
            SourceAlphaBlendFactor = srcAlpha; DestinationAlphaBlendFactor = dstAlpha;
            AlphaBlendOperation = alphaOp;
        }

        public static readonly BlendState AlphaBlend =
            new(BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);

        public static readonly BlendState NonPremultiplied =
            new(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

        /// <summary>叠加发光，用于粒子、爆炸、激光。</summary>
        public static readonly BlendState Additive =
            new(BlendMode.SrcAlpha, BlendMode.One, BlendMode.SrcAlpha, BlendMode.One);

        public static readonly BlendState Opaque =
            new(BlendMode.One, BlendMode.Zero, BlendMode.One, BlendMode.Zero);

        /// <summary>
        /// 乘法混合：Final = Dest × SrcColor。传奇 / 传奇昼夜系统用它把「暗度色 + 发光体」的光照图
        /// 逐像素乘回主画面实现压暗（原版 D3D9 固定管线：SourceBlend=Zero, DestinationBlend=SourceColor）。
        /// </summary>
        public static readonly BlendState Multiply =
            new(BlendMode.Zero, BlendMode.SrcColor, BlendMode.Zero, BlendMode.SrcColor);

        /// <summary>
        /// 全加法发光：Final = Dest + Src（忽略 alpha 权重），用于把火把 / 法术等发光体累积进光照 RT。
        /// （与 <see cref="Additive"/> 的区别：Additive 按源 alpha 加权，本状态直接整亮度相加。）
        /// </summary>
        public static readonly BlendState AdditiveFull =
            new(BlendMode.One, BlendMode.One, BlendMode.One, BlendMode.One);
    }
}
