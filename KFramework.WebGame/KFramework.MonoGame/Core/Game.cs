using KFramework.MonoGame;


namespace KFramework.MonoGame
{

    /// <summary>
    /// 游戏宿主。用法与 MonoGame 完全一致：继承它，重写 <see cref="Initialize"/> /
    /// <see cref="LoadContent"/> / <see cref="Update"/> / <see cref="Draw"/>，
    /// 然后在 Main 里 <c>await game.RunAsync()</c>。
    /// 在浏览器上主循环由 requestAnimationFrame 驱动，因此 Run 不会阻塞线程。
    /// </summary>
    public abstract class Game : IDisposable
    {
        /// <summary>单帧最大推进时间，防止切后台回来时一次性模拟过多（大 dt 截断，避免穿模 / 螺旋死亡）。</summary>
        private const double MaxElapsedSeconds = 0.333;

        private readonly TaskCompletionSource _exitSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private double _lastTimestamp = -1;
        private TimeSpan _totalGameTime;

        /// <summary>主循环限帧：每 _frameInterval 个 rAF 才真正推进一帧（C# 层实现，供 captureFramerate 使用）。</summary>
        private int _frameInterval = 1;
        private int _frameSkipCounter;

        private bool _initialized;
        private bool _frameFaulted;
        private bool _disposed;

        public GraphicsDevice GraphicsDevice { get; }

        public GameWindow Window { get; }

        /// <summary>
        /// 与本游戏关联的图形设备管理器（照 MonoGame 的 <c>Game.graphicsDeviceManager</c>）。
        /// 由 <see cref="GraphicsDeviceManager"/> 的构造函数注册；未使用管理器时为 null。
        /// </summary>
        internal GraphicsDeviceManager? graphicsDeviceManager;

        public ContentManager Content { get; }

        public GameComponentCollection Components { get; }

        /// <summary>
        /// 【已废弃】固定时间步长。Web 上主循环由 requestAnimationFrame 锁帧（频率即刷新率），
        /// 固定步长既不再承担“限帧”（那是 rAF 的活），也不再提供“稳定 dt”——
        /// 引擎现在始终按可变 <see cref="GameTime.ElapsedGameTime"/> 缩放 dt 运行。
        /// 本属性仅保留作兼容，设置无效。
        /// 需要“固定步长逻辑”（如物理）请用引擎提供的 <see cref="FixedUpdteFunc"/>。
        /// </summary>
        [Obsolete("Web 上固定步长多余：主循环由 requestAnimationFrame 锁帧。需要固定步长逻辑请用 FixedUpdteFunc。")]
        public bool IsFixedTimeStep { get; set; } = true;

        /// <summary>【已废弃】见 <see cref="IsFixedTimeStep"/>。引擎忽略此值，始终按可变 dt 运行。</summary>
        [Obsolete("Web 上固定步长多余：引擎忽略 TargetElapsedTime，始终按可变 dt 运行。")]
        public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        /// <summary>每帧绘制前的清屏色。</summary>
        public Color ClearColor { get; set; } = new Color(12, 14, 24);

        /// <summary>
        /// 创建游戏宿主。
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id（页面里没有时引擎自动建一块全屏画布）。</param>
        /// <param name="contentRoot">内容包根路径。</param>
        /// <param name="antialias">
        /// 是否启用 MSAA。上下文创建后不可改，只能在构造时决定
        /// （等价于 MonoGame 里「设备创建前」设置 <c>PreferMultiSampling</c>）。
        /// </param>
        protected Game(string canvasSelector = "#game", string contentRoot = "hot_update_res", bool antialias = false)
        {
            GraphicsDevice = new GraphicsDevice(canvasSelector, antialias);
            Window = new GameWindow(GraphicsDevice);
            Content = new ContentManager(contentRoot);
            Components = new GameComponentCollection();
            JSBind_GameHost.Current = this;
        }

        /// <summary>启动主循环；返回的 Task 在 <see cref="Exit"/> 后完成。</summary>
        public async Task RunAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_initialized)
            {
                try
                {
                    // 照 MonoGame 的 DoInitialize：进入用户 Initialize 之前先让管理器接管设备，
                    // 这样用户在 Initialize / LoadContent 里就能拿到 GraphicsDevice 与已应用的呈现参数。
                    if (graphicsDeviceManager != null)
                        ((IGraphicsDeviceManager)graphicsDeviceManager).CreateDevice();

                    Initialize();
                    Components.Initialize();
                    await LoadContentAsync().ConfigureAwait(false);
                    Components.LoadContent();
                }
                catch (Exception ex)
                {
                    // 初始化阶段的异常必须能完整看到，否则浏览器只会表现为"白屏"
                    Console.Error.WriteLine($"[KFramework.MonoGame] 初始化失败：{ex}");
                    throw;
                }
                _initialized = true;
            }

