using KFramework.MonoGame;

namespace KFramework.MonoGameExtend.Mask
{
    /// <summary>
    /// 模板遮罩（Stencil）：用 <see cref="DepthStencilState"/> + <see cref="StencilState"/> 两遍 Pass 实现
    /// （先"写模板"画出遮罩形状，再"测模板"只显示形状内的内容）。照 Unity 的 Stencil 遮罩思路。
    /// <para>
    /// WebGL 默认帧带 stencil 缓冲可用；Canvas2D / WebGPU 默认帧无 stencil（需渲染到
    /// <see cref="DepthFormat.Depth24Stencil8"/> 的 RenderTarget 再做两遍 Pass）。
    /// </para>
    /// <example>
    /// <code>
    /// // 第一遍：把 1 写进模板（画不透明遮罩形状）
    /// batch.Begin(StencilMask.CreateWriteMaterial());
    /// batch.Draw(maskShape, rect, ...); batch.End();
    /// // 第二遍：只在模板 == 1 处显示内容
    /// batch.Begin(StencilMask.CreateTestMaterial());
    /// batch.Draw(content, ...); batch.End();
    /// </code>
    /// </example>
    /// </summary>
    public static class StencilMask
    {
        /// <summary>
        /// 写模板材质：深度关闭、模板 Always + Replace，把参考值 1 写入遮罩形状覆盖到的像素。
        /// 用此材质画"遮罩形状"（建议画不透明形状；若遮罩图带透明，需另配 alpha-cutoff 着色器丢弃透明像素，否则透明处也会写 1）。
        /// </summary>
        public static Material CreateWriteMaterial()
            => new Material
            {
                DepthStencil = new DepthStencilState(
                    DepthState.Disabled,
                    new StencilState(true, 1, 0xFF, 0xFF, CompareFunction.Always, StencilOp.Replace)),
                Rasterizer = RasterizerState.CullNone,
            };

        /// <summary>
        /// 测模板材质：深度关闭、模板 Equal(参考值 1)，只在"模板缓冲 == 1"的区域绘制内容 —— 即只显示遮罩形状内的部分。
        /// </summary>
        public static Material CreateTestMaterial()
            => new Material
            {
                DepthStencil = new DepthStencilState(
                    DepthState.Disabled,
                    new StencilState(true, 1, 0xFF, 0xFF, CompareFunction.Equal, StencilOp.Keep)),
                Rasterizer = RasterizerState.CullNone,
            };
    }
}
