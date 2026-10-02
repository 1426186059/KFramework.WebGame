using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

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
        internal readonly ISpriteProgram Effect;

        // 初值必须为 null：SetBlendState 用引用相等做短路，若初值就等于目标值，
        // 首次调用会被跳过，glBlendFunc 永远不下发（表现为画面全黑）。
        private BlendState _blendState = null!;
        private SamplerState _samplerState = null!;
        private RasterizerState _rasterizerState = null!;
        private DepthStencilState _depthStencilState = null!;
        private ulong _sortingKeySource = 1;

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
        /// 本应用所绘制的那块 <c>&lt;canvas&gt;</c> 的 DOM id（全局唯一：一个 WebGL 应用只对应一块画布）。
        /// 页面里已有该元素就直接用它，没有则由 html_canvas.ts 自动创建一块填满整个 HTML 页面的默认画布。
        /// <para>设计为静态：应用生命周期内只有一块画布，任意模块（光标、GL 初始化等）都能直接读取，
        /// 不必把 canvasId 层层传参。默认值为 "game"，与 <see cref="Game"/> 的默认选择器 "#game" 对齐；
        /// 真正的值在 <see cref="GraphicsDevice"/> 构造时由 <c>canvasSelector</c> 归一化后写入。</para>
        /// </summary>
        public static string CanvasId { get; private set; } = "game";

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

        /// <summary>
        /// 创建图形设备（同步，固定使用 WebGL 2.0 后端）。
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id。</param>
        /// <param name="antialias">是否启用 MSAA（必须在上下文创建前指定，之后改不了）。</param>
        public GraphicsDevice(string canvasSelector = "#game", bool antialias = false)
            : this(CreateInitializedBackend(new WebGl20Backend(), canvasSelector, antialias), canvasSelector, antialias)
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
        public static async Task<GraphicsDevice> CreateAsync(string canvasSelector = "#game", bool antialias = false, bool preferWebGpu = true)
        {
            IGraphicsBackend backend = new WebGl20Backend();

            if (preferWebGpu)
            {
                var webgpu = new WebGpuBackend();
                try
                {
                    await webgpu.InitializeAsync(canvasSelector, antialias).ConfigureAwait(false);
                    backend = webgpu;
                }
                catch (Exception ex)
                {
                    PrintTool.Log($"[KFramework.MonoGame] WebGPU 不可用，回落 WebGL 2.0：{ex.Message}");
                    await backend.InitializeAsync(canvasSelector, antialias).ConfigureAwait(false);
                }
            }
            else
            {
                await backend.InitializeAsync(canvasSelector, antialias).ConfigureAwait(false);
            }

            return new GraphicsDevice(backend, canvasSelector, antialias);
        }

        /// <summary>
        /// 同步初始化后端（仅供 WebGL 构造函数使用）：WebGL 的 InitializeAsync 返回的是已完成的 Task，
        /// 因此这里不会真正阻塞。WebGPU 必须走 <see cref="CreateAsync"/>。
        /// </summary>
        private static IGraphicsBackend CreateInitializedBackend(IGraphicsBackend backend, string canvasSelector, bool antialias)
        {
            backend.InitializeAsync(canvasSelector, antialias).GetAwaiter().GetResult();
            return backend;
        }

        /// <summary>后端已初始化完毕后的构造入口（WebGL / WebGPU 共用）。</summary>
        private GraphicsDevice(IGraphicsBackend backend, string canvasSelector, bool antialias)
        {
            CanvasId = HTML_Canvas_Func.ToCanvasId(canvasSelector);
            Antialias = antialias;

            // 照 MonoGame 的无参内部构造：先建一份默认 PP，画布尺寸随后由 SyncCanvasSize 覆盖。
            // 注：MonoGame 在这里还会把 DepthStencilFormat 设为 Depth24，本后端画布不带深度附件，保持 None。
            PresentationParameters = new PresentationParameters();

            Backend = backend;
            MaxTextureSize = Backend.MaxTextureSize;
            Renderer = Backend.Renderer;

            Effect = Backend.CreateSpriteProgram();

            SyncCanvasSize();

            // 照 MonoGame：绘制前把状态强制下发一次，保证 2D 绘制不受外部遗留状态影响。
            _rasterizerState = RasterizerState.CullNone;
            ApplyRasterizerState();
            _depthStencilState = DepthStencilState.None;
            ApplyDepthStencilState();
            SetBlendState(BlendState.NonPremultiplied);

            PrintTool.Log($"[KFramework.MonoGame] {Backend.Name} 就绪 | {Renderer} | 画布 {Viewport.Width}x{Viewport.Height} | 最大纹理 {MaxTextureSize}");
        }

        /// <summary>
        /// 收帧（照 MonoGame 的 GraphicsDevice.Present）。
        /// WebGL 后端无需动作（画面由浏览器在 rAF 回调结束时自动合成）；
        /// WebGPU 后端在此结束渲染通道并提交命令缓冲，不调则画面永不呈现。
        /// </summary>
        public void EndFrame() => Backend.EndFrame();

        /// <summary>当前渲染后端名（"WebGL2" / "WebGPU"）。</summary>
        public string BackendName => Backend.Name;

        /// <summary>把视口同步为画布当前的绘制缓冲尺寸，返回是否发生了变化。</summary>
        public bool SyncCanvasSize()
        {
            Span<int> size = stackalloc int[5];
            JSBind_Platform.GetCanvasSize(size);

            // 顺手刷新缓存：这一版是跨界查的，之后 CssSize / DevicePixelRatio 就能直接读缓存，
            // 直到下一次尺寸事件把它覆盖。
            _cssWidth = size[0];
            _cssHeight = size[1];
            if (size[4] > 0) _dpr1000 = size[4];

            int width = size[2];
            int height = size[3];
            if (width <= 0 || height <= 0) return false;

            PresentationParameters.BackBufferWidth = width;
            PresentationParameters.BackBufferHeight = height;

            if (width == Viewport.Width && height == Viewport.Height) return false;

            // 正渲染到离屏目标时不能抢视口：否则 RT 的绘制区域会被画布尺寸带偏，
            // 这里只记录后备缓冲尺寸，等 SetRenderTarget(null) 切回屏幕时再恢复。
            if (_currentRenderTargetCount > 0) return false;

            Viewport = new Viewport(0, 0, width, height);
            return true;
        }

        // 画布尺寸由 input_window_event 在变化时上报（CanvasResized 事件），
        // 这里缓存下来供 CssSize / DevicePixelRatio 读取 —— 不必每帧为它们跨界查一次。
        private int _cssWidth;
        private int _cssHeight;
        private int _dpr1000 = 1000;

        /// <summary>
        /// 用画布尺寸事件带来的数据同步后备缓冲与视口，返回是否发生了变化。
        /// <para>
        /// 与 <see cref="SyncCanvasSize"/> 的区别：这一版的数据已经随事件过界送到了，
        /// <b>不再为它跨界调一次 <c>GetCanvasSize</c></b> —— 这正是把尺寸改成事件的收益。
        /// </para>
        /// </summary>
        public bool ApplyCanvasSize(int cssWidth, int cssHeight, int backingWidth, int backingHeight, int dpr1000)
        {
            _cssWidth = cssWidth;
            _cssHeight = cssHeight;
            if (dpr1000 > 0) _dpr1000 = dpr1000;

            if (backingWidth <= 0 || backingHeight <= 0) return false;

            PresentationParameters.BackBufferWidth = backingWidth;
            PresentationParameters.BackBufferHeight = backingHeight;

            if (backingWidth == Viewport.Width && backingHeight == Viewport.Height) return false;

            // 正渲染到离屏目标时不能抢视口：否则 RT 的绘制区域会被画布尺寸带偏，
            // 这里只记录后备缓冲尺寸，等 SetRenderTarget(null) 切回屏幕时再恢复。
            if (_currentRenderTargetCount > 0) return false;

            Viewport = new Viewport(0, 0, backingWidth, backingHeight);
            return true;
        }

        /// <summary>CSS 像素尺寸（不含设备像素比）。取自最近一次画布尺寸事件；事件还没来过则跨界查一次。</summary>
        public Vector2 CssSize
        {
            get
            {
                if (_cssWidth <= 0 || _cssHeight <= 0) SyncCanvasSize();
                return new Vector2(_cssWidth, _cssHeight);
            }
        }

        /// <summary>设备像素比。取自最近一次画布尺寸事件；事件还没来过则跨界查一次。</summary>
        public float DevicePixelRatio
        {
            get
            {
                if (_cssWidth <= 0) SyncCanvasSize();
                return _dpr1000 / 1000f;
            }
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
            Backend.Clear(color);
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

        internal void SetSamplerState(SamplerState state, Texture2D? current)
        {
            if (current is null) return;
            if (ReferenceEquals(_samplerState, state) && _samplerAppliedKey == current.SortingKey) return;
            _samplerState = state;
            _samplerAppliedKey = current.SortingKey;
            SamplerStates[0] = state;

            Backend.SetSamplerState(state);
        }

        private ulong _samplerAppliedKey;

        internal void BindTexture(Texture2D texture)
        {
            Backend.BindTexture(texture);
            Textures[0] = texture;
            _samplerAppliedKey = 0;   // 换纹理后采样参数需要重新下发
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

                // 目标换了，之前下发的纹理单元与采样参数全部失效（照 MonoGame 的 Textures.Dirty()）。
                _samplerAppliedKey = 0;
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

                // 目标换了，之前下发的纹理单元与采样参数全部失效（照 MonoGame 的 Textures.Dirty()）。
                _samplerAppliedKey = 0;
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
                int expected = SurfaceFormatGL.GetExpectedCompressedBytes(format, width, height);
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