            JSBind_Platform.StartRenderLoop();
            await _exitSignal.Task;
        }

        /// <summary>结束主循环。</summary>
        public void Exit() => _exitSignal.TrySetResult();

        /// <summary>
        /// 设置主循环限帧（C# 层实现）：每 <paramref name="framesPerPresent"/> 个 requestAnimationFrame 才真正推进一帧。
        /// 不再下发给 TS——rAF 由 TS 驱动，但“是否跳过本帧”的判定在这里做（见 <see cref="TickFrame"/>），
        /// 供 KTime.ApplyCaptureFramerate 等 C# 侧需求使用。
        /// 注意：与 PresentationInterval 的限帧（GraphicsDeviceManager → TS 的 frameInterval）是两条独立路径，
        /// 通常不同时设；逻辑仍按可变 dt 走，这与已废弃的 IsFixedTimeStep 不同。
        /// </summary>
        public void SetFrameInterval(int framesPerPresent)
            => _frameInterval = Math.Max(1, framesPerPresent);

        protected virtual void Initialize() { }

        /// <summary>同步加载（无异步需求时重写它即可）。</summary>
        protected virtual void LoadContent() { }

        /// <summary>
        /// 异步加载入口，默认转发到 <see cref="LoadContent"/>。
        /// 需要下载内容包时重写它：<c>await Content.LoadAsync(progress)</c>。
        /// </summary>
        protected virtual Task LoadContentAsync()
        {
            LoadContent();
            return Task.CompletedTask;
        }

        protected virtual void UnloadContent() { }

        protected virtual void Update(GameTime gameTime) { }

        protected virtual void Draw(GameTime gameTime) { }

        internal void TickFrame(double timestampMs)
        {
            if (_disposed || !_initialized || _frameFaulted) return;

            // C# 层限帧（captureFramerate 等）：每 _frameInterval 个 rAF 才真正推进一帧。
            // requestAnimationFrame 本身在 TS 层（浏览器 API），TS 仍按 PresentationInterval 做“每 N 个 rAF 画一帧”
            // 的标准限帧；这里的跳帧只服务于 capture 这类 C# 侧需求，不依赖 TS。默认 _frameInterval=1 即不跳帧。
            if (_frameSkipCounter++ % _frameInterval != 0) return;

            try
            {
                if (GraphicsDevice.SyncCanvasSize()) Window.RaiseSizeChanged();

                double elapsed = _lastTimestamp < 0 ? 0d : (timestampMs - _lastTimestamp) / 1000d;
                _lastTimestamp = timestampMs;
                if (elapsed < 0d) elapsed = 0d;

                // Web 上始终按可变 dt 运行：requestAnimationFrame 已把帧率锁在刷新率，
                // 固定步长（IsFixedTimeStep）在 Web 上既多余也会让高刷屏被强行节流到逻辑率，故废弃。
                // dt 直接用两次 rAF 的真实间隔（被 MaxElapsedSeconds 截断，避免切后台回来时一次性模拟过多）。
                var span = TimeSpan.FromSeconds(Math.Min(elapsed, MaxElapsedSeconds));
                _totalGameTime += span;
                var frameTime = new GameTime(_totalGameTime, span);

                //开始更新
                Input.Update();
                Update(frameTime);
                Components.Update(frameTime);
                Input.LateUpdate();

                //开始渲染
                GraphicsDevice.Clear(ClearColor);
                Draw(frameTime);
                Components.Draw(frameTime);

            }
            catch (Exception ex)
            {
                // 浏览器里异常会打断 rAF 链，这里就地截获并停止循环，避免持续刷屏
                _frameFaulted = true;
                Console.Error.WriteLine($"[KFramework.MonoGame] 运行时异常，主循环已停止：{ex}");
                Exit();
            }
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnloadContent();
            graphicsDeviceManager?.Dispose();
            graphicsDeviceManager = null;
            Content.Dispose();
            GraphicsDevice.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
