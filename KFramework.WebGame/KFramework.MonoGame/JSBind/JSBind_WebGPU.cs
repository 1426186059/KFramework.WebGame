using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGPU 原生后端绑定（非 WebGL 镜像）。模块名 <c>"render_webgpu"</c>，实现见
    /// KFramework.TSEngine/src/render_webgpu.ts（编译产物 render_webgpu.js 由各示例 SyncJsEngine 复制）。
    /// <para>
    /// 与 WebGL 的即时模式不同，本绑定提供原生 WebGPU 语义：<c>Init</c>（异步取设备 / configure 画布）
    /// → <c>CreateShaderModule</c> / <c>CreatePipeline</c> / <c>CreateBuffer</c> / <c>CreateTexture</c> /
    /// <c>CreateBindGroup</c> → <c>BeginFrame</c> / <c>SetPipeline</c> / <c>SetVertexBuffer</c> /
    /// <c>SetIndexBuffer</c> / <c>SetBindGroup</c> / <c>Draw</c> / <c>DrawIndexed</c> / <c>EndFrame</c>。
    /// </para>
    /// <para>GPU 对象以整数句柄返回（不再用 JSObject），避免跨边界持有 JS 对象带来的生命周期问题。</para>
    /// <para>上层应使用 GraphicsDevice / SpriteBatch 等抽象，而非直接调用本类。</para>
    /// </summary>
    public static partial class JSBind_WebGPU
    {

        // —— GPUBufferUsage 位标志（CreateBuffer 的 usage） ——
        public const int BUFFER_USAGE_MAP_READ = 0x0001;
        public const int BUFFER_USAGE_MAP_WRITE = 0x0002;
        public const int BUFFER_USAGE_COPY_SRC = 0x0004;
        public const int BUFFER_USAGE_COPY_DST = 0x0008;
        public const int BUFFER_USAGE_INDEX = 0x0010;
        public const int BUFFER_USAGE_VERTEX = 0x0020;
        public const int BUFFER_USAGE_UNIFORM = 0x0040;
        public const int BUFFER_USAGE_STORAGE = 0x0080;

        // —— GPUTextureUsage 位标志（CreateTexture 的 usage） ——
        public const int TEXTURE_USAGE_COPY_SRC = 0x01;
        public const int TEXTURE_USAGE_COPY_DST = 0x02;
        public const int TEXTURE_USAGE_TEXTURE_BINDING = 0x04;
        public const int TEXTURE_USAGE_STORAGE_BINDING = 0x08;
        public const int TEXTURE_USAGE_RENDER_ATTACHMENT = 0x10;

        // —— 上下文查询参数（GetParameterInt 的 pname） ——
        public const int MAX_TEXTURE_SIZE = 0x0D33;
        public const int MAX_SAMPLES = 0x8D57;

        // —— getParameterString 的 pname ——
        public const int VERSION = 0x1F02;
        public const int RENDERER = 0x1F01;



        /// <summary>初始化 WebGPU：异步 requestAdapter → requestDevice → configure 画布上下文（单画布模型，无需传 id）。返回是否成功。</summary>
        [JSImport("init", "render_webgpu")]
        public static partial Task<bool> Init(bool antialias);

        /// <summary>设置是否启用 MSAA（影响管线 sampleCount，须在 Init 之前调用）。</summary>
        [JSImport("setAntialias", "render_webgpu")]
        public static partial void SetAntialias(bool enabled);

        /// <summary>当前是否启用 MSAA。</summary>
        [JSImport("getAntialias", "render_webgpu")]
        public static partial bool GetAntialias();

        /// <summary>把画布尺寸设为 width×height 并重建深度 / 多重采样附件。</summary>
        [JSImport("resize", "render_webgpu")]
        public static partial void Resize(int width, int height);

        /// <summary>画布首选纹理格式（如 bgra8unorm）。</summary>
        [JSImport("getPreferredFormat", "render_webgpu")]
        public static partial string GetPreferredFormat();

        /// <summary>设备是否丢失（丢失需重建）。</summary>
        [JSImport("isContextLost", "render_webgpu")]
        public static partial bool IsContextLost();

        /// <summary>WebGPU 设备是否已就绪（requestDevice 成功后为真）。用于判断当前后端是否为 WebGPU。</summary>
        [JSImport("isActive", "render_webgpu")]
        public static partial bool IsActive();

        /// <summary>取当前画布元素（调试用，返回 JSObject 代理）。</summary>
        [JSImport("getCanvasElement", "render_webgpu")]
        public static partial JSObject? GetCanvasElement();



        /// <summary>创建 GPUBuffer；usage 为 GPUBufferUsage 位标志组合。返回整数句柄（0 表示失败）。</summary>
        [JSImport("createBuffer", "render_webgpu")]
        public static partial int CreateBuffer(int size, int usage);

        /// <summary>上传字节到缓冲（queue.writeBuffer）。data 为 .NET Span&lt;byte&gt;（JS 侧会转成 Uint8Array）。</summary>
        [JSImport("writeBuffer", "render_webgpu")]
        public static partial void WriteBuffer(int bufferId, int offset, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>销毁缓冲。</summary>
        [JSImport("destroyBuffer", "render_webgpu")]
        public static partial void DestroyBuffer(int bufferId);



        /// <summary>由 WGSL 源码创建 GPUShaderModule，返回整数句柄。</summary>
        [JSImport("createShaderModule", "render_webgpu")]
        public static partial int CreateShaderModule(string code);

        /// <summary>销毁着色器模块。</summary>
        [JSImport("destroyShaderModule", "render_webgpu")]
        public static partial void DestroyShaderModule(int id);



        /// <summary>创建 GPURenderPipeline。descriptorJson 为原生 WebGPU 描述对象的 JSON 字符串（见 render_webgpu.ts 注释）。返回整数句柄。</summary>
        [JSImport("createPipeline", "render_webgpu")]
        public static partial int CreatePipeline(string descriptorJson);

        /// <summary>销毁管线。</summary>
        [JSImport("destroyPipeline", "render_webgpu")]
        public static partial void DestroyPipeline(int id);



        /// <summary>用管线（layout:'auto' 推导）创建 GPUBindGroup。entriesJson 为描述对象的 JSON 字符串。返回整数句柄。</summary>
        [JSImport("createBindGroup", "render_webgpu")]
        public static partial int CreateBindGroup(int pipelineId, int groupIndex, string entriesJson);

        /// <summary>销毁绑定组。</summary>
        [JSImport("destroyBindGroup", "render_webgpu")]
        public static partial void DestroyBindGroup(int id);



        /// <summary>
        /// 创建 GPUTexture。sampleCount = 1 时 usage 含 TEXTURE_BINDING | RENDER_ATTACHMENT | COPY_DST（可采样、可上传）；
        /// &gt; 1 时创建的是多重采样附件（只含 RENDER_ATTACHMENT，不可采样、不可上传）。返回整数句柄。
        /// </summary>
        [JSImport("createTexture", "render_webgpu")]
        public static partial int CreateTexture(int width, int height, string format, int sampleCount, int extraUsage);

        /// <summary>
        /// 上传 RGBA8 像素到纹理（queue.writeTexture）。
        /// x / y 为写入原点的左上角：SpriteFont 的字形图集靠它逐个字形局部更新。
        /// </summary>
        [JSImport("uploadTexture", "render_webgpu")]
        public static partial void UploadTexture(int id, [JSMarshalAs<JSType.MemoryView>] Span<byte> data,
            int x, int y, int width, int height, string format);

        /// <summary>销毁纹理。</summary>
        [JSImport("destroyTexture", "render_webgpu")]
        public static partial void DestroyTexture(int id);

        /// <summary>创建 GPUSampler。descriptorJson 为原生描述对象的 JSON 字符串。返回整数句柄。</summary>
        [JSImport("createSampler", "render_webgpu")]
        public static partial int CreateSampler(string descriptorJson);

        /// <summary>销毁采样器。</summary>
        [JSImport("destroySampler", "render_webgpu")]
        public static partial void DestroySampler(int id);



        /// <summary>
        /// 开帧：建立命令编码器并 begin 一个渲染通道。
        /// </summary>
        /// <param name="depthClear">深度清屏值；&lt; 0 表示不带深度附件。</param>
        /// <param name="colorTarget">颜色附件句柄；0 = 画布交换链。</param>
        /// <param name="resolveTarget">
        /// 解析目标句柄（多重采样时必填）：渲染进 colorTarget，通道结束时自动解析到这里。
        /// WebGPU 的 resolveTarget 必须在通道创建时指定，不能像 GL 的 blitFramebuffer 那样事后解析。
        /// </param>
        /// <param name="depthTarget">深度附件句柄；0 = 用画布自带的深度纹理。</param>
        /// <param name="loadMode">
        /// 通道载入方式：0 = clear（按 clearValue 清屏），1 = load（沿用目标里已有的内容）。
        /// 切换渲染目标会结束当前通道，之后继续绘制<b>必须</b>用 1，否则会擦掉已画好的部分。
        /// </param>
        [JSImport("beginFrame", "render_webgpu")]
        public static partial void BeginFrame(float r, float g, float b, float a, float depthClear,
            int colorTarget, int resolveTarget, int depthTarget, int loadMode);

        /// <summary>绑定渲染管线。</summary>
        [JSImport("setPipeline", "render_webgpu")]
        public static partial void SetPipeline(int id);

        /// <summary>绑定第 slot 个顶点缓冲。</summary>
        [JSImport("setVertexBuffer", "render_webgpu")]
        public static partial void SetVertexBuffer(int slot, int bufferId);

        /// <summary>绑定索引缓冲（type: "uint16" / "uint32"）。</summary>
        [JSImport("setIndexBuffer", "render_webgpu")]
        public static partial void SetIndexBuffer(int bufferId, string type);

        /// <summary>绑定第 group 个绑定组。</summary>
        [JSImport("setBindGroup", "render_webgpu")]
        public static partial void SetBindGroup(int group, int bindGroupId);

        /// <summary>按顶点顺序绘制。</summary>
        [JSImport("draw", "render_webgpu")]
        public static partial void Draw(int vertexCount, int instanceCount);

        /// <summary>按索引缓冲绘制。</summary>
        /// <param name="indexCount">本次绘制的索引个数（每个精灵 6 个）。</param>
        /// <param name="instanceCount">实例数（非实例化绘制传 1）。</param>
        /// <param name="firstIndex">起始索引下标。批处理里恒为 0：静态索引是每个四边形内部编号为基准的。</param>
        /// <param name="baseVertex">GPU 会把它加到每个索引值上。批处理中顶点是【累加分区】写入动态顶点缓冲的，
        /// 用该参数把相对索引偏移到本次写入的顶点区间起点。</param>
        [JSImport("drawIndexed", "render_webgpu")]
        public static partial void DrawIndexed(int indexCount, int instanceCount, int firstIndex, int baseVertex);

        /// <summary>收帧：结束渲染通道并提交命令缓冲区。</summary>
        [JSImport("endFrame", "render_webgpu")]
        public static partial void EndFrame();



        /// <summary>WebGPU 无全局错误码，恒返回 0（错误走 device.lost / uncapturederror 异步事件）。</summary>
        [JSImport("getError", "render_webgpu")]
        public static partial int GetError();

        /// <summary>读取整数型上下文参数（如 MAX_TEXTURE_SIZE / MAX_SAMPLES）。</summary>
        [JSImport("getParameterInt", "render_webgpu")]
        public static partial int GetParameterInt(int pname);

        /// <summary>读取字符串型上下文参数（VERSION / RENDERER 等）。</summary>
        [JSImport("getParameterString", "render_webgpu")]
        public static partial string GetParameterString(int pname);

        /// <summary>
        /// 异步读回纹理区域像素（copyTextureToBuffer + mapAsync）：结果暂存于 JS 模块，随后调用
        /// <see cref="ReadPixelsGet"/> 同步拷出。textureId 为 createTexture 返回的整数句柄；纹理须带 COPY_SRC
        /// 用途（离屏 RT 已具备）。（JSImport 生成器不支持「异步 + 数组/MemoryView」，故拆成「异步发起 + 同步取回」两步。）
        /// </summary>
        [JSImport("readPixels", "render_webgpu")]
        public static partial Task ReadPixels(int textureId, int x, int y, int width, int height);

        /// <summary>把上一次 <see cref="ReadPixels"/> 异步读回的 RGBA8 像素（自上而下、长度 width*height*4）同步拷进 rgba。</summary>
        [JSImport("readPixelsGet", "render_webgpu")]
        public static partial void ReadPixelsGet([JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

    }
}
