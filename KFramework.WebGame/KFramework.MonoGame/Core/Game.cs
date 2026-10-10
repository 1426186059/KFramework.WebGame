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
        public static Game Current { get; private set; }
        /// <summary>单帧最大推进时间，防止切后台回来时一次性模拟过多（大 dt 截断，避免穿模 / 螺旋死亡）。</summary>
        private const double MaxElapsedSeconds = 0.333;

        private readonly TaskCompletionSource _exitSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private double _lastTimestamp = -1;
        private TimeSpan _totalGameTime;

        /// <summary>固定步长模式下累计的“未消费时间”，达到一个 TargetElapsedTime 就跑一次 Update。</summary>
        private double _accumulator;

        /// <summary>固定步长模式下单帧最多模拟次数，防止卡顿后螺旋死亡（一次性补太多步拖死主线程）。</summary>
        private const int MaxStepsPerFrame = 5;

        /// <summary>主循环限帧：每 _frameInterval 个 rAF 才真正推进一帧（C# 层实现，供 captureFramerate 使用）。</summary>
        private int _frameInterval = 1;
        private int _frameSkipCounter;

        private bool _initialized;
        private bool _frameFaulted;
        private bool _disposed;

        public GraphicsDevice GraphicsDevice { get; private set; }

        public GameWindow Window { get; private set; }

        /// <summary>
        /// 与本游戏关联的图形设备管理器（照 MonoGame 的 <c>Game.graphicsDeviceManager</c>）。
        /// 由 <see cref="GraphicsDeviceManager"/> 的构造函数注册；未使用管理器时为 null。
        /// </summary>
        internal GraphicsDeviceManager? graphicsDeviceManager;

        public GameComponentCollection Components { get; }

        /// <summary>
        /// 是否使用固定时间步长（经典 MonoGame 行为）。
        /// <list type="bullet">
        /// <item><description>true：主循环按 <see cref="TargetElapsedTime"/> 恒定间隔跑 Update，攒够一个间隔跑一次（可一帧多次），
        /// 不足一个间隔则跳过本帧渲染——Draw 被压到固定率，渲染与逻辑天然同频，因此无需渲染插值。</description></item>
        /// <item><description>false（本引擎默认，推荐用于 Web）：每渲染帧跑一次 Update，dt 用两次 rAF 的真实间隔（可变 dt）。
        /// 需要“稳定步长的物理”时在 Update 里用 <see cref="FixedUpdteFunc"/> 自行固定步进，
        /// 并可用其 <see cref="FixedUpdteFunc.InterpolationAlpha"/> 做渲染插值消除高刷屏抖动。</description></item>
        /// </list>
        /// 注：本引擎默认 false，与 MonoGame 默认 true 不同——Web 上更推荐“可变 dt + FixedUpdteFunc”的组合。
        /// </summary>
        public bool IsFixedTimeStep { get; set; } = true;

        /// <summary>固定步长模式下单次 Update 的间隔（默认 1/60 秒）。仅当 <see cref="IsFixedTimeStep"/> 为 true 时生效。</summary>
        public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        /// <summary>每帧绘制前的清屏色。</summary>
        public Color ClearColor { get; set; } = new Color(12, 14, 24);

        /// <summary>
        /// 创建游戏宿主（设备不在构造里建，照 MonoGame 在 Run 进入 Initialize 之前建）。
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id（页面里没有时引擎自动建一块全屏画布）。</param>
        /// <param name="antialias">
        /// 是否启用 MSAA。上下文创建后不可改，只能在构造时决定
        /// （等价于 MonoGame 里「设备创建前」设置 <c>PreferMultiSampling</c>）。
        /// </param>
        /// <param name="preferWebGpu">
        /// true（默认 false）：先试 WebGPU，失败回落 WebGL 2.0。开启时设备为<b>异步</b>创建，
        /// 因此必须在 <see cref="RunAsync"/> 里 await（不能在构造里）。false 则为纯 WebGL 2.0 同步创建。
        /// </param>
        protected Game(string canvasSelector = "#game", bool antialias = false, bool preferWebGpu = false)
        {
            if (Current != null)
            {
                throw new InvalidOperationException("Game.Current 已存在，不能重复创建 Game 实例。");
            }

            Current = this;
            HTML_Canvas.Current = new HTML_Canvas(canvasSelector);
            _antialias = antialias;
            _backendKind = preferWebGpu ? GraphicsBackendKind.WebGPU : GraphicsBackendKind.WebGL20;
            Components = new GameComponentCollection();
        }

        /// <summary>
        /// 直接指定渲染后端创建宿主。后端种类放在<b>第一个参数</b>，以免与上面那个 bool 重载在省略实参时产生歧义。
        /// </summary>
        protected Game(GraphicsBackendKind backend, string canvasSelector = "#game", bool antialias = false)
            : this(canvasSelector, antialias, preferWebGpu: backend == GraphicsBackendKind.WebGPU)
        {
            _backendKind = backend;
        }

        private readonly bool _antialias;
        private GraphicsBackendKind _backendKind = GraphicsBackendKind.WebGL20;

        /// <summary>
        /// 异步创建设备并搭建窗口（照 MonoGame 的 DoInitialize：Run 进入 Initialize 之前建好设备）。
        /// WebGPU 走 <see cref="GraphicsDevice.CreateAsync(GraphicsBackendKind, bool, bool)"/>（不支持时回落 WebGL 2.0），
        /// WebGL 2.0 / Canvas2D 走同步构造（它们的初始化本来就是同步的）。
        /// </summary>
        private async Task CreateDeviceAsync()
        {
            GraphicsDevice = _backendKind switch
            {
                GraphicsBackendKind.WebGPU => await GraphicsDevice.CreateAsync(_antialias, preferWebGpu: true),
                GraphicsBackendKind.Canvas2D => new GraphicsDevice(_antialias, GraphicsBackendKind.Canvas2D),
                _ => new GraphicsDevice(_antialias),
            };
            Window = new GameWindow(GraphicsDevice);
        }

        /// <summary>收到画布尺寸事件：应用新尺寸，变了才通知 Window（与原先每帧同步的行为一致）。</summary>
        private void OnWindowSizeChanged()
        {
            if (GraphicsDevice.ApplyCanvasSize())
                Window.OnWindowSizeChanged();
        }

        private void OnWindowFocusChanged(bool bFocus)
        {
            Window.OnWindowFocusChanged(bFocus);
        }

        /// <summary>启动主循环；返回的 Task 在 <see cref="Exit"/> 后完成。</summary>
        public async Task RunAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_initialized)
            {
                try
                {
                    // 照 MonoGame 的 DoInitialize：进入用户 Initialize 之前先建好设备并让管理器接管，
                    // 这样用户在 Initialize / LoadContent 里就能拿到 GraphicsDevice 与已应用的呈现参数。
                    // 设备创建（含 WebGPU 的异步取设备）在 Game 内部完成，不再由外部先建好再注入。
                    if (GraphicsDevice == null)
                        await CreateDeviceAsync();

                    if (graphicsDeviceManager != null)
                        ((IGraphicsDeviceManager)graphicsDeviceManager).CreateDevice();


                    JSBind_GameUpdate.Current = this;
                    Input_GameFrameData.WindowSizeChanged += OnWindowSizeChanged;
                    Input_GameFrameData.WindowFocusChanged += OnWindowFocusChanged;

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

            JSBind_GameUpdate.StartRenderLoop();
            await _exitSignal.Task;
        }

        /// <summary>结束主循环。</summary>
        public void Exit() => _exitSignal.TrySetResult();

        public void SetFrameInterval(int framesPerPresent)
            => _frameInterval = Math.Max(1, framesPerPresent);

        protected virtual void Initialize() { }
        /// <summary>
        /// 异步加载入口，默认转发到 <see cref="LoadContent"/>。
        /// 需要下载内容包时重写它：<c>await ContentManager.Default.LoadAsync(progress)</c>。
        /// </summary>
        protected virtual Task LoadContentAsync()
        {
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
                // 画布尺寸【不再每帧查】：原先这里每帧调 SyncCanvasSize()（内含一次跨界获取），
                // 现在由 input_window_event 在尺寸真变化时上报，经 Input_GameFrameData.CanvasResized
                // 事件回调到 OnCanvasResized 应用。初始化与手动改尺寸时仍会同步一次，见 GraphicsDeviceManager。

                double elapsed = _lastTimestamp < 0 ? 0d : (timestampMs - _lastTimestamp) / 1000d;
                _lastTimestamp = timestampMs;
                if (elapsed < 0d) elapsed = 0d;

                double target = TargetElapsedTime.TotalSeconds;
                if (target <= 0d) target = 1d / 60d;

                if (IsFixedTimeStep)
                {
                    // 经典 MonoGame 固定步长：累加真实间隔，攒够一个 target 就跑一次 Update（可一帧多次）。
                    _accumulator += elapsed;
                    // 螺旋死亡保护：单帧累计不超过 MaxElapsedSeconds，避免切后台回来一次性补爆。
                    if (_accumulator > MaxElapsedSeconds) _accumulator = MaxElapsedSeconds;

                    int steps = 0;
                    while (_accumulator >= target && steps < MaxStepsPerFrame)
                    {
                        _totalGameTime += TimeSpan.FromSeconds(target);
                        var stepTime = new GameTime(_totalGameTime, TimeSpan.FromSeconds(target));
                        Input.Update();
                        Update(stepTime);
                        Components.Update(stepTime);
                        Input.LateUpdate();
                        _accumulator -= target;
                        steps++;
                    }

                    // 本帧没攒够一个固定步：跳过渲染（对齐 MonoGame，Draw 被压到固定率，渲染/逻辑同频）。
                    if (steps == 0) return;

                    var drawElapsed = TimeSpan.FromSeconds(target * steps);
                    GraphicsDevice.Clear(ClearColor);
                    var drawTime = new GameTime(_totalGameTime, drawElapsed);
                    Draw(drawTime);
                    Components.Draw(drawTime);
                    // 收帧（照 MonoGame 在 Draw 结束后的 Present）。WebGL 后端无需动作；
                    // WebGPU 后端必须在此提交命令缓冲 —— 交换链纹理只在当前帧有效，
                    // 拖到下一帧再提交就会报 "Destroyed texture used in a submit" 且画面全黑。
                    GraphicsDevice.EndFrame();
                }
                else
                {
                    // 可变 dt：dt 用两次 rAF 的真实间隔（被 MaxElapsedSeconds 截断，避免切后台回来时一次性模拟过多）。
                    var span = TimeSpan.FromSeconds(Math.Min(elapsed, MaxElapsedSeconds));
                    _totalGameTime += span;
                    var frameTime = new GameTime(_totalGameTime, span);

                    Input.Update();
                    Update(frameTime);
                    Components.Update(frameTime);
                    Input.LateUpdate();

                    GraphicsDevice.Clear(ClearColor);
                    Draw(frameTime);
                    Components.Draw(frameTime);
                    // 同上：帧内必须收帧提交。
                    GraphicsDevice.EndFrame();
                }

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
            Input_GameFrameData.WindowSizeChanged -= OnWindowSizeChanged;
            Input_GameFrameData.WindowFocusChanged -= OnWindowFocusChanged;

            UnloadContent();
            graphicsDeviceManager?.Dispose();
            graphicsDeviceManager = null;
            GraphicsDevice?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
