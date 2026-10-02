using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGPU 渲染后端（与 <see cref="WebGl20Backend"/> 并列实现 <see cref="IGraphicsBackend"/>）。
    /// <para>
    /// 两个 WebGPU 特有的坑（照 WebGPU 规范，改动前务必读懂）：
    /// <list type="bullet">
    ///   <item><description><b>uniform 必须分槽</b>：<c>queue.writeBuffer</c> 排队到时间线，同一帧重复写同一段内存
    ///   会让该帧所有 draw 都看到最后一次写入的值，故每次 Apply（SpriteBatch.Begin）占一个独立槽位。</description></item>
    ///   <item><description><b>顶点必须累加分区</b>：同理，同一帧多次写同一顶点区间会互相覆盖，故顶点按 bump 分配器往后排，
    ///   用 DrawIndexed 的 baseVertex 把索引偏移到本次区间起点。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 暂未实现（会抛 <see cref="NotSupportedException"/>）：渲染目标 / 离屏渲染、读像素、区域纹理更新。
    /// 这些是步骤④的内容，先保证屏幕直绘的主链路可用。
    /// </para>
    /// </summary>
    internal sealed class WebGpuBackend : IGraphicsBackend
    {
        private const int VerticesPerSprite = 4;
        private const int IndicesPerSprite = 6;

        /// <summary>uniform 槽位步长：WebGPU 的 minUniformBufferOffsetAlignment = 256。</summary>
        private const int UniformSlotStride = 256;
        private const int UniformSlotCount = 64;

        /// <summary>mat4x4&lt;f32&gt; 的字节数。</summary>
        private const int TransformSizeInBytes = 64;

        private static readonly int VertexSizeInBytes = VertexPositionColorTexture.SizeInBytes; // 20

        /// <summary>单帧顶点容量（按精灵数）。uint16 索引上限决定了不能超过 16384 个精灵。</summary>
        private static readonly int MaxSpritesPerFrame = GraphicsDevice.MaxBatchSize * 4;

        // 后端自有资源表：领域对象 → GPU 句柄。
        // （WebGL 后端把 JSObject 直接存在 Texture2D.Handle 里；WebGPU 用整数句柄，故在后端自己建表。）
        private readonly Dictionary<Texture2D, int> _textures = new();
        private readonly Dictionary<string, int> _pipelines = new();
        private readonly Dictionary<string, int> _samplers = new();
        private readonly Dictionary<(int Pipeline, int Texture, int Sampler, int Slot), int> _bindGroups = new();

        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        private int _shaderModule;
        private int _vertexBuffer;
        private int _indexBuffer;
        private int _uniformBuffer;

        private BlendState _blend = null!;
        private SamplerState _sampler = null!;
        private Texture2D? _boundTexture;

        private bool _frameActive;
        private int _vertexBump;
        private int _nextUniformSlot;
        private int _uniformSlot;

        public string Name => "WebGPU";

        public int MaxTextureSize { get; private set; }

        public string Renderer { get; private set; } = string.Empty;

        public async Task InitializeAsync(string canvasSelector, bool antialias)
        {
            bool ok = await JSBind_WebGPU.Init(canvasSelector, antialias).ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException("无法创建 WebGPU 上下文，请使用支持 WebGPU 的浏览器。");

            MaxTextureSize = JSBind_WebGPU.GetParameterInt(JSBind_WebGPU.MAX_TEXTURE_SIZE);
            Renderer = JSBind_WebGPU.GetParameterString(JSBind_WebGPU.RENDERER);

            _shaderModule = JSBind_WebGPU.CreateShaderModule(WgslSource);
            if (_shaderModule == 0) throw new InvalidOperationException("[webgpu] 创建着色器模块失败。");

            _indexBuffer = JSBind_WebGPU.CreateBuffer(
                MaxSpritesPerFrame * IndicesPerSprite * sizeof(ushort),
                JSBind_WebGPU.BUFFER_USAGE_INDEX | JSBind_WebGPU.BUFFER_USAGE_COPY_DST);
            JSBind_WebGPU.WriteBuffer(_indexBuffer, 0, BuildQuadIndices(MaxSpritesPerFrame));

            _vertexBuffer = JSBind_WebGPU.CreateBuffer(
                MaxSpritesPerFrame * VerticesPerSprite * VertexSizeInBytes,
                JSBind_WebGPU.BUFFER_USAGE_VERTEX | JSBind_WebGPU.BUFFER_USAGE_COPY_DST);

            _uniformBuffer = JSBind_WebGPU.CreateBuffer(
                UniformSlotCount * UniformSlotStride,
                JSBind_WebGPU.BUFFER_USAGE_UNIFORM | JSBind_WebGPU.BUFFER_USAGE_COPY_DST);
        }

        public ISpriteProgram CreateSpriteProgram() => new WebGpuSpriteProgram(this);

        // ================================================================
        // 主链路：清屏 / 状态 / 绘制 / 收帧
        // ================================================================

        /// <summary>
        /// 开帧并清屏。WebGPU 没有独立的 clear：clearValue 必须在 beginRenderPass 时给出，
        /// 所以本方法即"开帧"。若上一帧的通道还开着，先提交掉。
        /// </summary>
        public void Clear(Color color)
        {
            if (_frameActive) EndFrame();

            JSBind_WebGPU.BeginFrame(
                color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f,
                -1f); // depthClear < 0 → 不带深度附件（2D 精灵不需要）

            _frameActive = true;
            _vertexBump = 0;
            _bindGroups.Clear();

            // 槽位每帧重置；先占 0 号槽写单位矩阵，避免未 Apply 就绘制时取到无效槽位。
            _nextUniformSlot = 0;
            WriteMatrix(Matrix4x4.Identity, _matrixBuffer);
            JSBind_WebGPU.WriteBuffer(_uniformBuffer, 0, _matrixBuffer);
            _uniformSlot = 0;
            _nextUniformSlot = 1;
        }

        /// <summary>收帧：结束渲染通道并提交命令缓冲。GraphicsDeviceManager.EndDraw 会调用它。</summary>
        public void EndFrame()
        {
            if (!_frameActive) return;
            JSBind_WebGPU.EndFrame();
            _frameActive = false;
            _vertexBump = 0;
        }

        public void SetBlendState(BlendState state) => _blend = state;

        public void SetSamplerState(SamplerState state) => _sampler = state;

        public void BindTexture(Texture2D texture) => _boundTexture = texture;

        /// <summary>
        /// 光栅化 / 深度状态在 WebGPU 里是<b>管线对象的属性</b>，不能像 WebGL 那样随时切换。
        /// 当前精灵管线固定为「不剔除 + 无深度」，故这两个方法暂不下发（2D 主链路不受影响）。
        /// </summary>
        public void ApplyRasterizerState(RasterizerState state) { }

        public void ApplyDepthStencilState(DepthStencilState state) { }

        public void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;

            EnsureFrameActive();

            if (_boundTexture is null || !_textures.TryGetValue(_boundTexture, out int textureHandle))
                return; // 没有绑定纹理就无从采样，跳过（WebGL 侧会绑到 0 号纹理，行为不同但 2D 链路必先绑纹理）

            int pipeline = GetOrCreatePipeline(_blend);
            int samplerHandle = GetOrCreateSampler(_sampler);
            int bindGroup = GetOrCreateBindGroup(pipeline, textureHandle, samplerHandle, _uniformSlot);

            if (_vertexBump + vRun > MaxSpritesPerFrame * VerticesPerSprite)
                throw new InvalidOperationException(
                    $"[webgpu] 单帧顶点超出容量：上限 {MaxSpritesPerFrame} 个精灵，已用 {_vertexBump / VerticesPerSprite} 个。");

            int baseVertex = _vertexBump;
            JSBind_WebGPU.WriteBuffer(
                _vertexBuffer,
                baseVertex * VertexSizeInBytes,
                MemoryMarshal.AsBytes(vertices.AsSpan(start, vRun)));

            JSBind_WebGPU.SetPipeline(pipeline);
            JSBind_WebGPU.SetVertexBuffer(0, _vertexBuffer);
            JSBind_WebGPU.SetIndexBuffer(_indexBuffer, "uint16");
            JSBind_WebGPU.SetBindGroup(0, bindGroup);
            // firstIndex 恒为 0：静态索引以"每个四边形内部编号"为基准，靠 baseVertex 偏移到本批次顶点区间。
            JSBind_WebGPU.DrawIndexed(vRun / VerticesPerSprite * IndicesPerSprite, 1, 0, baseVertex);

            _vertexBump += vRun;
        }

        private void EnsureFrameActive()
        {
            if (_frameActive) return;
            // 没显式 Clear 就开画（照 WebGL 的宽容行为）：开一个透明清屏的通道。
            Clear(new Color(0, 0, 0, 255));
        }


        // ================================================================
        // 资源：管线 / 采样器 / 绑定组
        // ================================================================

        private int GetOrCreatePipeline(BlendState blend)
        {
            string key = blend.SourceBlend + "|" + blend.DestinationBlend + "|" + blend.SourceAlphaBlend + "|" + blend.DestinationAlphaBlend;
            if (_pipelines.TryGetValue(key, out int pipeline)) return pipeline;

            // 注：BlendState 目前仍存 GL 枚举值（步骤② 会把它中和为中立枚举）。
            // render_webgpu.ts 的 blendFactor() 同时接受 GL 枚举数字与 WebGPU 字符串，
            // 这里直接把 GL 值透传过去，由 JS 侧统一映射——迁移期省一份重复映射表。
            string json = "{\"vertexShader\":" + _shaderModule +
                          ",\"fragmentShader\":" + _shaderModule +
                          ",\"vertex\":{\"entryPoint\":\"vs_main\",\"buffers\":" + VertexLayoutJson + "}" +
                          ",\"fragment\":{\"entryPoint\":\"fs_main\"}" +
                          ",\"primitive\":{\"topology\":\"triangle-list\",\"cullMode\":\"none\",\"frontFace\":\"ccw\"}" +
                          ",\"blend\":{\"color\":{\"srcFactor\":" + blend.SourceBlend +
                          ",\"dstFactor\":" + blend.DestinationBlend +
                          ",\"operation\":\"add\"},\"alpha\":{\"srcFactor\":" + blend.SourceAlphaBlend +
                          ",\"dstFactor\":" + blend.DestinationAlphaBlend +
                          ",\"operation\":\"add\"}}}";

            pipeline = JSBind_WebGPU.CreatePipeline(json);
            if (pipeline == 0) throw new InvalidOperationException("[webgpu] 创建渲染管线失败。");
            _pipelines[key] = pipeline;
            return pipeline;
        }

        private int GetOrCreateSampler(SamplerState sampler)
        {
            string key = sampler.MinFilter + "|" + sampler.MagFilter + "|" + sampler.WrapMode;
            if (_samplers.TryGetValue(key, out int handle)) return handle;

            string json = "{\"magFilter\":\"" + FilterName(sampler.MagFilter) +
                          "\",\"minFilter\":\"" + FilterName(sampler.MinFilter) +
                          "\",\"addressU\":\"" + AddressName(sampler.WrapMode) +
                          "\",\"addressV\":\"" + AddressName(sampler.WrapMode) +
                          "\",\"mipmapFilter\":\"nearest\"}";

            handle = JSBind_WebGPU.CreateSampler(json);
            if (handle == 0) throw new InvalidOperationException("[webgpu] 创建采样器失败。");
            _samplers[key] = handle;
            return handle;
        }

        private static string FilterName(int glFilter)
            => glFilter == JSBind_WEBGL20.NEAREST ? "nearest" : "linear";

        private static string AddressName(int glWrap)
            => glWrap switch
            {
                JSBind_WEBGL20.REPEAT => "repeat",
                JSBind_WEBGL20.MIRRORED_REPEAT => "mirror-repeat",
                _ => "clamp-to-edge",
            };

        private int GetOrCreateBindGroup(int pipeline, int textureHandle, int samplerHandle, int uniformSlot)
        {
            var key = (pipeline, textureHandle, samplerHandle, uniformSlot);
            if (_bindGroups.TryGetValue(key, out int bindGroup)) return bindGroup;

            string entries = "[{\"binding\":0,\"type\":\"uniform\",\"id\":" + _uniformBuffer +
                             ",\"offset\":" + (uniformSlot * UniformSlotStride) +
                             ",\"size\":" + TransformSizeInBytes + "}" +
                             ",{\"binding\":1,\"type\":\"texture\",\"id\":" + textureHandle + "}" +
                             ",{\"binding\":2,\"type\":\"sampler\",\"id\":" + samplerHandle + "}]";

            bindGroup = JSBind_WebGPU.CreateBindGroup(pipeline, 0, entries);
            if (bindGroup == 0) throw new InvalidOperationException("[webgpu] 创建绑定组失败。");
            _bindGroups[key] = bindGroup;
            return bindGroup;
        }

        /// <summary>分配一个新的 uniform 槽位并写入矩阵（由精灵程序的 Apply 调用）。</summary>
        internal void AllocateTransform(in Matrix4x4 matrix)
        {
            if (_nextUniformSlot >= UniformSlotCount)
                throw new InvalidOperationException(
                    $"[webgpu] 单帧 uniform 槽位耗尽（上限 {UniformSlotCount}）：SpriteBatch 的 Begin/End 次数过多，请合并批次。");

            _uniformSlot = _nextUniformSlot++;
            WriteMatrix(matrix, _matrixBuffer);
            JSBind_WebGPU.WriteBuffer(_uniformBuffer, _uniformSlot * UniformSlotStride, _matrixBuffer);
        }


        // ================================================================
        // 纹理
        // ================================================================

        public void CreateTexture(Texture2D texture, int width, int height, bool mipmap, SurfaceFormat format, Texture2D.SurfaceType type)
        {
            if (type == Texture2D.SurfaceType.RenderTarget)
                throw new NotSupportedException("WebGPU 后端暂不支持渲染目标（步骤④）。");

            int handle = JSBind_WebGPU.CreateTexture(width, height, "rgba8unorm");
            if (handle == 0) throw new InvalidOperationException("[webgpu] 创建纹理失败。");
            _textures[texture] = handle;
        }

        /// <summary>整张上传（origin 为 0,0）。</summary>
        public void SetTextureData(Texture2D texture, int level, byte[] bytes)
            => JSBind_WebGPU.UploadTexture(RequireTexture(texture), bytes,
                0, 0, texture.Width, texture.Height, "rgba8unorm");

        /// <summary>
        /// 区域上传。WebGPU 的 queue.writeTexture 原生支持写入原点，故子区域直接走 origin 即可
        /// —— SpriteFont 的字形图集正是靠它逐块填充的，这里是 2D 文本渲染的必经路径。
        /// </summary>
        public void SetTextureData(Texture2D texture, int level, Rectangle rect, byte[] bytes)
            => JSBind_WebGPU.UploadTexture(RequireTexture(texture), bytes,
                rect.X, rect.Y, rect.Width, rect.Height, "rgba8unorm");

        public void DeleteTexture(Texture2D texture)
        {
            if (!_textures.TryGetValue(texture, out int handle)) return;
            JSBind_WebGPU.DestroyTexture(handle);
            _textures.Remove(texture);
        }

        private int RequireTexture(Texture2D texture)
        {
            if (_textures.TryGetValue(texture, out int handle)) return handle;
            throw new InvalidOperationException("[webgpu] 纹理尚未在 WebGPU 后端创建。");
        }


        // ================================================================
        // 尚未实现的能力（步骤④）
        // ================================================================

        public void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba)
            => throw new NotSupportedException("WebGPU 后端暂不支持读像素（步骤④）。");

        public void SetScissor(int x, int y, int width, int height) { }

        public void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
            => throw new NotSupportedException("WebGPU 后端暂不支持渲染目标（步骤④）。");

        public void DeleteRenderTarget(IRenderTarget renderTarget)
        {
            // 未创建过就没有资源可释放。
        }

        public IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count)
            => throw new NotSupportedException("WebGPU 后端暂不支持渲染目标（步骤④）。");

        public void ApplyDefaultRenderTarget() { }

        public void ResolveRenderTarget(IRenderTarget renderTarget) { }


        public int GetError() => 0;

        public void SetViewport(int x, int y, int width, int height)
            => JSBind_WebGPU.Resize(width, height);

        public void Dispose()
        {
            foreach (int bindGroup in _bindGroups.Values) JSBind_WebGPU.DestroyBindGroup(bindGroup);
            _bindGroups.Clear();

            foreach (int sampler in _samplers.Values) JSBind_WebGPU.DestroySampler(sampler);
            _samplers.Clear();

            foreach (int pipeline in _pipelines.Values) JSBind_WebGPU.DestroyPipeline(pipeline);
            _pipelines.Clear();

            foreach (int texture in _textures.Values) JSBind_WebGPU.DestroyTexture(texture);
            _textures.Clear();

            JSBind_WebGPU.DestroyShaderModule(_shaderModule);
            JSBind_WebGPU.DestroyBuffer(_vertexBuffer);
            JSBind_WebGPU.DestroyBuffer(_indexBuffer);
            JSBind_WebGPU.DestroyBuffer(_uniformBuffer);
        }

        /// <summary>
        /// 静态四边形索引，与 <see cref="WebGl20Backend"/> 完全一致：
        /// 顶点顺序 TL,TR,BL,BR，两个三角形绕向相同 —— 三角1 = (0,1,2)，三角2 = (1,3,2)。
        /// </summary>
        private static byte[] BuildQuadIndices(int spriteCount)
        {
            byte[] data = new byte[spriteCount * IndicesPerSprite * sizeof(ushort)];
            Span<ushort> indices = MemoryMarshal.Cast<byte, ushort>(data.AsSpan());
            for (int i = 0; i < spriteCount; i++)
            {
                int v = i * VerticesPerSprite;
                int o = i * IndicesPerSprite;
                indices[o + 0] = (ushort)(v + 0);
                indices[o + 1] = (ushort)(v + 1);
                indices[o + 2] = (ushort)(v + 2);
                indices[o + 3] = (ushort)(v + 1);
                indices[o + 4] = (ushort)(v + 3);
                indices[o + 5] = (ushort)(v + 2);
            }
            return data;
        }

        /// <summary>
        /// 把矩阵写成 16 个 float 的小端字节流，按【字段原顺序】直写，不转置。
        /// WGSL 的 mat4x4&lt;f32&gt; 是列主序；"行主序内存原样摊平"恰好等于"转置矩阵的列主序"，
        /// 而列向量的 v' = Mᵀ × v 与引擎行向量约定的 p' = p × M 等价（与 WebGL 侧同一结论）。
        /// </summary>
        private static void WriteMatrix(in Matrix4x4 value, Span<byte> destination)
        {
            Write(destination, 0, value.M11);  Write(destination, 1, value.M12);
            Write(destination, 2, value.M13);  Write(destination, 3, value.M14);
            Write(destination, 4, value.M21);  Write(destination, 5, value.M22);
            Write(destination, 6, value.M23);  Write(destination, 7, value.M24);
            Write(destination, 8, value.M31);  Write(destination, 9, value.M32);
            Write(destination, 10, value.M33); Write(destination, 11, value.M34);
            Write(destination, 12, value.M41); Write(destination, 13, value.M42);
            Write(destination, 14, value.M43); Write(destination, 15, value.M44);
        }

        private static void Write(Span<byte> destination, int index, float value)
            => BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);

        /// <summary>
        /// 精灵着色器（WGSL）。与 WebGL 侧的 GLSL SpriteEffect 语义一致：
        /// 顶点 = 位置+UV+颜色，片元 = 纹理采样 × 顶点色。
        /// </summary>
        private const string WgslSource = """
            struct Transform {
                proj : mat4x4<f32>,
            };

            @group(0) @binding(0) var<uniform> uTransform : Transform;
            @group(0) @binding(1) var uTexture : texture_2d<f32>;
            @group(0) @binding(2) var uSampler : sampler;

            struct VertexInput {
                @location(0) position : vec2<f32>,
                @location(1) texCoord : vec2<f32>,
                @location(2) color : vec4<f32>,
            };

            struct VertexOutput {
                @builtin(position) clipPosition : vec4<f32>,
                @location(0) texCoord : vec2<f32>,
                @location(1) color : vec4<f32>,
            };

            @vertex
            fn vs_main(input : VertexInput) -> VertexOutput {
                var output : VertexOutput;
                var clip : vec4<f32> = uTransform.proj * vec4<f32>(input.position, 0.0, 1.0);
            // 【WebGPU 与 WebGL 的裁剪空间差异】
            // WebGL 的 NDC z ∈ [-1,1]，WebGPU 的 NDC z ∈ [0,1]。
            // 而本引擎的投影矩阵是按 OpenGL 约定生成的：CreateOrthographicScreen(w,h)
            // = CreateOrthographicOffCenter(0, w, h, 0, 0, 1)，其中 M33=-2、M43=-1，
            // 精灵 z=0 按行向量约定算出 z_ndc = 0*(-2) + 1*(-1) = -1。
            // 这个值在 WebGL 下正好落在近裁剪面上（可见），但在 WebGPU 下 < 0 会被引擎直接裁掉 ——
            // 症状是「不报任何错误、画面全黑」。故这里做标准换算 [-1,1] → [0,1]：(z + w) / 2。
            clip.z = (clip.z + clip.w) * 0.5;
            output.clipPosition = clip;
                output.texCoord = input.texCoord;
                output.color = input.color;
                return output;
            }

            @fragment
            fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
                return textureSample(uTexture, uSampler, input.texCoord) * input.color;
            }
            """;

        /// <summary>
        /// 顶点布局，与 VertexPositionColorTexture 严格对应（步长 20 字节）。
        /// <para>
        /// 格式名必须用 WebGPU 的 <c>GPUVertexFormat</c> 枚举值：颜色是 4 个【无符号归一化字节】，
        /// 对应 <c>unorm8x4</c>（不是 wgpu / Dawn 里的 <c>uchar4norm</c>，那个名字在浏览器会直接报
        /// "not a valid enum value of type GPUVertexFormat"）。
        /// </para>
        /// </summary>
        private const string VertexLayoutJson =
            "[{\"arrayStride\":20,\"stepMode\":\"vertex\",\"attributes\":[" +
            "{\"shaderLocation\":0,\"offset\":0,\"format\":\"float32x2\"}," +
            "{\"shaderLocation\":1,\"offset\":8,\"format\":\"float32x2\"}," +
            "{\"shaderLocation\":2,\"offset\":16,\"format\":\"unorm8x4\"}]}]";

        /// <summary>WebGPU 的精灵程序：Apply 时把投影矩阵写进一个新的 uniform 槽位。</summary>
        private sealed class WebGpuSpriteProgram : ISpriteProgram
        {
            private readonly WebGpuBackend _backend;

            internal WebGpuSpriteProgram(WebGpuBackend backend) => _backend = backend;

            public void Apply(Matrix4x4 projection) => _backend.AllocateTransform(projection);

            public void Dispose() { }
        }
    }

}
