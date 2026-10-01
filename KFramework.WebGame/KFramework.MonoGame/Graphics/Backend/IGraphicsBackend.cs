namespace KFramework.MonoGame
{

    /// <summary>
    /// 渲染后端抽象：把 <see cref="GraphicsDevice"/> 的平台层（目前是 WebGL 2.0）隔离出去，
    /// 使 WebGL 与 WebGPU 能作为两个可互换的实现共存于同一套框架。
    /// <para>
    /// 设计原则：方法签名一律用<b>领域类型</b>（<see cref="BlendState"/> / <see cref="Texture2D"/> /
    /// <see cref="IRenderTarget"/> 等），不出现具体后端的句柄类型，这样 WebGPU 也能实现。
    /// 状态去重、渲染统计、渲染目标绑定记账等<b>与后端无关的逻辑仍留在 GraphicsDevice</b>。
    /// </para>
    /// </summary>
    internal interface IGraphicsBackend : IDisposable
    {
        // ============ 初始化 / 查询 ============

        /// <summary>后端名（用于日志），如 "WebGL2" / "WebGPU"。</summary>
        string Name { get; }

        /// <summary>
        /// 创建上下文与常驻资源（缓冲、VAO、着色器程序等）。
        /// WebGPU 的初始化是异步的（requestAdapter / requestDevice），故统一返回 Task；
        /// WebGL 后端同步完成，返回的是已完成的 Task。
        /// </summary>
        Task InitializeAsync(string canvasSelector, bool antialias);

        int MaxTextureSize { get; }
        string Renderer { get; }

        /// <summary>
        /// 收帧。WebGL 由浏览器在 rAF 回调结束时自动合成（无需动作）；
        /// WebGPU 必须在这里结束渲染通道并提交命令缓冲，否则画面永不呈现。
        /// </summary>
        void EndFrame();

        /// <summary>取当前错误码（0 表示无错误）。</summary>
        int GetError();

        // ============ 视口 / 裁剪 / 清屏 / 读像素 ============

        void SetViewport(int x, int y, int width, int height);
        void SetScissor(int x, int y, int width, int height);
        void Clear(Color color);

        /// <summary>读取一个像素。传入视口高度是为了让后端自行做 Y 轴换算（WebGL 原点在左下）。</summary>
        void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba);

        // ============ 状态 ============

        void SetBlendState(BlendState state);
        void ApplyRasterizerState(RasterizerState state);
        void ApplyDepthStencilState(DepthStencilState state);
        void SetSamplerState(SamplerState state);
        void BindTexture(Texture2D texture);

        // ============ 绘制 ============

        void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end);

        // ============ 精灵着色器程序 ============

        /// <summary>创建本后端的精灵程序（须在 <see cref="Initialize"/> 之后调用）。</summary>
        ISpriteProgram CreateSpriteProgram();

        // ============ 纹理资源（由 Texture2D 的平台层调用） ============

        void CreateTexture(Texture2D texture, int width, int height, bool mipmap, SurfaceFormat format, Texture2D.SurfaceType type);
        void SetTextureData(Texture2D texture, int level, byte[] bytes);
        void SetTextureData(Texture2D texture, int level, Rectangle rect, byte[] bytes);
        void DeleteTexture(Texture2D texture);

        // ============ 渲染目标平台层 ============

        void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat);
        void DeleteRenderTarget(IRenderTarget renderTarget);

        /// <summary>建 / 复用 FBO 并绑定当前渲染目标组合，返回首个目标（供上层取尺寸与 usage）。</summary>
        IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count);

        void ApplyDefaultRenderTarget();

        /// <summary>把多重采样目标解析到可采样纹理。</summary>
        void ResolveRenderTarget(IRenderTarget renderTarget);
    }

}
