namespace KFramework.MonoGame
{
    /// <summary>
    /// 用于初始化并控制图形设备的呈现（照 MonoGame 的 Microsoft.Xna.Framework.GraphicsDeviceManager）。
    /// <para>
    /// 用法与 MonoGame 完全一致：在 <see cref="Game"/> 派生类的构造函数里
    /// <c>graphics = new GraphicsDeviceManager(this);</c>，随后设置
    /// <see cref="PreferredBackBufferWidth"/> / <see cref="PreferredBackBufferHeight"/> /
    /// <see cref="IsFullScreen"/> 等，改完调用 <see cref="ApplyChanges"/>。
    /// </para>
    /// <para>
    /// 画布由 <see cref="HTML_Canvas"/>（模块 <c>html_canvas.ts</c>）托管，因此下列设置是真生效的：
    /// <see cref="PreferredBackBufferWidth"/> / <see cref="PreferredBackBufferHeight"/>（换算成画布 CSS 尺寸）、
    /// 全屏相关属性（<see cref="IsFullScreen"/> / <see cref="ToggleFullScreen"/> / <see cref="HardwareModeSwitch"/>）已改为「保留签名、直接抛出 NotSupportedException」：
    /// 浏览器原生全屏是用户交互行为、不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。
    /// </para>
    /// <para>
    /// 【没有垂直同步开关】浏览器强制垂直同步：主循环固定由 requestAnimationFrame 驱动，回调频率就是屏幕刷新率，
    /// WebGL 没有任何 VSync API 可以开 / 关它。所以本类不提供 MonoGame 的 <c>SynchronizeWithVerticalRetrace</c>
    /// —— 在这里它是个伪开关，硬搬过来只会误导。想降帧率请用
    /// <see cref="PresentationParameters.PresentationInterval"/>（那是「限帧」，<see cref="PresentInterval.Two"/> = 半刷新率）。
    /// </para>
    /// <para>
    /// 与 MonoGame 的差异（均由 Web 平台特性决定）：
    /// ① 浏览器只有一个与画布绑定的 WebGL2 上下文，<see cref="GraphicsDevice"/> 由 <see cref="Game"/>
    /// 在构造时创建，本类只接管它、不会重建（没有 <c>GraphicsDevice.Reset</c>、也没有 DeviceReset 系列事件被触发）；
    /// ② 后备缓冲尺寸 = 画布 CSS 尺寸 × DPR，所以 <see cref="PreferredBackBufferWidth"/> /
    /// <see cref="PreferredBackBufferHeight"/> 是「后备缓冲像素」语义，应用时会除以 DPR 换算成 CSS 尺寸；
    /// ③ 原生全屏是用户交互行为，本框架不提供代码触发的全屏（IsFullScreen / ToggleFullScreen / HardwareModeSwitch 均抛 NotSupportedException）；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)；
    /// ④ <see cref="PreferredBackBufferFormat"/> / <see cref="PreferredDepthStencilFormat"/> /
    /// <see cref="PreferHalfPixelOffset"/> 只在 <see cref="PresentationParameters"/> 上记录：
    /// WebGL2 上下文建好后无法改像素格式；
    /// ⑤ <see cref="PreferMultiSampling"/> 对应上下文的 <c>antialias</c>，而它是「创建上下文」时定死的
    ///（照 MonoGame：MSAA 属性在设备创建前设置），所以只能在 <see cref="Game"/> 构造的 <c>antialias</c> 参数里指定，
    /// 运行时改不了（不一致时本类会提示一次）；
    /// ⑤ <see cref="Game"/> 没有服务容器（Services），故不注册 IGraphicsDeviceManager/IGraphicsDeviceService 服务，
    /// 改为通过 <c>Game.graphicsDeviceManager</c> 内部字段关联。
    /// </para>
    /// </summary>
    public class GraphicsDeviceManager : IGraphicsDeviceService, IDisposable, IGraphicsDeviceManager
    {
        private readonly Game _game;
        private GraphicsDevice _graphicsDevice = null!;
        private bool _initialized = false;
        private int _preferredBackBufferHeight;
        private int _preferredBackBufferWidth;
        private SurfaceFormat _preferredBackBufferFormat;
        private DepthFormat _preferredDepthStencilFormat;
        private bool _preferMultiSampling;
        private PresentInterval _preferredPresentInterval = PresentInterval.Default;
        private DisplayOrientation _supportedOrientations;
        // 【没有「垂直同步开关」是有意为之】
        // WebGL / 浏览器没有 VSync API：主循环固定由 requestAnimationFrame 驱动，
        // 浏览器强制垂直同步（rAF 的回调频率 = 屏幕刷新率），既关不掉也不用开。
        // 因此本类不提供 MonoGame 的 SynchronizeWithVerticalRetrace —— 它在这里是个伪开关。
        // 需要「降帧率」请用 PresentationParameters.PresentationInterval（那是限帧，不是 VSync）。
        private bool _drawBegun;
        private bool _disposed;
        private bool _preferHalfPixelOffset = false;
        private GraphicsProfile _graphicsProfile;

