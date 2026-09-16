namespace KFramework.MonoGame
{
    /// <summary>
    /// 深度/模板状态（照 MonoGame 的 DepthStencilState，本 2D 后端仅保留实际用到的成员）。
    /// GraphicsDevice 在属性 setter 里把 <see cref="DepthBufferEnable"/> 翻译成 WebGL 的 DEPTH_TEST 开关。
    /// </summary>
    public sealed class DepthStencilState
    {
        /// <summary>是否开启深度测试（照 MonoGame）。</summary>
        public bool DepthBufferEnable { get; set; } = true;

        /// <summary>是否可写深度缓冲（照 MonoGame）。</summary>
        public bool DepthBufferWriteEnable { get; set; } = true;

        /// <summary>深度比较函数（照 MonoGame）。</summary>
        public CompareFunction DepthBufferFunction { get; set; } = CompareFunction.LessEqual;

        /// <summary>创建默认（开启深度读写）实例。</summary>
        public DepthStencilState() { }

        private DepthStencilState(bool depthBufferEnable, bool depthBufferWriteEnable, CompareFunction depthBufferFunction)
        {
            DepthBufferEnable = depthBufferEnable;
            DepthBufferWriteEnable = depthBufferWriteEnable;
            DepthBufferFunction = depthBufferFunction;
        }

        /// <summary>关闭深度缓冲（照 MonoGame 的 DepthStencilState.None，SpriteBatch 默认）。</summary>
        public static readonly DepthStencilState None = new(false, false, CompareFunction.LessEqual);

        /// <summary>开启深度读写的默认状态（照 MonoGame 的 DepthStencilState.Default）。</summary>
        public static readonly DepthStencilState Default = new(true, true, CompareFunction.LessEqual);
    }
}
