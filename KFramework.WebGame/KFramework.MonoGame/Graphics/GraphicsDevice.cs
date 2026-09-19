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

        internal readonly SpriteEffect Effect;
        internal readonly JSObject VertexBuffer;
        internal readonly JSObject IndexBuffer;
        internal readonly JSObject VertexArray;

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

        /// <summary>FBO 缓存：一组渲染目标绑定组合对应一个 FBO（照 MonoGame 的 glFramebuffers）。</summary>
        private readonly Dictionary<RenderTargetBinding[], JSObject> _glFramebuffers =
            new Dictionary<RenderTargetBinding[], JSObject>(new RenderTargetBindingArrayComparer());

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
                JSBind_GL.Viewport(value.X, value.Y, value.Width, value.Height);
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
        /// 本设备所绘制的那块 <c>&lt;canvas&gt;</c> 的 DOM id。
        /// 页面里已有该元素就直接用它，没有则由 html_canvas.ts 自动创建一块铺满视口的默认画布。
        /// </summary>
        public string CanvasId { get; }

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
        /// 创建 WebGL2 上下文。
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id。</param>
        /// <param name="antialias">是否启用 MSAA（必须在上下文创建前指定，之后改不了）。</param>
        public GraphicsDevice(string canvasSelector = "#game", bool antialias = false)
        {
            CanvasId = HTML_Canvas_Func.ToCanvasId(canvasSelector);

            // 照 MonoGame：MSAA 属性在上下文创建之前设置
            JSBind_GL.SetAntialias(antialias);
            Antialias = antialias;

            // 照 MonoGame 的无参内部构造：先建一份默认 PP，画布尺寸随后由 SyncCanvasSize 覆盖。
            // 注：MonoGame 在这里还会把 DepthStencilFormat 设为 Depth24，本后端画布不带深度附件，保持 None。
            PresentationParameters = new PresentationParameters();

            if (!JSBind_GL.InitContext(canvasSelector))
                throw new InvalidOperationException("无法创建 WebGL 2.0 上下文，请使用支持 WebGL2 的浏览器。");

            MaxTextureSize = JSBind_GL.GetParameterInt(JSBind_GL.MAX_TEXTURE_SIZE);
            Renderer = JSBind_GL.GetParameterString(JSBind_GL.RENDERER);

            Effect = new SpriteEffect();

            VertexBuffer = JSBind_GL.CreateBuffer();
            IndexBuffer = JSBind_GL.CreateBuffer();
            VertexArray = JSBind_GL.CreateVertexArray();

            JSBind_GL.BindVertexArray(VertexArray);

            JSBind_GL.BindBuffer(JSBind_GL.ARRAY_BUFFER, VertexBuffer);
            JSBind_GL.BufferDataSize(JSBind_GL.ARRAY_BUFFER, MaxBatchSize * 4 * VertexPositionColorTexture.SizeInBytes, JSBind_GL.DYNAMIC_DRAW);

            JSBind_GL.BindBuffer(JSBind_GL.ELEMENT_ARRAY_BUFFER, IndexBuffer);
            JSBind_GL.BufferData(JSBind_GL.ELEMENT_ARRAY_BUFFER, BuildQuadIndices(MaxBatchSize), JSBind_GL.STATIC_DRAW);

            ConfigureAttributes();

            SyncCanvasSize();

            // 与 MonoGame 一致：正面 = 逆时针（CCW），供 3D 渲染使用。
            // 注意：本后端的正交投影会翻转 Y，2D 精灵四边形在窗口空间是顺时针绕序，
            // 若按 CCW 正面 + 背面剔除会把所有精灵判为背面而整批剔除（表现为“啥都不渲染”），
            // 因此 2D 精灵管线默认用 CullNone 关闭剔除（见 SpriteBatch）。
            JSBind_GL.FrontFace(JSBind_GL.CCW);

            _rasterizerState = RasterizerState.CullNone;
            ApplyRasterizerState();
            _depthStencilState = DepthStencilState.None;
            ApplyDepthStencilState();

            JSBind_GL.Enable(JSBind_GL.BLEND);
            JSBind_GL.BlendEquation(JSBind_GL.FUNC_ADD);
            SetBlendState(BlendState.NonPremultiplied);

            PrintTool.Log($"[KFramework.MonoGame] WebGL2 就绪 | {Renderer} | 画布 {Viewport.Width}x{Viewport.Height} | 最大纹理 {MaxTextureSize}");
        }

        private void ConfigureAttributes()
        {
            int stride = VertexPositionColorTexture.SizeInBytes;
            if (Effect.PositionLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.PositionLocation);
                JSBind_GL.VertexAttribPointer(Effect.PositionLocation, 2, JSBind_GL.FLOAT, false, stride, 0);
            }
            if (Effect.TexCoordLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.TexCoordLocation);
                JSBind_GL.VertexAttribPointer(Effect.TexCoordLocation, 2, JSBind_GL.FLOAT, false, stride, 8);
            }
            if (Effect.ColorLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.ColorLocation);
                JSBind_GL.VertexAttribPointer(Effect.ColorLocation, 4, JSBind_GL.UNSIGNED_BYTE, true, stride, 16);
            }
        }

        private static byte[] BuildQuadIndices(int spriteCount)
        {
            // 每个精灵 2 个三角形：0,1,2 / 0,2,3（顶点顺序 TL,TR,BR,BL）
            byte[] data = new byte[spriteCount * 6 * sizeof(ushort)];
            Span<ushort> indices = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(data.AsSpan());
            for (int i = 0; i < spriteCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                // 与官方 MonoGame 完全一致的三角剖分：两个三角形绕向相同（都为 +1）。
                // 三角1 = (TL, TR, BL)，三角2 = (TR, BR, BL)。
                // 注意：本仓库早期版本用过 (TL, BL, BR) 写第二个三角形，绕向与三角1相反；
                // 一旦外部代码（如 3D 渲染）开启了背面剔除而没关，绕向相反的那个三角形就会被剔掉，
                // 表现为“每个四边形缺一个三角”。这里改回与 MonoGame 一致，使两个三角形绕向相同。
                indices[o + 0] = (ushort)(v + 0);
                indices[o + 1] = (ushort)(v + 1);
                indices[o + 2] = (ushort)(v + 2);
                indices[o + 3] = (ushort)(v + 1);
                indices[o + 4] = (ushort)(v + 3);
                indices[o + 5] = (ushort)(v + 2);
            }
            return data;
        }

        /// <summary>把视口同步为画布当前的绘制缓冲尺寸，返回是否发生了变化。</summary>
        public bool SyncCanvasSize()
        {
            Span<int> size = stackalloc int[5];
            JSBind_Platform.GetCanvasSize(size);
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

        /// <summary>CSS 像素尺寸（不含设备像素比）。</summary>
        public Vector2 CssSize
        {
            get
            {
                Span<int> size = stackalloc int[5];
                JSBind_Platform.GetCanvasSize(size);
                return new Vector2(size[0], size[1]);
            }
        }

        public float DevicePixelRatio
        {
            get
            {
                Span<int> size = stackalloc int[5];
                JSBind_Platform.GetCanvasSize(size);
                return size[4] / 1000f;
            }
        }

        /// <summary>
        /// 读取画布上的一个像素。坐标以**左上角为原点**（与精灵坐标系一致），
        /// 内部会自动换算成 WebGL 的左下原点。用于截图式自检。
        /// </summary>
        public Color ReadPixel(int x, int y)
        {
            Span<byte> rgba = stackalloc byte[4];
            JSBind_GL.ReadPixel(x, Viewport.Height - 1 - y, rgba);
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
        }

        public void Clear(Color color)
        {
            // 每帧清一次渲染统计，照 MonoGame 在 Present 里 _graphicsMetrics = new GraphicsMetrics()（跨所有 SpriteBatch 批次累计）。
            _metrics = new GraphicsMetrics();
            _metrics._clearCount++;
            JSBind_GL.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            JSBind_GL.Clear(JSBind_GL.COLOR_BUFFER_BIT | JSBind_GL.DEPTH_BUFFER_BIT);
        }

        internal void SetBlendState(BlendState state)
        {
            if (ReferenceEquals(_blendState, state)) return;
            _blendState = state;
            JSBind_GL.Enable(JSBind_GL.BLEND);
            JSBind_GL.BlendFuncSeparate(state.SourceBlend, state.DestinationBlend,
                                 state.SourceAlphaBlend, state.DestinationAlphaBlend);
        }

        /// <summary>
        /// 把当前 <see cref="RasterizerState"/> 下发给 WebGL。
        /// 本 2D 后端只用 CullCounterClockwiseFace（与 WebGL 默认 BACK 剔除一致），
        /// 故只切 Enable/Disable(CULL_FACE)，不调 glCullFace；CullNone 即关闭剔除。
        /// 每次 <see cref="SpriteBatch.Setup"/> 都会重新设置，因此绘制前状态始终被强制回 2D 设定，
        /// 不受外部 GL 状态（如 3D 渲染遗留的 CULL_FACE）影响。
        /// </summary>
        /// <summary>
        /// 把当前 <see cref="RasterizerState"/> 下发给 WebGL（照 MonoGame 的 RasterizerState.Apply）。
        /// CullMode.None 关闭剔除；否则开启 CULL_FACE 并按绕向选 FRONT/BACK，
        /// 配合构造函数里设定的 CCW 正面，CullCounterClockwiseFace 即“剔除逆时针背面”。
        /// 每次 <see cref="SpriteBatch.Setup"/> 都会重新设置，因此绘制前状态始终被强制回 2D 设定，
        /// 不受外部 GL 状态（如 3D 渲染遗留）影响。
        /// </summary>
        private void ApplyRasterizerState()
        {
            switch (_rasterizerState.CullMode)
            {
                case CullMode.None:
                    JSBind_GL.Disable(JSBind_GL.CULL_FACE);
                    break;
                case CullMode.CullClockwiseFace:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.FRONT);
                    break;
                case CullMode.CullCounterClockwiseFace:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.BACK);
                    break;
                default:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.BACK);
                    break;
            }

            if (_rasterizerState.ScissorTestEnable)
                JSBind_GL.Enable(JSBind_GL.SCISSOR_TEST);
            else
                JSBind_GL.Disable(JSBind_GL.SCISSOR_TEST);
        }

        /// <summary>
        /// 把当前 <see cref="DepthStencilState"/> 下发给 WebGL。
        /// 深度测试关闭时 glDepthMask / glDepthFunc 无影响，故只切 Enable/Disable(DEPTH_TEST)；
        /// SpriteBatch 默认用 <see cref="DepthStencilState.None"/>（关闭深度测试）。
        /// 同样在每次 Setup 被强制下发，保证 2D 绘制不受外部状态干扰。
        /// </summary>
        /// <summary>
        /// 把当前 <see cref="DepthStencilState"/> 下发给 WebGL（照 MonoGame 的 DepthStencilState.Apply）。
        /// 切换 DEPTH_TEST 开关，并下发深度写入掩码与比较函数；SpriteBatch 默认用
        /// <see cref="DepthStencilState.None"/>（关闭深度测试）。
        /// 同样在每次 Setup 被强制下发，保证 2D 绘制不受外部状态干扰。
        /// </summary>
        private void ApplyDepthStencilState()
        {
            if (_depthStencilState.DepthBufferEnable)
                JSBind_GL.Enable(JSBind_GL.DEPTH_TEST);
            else
                JSBind_GL.Disable(JSBind_GL.DEPTH_TEST);

            JSBind_GL.DepthMask(_depthStencilState.DepthBufferWriteEnable);
            JSBind_GL.DepthFunc(ToGLDepthFunc(_depthStencilState.DepthBufferFunction));
        }

        private static int ToGLDepthFunc(CompareFunction func) => func switch
        {
            CompareFunction.Never => JSBind_GL.NEVER,
            CompareFunction.Less => JSBind_GL.LESS,
            CompareFunction.Equal => JSBind_GL.EQUAL,
            CompareFunction.LessEqual => JSBind_GL.LEQUAL,
            CompareFunction.Greater => JSBind_GL.GREATER,
            CompareFunction.NotEqual => JSBind_GL.NOTEQUAL,
            CompareFunction.GreaterEqual => JSBind_GL.GEQUAL,
            _ => JSBind_GL.ALWAYS,
        };

        internal void SetSamplerState(SamplerState state, Texture2D? current)
        {
            if (current is null) return;
            if (ReferenceEquals(_samplerState, state) && _samplerAppliedKey == current.SortingKey) return;
            _samplerState = state;
            _samplerAppliedKey = current.SortingKey;
            SamplerStates[0] = state;

            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MIN_FILTER, state.MinFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MAG_FILTER, state.MagFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_S, state.WrapMode);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_T, state.WrapMode);
        }

        private ulong _samplerAppliedKey;

        internal void BindTexture(Texture2D texture)
        {
            JSBind_GL.ActiveTexture(JSBind_GL.TEXTURE0);
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, texture.Handle);
            Textures[0] = texture;
            _samplerAppliedKey = 0;   // 换纹理后采样参数需要重新下发
        }

        /// <summary>
        /// 把若干顶点上传到动态顶点缓冲并发起一次索引绘制（照 MonoGame 的 DrawUserIndexedPrimitives）。
        /// 索引来自初始化时建好的静态 ELEMENT_ARRAY_BUFFER（已绑进 VertexArray）。
        /// 每调用一次累加一次 DrawCount；PrimitiveCount 同步累加（每个四边形 = 2 三角形）。
        /// SpriteCount 由 SpriteBatcher.DrawBatch 整批累加一次，这里不再加。
        /// </summary>
        internal void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;

            JSBind_GL.BindVertexArray(VertexArray);
            JSBind_GL.BindBuffer(JSBind_GL.ARRAY_BUFFER, VertexBuffer);
            // 索引缓冲是[0, MaxBatchSize*4)的绝对下标：第 i 个四边形占 6 个索引，起始字节 i*6*2。
            // 因此把本批顶点（从 start 顶点起）上传到顶点缓冲的 start*SizeInBytes 处，
            // 并让 DrawElements 从 (start/4)*6*2 字节处读取索引，即可精确引用到本批顶点——
            // 多纹理切批后，后面的 run 不会再串到前一批的几何（否则会丢失/错位三角形）。
            JSBind_GL.BufferSubData(JSBind_GL.ARRAY_BUFFER, start * VertexPositionColorTexture.SizeInBytes,
                                    MemoryMarshal.AsBytes(vertices.AsSpan(start, vRun)));
            JSBind_GL.DrawElements(JSBind_GL.TRIANGLES, vRun / 4 * 6, JSBind_GL.UNSIGNED_SHORT, (start / 4) * 6 * 2);

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
                    ResolveRenderTarget((IRenderTarget)_currentRenderTargetBindings[i].RenderTarget!);
            }

            Array.Clear(_currentRenderTargetBindings, 0, _currentRenderTargetBindings.Length);

            if (renderTargets == null || renderTargets.Length == 0)
            {
                _currentRenderTargetCount = 0;
                PlatformApplyDefaultRenderTarget();

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

                IRenderTarget platformTarget = PlatformApplyRenderTargets();

                renderTargetWidth = platformTarget.Width;
                renderTargetHeight = platformTarget.Height;
                clearTarget = platformTarget.RenderTargetUsage == RenderTargetUsage.DiscardContents;
            }

            // 视口与裁剪矩形跟随渲染目标尺寸（照 MonoGame）。
            Viewport = new Viewport(0, 0, renderTargetWidth, renderTargetHeight);
            JSBind_GL.Scissor(0, 0, renderTargetWidth, renderTargetHeight);

            if (clearTarget) Clear(DiscardColor);
        }

        /// <summary>把多重采样帧缓冲解析到可采样纹理（照 MonoGame 的 PlatformResolveRenderTargets）。</summary>
        private void ResolveRenderTarget(IRenderTarget rt)
        {
            if (rt.MultiSampleCount <= 0) return;
            if (rt.GLMultiSampleFramebuffer is not { } ms || rt.GLResolveFramebuffer is not { } resolve) return;

            JSBind_GL.BindFramebuffer(JSBind_GL.READ_FRAMEBUFFER, ms);
            JSBind_GL.BindFramebuffer(JSBind_GL.DRAW_FRAMEBUFFER, resolve);
            JSBind_GL.BlitFramebuffer(0, 0, rt.Width, rt.Height, 0, 0, rt.Width, rt.Height,
                JSBind_GL.COLOR_BUFFER_BIT, JSBind_GL.LINEAR);
            JSBind_GL.BindFramebuffer(JSBind_GL.READ_FRAMEBUFFER, null);
            JSBind_GL.BindFramebuffer(JSBind_GL.DRAW_FRAMEBUFFER, null);
        }

        /// <summary>建 / 复用并绑定当前渲染目标组合的 FBO（照 MonoGame 的 PlatformApplyRenderTargets）。</summary>
        private IRenderTarget PlatformApplyRenderTargets()
        {
            var first = (IRenderTarget)_currentRenderTargetBindings[0].RenderTarget!;

            // 多重采样：颜色是 multisample renderbuffer，渲染时直接绑 MSAA FBO，不走组合缓存，
            // 解析后才通过 GLResolveFramebuffer 写入 GLTexture。
            if (first.MultiSampleCount > 0 && first.GLMultiSampleFramebuffer is { } msFbo)
            {
                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, msFbo);
                _samplerAppliedKey = 0;
                Textures.Clear();
                return first;
            }

            if (!_glFramebuffers.TryGetValue(_currentRenderTargetBindings, out JSObject? framebuffer))
            {
                framebuffer = JSBind_GL.CreateFramebuffer();
                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, framebuffer);

                // 深度 / 模板附件（照 MonoGame：Depth24Stencil8 时二者共用同一个 renderbuffer）。
                if (first.GLDepthBuffer is { } depth)
                    JSBind_GL.FramebufferRenderbuffer(JSBind_GL.FRAMEBUFFER, JSBind_GL.DEPTH_ATTACHMENT,
                        JSBind_GL.RENDERBUFFER, depth);
                if (first.GLStencilBuffer is { } stencil)
                    JSBind_GL.FramebufferRenderbuffer(JSBind_GL.FRAMEBUFFER, JSBind_GL.STENCIL_ATTACHMENT,
                        JSBind_GL.RENDERBUFFER, stencil);

                for (int i = 0; i < _currentRenderTargetCount; i++)
                {
                    var target = (IRenderTarget)_currentRenderTargetBindings[i].RenderTarget!;
                    JSBind_GL.FramebufferTexture2D(JSBind_GL.FRAMEBUFFER, JSBind_GL.COLOR_ATTACHMENT0 + i,
                        JSBind_GL.TEXTURE_2D, target.GLTexture, 0);
                }

                int status = JSBind_GL.CheckFramebufferStatus(JSBind_GL.FRAMEBUFFER);
                if (status != JSBind_GL.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] Framebuffer 不完整: 0x{status:X4}");

                _glFramebuffers.Add((RenderTargetBinding[])_currentRenderTargetBindings.Clone(), framebuffer);
            }
            else
            {
                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, framebuffer);
            }

            // 目标换了，之前下发的纹理单元与采样参数全部失效（照 MonoGame 的 Textures.Dirty()）。
            _samplerAppliedKey = 0;
            Textures.Clear();

            return (IRenderTarget)_currentRenderTargetBindings[0].RenderTarget!;
        }

        private void PlatformApplyDefaultRenderTarget()
        {
            JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, null);

            _samplerAppliedKey = 0;
            Textures.Clear();
        }

        /// <summary>创建渲染目标的深度 / 模板 renderbuffer（照 MonoGame 的 PlatformCreateRenderTarget）。</summary>
        internal void PlatformCreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
        {
            int internalFormat = depthFormat switch
            {
                DepthFormat.Depth16 => JSBind_GL.DEPTH_COMPONENT16,
                DepthFormat.Depth24 => JSBind_GL.DEPTH_COMPONENT24,
                DepthFormat.Depth24Stencil8 => JSBind_GL.DEPTH24_STENCIL8,
                _ => 0,
            };

            // —— 多重采样：颜色用 multisample renderbuffer，解析后写入纹理 ——
            if (renderTarget.MultiSampleCount > 0)
            {
                int samples = renderTarget.MultiSampleCount;
                int max = JSBind_GL.GetParameterInt(JSBind_GL.MAX_SAMPLES);
                if (samples > max) samples = Math.Max(1, max);
                if (samples < 1) samples = 1;

                // MSAA 颜色 renderbuffer（RGBA8）
                JSObject colorRB = JSBind_GL.CreateRenderbuffer();
                JSBind_GL.BindRenderbuffer(JSBind_GL.RENDERBUFFER, colorRB);
                JSBind_GL.RenderbufferStorageMultisample(JSBind_GL.RENDERBUFFER, samples, JSBind_GL.RGBA8, width, height);
                renderTarget.GLColorRenderbuffer = colorRB;

                // MSAA FBO：颜色挂 multisample renderbuffer
                JSObject msFbo = JSBind_GL.CreateFramebuffer();
                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, msFbo);
                JSBind_GL.FramebufferRenderbuffer(JSBind_GL.FRAMEBUFFER, JSBind_GL.COLOR_ATTACHMENT0, JSBind_GL.RENDERBUFFER, colorRB);

                if (internalFormat != 0)
                {
                    JSObject depthRB = JSBind_GL.CreateRenderbuffer();
                    JSBind_GL.BindRenderbuffer(JSBind_GL.RENDERBUFFER, depthRB);
                    JSBind_GL.RenderbufferStorageMultisample(JSBind_GL.RENDERBUFFER, samples, internalFormat, width, height);
                    JSBind_GL.FramebufferRenderbuffer(JSBind_GL.FRAMEBUFFER, JSBind_GL.DEPTH_ATTACHMENT, JSBind_GL.RENDERBUFFER, depthRB);
                    renderTarget.GLDepthBuffer = depthRB;
                    // 照 MonoGame：Depth24Stencil8 时 stencil 与 depth 是同一个 renderbuffer。
                    renderTarget.GLStencilBuffer = depthFormat == DepthFormat.Depth24Stencil8 ? depthRB : null;
                }

                int st = JSBind_GL.CheckFramebufferStatus(JSBind_GL.FRAMEBUFFER);
                if (st != JSBind_GL.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] MSAA 帧缓冲不完整: 0x{st:X4}");
                renderTarget.GLMultiSampleFramebuffer = msFbo;

                // 解析 FBO：把可采样纹理挂上，resolve 时 blit 进来
                JSObject resolveFbo = JSBind_GL.CreateFramebuffer();
                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, resolveFbo);
                JSBind_GL.FramebufferTexture2D(JSBind_GL.FRAMEBUFFER, JSBind_GL.COLOR_ATTACHMENT0,
                    JSBind_GL.TEXTURE_2D, renderTarget.GLTexture, 0);
                int st2 = JSBind_GL.CheckFramebufferStatus(JSBind_GL.FRAMEBUFFER);
                if (st2 != JSBind_GL.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] 解析帧缓冲不完整: 0x{st2:X4}");
                renderTarget.GLResolveFramebuffer = resolveFbo;

                JSBind_GL.BindFramebuffer(JSBind_GL.FRAMEBUFFER, null);
                return;
            }

            if (internalFormat == 0)
            {
                renderTarget.GLDepthBuffer = null;
                renderTarget.GLStencilBuffer = null;
                return;
            }

            JSObject depth = JSBind_GL.CreateRenderbuffer();
            JSBind_GL.BindRenderbuffer(JSBind_GL.RENDERBUFFER, depth);
            JSBind_GL.RenderbufferStorage(JSBind_GL.RENDERBUFFER, internalFormat, width, height);

            renderTarget.GLDepthBuffer = depth;
            // 照 MonoGame：Depth24Stencil8 时 stencil 与 depth 是同一个 renderbuffer（GLES 无独立 stencil 格式）。
            renderTarget.GLStencilBuffer = depthFormat == DepthFormat.Depth24Stencil8 ? depth : null;
        }

        /// <summary>释放渲染目标的 renderbuffer，并丢弃引用到它的 FBO 缓存（照 MonoGame 的 PlatformDeleteRenderTarget）。</summary>
        internal void PlatformDeleteRenderTarget(IRenderTarget renderTarget)
        {
            if (renderTarget.GLColorRenderbuffer is { } colorRB)
            {
                JSBind_GL.DeleteRenderbuffer(colorRB);
                renderTarget.GLColorRenderbuffer = null;
            }
            if (renderTarget.GLMultiSampleFramebuffer is { } msFbo)
            {
                JSBind_GL.DeleteFramebuffer(msFbo);
                renderTarget.GLMultiSampleFramebuffer = null;
            }
            if (renderTarget.GLResolveFramebuffer is { } resolveFbo)
            {
                JSBind_GL.DeleteFramebuffer(resolveFbo);
                renderTarget.GLResolveFramebuffer = null;
            }

            if (renderTarget.GLDepthBuffer is { } depth)
                JSBind_GL.DeleteRenderbuffer(depth);
            renderTarget.GLDepthBuffer = null;
            renderTarget.GLStencilBuffer = null;

            List<RenderTargetBinding[]>? dead = null;
            foreach (KeyValuePair<RenderTargetBinding[], JSObject> pair in _glFramebuffers)
            {
                foreach (RenderTargetBinding binding in pair.Key)
                {
                    if (ReferenceEquals(binding.RenderTarget, renderTarget))
                    {
                        (dead ??= new List<RenderTargetBinding[]>()).Add(pair.Key);
                        break;
                    }
                }
            }

            if (dead is null) return;
            foreach (RenderTargetBinding[] key in dead)
            {
                if (_glFramebuffers.TryGetValue(key, out JSObject? framebuffer) && _glFramebuffers.Remove(key))
                    JSBind_GL.DeleteFramebuffer(framebuffer);
            }
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

            int error = JSBind_GL.GetError();
            if (error != 0) Console.Error.WriteLine($"[KFramework.MonoGame] 纹理上传失败 0x{error:X4}（{(format.IsCompressed() ? "压缩" : "RGBA8")} {width}x{height}，{data.Length} 字节）");

            texture._sortingKey = _sortingKeySource++;
            return texture;
        }

        public void Dispose()
        {
            // 渲染目标的 FBO 缓存归设备所有，随设备一起释放（单个 RT 的 Dispose 会先摘掉自己的条目）。
            foreach (JSObject framebuffer in _glFramebuffers.Values)
                JSBind_GL.DeleteFramebuffer(framebuffer);
            _glFramebuffers.Clear();

            JSBind_GL.DeleteBuffer(VertexBuffer);
            JSBind_GL.DeleteBuffer(IndexBuffer);
            Effect.Dispose();
        }

        /// <summary>渲染目标绑定组合的比较器（照 MonoGame 的 RenderTargetBindingArrayComparer）。</summary>
        private sealed class RenderTargetBindingArrayComparer : IEqualityComparer<RenderTargetBinding[]>
        {
            public bool Equals(RenderTargetBinding[]? first, RenderTargetBinding[]? second)
            {
                if (ReferenceEquals(first, second)) return true;
                if (first is null || second is null) return false;
                if (first.Length != second.Length) return false;

                for (int i = 0; i < first.Length; i++)
                {
                    // 照 MonoGame：只比渲染目标本身与切片，槽位为 null 的两项也算相等。
                    if (!ReferenceEquals(first[i].RenderTarget, second[i].RenderTarget)) return false;
                    if (first[i].ArraySlice != second[i].ArraySlice) return false;
                }
                return true;
            }

            public int GetHashCode(RenderTargetBinding[] array)
            {
                var hashCode = new HashCode();
                foreach (RenderTargetBinding binding in array)
                {
                    hashCode.Add(binding.RenderTarget?.GetHashCode() ?? 0);
                    hashCode.Add(binding.ArraySlice);
                }
                return hashCode.ToHashCode();
            }
        }
    }
}
