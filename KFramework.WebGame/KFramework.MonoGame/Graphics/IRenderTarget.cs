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

        /// <summary>每个像素的采样数；>0 时该目标走「多重采样 FBO → blitFramebuffer 解到纹理」的路径。</summary>
        int MultiSampleCount { get; }

        /// <summary>底层的颜色纹理（作为 resolve 后的可采样结果；多重采样时由 <see cref="GLResolveFramebuffer"/> 写入）。</summary>
        JSObject GLTexture { get; }

        /// <summary>多重采样颜色 renderbuffer（RenderbufferStorageMultisample 分配）；非多重采样时为 null。</summary>
        JSObject? GLColorRenderbuffer { get; set; }

        /// <summary>多重采样 FBO（渲染时绑定的目标）；非多重采样时为 null。</summary>
        JSObject? GLMultiSampleFramebuffer { get; set; }

        /// <summary>解析 FBO（把多重采样结果 blit 进 <see cref="GLTexture"/> 用）；非多重采样时为 null。</summary>
        JSObject? GLResolveFramebuffer { get; set; }

        /// <summary>深度 renderbuffer（DepthFormat.None 时为 null）。</summary>
        JSObject? GLDepthBuffer { get; set; }

        /// <summary>模板 renderbuffer；Depth24Stencil8 时与 <see cref="GLDepthBuffer"/> 是同一个对象。</summary>
        JSObject? GLStencilBuffer { get; set; }
    }
}
