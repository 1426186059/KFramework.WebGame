namespace KFramework.MonoGame
{
    /// <summary>
    /// 一次渲染目标绑定的描述（照 MonoGame 的 RenderTargetBinding）。
    /// 本后端只支持 <see cref="RenderTarget2D"/>（无 cube / 3D / 纹理数组渲染目标），
    /// 故 ArraySlice 恒为 0，保留字段以对齐官方 API。
    /// </summary>
    public struct RenderTargetBinding
    {
        private readonly Texture? _renderTarget;
        private readonly int _arraySlice;
        private readonly DepthFormat _depthFormat;

        /// <summary>绑定的渲染目标。</summary>
        public Texture RenderTarget => _renderTarget!;

        /// <summary>数组切片 / cube 面；本后端恒为 0。</summary>
        public int ArraySlice => _arraySlice;

        /// <summary>该绑定的深度缓冲格式。</summary>
        internal DepthFormat DepthFormat => _depthFormat;

        public RenderTargetBinding(RenderTarget2D renderTarget)
        {
            if (renderTarget == null) throw new System.ArgumentNullException(nameof(renderTarget));

            _renderTarget = renderTarget;
            _arraySlice = 0;
            _depthFormat = renderTarget.DepthStencilFormat;
        }

        /// <summary>RenderTarget2D 可隐式转为绑定（照 MonoGame，便于 SetRenderTargets(rt) 直接传目标）。</summary>
        public static implicit operator RenderTargetBinding(RenderTarget2D renderTarget)
            => new RenderTargetBinding(renderTarget);
    }
}
