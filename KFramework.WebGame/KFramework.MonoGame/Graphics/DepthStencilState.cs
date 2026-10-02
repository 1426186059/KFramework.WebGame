namespace KFramework.MonoGame
{
    /// <summary>
    /// 深度 / 模板状态。本身只是个<b>组合容器</b>，真正的字段在
    /// <see cref="DepthState"/>（照 Unity 的 DepthState）与
    /// <see cref="StencilState"/>（照 Unity 的 StencilState）里。
    /// <para>
    /// 之所以保留这一层：深度与模板在硬件上是同一组附件（GL 的 DEPTH24_STENCIL8 renderbuffer、
    /// WebGPU 的同一个 depthStencilAttachment），且 Unity 之外 MonoGame / XNA 也用
    /// <c>DepthStencilState</c> 这个名字，作为下发单元更顺手。
    /// </para>
    /// </summary>
    public sealed class DepthStencilState
    {
        /// <summary>深度状态。</summary>
        public readonly DepthState Depth;

        /// <summary>模板状态。</summary>
        public readonly StencilState Stencil;

        public DepthStencilState(DepthState depth) : this(depth, StencilState.Default) { }

        public DepthStencilState(DepthState depth, StencilState stencil)
        {
            Depth = depth;
            Stencil = stencil;
        }

        /// <summary>
        /// 关闭深度测试、保持模板默认（关闭）。对应 MonoGame 的 <c>DepthStencilState.None</c>，
        /// 是 <see cref="SpriteBatch"/> 的默认状态。
        /// </summary>
        public static readonly DepthStencilState None = new(DepthState.Disabled);

        /// <summary>
        /// 开启深度读写的默认状态。对应 MonoGame 的 <c>DepthStencilState.Default</c>。
        /// </summary>
        public static readonly DepthStencilState Default = new(DepthState.Default);
    }
}
