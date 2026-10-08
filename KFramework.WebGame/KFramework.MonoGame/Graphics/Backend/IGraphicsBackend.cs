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
        Task InitializeAsync(bool antialias);

        int MaxTextureSize { get; }
        string Renderer { get; }

        /// <summary>
        /// 收帧。WebGL 由浏览器在 rAF 回调结束时自动合成（无需动作）；
        /// WebGPU 必须在这里结束渲染通道并提交命令缓冲，否则画面永不呈现。
        /// </summary>
        void EndFrame();

        /// <summary>取当前错误码（0 表示无错误）。</summary>
        int GetError();

        /// <summary>
        /// 渲染到<b>离屏目标</b>时，是否需要改用 Y 向上的投影
        /// （<c>CreateOrthographicOffCenter(0, w, 0, h)</c>，与屏幕的 Y 向下相反）。
        /// <para>
        /// 这不是偏好，而是两个后端的<b>坐标系原点不同</b>决定的，取错值会让离屏画面上下颠倒：
        /// <list type="bullet">
        ///   <item><description><b>WebGL：需要（true）</b>。FBO 与纹理原点在<b>左下</b>：
        ///   渲染时 NDC y=-1 落在纹理第 0 行，而采样时 UV v=0 也取第 0 行 ——
        ///   于是"屏幕翻转"与"FBO 翻转"正好抵消，必须改用 Y 向上投影，离屏内容画出来才是正的。
        ///   （等价于 MonoGame GL 后端在顶点着色器里对离屏渲染做的 <c>posFixup.y *= -1</c>。）</description></item>
        ///   <item><description><b>WebGPU：不需要（false）</b>。附件与纹理原点都在<b>左上</b>：
        ///   NDC y=+1 落在第 0 行，采样 v=0 也取第 0 行 —— 与屏幕完全一致。
        ///   若再翻一次，离屏内容就会上下颠倒。</description></item>
        /// </list>
        /// </para>
        /// </summary>
        bool NeedsOffscreenYFlip { get; }

        // ============ 视口 / 裁剪 / 清屏 / 读像素 ============

        void SetViewport(int x, int y, int width, int height);
        void SetScissor(int x, int y, int width, int height);
        void Clear(Color color);

        /// <summary>读取一个像素。传入视口高度是为了让后端自行做 Y 轴换算（WebGL 原点在左下）。</summary>
        void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba);

        /// <summary>从当前绑定的帧缓冲读取矩形区域像素（x/y 为坐标，原点在左上）。WebGL 由调用方负责 Y 翻转；
        /// WebGPU 为异步（copyTextureToBuffer + mapAsync），故返回 Task。</summary>
        Task ReadPixels(int x, int y, int width, int height, byte[] rgba);

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
        IShaderProgram CreateShaderProgram();

        /// <summary>
        /// 用自定义 GLSL 顶点/片元源码创建精灵程序（仅 WebGL 后端真正实现；WebGPU 回落默认精灵着色器）。
        /// 自定义程序支持 uTime / uParams 两个额外 uniform，详见 <see cref="ShaderEffect"/>。
        /// </summary>
        IShaderProgram CreateCustomShaderProgram(string vertexSource, string fragmentSource);

        // ============ GPU 实例化 ============

        /// <summary>后端是否支持 GPU 实例化（纯能力查询，不创建任何资源）。
        /// 必须与 <see cref="CreateGpuInstanceProgram"/> 返回是否为 null 一致。WebGL2 支持；WebGPU 尚未接入。</summary>
        bool SupportsGpuInstancing { get; }

        /// <summary>
        /// 创建 GPU 实例化程序；后端不支持时返回 null。
        /// </summary>
        /// <param name="fragmentSource">片元着色器源码（null = 后端内置的"纹理 × 逐实例颜色"）。
        /// 来源是 <see cref="ShaderEffect.FragmentSource"/> —— 调用方通过 <see cref="Material.Effect"/> 指定自定义着色器。</param>
        /// <param name="capacity">实例缓冲容量（单次 draw 的实例上限）。</param>
        IGpuInstanceProgram? CreateGpuInstanceProgram(string? fragmentSource, int capacity);

        // ============ SRP Batcher 式（uniform buffer）============

        /// <summary>后端是否支持 SRP-Batcher 式的 UBO 绘制（uniform buffer + bindBufferRange，纯能力查询）。
        /// 必须与 <see cref="CreateUrpProgram"/> 返回是否为 null 一致。WebGL2 支持；WebGPU 尚未接入。</summary>
        bool SupportsUrpBatching { get; }

        /// <summary>
        /// 创建 SRP-Batcher 式程序；后端不支持时返回 null。
        /// <para>逐物体 / 逐材质常量都走 uniform buffer，逐物体缓冲按需增长，故不需要容量参数。</para>
        /// </summary>
        /// <param name="fragmentSource">片元着色器源码（null = 后端内置的"纹理 × 逐物体颜色 × 材质常量"）。
        /// 来源是 <see cref="ShaderEffect.FragmentSource"/>。自定义着色器必须声明 <c>UnityPerMaterial</c> 常量块
        /// （照 Unity 的 SRP Batcher 兼容要求）。</param>
        IUrpProgram? CreateUrpProgram(string? fragmentSource);

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
