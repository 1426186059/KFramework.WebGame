using System.Diagnostics;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 对 WebGL 2.0 上下文的封装：负责上下文创建、视口同步、状态切换与资源分配。
    /// 所有绘制都通过 <see cref="SpriteBatch"/> 完成。
    /// </summary>
    public sealed class GraphicsDevice : IDisposable
    {
        /// <summary>单批最大精灵数，决定顶点/索引缓冲大小。</summary>
        public const int MaxBatchSize = 4096;

        /// <summary>渲染后端（WebGL 2.0 / WebGPU 二选一）。所有平台层调用都经它下发，本类不再直接碰具体图形 API。</summary>
        internal readonly IGraphicsBackend Backend;

        /// <summary>精灵着色器程序（由后端创建并持有）。</summary>
        internal readonly Effect Effect;

        /// <summary>实例化绘制器（设备级，懒创建一次后复用；后端不支持实例化时为 null）。</summary>
        private ISpriteInstancer? _instancer;

        /// <summary>
        /// GPU 实例化的绘制器，由 <see cref="SpriteBatch"/> 在 <c>Begin(..., instanced: true)</c> 时使用。
        /// 一次 <c>drawElementsInstanced</c> 画一批实例；容量即单次 draw 的实例上限（超出自动分块）。
        /// </summary>
        internal ISpriteInstancer? Instancer => _instancer ??= Backend.CreateInstancer(null, MaxBatchSize);

        // 初值必须为 null：SetBlendState 用引用相等做短路，若初值就等于目标值，
        // 首次调用会被跳过，glBlendFunc 永远不下发（表现为画面全黑）。
        private BlendState _blendState = null!;
        private SamplerState _samplerState = null!;
        private RasterizerState _rasterizerState = null!;
        private DepthStencilState _depthStencilState = null!;
        private ulong _sortingKeySource = 1;

        // 材质级状态去重缓存：ApplyMaterial 按「材质内容 + 变换」短路，相同配置不重复跨 JS 下发。
        private BlendState _appliedBlend = null!;
        private SamplerState _appliedSampler = null!;
        private DepthStencilState _appliedDepth = null!;
        private RasterizerState _appliedRasterizer = null!;
        private IShaderProgram _appliedEffect = null!;
        private Matrix4x4 _appliedTransform = Matrix4x4.Identity;
        // 材质上的着色器属性（SetFloat / SetVector / …）：按「材质实例 + 属性版本号」判断内容有没有变。
        private Material _appliedMaterial = null!;
        private int _appliedPropertiesVersion = -1;
        // 每次绘制的属性覆盖块（MaterialPropertyBlock）：按「块实例 + 块的属性版本号」判断有没有变。
        private MaterialPropertyBlock? _appliedPropertyBlock;
        private int _appliedBlockVersion = -1;

        // ---- 渲染目标状态（照 MonoGame 的 GraphicsDevice 渲染目标管理） ----

        /// <summary>当前绑定的渲染目标；长度固定为 4，只有前 RenderTargetCount 项有效（照 MonoGame）。</summary>
        private readonly RenderTargetBinding[] _currentRenderTargetBindings = new RenderTargetBinding[4];

        /// <summary>SetRenderTarget(单个) 用的临时数组（照 MonoGame，避免每次分配）。</summary>
        private readonly RenderTargetBinding[] _tempRenderTargetBinding = new RenderTargetBinding[1];

        private int _currentRenderTargetCount;

        // 照 MonoGame 的 GraphicsDevice：Intel 集显对「各分量非 0 即 255」的颜色有清屏硬件快路径，
        // 用紫色会触发性能告警，故 Release 用不透明黑；XNA4 传统的紫色只在 Debug 下保留。
#if DEBUG
        private static Color _discardColor = new Color(68, 34, 136, 255);
#else
        private static Color _discardColor = new Color(0, 0, 0, 255);
#endif

        private Viewport _viewport;

        /// <summary>
        /// 当前渲染视口（照 MonoGame：切换渲染目标时会被同步为目标尺寸，切回屏幕时恢复为画布尺寸）。
        /// setter 立即下发 glViewport。
        /// </summary>
        public Viewport Viewport
        {
            get => _viewport;
            set
            {
                _viewport = value;
                Backend.SetViewport(value.X, value.Y, value.Width, value.Height);
            }
        }

        /// <summary>
        /// 渲染目标在「被绑定」时清屏所用的颜色（照 MonoGame 的 GraphicsDevice.DiscardColor，静态属性）。
        /// 仅对 <see cref="RenderTargetUsage.DiscardContents"/> 的目标生效。
        /// <para>
        /// 默认值照 MonoGame：Debug 为 XNA4 传统的紫色，Release 为黑色（不透明）——
        /// MonoGame 的注释说明：Intel 集显对「分量全 0 或全 255」的清屏有硬件快路径，
        /// 用紫色会触发性能告警，故 Release 改用黑色。
        /// </para>
        /// </summary>
        public static Color DiscardColor
        {
            get { return _discardColor; }
            set { _discardColor = value; }
        }

        /// <summary>
        /// 与本机关联的呈现参数（照 MonoGame 的 <c>GraphicsDevice.PresentationParameters</c>）。
        /// <para>
        /// 其中 <see cref="PresentationParameters.BackBufferWidth/Height"/> 由
        /// <see cref="SyncCanvasSize"/> 同步为真实画布尺寸；<see cref="PresentationParameters.RenderTargetUsage"/>
        /// 决定 SetRenderTarget(null) 切回画布时是否自动清屏（默认 DiscardContents → 清）。
        /// </para>
        /// </summary>
        public PresentationParameters PresentationParameters { get; private set; }
        /// <summary>
        /// WebGL2 上下文是否带 MSAA（<c>antialias</c>）。
        /// </summary>
        /// <remarks>
        /// 照 MonoGame：MSAA 是「创建设备时」决定的属性（SDL 里在窗口/上下文创建前设 MultiSampleSamples），
        /// 上下文建好后就改不了 —— 所以只能在 <see cref="Game"/> / 本类构造时指定。
        /// </remarks>
        public bool Antialias { get; }

        internal GraphicsMetrics _metrics;

        /// <summary>
        /// 渲染统计快照，照 MonoGame 的 GraphicsDevice.Metrics。
        /// 每帧由 Clear 重置一次（照 MonoGame 在 Present 里重置），跨所有 SpriteBatch 批次累计。
        /// </summary>
        public GraphicsMetrics Metrics { get { return _metrics; } set { _metrics = value; } }

        public int MaxTextureSize { get; }

        public string Renderer { get; }

        public BlendState BlendState => _blendState;
        public SamplerState SamplerState => _samplerState;

        /// <summary>光栅化状态（照 MonoGame 的 GraphicsDevice.RasterizerState）。setter 立即下发到 WebGL。</summary>
        public RasterizerState RasterizerState
        {
            get => _rasterizerState;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _rasterizerState = value;
                ApplyRasterizerState();
            }
        }

        /// <summary>深度/模板状态（照 MonoGame 的 GraphicsDevice.DepthStencilState）。setter 立即下发到 WebGL。</summary>
        public DepthStencilState DepthStencilState
        {
            get => _depthStencilState;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _depthStencilState = value;
                ApplyDepthStencilState();
            }
        }

        /// <summary>设备纹理槽（照 MonoGame 的 GraphicsDevice.Textures，本 2D 后端只用单元 0）。</summary>
        public TextureCollection Textures { get; } = new TextureCollection(1);

        /// <summary>设备采样器状态槽（照 MonoGame 的 GraphicsDevice.SamplerStates，本 2D 后端只用单元 0）。</summary>
        public SamplerStateCollection SamplerStates { get; } = new SamplerStateCollection(1);


        public GraphicsDevice(bool antialias = false)
            : this(CreateInitializedBackend(new WebGl20Backend(), antialias), antialias)
        {
        }

        /// <summary>
        /// 异步创建图形设备：优先 WebGPU，不可用时回落 WebGL 2.0。
        /// <para>
        /// WebGPU 的 requestAdapter / requestDevice 是<b>异步</b>的，因此必须走本工厂，
        /// 不能像 WebGL 那样在构造函数里同步建好上下文。
        /// </para>
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id。</param>
        /// <param name="antialias">是否启用 MSAA。</param>
        /// <param name="preferWebGpu">true（默认）：先试 WebGPU，失败则回落 WebGL 2.0。</param>
        public static async Task<GraphicsDevice> CreateAsync(bool antialias = false, bool preferWebGpu = true)
        {
            IGraphicsBackend backend = null;
            if (preferWebGpu)
            {
                try
                {
                    var webgpu = new WebGpuBackend();
                    await webgpu.InitializeAsync(antialias).ConfigureAwait(false);
                    backend = webgpu;
                    return new GraphicsDevice(backend, antialias);
                }
                catch (Exception ex)
                {
                    PrintTool.Log($"[KFramework.MonoGame] WebGPU 不可用，回落 WebGL 2.0：{ex.Message}");
                }
            }

            backend = new WebGl20Backend();
            await backend.InitializeAsync(antialias).ConfigureAwait(false);
            return new GraphicsDevice(backend, antialias);
        }

        /// <summary>
        /// 同步初始化后端（仅供 WebGL 构造函数使用）：WebGL 的 InitializeAsync 返回的是已完成的 Task，
        /// 因此这里不会真正阻塞。WebGPU 必须走 <see cref="CreateAsync"/>。
        /// </summary>
        private static IGraphicsBackend CreateInitializedBackend(IGraphicsBackend backend, bool antialias)
        {
            backend.InitializeAsync(antialias).GetAwaiter().GetResult();
            return backend;
        }

        /// <summary>后端已初始化完毕后的构造入口（WebGL / WebGPU 共用）。</summary>
        private GraphicsDevice(IGraphicsBackend backend, bool antialias)
        {
            // 画布必须在进入本构造前已创建（CreateAsync / 同步构造路径都会先 EnsureCanvas），
            // 照 MonoGame：先有 GameWindow，再有 GraphicsDevice——后端 Initialize 只认已存在的画布。
            if (HTML_Canvas.Current == null)
                throw new InvalidOperationException(
                    "画布尚未创建：GraphicsDevice 构造前必须先创建画布（照 MonoGame 先窗口后设备的顺序）。");

            Antialias = antialias;

            // 照 MonoGame 的无参内部构造：先建一份默认 PP，画布尺寸随后由 SyncCanvasSize 覆盖。
            // 注：MonoGame 在这里还会把 DepthStencilFormat 设为 Depth24，本后端画布不带深度附件，保持 None。
            PresentationParameters = new PresentationParameters();

            Backend = backend;
            MaxTextureSize = Backend.MaxTextureSize;
            Renderer = Backend.Renderer;

            Effect = new Effect(Backend.CreateShaderProgram());

            ApplyCanvasSize(true);

            // 照 MonoGame：绘制前把状态强制下发一次，保证 2D 绘制不受外部遗留状态影响。
            _rasterizerState = RasterizerState.CullNone;
            ApplyRasterizerState();
            _depthStencilState = DepthStencilState.None;
            ApplyDepthStencilState();
            SetBlendState(BlendState.NonPremultiplied);

            PrintTool.Log($"[KFramework.MonoGame] {Backend.Name} 就绪 | {Renderer} | 画布 {Viewport.Width}x{Viewport.Height} | 最大纹理 {MaxTextureSize}");
        }

        /// <summary>
        /// 用一段自定义 GLSL 片元着色器源码创建一个可被 <see cref="SpriteBatch.Begin"/> 使用的 <see cref="ShaderEffect"/>。
        /// 返回的 Effect 每帧应在场景中写入 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/> 以驱动动画。
        /// <para>当前仅 WebGL 后端真正编译自定义着色器；WebGPU 回落默认精灵着色器（效果不生效，但页面照常运行）。</para>
        /// </summary>
        public Effect CreateShaderEffect(string fragmentSource, string? vertexSource = null)
        {
            IShaderProgram program = Backend.CreateCustomShaderProgram(vertexSource ?? string.Empty, fragmentSource);
            var effect = new ShaderEffect(program);
            if (program is ICustomShaderProgram csp) csp.SetOwner(effect);
            return effect;
        }

        /// <summary>
        /// 收帧（照 MonoGame 的 GraphicsDevice.Present）。
        /// WebGL 后端无需动作（画面由浏览器在 rAF 回调结束时自动合成）；
        /// WebGPU 后端在此结束渲染通道并提交命令缓冲，不调则画面永不呈现。
        /// </summary>
        public void EndFrame() => Backend.EndFrame();

        /// <summary>当前渲染后端名（"WebGL2" / "WebGPU"）。</summary>
        public string BackendName => Backend.Name;

        /// <summary>
        /// 用画布尺寸事件带来的数据同步后备缓冲与视口，返回是否发生了变化。
        /// <para>
        /// 与 <see cref="SyncCanvasSize"/> 的区别：这一版的数据已经随事件过界送到了，
        /// <b>不再为它跨界调一次 <c>GetCanvasSize</c></b> —— 这正是把尺寸改成事件的收益。
        /// </para>
        /// </summary>
        public bool ApplyCanvasSize(bool bSync = false)
        {
            if (bSync) HTML_Canvas.Current.SyncJSInfo();

            if (HTML_Canvas.Current.DrawSize.X <= 0 || HTML_Canvas.Current.DrawSize.Y <= 0) return false;

            PresentationParameters.BackBufferWidth = HTML_Canvas.Current.DrawSize.X;
            PresentationParameters.BackBufferHeight = HTML_Canvas.Current.DrawSize.Y;
            if (HTML_Canvas.Current.DrawSize.X == Viewport.Width && HTML_Canvas.Current.DrawSize.Y == Viewport.Height)
            {
                return false;
            }

            if (_currentRenderTargetCount > 0) return false;
            Viewport = new Viewport(
                0, 
                0, 
                HTML_Canvas.Current.DrawSize.X, 
                HTML_Canvas.Current.DrawSize.Y);
            return true;
        }

        /// <summary>
        /// 读取画布上的一个像素。坐标以**左上角为原点**（与精灵坐标系一致）；
        /// 是否需要换算 Y 轴由后端决定（WebGL 帧缓冲原点在左下）。用于截图式自检。
        /// </summary>
        public Color ReadPixel(int x, int y)
        {
            Span<byte> rgba = stackalloc byte[4];
            Backend.ReadPixel(x, y, Viewport.Height, rgba);
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
        }

        public void Clear(Color color)
        {
            // 每帧清一次渲染统计，照 MonoGame 在 Present 里 _graphicsMetrics = new GraphicsMetrics()（跨所有 SpriteBatch 批次累计）。
            _metrics = new GraphicsMetrics();
            _metrics._clearCount++;

            // 【必须与后端的新通道同步失效材质去重缓存】
            // 开新通道（Backend.Clear → BeginPass）时后端会重置 uniform 槽位编号，并把 0 号槽
            // 回填成单位矩阵、让"当前槽"重新指向它。而上方 ApplyMaterial 是按「材质内容 + 变换」
            // 去重的：缓存里留着上一帧的值，就会判定"配置和上次一样"而直接跳过 effect.Apply ——
            // 本批次的 draw 于是仍旧绑在 0 号槽上，也就是【用单位矩阵代替正交投影】。
            // 精灵坐标被当成 NDC 原样送出，几乎全部落在 [-1,1] 之外被裁掉：
            // 离屏 RT 变成一块纯背景色的死矩形，画布上也只残留下零星内容。
            // 这里同步清掉去重标记，保证每个新通道都真正下发一次状态与变换矩阵。
            InvalidateMaterialCache();

            Backend.Clear(color);
        }

        /// <summary>
        /// 让下一次 <see cref="ApplyMaterial"/> 必定完整下发（只失效去重标记，不改真状态）。
        /// 用于"后端侧的状态已被重置、而本类的去重缓存还以为没变"的场合（见 <see cref="Clear"/>）。
        /// </summary>
        private void InvalidateMaterialCache()
        {
            _appliedBlend = null!;
            _appliedSampler = null!;
            _appliedDepth = null!;
            _appliedRasterizer = null!;
            _appliedEffect = null!;
            _appliedTransform = Matrix4x4.Identity;
            _appliedMaterial = null!;
            _appliedPropertiesVersion = -1;
            _appliedPropertyBlock = null;
            _appliedBlockVersion = -1;
        }

        internal void SetBlendState(BlendState state)
        {
            if (ReferenceEquals(_blendState, state)) return;
            _blendState = state;
            Backend.SetBlendState(state);
        }

        /// <summary>
        /// 把当前 <see cref="RasterizerState"/> 下发给后端（照 MonoGame 的 RasterizerState.Apply）。
        /// <see cref="CullMode.Off"/> 关闭剔除；Front / Back 剔除对应朝向的面。每次 <see cref="SpriteBatch.Setup"/> 都会重新设置，
        /// 因此绘制前状态始终被强制回 2D 设定，不受外部遗留状态影响。
        /// </summary>
        private void ApplyRasterizerState()
        {
            Backend.ApplyRasterizerState(_rasterizerState);
        }

        /// <summary>
        /// 把当前 <see cref="DepthStencilState"/> 下发给后端（照 MonoGame 的 DepthStencilState.Apply）。
        /// 切换深度测试开关并下发写入掩码与比较函数；SpriteBatch 默认用
        /// <see cref="DepthStencilState.None"/>（关闭深度测试）。
        /// </summary>
        private void ApplyDepthStencilState()
        {
            Backend.ApplyDepthStencilState(_depthStencilState);
        }

        /// <summary>
        /// 记录当前期望的采样参数（不再直接下发：实际 texParameteri 在 <see cref="BindTexture"/> 里
        /// 按「每张纹理记住自己已应用的采样器」惰性下发，避免每次换纹理重发 4 次 texParameteri）。
        /// </summary>
        internal void SetSamplerState(SamplerState state)
        {
            _samplerState = state;
        }

        internal void BindTexture(Texture2D texture)
        {
            Backend.BindTexture(texture);
            Textures[0] = texture;
            // WebGL2 无独立 sampler 对象：采样参数写在当前绑定的纹理上。
            // 每张纹理记住自己上次应用的采样器，仅当与期望不一致才重发 texParameteri（每纹理至多一次，跨帧也复用）。
            if (!ReferenceEquals(texture._appliedSampler, _samplerState))
            {
                Backend.SetSamplerState(_samplerState);
                texture._appliedSampler = _samplerState;
                SamplerStates[0] = _samplerState;
            }
        }

        /// <summary>
        /// 按「材质内容 + 变换」下发渲染状态。与上次完全一致则整体跳过（省去 blend / depth / rasterizer /
        /// 着色器切换与矩阵上传这一串跨 JS 调用）。材质正是 Unity 的 Material：打包着色器 + 采样/混合/深度/剔除状态。
        /// </summary>
        internal void ApplyMaterial(
            Material material, 
            Matrix4x4 transform, 
            MaterialPropertyBlock? properties)
        {
            IShaderProgram effect = (material.Effect ?? Effect).Program;
            // 动画效果（如自定义 ShaderEffect）每帧都要重灌 uTime / 自定义参数，不做材质去重短路。
            bool animated = effect.IsAnimated;
            // 材质上的着色器属性一旦被改（PropertiesVersion 变了）也必须重发，故把它并进去重键；
            // 每次绘制的属性覆盖块（MaterialPropertyBlock）同理：块换了、或块里的值被改了都要重发。
            bool sameProperties = ReferenceEquals(_appliedMaterial, material)
                                  && _appliedPropertiesVersion == material.PropertiesVersion
                                  && ReferenceEquals(_appliedPropertyBlock, properties)
                                  && _appliedBlockVersion == (properties?.PropertiesVersion ?? -1);
            if (!animated
                && sameProperties
                && ReferenceEquals(_appliedBlend, material.Blend)
                && ReferenceEquals(_appliedSampler, material.Sampler)
                && ReferenceEquals(_appliedDepth, material.DepthStencil)
                && ReferenceEquals(_appliedRasterizer, material.Rasterizer)
                && ReferenceEquals(_appliedEffect, effect)
                && _appliedTransform.Equals(transform))
            {
                return;
            }

            _appliedBlend = material.Blend;
            _appliedSampler = material.Sampler;
            _appliedDepth = material.DepthStencil;
            _appliedRasterizer = material.Rasterizer;
            _appliedEffect = effect;
            _appliedTransform = transform;
            _appliedMaterial = material;
            _appliedPropertiesVersion = material.PropertiesVersion;
            _appliedPropertyBlock = properties;
            _appliedBlockVersion = properties?.PropertiesVersion ?? -1;

            SetBlendState(material.Blend);
            _depthStencilState = material.DepthStencil;
            ApplyDepthStencilState();
            _rasterizerState = material.Rasterizer;
            ApplyRasterizerState();
            _samplerState = material.Sampler;
            effect.Apply(transform, material, properties);
        }

        /// <summary>
        /// 把若干顶点上传并发起一次索引绘制（照 MonoGame 的 DrawUserIndexedPrimitives）。
        /// 顶点缓冲 / 索引缓冲的绑定与上传由后端负责。
        /// 每调用一次累加一次 DrawCount；PrimitiveCount 同步累加（每个四边形 = 2 三角形）。
        /// SpriteCount 由 SpriteBatcher.DrawBatch 整批累加一次，这里不再加。
        /// </summary>
        internal void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;
            Backend.DrawUserIndexedPrimitives(vertices, start, end);
            _metrics._drawCount++;
            _metrics._primitiveCount += vRun / 2;
        }

        /// <summary>
        /// 实例化绘制：下发材质状态（混合/深度/剔除/采样）→ 绑定纹理 → 后端发起一次实例化绘制 → 计入渲染统计。
        /// 与 <see cref="DrawUserIndexedPrimitives"/> 的区别：这里一份数据对应一个实例，
        /// 每个实例都算 1 个精灵、2 个三角形、而整批只算 1 次 DrawCall。
        /// </summary>
        internal void DrawInstanced(ISpriteInstancer instancer, Material material, in Matrix4x4 transform,
                                    Span<SpriteInstance> instances, int count, Texture2D texture)
        {
            ArgumentNullException.ThrowIfNull(instancer);
            if (count <= 0) return;

            SetBlendState(material.Blend);
            _depthStencilState = material.DepthStencil;
            ApplyDepthStencilState();
            _rasterizerState = material.Rasterizer;
            ApplyRasterizerState();
            _samplerState = material.Sampler;
            SetSamplerState(material.Sampler);
            BindTexture(texture);

            instancer.Draw(transform, instances, count, texture);

            // 实例化程序会自己 UseProgram / 绑定自己的 VAO，绕过了 ApplyMaterial 维护的「材质级去重」缓存。
            // 这里必须把缓存作废，否则紧接着的 SpriteBatch 批次会因为"材质/变换都没变"被去重短路，
            // 从而不重新 UseProgram —— 于是后续精灵继续用着实例化程序绘制，表现是"实例化之后的文字/精灵全都不显示"。
            _appliedEffect = null!;
            _appliedMaterial = null!;

            _metrics._drawCount++;
            _metrics._primitiveCount += count * 2;
            _metrics._spriteCount += count;
        }

        // ================================================================
        // 渲染目标 / 离屏渲染（照 MonoGame 的 GraphicsDevice + OpenGL 平台层）
        // ================================================================

        /// <summary>当前绑定的渲染目标数量；0 表示直接渲染到画布。</summary>
        public int RenderTargetCount => _currentRenderTargetCount;

        /// <summary>绑定单个渲染目标；传 null 回到画布（照 MonoGame 的 SetRenderTarget）。</summary>
        public void SetRenderTarget(RenderTarget2D? renderTarget)
        {
            if (renderTarget == null)
            {
                SetRenderTargets(Array.Empty<RenderTargetBinding>());
                return;
            }

            _tempRenderTargetBinding[0] = new RenderTargetBinding(renderTarget);
            SetRenderTargets(_tempRenderTargetBinding);
        }

        /// <summary>
        /// 同时绑定多个渲染目标（MRT，照 MonoGame 的 SetRenderTargets）；传空数组回到画布。
        /// </summary>
        public void SetRenderTargets(params RenderTargetBinding[] renderTargets)
        {
            ArgumentNullException.ThrowIfNull(renderTargets);
            if (renderTargets.Length > _currentRenderTargetBindings.Length)
                throw new ArgumentOutOfRangeException(nameof(renderTargets),
                    $"最多支持 {_currentRenderTargetBindings.Length} 个渲染目标。");

            // 与当前绑定完全一致则复用（照 MonoGame：避免重复 resolve / 重建绘制批）。
            if (_currentRenderTargetCount == renderTargets.Length)
            {
                bool isEqual = true;
                for (int i = 0; i < _currentRenderTargetCount; i++)
                {
                    if (!ReferenceEquals(_currentRenderTargetBindings[i].RenderTarget, renderTargets[i].RenderTarget))
                    {
                        isEqual = false;
                        break;
                    }
                }
                if (isEqual) return;
            }

            ApplyRenderTargets(renderTargets);
        }

        /// <summary>取当前绑定渲染目标的副本（照 MonoGame 的 GetRenderTargets）。</summary>
        public RenderTargetBinding[] GetRenderTargets()
        {
            var bindings = new RenderTargetBinding[_currentRenderTargetCount];
            Array.Copy(_currentRenderTargetBindings, bindings, _currentRenderTargetCount);
            return bindings;
        }

        /// <summary>真正切换渲染目标：解绑 → 重绑 → 同步视口 / 裁剪 → 按需清屏（照 MonoGame 的 ApplyRenderTargets）。</summary>
        internal void ApplyRenderTargets(RenderTargetBinding[]? renderTargets)
        {
            bool clearTarget;
            int renderTargetWidth;
            int renderTargetHeight;

            // 多重采样：切走之前先把当前绑定的 MSAA 目标 resolve 到纹理（否则离屏结果是未解析的多重采样缓冲）。
            if (_currentRenderTargetCount > 0)
            {
                for (int i = 0; i < _currentRenderTargetCount; i++)
                    Backend.ResolveRenderTarget((IRenderTarget)_currentRenderTargetBindings[i].RenderTarget!);
            }

            Array.Clear(_currentRenderTargetBindings, 0, _currentRenderTargetBindings.Length);

            if (renderTargets == null || renderTargets.Length == 0)
            {
                _currentRenderTargetCount = 0;
                Backend.ApplyDefaultRenderTarget();

                // 目标换了，纹理单元绑定失效（照 MonoGame 的 Textures.Dirty()）；
                // 采样参数写在纹理对象上、随纹理存活，切 FBO 不会失效，无需重发。
                Textures.Clear();

                // 照 MonoGame 的 ApplyRenderTargets：切回画布是否清屏由后台缓冲的 RenderTargetUsage 决定
                //（默认 DiscardContents → 清屏）。多个离屏目标轮流回绑画布做合成时，把 PP 里的
                // RenderTargetUsage 设为 PreserveContents，才不会擦掉已合成的内容（清屏交给每帧显式 Clear）。
                clearTarget = PresentationParameters.RenderTargetUsage == RenderTargetUsage.DiscardContents;
                renderTargetWidth = PresentationParameters.BackBufferWidth;
                renderTargetHeight = PresentationParameters.BackBufferHeight;
            }
            else
            {
                _currentRenderTargetCount = renderTargets.Length;
                Array.Copy(renderTargets, _currentRenderTargetBindings, renderTargets.Length);

                IRenderTarget platformTarget = Backend.ApplyRenderTargets(_currentRenderTargetBindings, _currentRenderTargetCount);

                // 目标换了，纹理单元绑定失效（照 MonoGame 的 Textures.Dirty()）；
                // 采样参数写在纹理对象上、随纹理存活，切 FBO 不会失效，无需重发。
                Textures.Clear();

                renderTargetWidth = platformTarget.Width;
                renderTargetHeight = platformTarget.Height;
                clearTarget = platformTarget.RenderTargetUsage == RenderTargetUsage.DiscardContents;
            }

            // 视口与裁剪矩形跟随渲染目标尺寸（照 MonoGame）。
            Viewport = new Viewport(0, 0, renderTargetWidth, renderTargetHeight);
            Backend.SetScissor(0, 0, renderTargetWidth, renderTargetHeight);

            if (clearTarget) Clear(DiscardColor);
        }

        /// <summary>创建渲染目标的深度 / 模板附件（转发给后端，照 MonoGame 的 PlatformCreateRenderTarget）。</summary>
        internal void PlatformCreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
        {
            Backend.CreateRenderTarget(renderTarget, width, height, depthFormat);
        }

        /// <summary>释放渲染目标的附件，并丢弃引用到它的 FBO 缓存（转发给后端，照 MonoGame 的 PlatformDeleteRenderTarget）。</summary>
        internal void PlatformDeleteRenderTarget(IRenderTarget renderTarget)
        {
            Backend.DeleteRenderTarget(renderTarget);
        }

        /// <summary>渲染目标等内部资源用的排序键（照 MonoGame 的 sorting key 分配）。</summary>
        internal ulong NextSortingKey() => _sortingKeySource++;

        /// <summary>创建一张空的 RGBA8 纹理（便捷重载）。</summary>
        public Texture2D CreateTexture(int width, int height)
            => CreateTexture(width, height, new byte[width * height * 4], SurfaceFormat.Color);

        /// <summary>
        /// 创建纹理：<paramref name="data"/> 为 RGBA8 像素（<paramref name="format"/> = Color）或 GPU 压缩字节（DXT / ASTC / BC7 等）。
        /// 压缩格式会先按「宽高 × 压缩块大小」校验 <paramref name="data"/> 长度；压缩纹理为不可变 GPU 数据，
        /// 过滤固定为 LINEAR + CLAMP_TO_EDGE（不支持 generateMipmap / SetData 局部更新）。
        /// </summary>
        public Texture2D CreateTexture(int width, int height, byte[] data, SurfaceFormat format = SurfaceFormat.Color)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            if (width > MaxTextureSize || height > MaxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(width), $"纹理尺寸超过上限 {MaxTextureSize}");
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
                throw new ArgumentException("纹理数据不能为空", nameof(data));

            // 压缩格式：先按宽高 × 块大小校验长度（上传前的兜底，避免创建无效 GL 纹理）。
            if (format.IsCompressed())
            {
                int expected = SurfaceFormatInfo.GetExpectedCompressedBytes(format, width, height);
                if (expected > 0 && data.Length != expected)
                    throw new ArgumentException(
                        $"压缩数据长度 {data.Length} 与格式 {format} 预期的 {expected} 字节不一致（宽高 {width}x{height}）。",
                        nameof(data));
            }

            // 照 MonoGame 的「构造 + SetData」流程：构造时创建 GL 纹理，再由 SetData 上传像素。
            var texture = new Texture2D(this, width, height, mipmap: false, format);
            texture.SetData(data);

            int error = Backend.GetError();
            if (error != 0) Console.Error.WriteLine($"[KFramework.MonoGame] 纹理上传失败 0x{error:X4}（{(format.IsCompressed() ? "压缩" : "RGBA8")} {width}x{height}，{data.Length} 字节）");

            texture._sortingKey = _sortingKeySource++;
            return texture;
        }

        public void Dispose()
        {
            // FBO 缓存、常驻缓冲与着色器程序都归后端所有，随设备一起释放
            //（单个 RT 的 Dispose 会先摘掉自己的条目）。
            Backend.Dispose();
        }
    }
}
