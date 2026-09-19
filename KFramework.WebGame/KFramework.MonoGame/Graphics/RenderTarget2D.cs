using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 离屏渲染目标（照 MonoGame 的 RenderTarget2D）：本身是一张 <see cref="Texture2D"/>，
    /// 但内容由 GPU 绘制产生，绘制完成后可当作普通纹理采样。
    /// <para>
    /// 用法（照 MonoGame）：
    /// <code>
    ///   var rt = new RenderTarget2D(device, 256, 256);
    ///   device.SetRenderTarget(rt);            // 之后的绘制都进这张离屏纹理
    ///   device.Clear(Color.Transparent);
    ///   batch.Begin(); batch.Draw(...); batch.End();
    ///   device.SetRenderTarget(null);          // 回到画布
    ///   batch.Begin(); batch.Draw(rt, ...); batch.End();   // 把离屏结果画到屏幕
    /// </code>
    /// </para>
    /// <para>
    /// 平台实现照 MonoGame 的 OpenGL 后端：纹理挂到 FBO 的 COLOR_ATTACHMENT0，
    /// 深度 / 模板用 renderbuffer 挂在 DEPTH_ATTACHMENT / STENCIL_ATTACHMENT；
    /// FBO 由 GraphicsDevice 按「绑定组合」缓存复用（PlatformCreateRenderTarget 只建附件）。
    /// </para>
    /// </summary>
    public sealed class RenderTarget2D : Texture2D, IRenderTarget
    {
        /// <summary>该渲染目标附带的深度 / 模板格式。</summary>
        public DepthFormat DepthStencilFormat { get; }

        /// <summary>每像素采样数；本后端不做 MSAA resolve，恒为 0。</summary>
        public int MultiSampleCount { get; }

        /// <summary>内容保留策略（照 MonoGame）。</summary>
        public RenderTargetUsage RenderTargetUsage { get; }

        JSObject IRenderTarget.GLTexture => Handle;

        JSObject? IRenderTarget.GLDepthBuffer { get; set; }

        JSObject? IRenderTarget.GLStencilBuffer { get; set; }

        /// <summary>创建一个默认 RGBA8、无深度缓冲、DiscardContents 的渲染目标。</summary>
        public RenderTarget2D(GraphicsDevice graphicsDevice, int width, int height)
            : this(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None,
                   0, RenderTargetUsage.DiscardContents) { }

        /// <summary>创建指定格式与深度格式的渲染目标（默认 DiscardContents）。</summary>
        public RenderTarget2D(GraphicsDevice graphicsDevice, int width, int height, bool mipMap,
                              SurfaceFormat preferredFormat, DepthFormat preferredDepthFormat)
            : this(graphicsDevice, width, height, mipMap, preferredFormat, preferredDepthFormat,
                   0, RenderTargetUsage.DiscardContents) { }

        /// <summary>
        /// 完整参数构造（照 MonoGame）。
        /// </summary>
        /// <param name="preferredMultiSampleCount">请求的 MSAA 采样数；本后端不做 resolve，一律按 0 处理。</param>
        public RenderTarget2D(GraphicsDevice graphicsDevice, int width, int height, bool mipMap,
                              SurfaceFormat preferredFormat, DepthFormat preferredDepthFormat,
                              int preferredMultiSampleCount, RenderTargetUsage usage)
            : base(graphicsDevice, width, height, mipMap, preferredFormat, SurfaceType.RenderTarget)
        {
            DepthStencilFormat = preferredDepthFormat;
            // 本后端不做 MSAA resolve（MonoGame 需要 blitFramebuffer 把多重采样 FBO 解到纹理），
            // 因此无论请求多少采样都钳到 0，避免“看起来生效、实际没 resolve”。
            MultiSampleCount = 0;
            RenderTargetUsage = usage;

            // 平台层：建深度 / 模板 renderbuffer（颜色附件就是本纹理自己，FBO 由设备按需缓存）。
            graphicsDevice.PlatformCreateRenderTarget(this, width, height, preferredDepthFormat);

            _sortingKey = graphicsDevice.NextSortingKey();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                // 先摘掉设备的 FBO 缓存与 renderbuffer，再释放纹理本身。
                graphicsDevice?.PlatformDeleteRenderTarget(this);
            }

            base.Dispose(disposing);
        }
    }
}
