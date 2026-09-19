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
    /// <para>
    /// ⚠️ <b>本类的多重采样（<see cref="MultiSampleCount"/>）与画布的 <c>antialias</c> 是两回事：</b>
    /// <list type="bullet">
    ///   <item>画布的 <c>antialias</c> 是 <c>getContext('webgl2', {antialias})</c> 的<b>建上下文参数</b>，
    ///   创建后冻结、只能整画布一个值，且<b>只对直接画到画布的几何体轮廓边缘</b>生效，碰不到离屏内容。</item>
    ///   <item>本类的 <see cref="MultiSampleCount"/> 是<b>渲染目标（离屏 FBO）级别</b>的 MSAA：
    ///   在源头用多重采样 renderbuffer 把画面磨平，再 <c>blitFramebuffer</c> 解到本纹理。
    ///   它决定的是「这张离屏纹理<b>内部内容</b>自己抗不抗锯齿」，与画布 antialias 无关。</item>
    /// </list>
    /// 因此即使画布 <c>antialias=false</c>，把 <see cref="MultiSampleCount"/>=4 的 RT 当贴图 blit 到画布，
    /// 该贴图内部仍是平滑的；而 <see cref="MultiSampleCount"/>=0 的 RT 内部仍带锯齿——画布 antialias 改变不了这个区别。
    /// </para>
    /// </summary>
    public sealed class RenderTarget2D : Texture2D, IRenderTarget
    {
        /// <summary>该渲染目标附带的深度 / 模板格式。</summary>
        public DepthFormat DepthStencilFormat { get; }

        /// <summary>每像素采样数；>0 表示多重采样（由 GraphicsDevice 用 renderbufferStorageMultisample + blitFramebuffer 实现）。</summary>
        public int MultiSampleCount { get; }

        /// <summary>内容保留策略（照 MonoGame）。</summary>
        public RenderTargetUsage RenderTargetUsage { get; }

        JSObject IRenderTarget.GLTexture => Handle;

        JSObject? IRenderTarget.GLColorRenderbuffer { get; set; }

        JSObject? IRenderTarget.GLMultiSampleFramebuffer { get; set; }

        JSObject? IRenderTarget.GLResolveFramebuffer { get; set; }

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
        /// <param name="preferredMultiSampleCount">请求的 MSAA 采样数；0 表示不多重采样，>0 由 GraphicsDevice 实现 resolve。</param>
        public RenderTarget2D(GraphicsDevice graphicsDevice, int width, int height, bool mipMap,
                              SurfaceFormat preferredFormat, DepthFormat preferredDepthFormat,
                              int preferredMultiSampleCount, RenderTargetUsage usage)
            : base(graphicsDevice, width, height, mipMap, preferredFormat, SurfaceType.RenderTarget)
        {
            DepthStencilFormat = preferredDepthFormat;
            // 保留请求的采样数；实际能否生效、上限多少由 GraphicsDevice.PlatformCreateRenderTarget 决定。
            MultiSampleCount = preferredMultiSampleCount;
            RenderTargetUsage = usage;

            // 平台层：多重采样时建「MSAA 颜色/深度 renderbuffer + MSAA FBO + 解析 FBO」，
            // 非多重采样时颜色附件就是本纹理自己（FBO 由设备按需缓存）。
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
