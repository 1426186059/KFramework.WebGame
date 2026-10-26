using KFramework.MonoGame;

namespace KFramework.MonoGameExtend.Mask
{
    /// <summary>
    /// 矩形遮罩（Scissor 版）：开启 <see cref="RasterizerState.ScissorTestEnable"/>，绘制时只保留
    /// <see cref="GraphicsDevice.SetScissor"/> 设定的矩形内像素 —— 屏幕级裁剪（区别于 <see cref="RectMaskShader"/> 的逐元素 Shader 裁）。
    /// <para>WebGL / Canvas2D 后端已实现；WebGPU 的 SetScissor 当前为 no-op。</para>
    /// <example>
    /// <code>
    /// var mat = RectMaskScissor.CreateMaterial();
    /// RectMaskScissor.Begin(device, clipRect);          // device.SetScissor(clipRect)
    /// batch.Begin(mat); batch.Draw(content, ...); batch.End();
    /// RectMaskScissor.End(device);                      // device.ClearScissor()
    /// </code>
    /// </example>
    /// </summary>
    public static class RectMaskScissor
    {
        /// <summary>
        /// 矩形遮罩材质（Scissor 版）：开启 ScissorTest，绘制时只保留 <see cref="GraphicsDevice.SetScissor"/> 设定的矩形内像素。
        /// 配合 <see cref="Begin"/> / <see cref="End"/> 使用。
        /// </summary>
        public static Material CreateMaterial()
            => new Material
            {
                Rasterizer = new RasterizerState { CullMode = CullMode.Off, ScissorTestEnable = true },
                Blend = BlendState.NonPremultiplied,
            };

        /// <summary>内容绘制前调用：把裁剪矩形设为 <paramref name="rect"/>（屏幕左上坐标，Y 向下）。</summary>
        public static void Begin(GraphicsDevice device, Rectangle rect)
            => device.SetScissor(rect.X, rect.Y, rect.Width, rect.Height);

        /// <summary>内容绘制完后调用：恢复为不裁剪（剪刀矩形扩回整个视口）。</summary>
        public static void End(GraphicsDevice device)
            => device.ClearScissor();
    }
}
