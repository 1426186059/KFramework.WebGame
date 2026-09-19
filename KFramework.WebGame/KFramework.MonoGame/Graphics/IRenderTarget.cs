using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染目标的平台层视图（照 MonoGame 的 IRenderTarget）：GraphicsDevice 只通过它访问
    /// 「尺寸 + 使用策略 + 底层 GL 附件」，无需知道具体是 RenderTarget2D 还是别的类型。
    /// </summary>
    internal interface IRenderTarget
    {
        /// <summary>渲染目标像素宽度。</summary>
        int Width { get; }

        /// <summary>渲染目标像素高度。</summary>
        int Height { get; }

        /// <summary>内容保留策略（决定绑定时是否清屏）。</summary>
        RenderTargetUsage RenderTargetUsage { get; }

        /// <summary>每个像素的采样数；本后端不做 MSAA resolve，恒为 0。</summary>
        int MultiSampleCount { get; }

        /// <summary>底层的颜色纹理（作为 FBO 的 COLOR_ATTACHMENT0）。</summary>
        JSObject GLTexture { get; }

        /// <summary>深度 renderbuffer（DepthFormat.None 时为 null）。</summary>
        JSObject? GLDepthBuffer { get; set; }

        /// <summary>模板 renderbuffer；Depth24Stencil8 时与 <see cref="GLDepthBuffer"/> 是同一个对象。</summary>
        JSObject? GLStencilBuffer { get; set; }
    }
}
