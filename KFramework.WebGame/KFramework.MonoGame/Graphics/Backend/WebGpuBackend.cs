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

        // 槽位只在"清屏开帧"时重置，故一帧内切换 PreserveContents 目标（走 load，不重置）会持续消耗槽位。
        // 传奇那种一帧内多次在画布与离屏之间来回切的用法需要足够余量，256 个（64KB）留足空间。
        private const int UniformSlotCount = 256;

        /// <summary>mat4x4&lt;f32&gt; 的字节数。</summary>
        private const int TransformSizeInBytes = 64;

        private static readonly int VertexSizeInBytes = VertexPositionColorTexture.SizeInBytes; // 20

        /// <summary>离屏渲染目标的纹理格式（画布格式可能不同，二者管线不可混用）。</summary>
        private const string RtColorFormat = "rgba8unorm";

        /// <summary>深度附件格式（通道的 depthStencil 与管线的 depthStencil 必须一致）。</summary>
        private const string RtDepthFormat = "depth24plus";

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

        // 最近一次写入 uniform 的变换矩阵，用于复用槽位（见 AllocateTransform）。
        private Matrix4x4 _lastTransform;
        private bool _hasTransform;

        public string Name => "WebGPU";

        /// <summary>WebGPU 的附件与纹理原点都在左上，与屏幕一致，离屏渲染<b>不需要</b>额外翻转（详见接口注释）。</summary>
        public bool NeedsOffscreenYFlip => false;

        public int MaxTextureSize { get; private set; }

        public string Renderer { get; private set; } = string.Empty;

        public async Task InitializeAsync(bool antialias)
        {
            bool ok = await JSBind_WebGPU.Init(antialias).ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException("无法创建 WebGPU 上下文，请使用支持 WebGPU 的浏览器。");

            MaxTextureSize = JSBind_WebGPU.GetParameterInt(JSBind_WebGPU.MAX_TEXTURE_SIZE);
            Renderer = JSBind_WebGPU.GetParameterString(JSBind_WebGPU.RENDERER);

            _canvasFormat = JSBind_WebGPU.GetPreferredFormat();
            _targetFormat = _canvasFormat;
            // 画布 MSAA 采样数由 render_webgpu.ts 按 antialias 决定（4 或 1），这里保持一致。
            _canvasSampleCount = antialias ? 4 : 1;
            _targetSampleCount = _canvasSampleCount;

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
        public void Clear(Color color) => BeginPass(color, clear: true);

        /// <summary>
        /// 开一个通道。
        /// </summary>
        /// <param name="clear">
        /// true = 按 clearValue 清屏（游戏显式调 <c>Clear</c>）；
        /// false = <b>沿用目标里已有的内容</b>。切换渲染目标会结束当前通道，之后继续绘制必须走 false，
        /// 否则会擦掉已画好的部分 —— 传奇那种「画布 ↔ 多个离屏 RT 来回切换做合成」正是这个模式。
        /// </param>
        private void BeginPass(Color clearColor, bool clear)
        {
            if (_frameActive) EndFrame();

            JSBind_WebGPU.BeginFrame(
                clearColor.R / 255f, clearColor.G / 255f, clearColor.B / 255f, clearColor.A / 255f,
                _depthTarget != 0 ? 1f : -1f,   // depthClear < 0 → 不带深度附件（2D 精灵不需要）
                _colorTarget, _resolveTarget, _depthTarget,
                clear ? 0 : 1);                 // 0 = clear，1 = load

            _frameActive = true;
            _vertexBump = 0;

            // 【关键】只有"清屏开帧"才重置 uniform 槽位。
            // "沿用内容开帧"（load）绝不能动槽位：它是在【本批次第一次绘制】时才被触发的，
            // 此时 SpriteBatch.Begin 早已经 Effect.Apply 分配好槽位并写入投影矩阵；
            // 一重置就会让本批次退回 0 号槽（单位矩阵），画面整体塌进右上角（看着像黑屏）。
            if (!clear) return;

            _bindGroups.Clear();

            // 槽位重置；先占 0 号槽写单位矩阵，避免未 Apply 就绘制时取到无效槽位。
            _nextUniformSlot = 0;
            WriteMatrix(Matrix4x4.Identity, _matrixBuffer);
            JSBind_WebGPU.WriteBuffer(_uniformBuffer, 0, _matrixBuffer);
            _uniformSlot = 0;
            _nextUniformSlot = 1;
            // 0 号槽刚写入单位矩阵，记录下来以便首个批次直接复用它。
            _lastTransform = Matrix4x4.Identity;
            _hasTransform = true;
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

        // ---- 当前渲染目标集合（0 = 画布交换链）----
        // 通道附件在【通道创建时】就固定下来，所以切换目标必须先结束当前通道。
        private int _colorTarget;
        private int _resolveTarget;
        private int _depthTarget;

        /// <summary>当前目标集合的颜色格式（画布可能是 bgra8unorm，离屏 RT 是 rgba8unorm）。初始化时填入画布格式。</summary>
        private string _targetFormat = string.Empty;

        /// <summary>当前目标集合的采样数（离屏 RT 的 MSAA 与画布 antialias 是两回事）。</summary>
        private int _targetSampleCount = 1;

        /// <summary>画布首选格式（切回画布时恢复用）。</summary>
        private string _canvasFormat = "rgba8unorm";

        /// <summary>画布 MSAA 采样数（切回画布时恢复用）。</summary>
        private int _canvasSampleCount = 1;

        /// <summary>离屏渲染目标的额外附件（多重采样颜色 + 深度）；颜色纹理本身走 _textures。</summary>
        private readonly Dictionary<IRenderTarget, (int Msaa, int Depth)> _renderTargetExtras = new();

        /// <summary>当前深度/模板状态。WebGPU 里它是管线的烘焙属性，故先记下来，建管线时一起编进去。</summary>
        private DepthStencilState _depthStencil = DepthStencilState.None;

        /// <summary>
        /// 光栅化状态在 WebGPU 里是<b>管线对象的属性</b>，不能像 WebGL 那样随时切换。
        /// 当前精灵管线固定为「不剔除」（<see cref="CullMode.Off"/>），故这里暂不下发（2D 主链路不受影响）。
        /// </summary>
        public void ApplyRasterizerState(RasterizerState state) { }

        public void ApplyDepthStencilState(DepthStencilState state) => _depthStencil = state;

        public void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;

            EnsureFrameActive();

            if (_boundTexture is null || !_textures.TryGetValue(_boundTexture, out int textureHandle))
            {
                Console.WriteLine($"[wgpu-diag] SKIP(无纹理) tgt={(_colorTarget == 0 ? "canvas" : "rt")} bound={(_boundTexture is null ? "null" : "不在表中")}");
                return;
            }

            int pipeline = GetOrCreatePipeline(_blend, _depthStencil);
            int samplerHandle = GetOrCreateSampler(_sampler);
            int bindGroup = GetOrCreateBindGroup(pipeline, textureHandle, samplerHandle, _uniformSlot);

            // ==== 临时诊断（定位离屏 RT 显示异常用，跑完即删）====
            Console.WriteLine($"[wgpu-diag] tgt={(_colorTarget == 0 ? "canvas" : "rt")} fmt={_targetFormat} sc={_targetSampleCount} " +
                $"slot={_uniformSlot} bv={_vertexBump} start={start} end={end} vRun={vRun} pipe={pipeline} bg={bindGroup} tex={textureHandle}");

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

            // 没显式 Clear 就开画：<b>沿用目标里已有的内容</b>（load），绝不清屏。
            // 这里曾无条件走 Clear()，于是每次切换渲染目标都会擦掉目标里已画好的东西 ——
            // 表现为离屏 RT 永远只剩最后一次绘制（PreserveContents 形同虚设）、
            // 以及"画布画一半 → 切 RT → 切回"时前半段凭空消失。
            // 需要清屏时上层会显式调 Clear()，或由 GraphicsDevice 按
            // RenderTargetUsage.DiscardContents 自动清（见 GraphicsDevice.ApplyRenderTargets）。
            BeginPass(default, clear: false);
        }


        // ================================================================
        // 资源：管线 / 采样器 / 绑定组
        // ================================================================

        private int GetOrCreatePipeline(BlendState blend, DepthStencilState depthStencil)
        {
            // 模板尚未接入 WebGPU 后端：需要把 RT 深度格式换成 depth24plus-stencil8，
            // 并在 beginRenderPass 里补上 stencilAttachment / stencilClearValue。与其静默失效，宁可报错。
            if (depthStencil.Stencil.Enabled)
                throw new NotSupportedException(
                    "[webgpu] 模板缓冲尚未接入 WebGPU 后端（需 depth24plus-stencil8 格式 + 通道的模板附件）。当前请用 WebGL 后端。");

            DepthState depth = depthStencil.Depth;
            // PSO 缓存键 = 完整状态的内容键（照 DX12 的 PipelineStateManager：对管线描述做内容哈希）。
            // 不能用 Vulkan 那种"指针身份哈希"—— JS/TS 侧没有稳定的指针身份。
            // WebGPU 的管线必须与通道附件严格匹配，故颜色格式 / 采样数 / 是否有深度附件都得进键，
            // 否则切换到不同目标时会因管线与附件不匹配而报错。
            // 混合运算（BlendOp）与深度状态同样是烘焙进管线的，也必须进键。
            string key = _targetFormat + "|" + _targetSampleCount + "|" + (_depthTarget != 0 ? "d" : "-") + "|"
                + blend.SourceColorBlendFactor + "|" + blend.DestinationColorBlendFactor + "|" + blend.ColorBlendOperation + "|"
                + blend.SourceAlphaBlendFactor + "|" + blend.DestinationAlphaBlendFactor + "|" + blend.AlphaBlendOperation + "|"
                + depth.DepthWrite + "|" + depth.DepthCompare;
            if (_pipelines.TryGetValue(key, out int pipeline)) return pipeline;

            string json = "{\"vertexShader\":" + _shaderModule +
                          ",\"fragmentShader\":" + _shaderModule +
                          ",\"vertex\":{\"entryPoint\":\"vs_main\",\"buffers\":" + VertexLayoutJson + "}" +
                          ",\"fragment\":{\"entryPoint\":\"fs_main\"}" +
                          ",\"primitive\":{\"topology\":\"triangle-list\",\"cullMode\":\"none\",\"frontFace\":\"ccw\"}" +
                          ",\"colorFormat\":\"" + _targetFormat + "\"" +
                          ",\"sampleCount\":" + _targetSampleCount +
                          (_depthTarget != 0
                              // 深度测试关闭时用 always 比较 + 禁止写入，等价于 GL 的 disable(DEPTH_TEST)；
                              // WebGPU 没有独立的深度开关，只能这样表达。
                              ? ",\"depthStencil\":{\"format\":\"" + RtDepthFormat +
                                "\",\"depthWriteEnabled\":" + (depth.DepthWrite ? "true" : "false") +
                                ",\"depthCompare\":\"" + CompareFunctionName(depth.DepthCompare) + "\"}"
                              : string.Empty) +
                          ",\"blend\":{\"color\":{\"srcFactor\":\"" + BlendModeName(blend.SourceColorBlendFactor) +
                          "\",\"dstFactor\":\"" + BlendModeName(blend.DestinationColorBlendFactor) +
                          "\",\"operation\":\"" + BlendOpName(blend.ColorBlendOperation) +
                          "\"},\"alpha\":{\"srcFactor\":\"" + BlendModeName(blend.SourceAlphaBlendFactor) +
                          "\",\"dstFactor\":\"" + BlendModeName(blend.DestinationAlphaBlendFactor) +
                          "\",\"operation\":\"" + BlendOpName(blend.AlphaBlendOperation) +
                          "\"}}}";

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

        /// <summary>中立混合因子 → WebGPU 的 GPUBlendFactor 字符串。</summary>
        private static string BlendModeName(BlendMode mode) => mode switch
        {
            BlendMode.Zero => "zero",
            BlendMode.One => "one",
            BlendMode.DstColor => "dst",
            BlendMode.SrcColor => "src",
            BlendMode.OneMinusDstColor => "one-minus-dst",
            BlendMode.SrcAlpha => "src-alpha",
            BlendMode.OneMinusSrcColor => "one-minus-src",
            BlendMode.DstAlpha => "dst-alpha",
            BlendMode.OneMinusDstAlpha => "one-minus-dst-alpha",
            BlendMode.SrcAlphaSaturate => "src-alpha-saturated",
            BlendMode.OneMinusSrcAlpha => "one-minus-src-alpha",
            _ => "one",
        };

        /// <summary>中立混合运算 → WebGPU 的 GPUBlendOperation 字符串。</summary>
        private static string BlendOpName(BlendOp op) => op switch
        {
            BlendOp.Sub => "subtract",
            BlendOp.RevSub => "reverse-subtract",
            BlendOp.Min => "min",
            BlendOp.Max => "max",
            _ => "add",
        };

        /// <summary>中立比较函数 → WebGPU 的 GPUCompareFunction 字符串。</summary>
        private static string CompareFunctionName(CompareFunction func) => func switch
        {
            CompareFunction.Never => "never",
            CompareFunction.Less => "less",
            CompareFunction.Equal => "equal",
            CompareFunction.LessEqual => "less-equal",
            CompareFunction.Greater => "greater",
            CompareFunction.NotEqual => "not-equal",
            CompareFunction.GreaterEqual => "greater-equal",
            _ => "always",     // Always 与 Disabled
        };

        private static string FilterName(TextureFilter filter)
            => filter == TextureFilter.Point ? "nearest" : "linear";

        /// <summary>DepthFormat → WebGPU 的 GPUTextureFormat；None 返回空串（不建深度附件）。</summary>
        private static string DepthFormatName(DepthFormat format)
            => format switch
            {
                DepthFormat.None => string.Empty,
                _ => RtDepthFormat,
            };

        private static string AddressName(TextureAddressMode mode)
            => mode switch
            {
                TextureAddressMode.Wrap => "repeat",
                TextureAddressMode.Mirror => "mirror-repeat",
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

        /// <summary>
        /// 分配 uniform 槽位并写入矩阵（由精灵程序的 Apply 调用）。
        /// <para>
        /// 【矩阵相同则复用槽位】同一渲染目标、同一视口下，一帧内大量 <c>SpriteBatch.Begin/End</c>
        /// 用的其实是<b>同一个投影矩阵</b>。重 2D 游戏（每个图元一次 Begin/End）一帧能有几百个批次，
        /// 逐个占槽就等于往 uniform 缓冲里写几百份完全相同的数据，白白耗尽上限。
        /// 这里只在矩阵真的变化时才占新槽 —— 于是槽位消耗从"批次数"降为"投影变化次数"。
        /// </para>
        /// </summary>
        internal void AllocateTransform(in Matrix4x4 matrix)
        {
            if (_hasTransform && SameTransform(matrix, _lastTransform)) return;

            if (_nextUniformSlot >= UniformSlotCount)
                throw new InvalidOperationException(
                    $"[webgpu] 单帧 uniform 槽位耗尽（上限 {UniformSlotCount}）：SpriteBatch 的 Begin/End 次数过多，请合并批次。");

            _uniformSlot = _nextUniformSlot++;
            _lastTransform = matrix;
            _hasTransform = true;
            WriteMatrix(matrix, _matrixBuffer);
            JSBind_WebGPU.WriteBuffer(_uniformBuffer, _uniformSlot * UniformSlotStride, _matrixBuffer);
        }

        /// <summary>逐元素判等（Matrix4x4 未定义 == 运算符，且 Equals 走装箱，不适合每帧数百次的比较）。</summary>
        private static bool SameTransform(in Matrix4x4 a, in Matrix4x4 b)
            => a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14
            && a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24
            && a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34
            && a.M41 == b.M41 && a.M42 == b.M42 && a.M43 == b.M43 && a.M44 == b.M44;


        // ================================================================
        // 纹理
        // ================================================================

        public void CreateTexture(Texture2D texture, int width, int height, bool mipmap, SurfaceFormat format, Texture2D.SurfaceType type)
        {
            // 渲染目标也是一张普通纹理（单采样、可采样），额外附件（MSAA / 深度）在
            // CreateRenderTarget 里补建，与 MonoGame 各后端的分工一致。
            // 仅渲染目标（RenderTarget 类型）需要 COPY_SRC 用途，供 readPixels 中转读回；
            // 普通纹理（字体图集、白色像素、加载的贴图等）一律不加，避免污染用途组合导致闪烁。
            int handle = JSBind_WebGPU.CreateTexture(width, height, "rgba8unorm", 1,
                type == Texture2D.SurfaceType.RenderTarget ? 0x01 : 0);
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

        public async Task ReadPixels(int x, int y, int width, int height, byte[] rgba)
        {
            // 读回前必须先把通道提交掉：pass 还开着时，本通道的写入尚未落进纹理，
            // 此时 copyTextureToBuffer（另一个 encoder 提交）拷到的是【本通道之前】的内容。
            if (_frameActive) EndFrame();

            // 取当前绑定的单采样颜色纹理（MSAA 时取解析目标）；它须带 COPY_SRC 用途（createTexture 已加）。
            int srcTex = _resolveTarget != 0 ? _resolveTarget : _colorTarget;
            if (srcTex == 0) return;
            // 异步发起（copyTextureToBuffer + mapAsync），结果暂存 JS 模块；再同步拷回 rgba。
            await JSBind_WebGPU.ReadPixels(srcTex, x, y, width, height).ConfigureAwait(false);
            JSBind_WebGPU.ReadPixelsGet(rgba);
        }

        public void SetScissor(int x, int y, int width, int height)
        {
            // WebGPU 的 scissor 永远生效（没有 enable 位）。未设置时通道默认用整个附件范围，
            // 因此"不启用裁剪"的行为天然正确；要支持真正的裁剪矩形需另加 setScissorRect 绑定（待办）。
        }

        /// <summary>
        /// 创建渲染目标的额外附件（照 MonoGame 各后端的分工）：多重采样颜色纹理 + 深度纹理。
        /// 可采样的单采样颜色纹理已由 <see cref="CreateTexture"/> 建好。
        /// </summary>
        public void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
        {
            int msaa = 0;
            if (renderTarget.MultiSampleCount > 0)
            {
                // 多重采样颜色附件：只用于渲染，最后解析进单采样纹理。
                msaa = JSBind_WebGPU.CreateTexture(width, height, RtColorFormat, renderTarget.MultiSampleCount, 0);
                if (msaa == 0) throw new InvalidOperationException("[webgpu] 创建多重采样附件失败。");
            }

            int depth = 0;
            string depthName = DepthFormatName(depthFormat);
            if (depthName.Length > 0)
            {
                depth = JSBind_WebGPU.CreateTexture(width, height, depthName, renderTarget.MultiSampleCount, 0);
                if (depth == 0) throw new InvalidOperationException("[webgpu] 创建深度附件失败。");
            }

            _renderTargetExtras[renderTarget] = (msaa, depth);
        }

        public void DeleteRenderTarget(IRenderTarget renderTarget)
        {
            if (!_renderTargetExtras.TryGetValue(renderTarget, out (int Msaa, int Depth) extras)) return;

            if (extras.Msaa != 0) JSBind_WebGPU.DestroyTexture(extras.Msaa);
            if (extras.Depth != 0) JSBind_WebGPU.DestroyTexture(extras.Depth);
            _renderTargetExtras.Remove(renderTarget);
        }

        /// <summary>
        /// 绑定渲染目标集合。
        /// <para>
        /// 关键：WebGPU 的通道附件（含 <c>resolveTarget</c>）在通道创建时就固定，
        /// 因此切换目标<b>必须先结束当前通道</b> —— 这与 MonoGame 里
        /// <c>ApplyRenderTargets</c> 开头先调 <c>PlatformResolveRenderTargets</c> 是同一个契约。
        /// </para>
        /// <para>MSAA 走 Vulkan 的 pResolveAttachments 语义：渲染进多重采样纹理，
        /// 解析目标指向 RT 自己的单采样纹理，通道结束时自动完成解析。</para>
        /// </summary>
        public IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count)
        {
            var first = (IRenderTarget)bindings[0].RenderTarget!;

            int color = RequireTexture((Texture2D)first);
            _renderTargetExtras.TryGetValue(first, out (int Msaa, int Depth) extras);

            if (extras.Msaa != 0)
            {
                _colorTarget = extras.Msaa;   // 渲染进多重采样附件
                _resolveTarget = color;       // 解析回可采样的单采样纹理
            }
            else
            {
                _colorTarget = color;
                _resolveTarget = 0;
            }
            _depthTarget = extras.Depth;

            _targetFormat = RtColorFormat;
            _targetSampleCount = first.MultiSampleCount > 0 ? first.MultiSampleCount : 1;

            // 附件变了，已经开始绘制的通道必须收掉重开。
            if (_frameActive) EndFrame();
            return first;
        }

        public void ApplyDefaultRenderTarget()
        {
            _colorTarget = 0;
            _resolveTarget = 0;
            _depthTarget = 0;
            _targetFormat = _canvasFormat;
            _targetSampleCount = _canvasSampleCount;

            if (_frameActive) EndFrame();
        }

        /// <summary>
        /// 解析渲染目标。WebGPU 的解析由通道的 resolveTarget 在<b>通道结束时自动完成</b>
        /// （等价 Vulkan 的 pResolveAttachments，而非 GL 的 blitFramebuffer），
        /// 所以这里只需保证通道已经结束 —— 上层正是在切换 / 解绑目标前调用它。
        /// </summary>
        public void ResolveRenderTarget(IRenderTarget renderTarget)
        {
            if (_frameActive) EndFrame();
        }


        public int GetError() => 0;

        /// <summary>
        /// 设置视口。
        /// <para>
        /// 渲染到<b>离屏目标</b>时不能去动画布尺寸：WebGPU 的绘制区域默认就是通道附件的整个范围，
        /// 而改画布尺寸会连带重新配置交换链（作废当前帧纹理）。故离屏时这里只记尺寸、不下发。
        /// </para>
        /// </summary>
        public void SetViewport(int x, int y, int width, int height)
        {
            if (_colorTarget != 0) return;
            JSBind_WebGPU.Resize(width, height);
        }

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
            Write(destination, 0, value.M11); Write(destination, 1, value.M12);
            Write(destination, 2, value.M13); Write(destination, 3, value.M14);
            Write(destination, 4, value.M21); Write(destination, 5, value.M22);
            Write(destination, 6, value.M23); Write(destination, 7, value.M24);
            Write(destination, 8, value.M31); Write(destination, 9, value.M32);
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
