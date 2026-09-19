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
    /// 与 MonoGame 的差异（均由 Web 平台特性决定）：
    /// ① 浏览器只有一个与画布绑定的 WebGL2 上下文，<see cref="GraphicsDevice"/> 由 <see cref="Game"/>
    /// 在构造时创建，本类只接管它、不会重建（没有 <c>GraphicsDevice.Reset</c>、也没有 DeviceReset 系列事件被触发）；
    /// ② 后备缓冲尺寸由画布的 CSS 尺寸 × DPR 决定（每帧 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步），
    /// 故 <see cref="PreferredBackBufferWidth"/> / <see cref="PreferredBackBufferHeight"/> 只作为期望值记录，
    /// 常用于虚拟分辨率与 UI 布局换算，不会改变画布大小；
    /// ③ <see cref="IsFullScreen"/> / <see cref="HardwareModeSwitch"/> /
    /// <see cref="PreferHalfPixelOffset"/> / <see cref="PreferMultiSampling"/> 只写入
    /// <see cref="PresentationParameters"/>，浏览器侧没有对应的模式切换能力；
    /// ④ <see cref="Game"/> 没有服务容器（Services），故不注册 IGraphicsDeviceManager/IGraphicsDeviceService 服务，
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
        private DisplayOrientation _supportedOrientations;
        private bool _synchronizedWithVerticalRetrace = true;
        private bool _drawBegun;
        private bool _disposed;
        private bool _hardwareModeSwitch = true;
        private bool _preferHalfPixelOffset = false;
        private bool _wantFullScreen;
        private GraphicsProfile _graphicsProfile;

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
            _synchronizedWithVerticalRetrace = true;

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

            // 默认窗口模式（Web 上忽略）。
            _wantFullScreen = false;

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
            // 故改为把参数写回设备的 PresentationParameters（宽高除外，见类注释）。
            ApplyPresentationParameters(gdi.PresentationParameters);

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
            presentationParameters.IsFullScreen = _wantFullScreen;
            presentationParameters.HardwareModeSwitch = _hardwareModeSwitch;
            presentationParameters.PresentationInterval = _synchronizedWithVerticalRetrace ? PresentInterval.One : PresentInterval.Immediate;
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
        }

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
            // 再把这份设置应用到设备。
            var gdi = DoPreparingDeviceSettings();
            ApplyPresentationParameters(gdi.PresentationParameters);
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
        /// <remarks>Web 上没有可用的全屏切换能力（需要浏览器 requestFullscreen），本方法只切换与记录状态。</remarks>
        public void ToggleFullScreen()
        {
            IsFullScreen = !IsFullScreen;
            ApplyChanges();
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
        /// 是否希望切换到全屏模式。
        /// </summary>
        /// <remarks>Web 上只记录到 <see cref="PresentationParameters.IsFullScreen"/>，不会真的切换。</remarks>
        public bool IsFullScreen
        {
            get => _wantFullScreen;
            set
            {
                _shouldApplyChanges = true;
                _wantFullScreen = value;
            }
        }

        /// <summary>
        /// 窗口切到全屏时使用「硬」模式（true，真正的显示模式切换，慢但更高效）还是「软」模式（false，无边框最大化窗口）。
        /// 默认 true。
        /// </summary>
        /// <remarks>Web 上无对应能力，仅记录。</remarks>
        public bool HardwareModeSwitch
        {
            get => _hardwareModeSwitch;
            set
            {
                _shouldApplyChanges = true;
                _hardwareModeSwitch = value;
            }
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
        /// 是否希望后备缓冲启用多重采样。
        /// </summary>
        /// <remarks>本后端未移植 MSAA resolve，仅记录到 <see cref="PresentationParameters.MultiSampleCount"/>。</remarks>
        public bool PreferMultiSampling
        {
            get => _preferMultiSampling;
            set
            {
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
        /// <remarks>Web 上不改变画布大小，只作为期望值记录（详见类注释）。</remarks>
        public int PreferredBackBufferHeight
        {
            get => _preferredBackBufferHeight;
            set
            {
                _shouldApplyChanges = true;
                _preferredBackBufferHeight = value;
            }
        }

        /// <summary>期望的后备缓冲宽度（像素）。</summary>
        /// <remarks>Web 上不改变画布大小，只作为期望值记录（详见类注释）。</remarks>
        public int PreferredBackBufferWidth
        {
            get => _preferredBackBufferWidth;
            set
            {
                _shouldApplyChanges = true;
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
        /// 是否希望呈现时等待垂直回扫（垂直同步）。
        /// </summary>
        /// <remarks>Web 的主循环由 requestAnimationFrame 驱动，本身就是按屏幕刷新率回调，本项只记录到参数。</remarks>
        public bool SynchronizeWithVerticalRetrace
        {
            get => _synchronizedWithVerticalRetrace;
            set
            {
                _shouldApplyChanges = true;
                _synchronizedWithVerticalRetrace = value;
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
