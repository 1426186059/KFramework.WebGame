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

        public GraphicsDevice GraphicsDevice { get; }

        public GameWindow Window { get; }

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
        /// 创建游戏宿主。
        /// </summary>
        /// <param name="canvasSelector">画布选择器或 DOM id（页面里没有时引擎自动建一块全屏画布）。</param>
        /// <param name="contentRoot">内容包根路径。</param>
        /// <param name="antialias">
        /// 是否启用 MSAA。上下文创建后不可改，只能在构造时决定
        /// （等价于 MonoGame 里「设备创建前」设置 <c>PreferMultiSampling</c>）。
        /// </param>
        protected Game(string canvasSelector = "#game", bool antialias = false)
        {
            GraphicsDevice = new GraphicsDevice(canvasSelector, antialias);
            Window = new GameWindow(GraphicsDevice);
            Components = new GameComponentCollection();
            JSBind_GameUpdate.Current = this;
        }

        /// <summary>
        /// 用「外部已创建好的」图形设备创建游戏宿主。
        /// <para>
        /// WebGPU 的设备初始化是异步的（requestAdapter / requestDevice），在 wasm 单线程下
        /// <b>不能</b>阻塞等待（JS Promise 要回到事件循环才 resolve，阻塞会死锁），
        /// 因此无法在构造函数里同步建好设备。用法：
        /// <code>
        /// var device = await GraphicsDevice.CreateAsync("#game", antialias, preferWebGpu: true);
        /// var game = new MyGame(device);
        /// await game.RunAsync();
        /// </code>
        /// </para>
        /// </summary>
        protected Game(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            GraphicsDevice = device;
            Window = new GameWindow(device);
            Components = new GameComponentCollection();
            JSBind_GameUpdate.Current = this;
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

            JSBind_GameUpdate.StartRenderLoop();
            await _exitSignal.Task;
        }

        /// <summary>结束主循环。</summary>
        public void Exit() => _exitSignal.TrySetResult();

        public void SetFrameInterval(int framesPerPresent)
            => _frameInterval = Math.Max(1, framesPerPresent);

        protected virtual void Initialize() { }

        /// <summary>同步加载（无异步需求时重写它即可）。</summary>
        protected virtual void LoadContent() { }

        /// <summary>
        /// 异步加载入口，默认转发到 <see cref="LoadContent"/>。
        /// 需要下载内容包时重写它：<c>await ContentManager.Default.LoadAsync(progress)</c>。
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
            UnloadContent();
            graphicsDeviceManager?.Dispose();
            graphicsDeviceManager = null;
            GraphicsDevice.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