        // 用户是否显式设置过期望的后备缓冲尺寸：没设置过就不去动画布，
        // 保留页面自身的布局（例如 index.html 里 <canvas> 的 width:100%），画布继续随浏览器缩放。
        private bool _preferredSizeSet;

        // ApplyChanges 的脏标记
        private bool _shouldApplyChanges;

        /// <summary>默认后备缓冲宽度。</summary>
        public static readonly int DefaultBackBufferWidth = 800;

        /// <summary>默认后备缓冲高度。</summary>
        public static readonly int DefaultBackBufferHeight = 480;

        /// <summary>
        /// 把本管理器关联到一个 <see cref="Game"/> 实例。
        /// </summary>
        /// <param name="game">要关联的游戏实例。</param>
        public GraphicsDeviceManager(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game), "Game cannot be null.");

            _game = game;

            _supportedOrientations = DisplayOrientation.Default;
            _preferredBackBufferFormat = SurfaceFormat.Color;
            _preferredDepthStencilFormat = DepthFormat.Depth24;

            // 与 MonoGame 一致：以窗口客户区尺寸作为后备缓冲的默认分辨率，横竖屏时取「长边为宽」。
            GameWindow window = _game.Window;
            if (window.Width >= window.Height)
            {
                _preferredBackBufferWidth = window.Width;
                _preferredBackBufferHeight = window.Height;
            }
            else
            {
                _preferredBackBufferWidth = window.Height;
                _preferredBackBufferHeight = window.Width;
            }

            // XNA 从清单读取，默认始终是 Reach，这里同样默认 Reach。
            GraphicsProfile = GraphicsProfile.Reach;

