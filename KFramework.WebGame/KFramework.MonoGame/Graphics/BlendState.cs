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
    /// <remarks>
    /// <b>通用混合公式</b>（颜色 RGB 三通道、Alpha 通道各自独立、逐分量计算）：
    /// <code>
    /// FinalRGB = (SrcRGB × Fsrc_c) OP_c (DstRGB × Fdst_c)
    /// FinalA   = (SrcA   × Fsrc_a) OP_a (DstA   × Fdst_a)
    /// </code>
    /// <c>Fsrc_c / Fdst_c</c> 由 <see cref="SourceColorBlendFactor"/> / <see cref="DestinationColorBlendFactor"/> 的
    /// <see cref="BlendMode"/> 决定，<c>Fsrc_a / Fdst_a</c> 由 Alpha 那组因子决定；<c>OP</c> 一般是 <see cref="BlendOp.Add"/>（+）。
    /// <para>
    /// 每个 <see cref="BlendMode"/> 翻译成的具体乘子（记 Src=源像素、Dst=目标像素，分量范围 0..1）：
    /// <list type="table">
    ///   <item><term>Zero</term><description>0</description></item>
    ///   <item><term>One</term><description>1</description></item>
    ///   <item><term>SrcColor</term><description>SrcRGB（逐分量）</description></item>
    ///   <item><term>DstColor</term><description>DstRGB</description></item>
    ///   <item><term>OneMinusSrcColor</term><description>1 − SrcRGB</description></item>
    ///   <item><term>OneMinusDstColor</term><description>1 − DstRGB</description></item>
    ///   <item><term>SrcAlpha</term><description>SrcA</description></item>
    ///   <item><term>DstAlpha</term><description>DstA</description></item>
    ///   <item><term>OneMinusSrcAlpha</term><description>1 − SrcA</description></item>
    ///   <item><term>OneMinusDstAlpha</term><description>1 − DstA</description></item>
    ///   <item><term>SrcAlphaSaturate</term><description>min(SrcA, 1 − DstA)</description></item>
    /// </list>
    /// 下面每个预设都用「S=SrcRGB、Sa=SrcA、D=DstRGB、Da=DstA」把公式完全展开。
    /// </para>
    /// <para>
    /// <b>预乘 vs 非预乘提醒</b>：公式里的 SrcRGB 是「纹理/顶点已输出的颜色」。若该颜色是
    /// <b>预乘</b>形式（已乘过 alpha，即 S = 原色 × Sa），则配 <see cref="AlphaBlend"/>（Fsrc_c=One）；
    /// 若是 <b>非预乘</b>（直出原色，S = 原色），则应配 <see cref="NonPremultiplied"/>（Fsrc_c=SrcAlpha）。
    /// 配错会让半透明层偏暗（二次预乘）或偏亮（少乘一次 alpha）。
    /// </para>
    /// </remarks>
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
        /// 预乘 alpha 的 source-over（Unity 的 <c>Blend One OneMinusSrcAlpha</c>）。
        /// <para>公式：Final = S + D×(1−Sa)；FinalA = Sa + Da×(1−Sa)。</para>
        /// <para>前提：S 必须是<b>预乘</b>颜色（已 ×Sa）。若 S 是非预乘原色，会偏亮（把整色当预乘）。</para>
        /// </summary>
        public static readonly BlendState AlphaBlend =
            new(BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);

        /// <summary>
        /// 非预乘的 source-over（标准直出 alpha 的「普通透明」）。
        /// <para>公式：Final = S×Sa + D×(1−Sa)；FinalA = Sa×Sa + Da×(1−Sa)。</para>
        /// <para>注意：半透明层叠在透明上时 alpha 算成 Sa×Sa（≠Sa）；若要把烘焙出的 RT 当预乘数据读，别用它烘焙。</para>
        /// </summary>
        public static readonly BlendState NonPremultiplied =
            new(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

        /// <summary>
        /// 烘焙 RT 专用：把「非预乘（直出 alpha）」的源预乘进颜色、但 alpha 保持直线，使产物可被 <see cref="AlphaBlend"/> 当预乘纹理读取。
        /// <para>公式：Final = S×Sa + D×(1−Sa)（颜色已预乘）；FinalA = Sa + Da×(1−Sa)（alpha 保持直线，≠ Sa×Sa）。</para>
        /// <para>用途：把若干层烘进同一张 RT、之后用 AlphaBlend 合成/显示，可避开 <see cref="NonPremultiplied"/> 把 alpha 算成 Sa×Sa 的坑；与 Unity 预乘思路一致。
        /// 注意：它不是用来直接画到屏幕的（屏幕直出 alpha 仍用 <see cref="NonPremultiplied"/>）。</para>
        /// </summary>
        public static readonly BlendState BakePremultiplied =
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