            // Web 上没有服务容器：直接挂在 Game 上，一个 Game 只能有一个管理器。
            if (_game.graphicsDeviceManager != null)
                throw new ArgumentException("A graphics device manager is already registered.  The graphics device manager cannot be changed once it is set.");
            _game.graphicsDeviceManager = this;
        }

        /// <summary/>
        ~GraphicsDeviceManager()
        {
            Dispose(false);
        }

        private void CreateDevice()
        {
            if (_graphicsDevice != null)
                return;

            try
            {
                var gdi = DoPreparingDeviceSettings();

                if (!_initialized)
                    Initialize(gdi);

                CreateDevice(gdi);
            }
            catch (NoSuitableGraphicsDeviceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new NoSuitableGraphicsDeviceException("Failed to create graphics device!", ex);
            }
        }

        private void CreateDevice(GraphicsDeviceInformation gdi)
        {
            if (_graphicsDevice != null)
                return;

            // Web：唯一的 WebGL2 上下文由 Game 构造时按画布选择器建好，这里直接接管，不重建。
            _graphicsDevice = _game.GraphicsDevice;
            _shouldApplyChanges = false;

            // MonoGame 是把 gdi.PresentationParameters 传进设备构造函数；本后端的设备早已建好，
            // 故改为把参数写回设备的 PresentationParameters（宽高除外，见类注释），
            // 并按参数摆好画布（尺寸 / 全屏）。
            ApplyPresentationParameters(gdi.PresentationParameters);
            ApplyCanvas(gdi.PresentationParameters);

            OnDeviceCreated(EventArgs.Empty);
        }

        void IGraphicsDeviceManager.CreateDevice()
        {
            CreateDevice();
        }

        /// <summary>
        /// 开始绘制过程。
        /// </summary>
        /// <returns>设备可用时返回 true；否则返回 false。</returns>
        public bool BeginDraw()
        {
            if (_graphicsDevice == null)
                return false;

            _drawBegun = true;
            return true;
        }

        /// <summary>
        /// 结束绘制过程。
        /// </summary>
        /// <remarks>
        /// MonoGame 在这里调用 <c>GraphicsDevice.Present()</c>；Web 上画面由浏览器在
        /// requestAnimationFrame 回调结束时自动合成，没有显式 Present，故只复位标记。
        /// </remarks>
        public void EndDraw()
        {
            if (_graphicsDevice != null && _drawBegun)
                _drawBegun = false;
        }

        #region Events

        /// <summary>创建了 <see cref="GraphicsDevice"/> 后触发。</summary>
        public event EventHandler<EventArgs>? DeviceCreated;

        /// <summary><see cref="GraphicsDevice"/> 被释放时触发。</summary>
        /// <remarks>本后端设备由 <see cref="Game"/> 释放，通常不会触发。</remarks>
        public event EventHandler<EventArgs>? DeviceDisposing;

        /// <summary><see cref="GraphicsDevice"/> 即将重置前触发。</summary>
        /// <remarks>本后端不会重置设备，故不会触发，仅为 API 兼容保留。</remarks>
        public event EventHandler<EventArgs>? DeviceResetting;

        /// <summary><see cref="GraphicsDevice"/> 已重置后触发。</summary>
        /// <remarks>本后端不会重置设备，故不会触发，仅为 API 兼容保留。</remarks>
        public event EventHandler<EventArgs>? DeviceReset;

        /// <summary>创建 <see cref="GraphicsDevice"/> 时触发，触发 <see cref="DeviceCreated"/> 事件。</summary>
        protected void OnDeviceCreated(EventArgs e) => EventHelpers.Raise(this, DeviceCreated, e);

        /// <summary>释放 <see cref="GraphicsDevice"/> 时触发，触发 <see cref="DeviceDisposing"/> 事件。</summary>
        protected void OnDeviceDisposing(EventArgs e) => EventHelpers.Raise(this, DeviceDisposing, e);

        /// <summary>重置 <see cref="GraphicsDevice"/> 前触发，触发 <see cref="DeviceResetting"/> 事件。</summary>
        protected void OnDeviceResetting(EventArgs e) => EventHelpers.Raise(this, DeviceResetting, e);

        /// <summary>重置 <see cref="GraphicsDevice"/> 后触发，触发 <see cref="DeviceReset"/> 事件。</summary>
        protected void OnDeviceReset(EventArgs e) => EventHelpers.Raise(this, DeviceReset, e);

        /// <summary>
        /// 由 <see cref="ApplyChanges"/> 触发，允许用户在创建 / 应用设置前改写 <see cref="PresentationParameters"/>。
        /// </summary>
        public event EventHandler<PreparingDeviceSettingsEventArgs>? PreparingDeviceSettings;

        /// <summary>本管理器被释放时触发。</summary>
        public event EventHandler<EventArgs>? Disposed;

        /// <summary>
        /// 填充一份 <see cref="GraphicsDeviceInformation"/> 并触发 <see cref="PreparingDeviceSettings"/>
        /// 让用户改写设置，然后返回它。
        /// 若用户把 GraphicsDeviceInformation.PresentationParameters 或 Adapter 设为 null，则抛 NullReferenceException。
        /// </summary>
        private GraphicsDeviceInformation DoPreparingDeviceSettings()
        {
            var gdi = new GraphicsDeviceInformation();
            PrepareGraphicsDeviceInformation(gdi);
            var preparingDeviceSettingsHandler = PreparingDeviceSettings;

            if (preparingDeviceSettingsHandler != null)
            {
                // 允许用户通过参数覆写设置
                var args = new PreparingDeviceSettingsEventArgs(gdi);
                preparingDeviceSettingsHandler(this, args);

                if (gdi.PresentationParameters == null || gdi.Adapter == null)
                    throw new NullReferenceException("Members should not be set to null in PreparingDeviceSettingsEventArgs");
            }

            return gdi;
        }

        #endregion Events

        #region IDisposable Members

        /// <inheritdoc cref="IDisposable.Dispose()"/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary/>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 与 MonoGame 的差异：设备归 Game 所有（Game.Dispose 会释放它），
                // 这里只解绑并触发 DeviceDisposing，避免 WebGL 资源被重复释放。
                if (_graphicsDevice != null)
                    OnDeviceDisposing(EventArgs.Empty);

                if (_game.graphicsDeviceManager == this)
                    _game.graphicsDeviceManager = null;
            }

            _disposed = true;
            EventHelpers.Raise(this, Disposed, EventArgs.Empty);
        }

        #endregion IDisposable Members

        private void PreparePresentationParameters(PresentationParameters presentationParameters)
        {
            presentationParameters.BackBufferFormat = _preferredBackBufferFormat;
            presentationParameters.BackBufferWidth = _preferredBackBufferWidth;
            presentationParameters.BackBufferHeight = _preferredBackBufferHeight;
            presentationParameters.DepthStencilFormat = _preferredDepthStencilFormat;
            // 全屏相关属性已从本管理器移除（IsFullScreen / HardwareModeSwitch 改抛 NotSupportedException），
            // PresentationParameters 上的对应字段保持 false 即可。
            presentationParameters.IsFullScreen = false;
            presentationParameters.HardwareModeSwitch = false;
            // 浏览器强制垂直同步（rAF 本身就是），所以这里只表达「限帧」：
            // Default / One / Immediate 都是每个垂直同步画一帧，只有 Two 会限到半刷新率。
            presentationParameters.PresentationInterval = _preferredPresentInterval;
            // 本后端没有窗口方向概念（GameWindow 不暴露 CurrentOrientation），恒为 Default。
            presentationParameters.DisplayOrientation = DisplayOrientation.Default;
            // 本后端没有 DeviceWindowHandle（Web 无窗口句柄），保持 IntPtr.Zero。

            // MonoGame 在开启多重采样时会取设备的最大采样数（PlatformGetMaxMultiSampleCount）；
            // 本后端未移植 MSAA resolve，这里照上游写 32，仅作为记录在参数里，不改变实际渲染。
            presentationParameters.MultiSampleCount = _preferMultiSampling ? 32 : 0;
        }

        private void PrepareGraphicsDeviceInformation(GraphicsDeviceInformation gdi)
        {
            gdi.Adapter = GraphicsAdapter.DefaultAdapter;
            gdi.GraphicsProfile = GraphicsProfile;
            var pp = new PresentationParameters();
            PreparePresentationParameters(pp);
            gdi.PresentationParameters = pp;
        }

        /// <summary>
        /// 把 <paramref name="source"/> 里的呈现参数写回设备的 <see cref="PresentationParameters"/>。
        /// </summary>
        /// <remarks>
        /// 对应 MonoGame「把 gdi.PresentationParameters 交给 GraphicsDevice / Reset」这一步。
        /// BackBufferWidth / BackBufferHeight 不写回：它们由画布决定，每帧由
        /// <see cref="GraphicsDevice.SyncCanvasSize"/> 同步。
        /// </remarks>
        private void ApplyPresentationParameters(PresentationParameters source)
        {
            PresentationParameters pp = _graphicsDevice.PresentationParameters;
            pp.BackBufferFormat = source.BackBufferFormat;
            pp.DepthStencilFormat = source.DepthStencilFormat;
            pp.IsFullScreen = source.IsFullScreen;
            pp.HardwareModeSwitch = source.HardwareModeSwitch;
            pp.PresentationInterval = source.PresentationInterval;
            pp.DisplayOrientation = source.DisplayOrientation;
            pp.MultiSampleCount = source.MultiSampleCount;

            ApplyPresentInterval(source);
        }

        /// <summary>
        /// 把呈现间隔落实到主循环：每 N 个 requestAnimationFrame 才回调一帧。
        /// </summary>
        /// <remarks>
        /// 这不是 VSync —— 浏览器强制垂直同步（rAF 的回调频率就是刷新率），WebGL 没有任何开关可以改它。
        /// 这里做的是「限帧」：Default / One / Immediate 都是每个 rAF 都画（N = 1），
        /// 只有 <see cref="PresentInterval.Two"/> 是隔一个 rAF 画（N = 2，半刷新率），用于省电 / 限帧测试。
        /// </remarks>
        private void ApplyPresentInterval(PresentationParameters pp)
            => JSBind_Platform.SetFrameInterval(pp.PresentationInterval.ToFramesPerPresent());

        /// <summary>
        /// 把所有挂起的属性改动应用到图形设备。
        /// </summary>
        public void ApplyChanges()
        {
            // 设备还没接管就先接管（MonoGame 在这里是「设备还没建就先建」）。
            if (_graphicsDevice == null)
                CreateDevice();

            if (!_shouldApplyChanges)
                return;

            _shouldApplyChanges = false;

            // MonoGame 在这里还会设置窗口支持的方向并调用平台层的 PlatformApplyChanges；
            // Web 上画布尺寸与全屏都由浏览器 / CSS 决定，没有对应能力，跳过。

            // 用本管理器的设置填充一份 gdi，并允许 PreparingDeviceSettings 事件改写，
            // 再把这份设置应用到设备与画布。
            var gdi = DoPreparingDeviceSettings();
            ApplyPresentationParameters(gdi.PresentationParameters);
            ApplyCanvas(gdi.PresentationParameters);
        }

        /// <summary>
        /// 把期望的后备缓冲设置落实到画布上（照 MonoGame「按 PresentationParameters 准备 / 重置设备」这一步）。
        /// </summary>
        /// <remarks>
        /// 只有「显式设置过期望尺寸」时才会写画布，否则保留页面自身布局，避免一上来就把画布钉死成固定像素。
        /// 原生全屏已移除（浏览器原生全屏是用户交互行为）；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。
        /// </remarks>
        private void ApplyCanvas(PresentationParameters pp)
        {
            HTML_Canvas canvas = Canvas;

            if (!_preferredSizeSet)
            {
                // 没显式设置过期望尺寸：不去碰画布，
                // 保留页面自身给画布的布局（如 <canvas> 的 width:100%），让它继续随浏览器缩放。
                return;
            }

            SetCanvasSize(canvas, pp);

            // 画布改完立即同步后备缓冲与视口，不等下一帧 Game.TickFrame 里的 SyncCanvasSize。
            // 与 Game.TickFrame 一样：尺寸真的变了才 RaiseSizeChanged。
            if (_graphicsDevice.SyncCanvasSize())
                _game.Window.RaiseSizeChanged();
        }

        /// <summary>按「CSS 尺寸 = 期望后备缓冲 ÷ DPR」设置画布大小。</summary>
        private void SetCanvasSize(HTML_Canvas canvas, PresentationParameters pp)
        {
            float dpr = _graphicsDevice.DevicePixelRatio;
            if (dpr <= 0f) dpr = 1f;

            int cssWidth = Math.Max(1, (int)Math.Round(pp.BackBufferWidth / dpr));
            int cssHeight = Math.Max(1, (int)Math.Round(pp.BackBufferHeight / dpr));
            canvas.SetSize(cssWidth, cssHeight);
        }

        private void Initialize(GraphicsDeviceInformation gdi)
        {
            // MonoGame 在这里设置窗口支持的方向、并调用平台层的 PlatformInitialize 创建窗口 / 上下文；
            // Web 上画布与上下文在 Game 构造时已就绪，只需置位。
            _initialized = true;
        }

        /// <summary>
        /// 在窗口模式与全屏模式之间切换。
        /// </summary>
        /// <remarks>
        /// 浏览器原生全屏是用户交互行为（必须由用户手势触发），不应由代码触发，故本方法保留签名但直接抛出
        /// <see cref="NotSupportedException"/>。需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。
        /// </remarks>
        public void ToggleFullScreen()
        {
            throw new NotSupportedException(
                "浏览器原生全屏是用户交互行为，不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。");
        }

        /// <summary>
        /// 决定图形特性等级的 profile。
        /// </summary>
        public GraphicsProfile GraphicsProfile
        {
            get => _graphicsProfile;
            set
            {
                _shouldApplyChanges = true;
                _graphicsProfile = value;
            }
        }

        /// <summary>
        /// 本管理器关联的图形设备。
        /// </summary>
        /// <remarks>接管设备之前为 null（见 <see cref="CreateDevice"/>）。</remarks>
        public GraphicsDevice GraphicsDevice => _graphicsDevice;

        /// <summary>
        /// 这块设备所绘制的画布（与 TS 层 <c>html_canvas.ts</c> 一一对应）。
        /// </summary>
        public HTML_Canvas Canvas => _game.Window.Canvas;

        /// <summary>
        /// 是否希望切换到全屏模式。
        /// </summary>
        /// <remarks>
        /// 浏览器原生全屏是用户交互行为（必须由用户手势触发），不应由代码触发，
        /// 故本属性保留签名但读取 / 赋值都直接抛出 <see cref="NotSupportedException"/>。
        /// 需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。
        /// </remarks>
        public bool IsFullScreen
        {
            get => throw new NotSupportedException(
                "浏览器原生全屏是用户交互行为，不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。");
            set => throw new NotSupportedException(
                "浏览器原生全屏是用户交互行为，不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。");
        }

        /// <summary>
        /// 窗口切到全屏时使用「硬」模式（原生全屏）还是「软」模式（填满整个 HTML 页面）。
        /// </summary>
        /// <remarks>
        /// 浏览器原生全屏是用户交互行为（必须由用户手势触发），不应由代码触发，
        /// 故本属性保留签名但读取 / 赋值都直接抛出 <see cref="NotSupportedException"/>。
        /// 需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。
        /// </remarks>
        public bool HardwareModeSwitch
        {
            get => throw new NotSupportedException(
                "浏览器原生全屏是用户交互行为，不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。");
            set => throw new NotSupportedException(
                "浏览器原生全屏是用户交互行为，不应由代码触发；需要填满整个 HTML 页面请用 HTML_Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen)。");
        }

        /// <summary>
        /// 是否使用 DX9 风格的像素寻址（XNA 兼容）。默认 false。
        /// 该值对应 MonoGame 的 <c>GraphicsDevice.UseHalfPixelOffset</c>。
        /// </summary>
        /// <remarks>本后端的 <see cref="GraphicsDevice"/> 没有半像素偏移开关，仅记录。</remarks>
        public bool PreferHalfPixelOffset
        {
            get => _preferHalfPixelOffset;
            set
            {
                if (_graphicsDevice != null)
                    throw new InvalidOperationException("Setting PreferHalfPixelOffset is not allowed after the creation of GraphicsDevice.");
                _preferHalfPixelOffset = value;
            }
        }

        /// <summary>
        /// 主循环当前的呈现间隔：每几个垂直同步（rAF）才画一帧，1 = 每个都画（见 <see cref="ApplyPresentInterval"/>）。
        /// </summary>
        public int FramesPerPresent => JSBind_Platform.GetFrameInterval();

        /// <summary>
        /// 期望的呈现间隔 —— 也就是「限帧」。
        /// </summary>
        /// <remarks>
        /// 这不是垂直同步开关（浏览器强制 VSync，WebGL 无此 API），而是人为降帧率：
        /// <see cref="PresentInterval.Default"/> / <see cref="PresentInterval.One"/> /
        /// <see cref="PresentInterval.Immediate"/> 都是每个 rAF 画一帧，
        /// 只有 <see cref="PresentInterval.Two"/> 是隔一个 rAF 画（半刷新率，省电 / 限帧测试用）。
        /// 改完要调用 <see cref="ApplyChanges"/>。
        /// </remarks>
        public PresentInterval PreferredPresentInterval
        {
            get => _preferredPresentInterval;
            set
            {
                _shouldApplyChanges = true;
                _preferredPresentInterval = value;
            }
        }

        /// <summary>
        /// 是否希望后备缓冲启用多重采样。
        /// </summary>
        /// <remarks>
        /// 对应 WebGL2 上下文的 <c>antialias</c>。它必须在创建上下文（<see cref="Game"/> / <see cref="GraphicsDevice"/> 构造）之前决定，
        /// 所以运行时改这个值不会生效（只写进 <see cref="PresentationParameters.MultiSampleCount"/> 并在日志里提示一次）。
        /// 想开 MSAA 就 <c>base(..., antialias: true)</c>。
        /// </remarks>
        public bool PreferMultiSampling
        {
            get => _preferMultiSampling;
            set
            {
                // WebGL2 的 antialias 是创建上下文时定死的，运行时改不了（见 GraphicsDevice.Antialias / gl.ts 的 setAntialias）。
                // 设备已建好后还试图改它属于无效操作 —— 直接抛异常，而不是静默吞掉假装生效。
                if (_graphicsDevice != null && value != _graphicsDevice.Antialias)
                    throw new NotSupportedException(
                        $"PreferMultiSampling 不能在运行时修改：WebGL2 的 antialias 在创建 GraphicsDevice/Game 上下文时就已定死，" +
                        $"当前上下文 antialias = {_graphicsDevice.Antialias}。请改用 Game/GraphicsDevice 构造函数的 antialias 参数，" +
                        $"或改 Example1Game.Antialias 后重启对比。");

                _shouldApplyChanges = true;
                _preferMultiSampling = value;
            }
        }

        /// <summary>期望的后备缓冲像素格式。</summary>
        public SurfaceFormat PreferredBackBufferFormat
        {
            get => _preferredBackBufferFormat;
            set
            {
                _shouldApplyChanges = true;
                _preferredBackBufferFormat = value;
            }
        }

        /// <summary>期望的后备缓冲高度（像素）。</summary>
        /// <remarks>
        /// 应用时按「CSS 尺寸 = 后备缓冲 ÷ DPR」换算后设置画布大小，即后备缓冲会真的变成这个高度
        /// （受 DPR 取整影响可能差 1~2 像素）。
        /// 从不显式设置这两个属性，画布就保持页面自身的布局（如 <c>width:100%</c>），可以继续随浏览器缩放。
        /// </remarks>
        public int PreferredBackBufferHeight
        {
            get => _preferredBackBufferHeight;
            set
            {
                _shouldApplyChanges = true;
                _preferredSizeSet = true;
                _preferredBackBufferHeight = value;
            }
        }

        /// <summary>
        /// 是否已显式设置过期望的后备缓冲尺寸。
        /// </summary>
        /// <remarks>
        /// false 表示「没指定」：画布保持页面自身的布局（如 <c>width:100%</c>），随浏览器缩放，
        /// <see cref="ApplyChanges"/> 也不会去写画布。
        /// </remarks>
        public bool HasPreferredBackBufferSize => _preferredSizeSet;

        /// <summary>
        /// 取消「期望的后备缓冲尺寸」，把画布交还给页面自身的布局：填满整个 HTML 页面（百分比，不是固定像素），
        /// 于是它重新随浏览器缩放 —— 也就是从「钉死成固定分辨率」回到响应式。
        /// </summary>
        /// <remarks>
        /// 仅设置 <see cref="PreferredBackBufferWidth"/> / <see cref="PreferredBackBufferHeight"/> 是回不到这个状态的
        /// （那只会把画布钉成另一个固定尺寸），必须走本方法。
        /// </remarks>
        public void ReleasePreferredBackBufferSize()
        {
            _preferredSizeSet = false;
            _shouldApplyChanges = true;

            if (_graphicsDevice == null) return;

            // 填满整个 HTML 页面用的是百分比，不是固定像素，所以浏览器缩放时它会跟着变。
            Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen, 0, 0, 0, 0);
            if (_graphicsDevice.SyncCanvasSize())
                _game.Window.RaiseSizeChanged();
        }

        /// <summary>期望的后备缓冲宽度（像素）。</summary>
        /// <remarks>
        /// 应用时按「CSS 尺寸 = 后备缓冲 ÷ DPR」换算后设置画布大小，即后备缓冲会真的变成这个宽度
        /// （受 DPR 取整影响可能差 1~2 像素）。
        /// </remarks>
        public int PreferredBackBufferWidth
        {
            get => _preferredBackBufferWidth;
            set
            {
                _shouldApplyChanges = true;
                _preferredSizeSet = true;
                _preferredBackBufferWidth = value;
            }
        }

        /// <summary>期望的深度 / 模板缓冲格式。</summary>
        public DepthFormat PreferredDepthStencilFormat
        {
            get => _preferredDepthStencilFormat;
            set
            {
                _shouldApplyChanges = true;
                _preferredDepthStencilFormat = value;
            }
        }

        /// <summary>
        /// 设备旋转时允许的方向。
        /// </summary>
        /// <remarks>本后端没有窗口方向设置能力，仅记录。</remarks>
        public DisplayOrientation SupportedOrientations
        {
            get => _supportedOrientations;
            set
            {
                _shouldApplyChanges = true;
                _supportedOrientations = value;
            }
        }
    }
}
