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
        /// <summary>单帧最大推进时间，防止切后台回来时一次性模拟过多。</summary>
        private const double MaxElapsedSeconds = 0.25;

        /// <summary>固定步长模式下单帧最多模拟次数。</summary>
        private const int MaxStepsPerFrame = 5;

        private readonly TaskCompletionSource _exitSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private double _lastTimestamp = -1;
        private double _accumulator;
        private TimeSpan _totalGameTime;
        private bool _initialized;
        private bool _frameFaulted;
        private bool _disposed;

        public GraphicsDevice GraphicsDevice { get; }

        public GameWindow Window { get; }

        public ContentManager Content { get; }

        public GameComponentCollection Components { get; }

        /// <summary>是否使用固定时间步长（默认 60Hz 逻辑帧，渲染仍是每帧一次）。</summary>
        public bool IsFixedTimeStep { get; set; } = true;

        public TimeSpan TargetElapsedTime { get; set; } = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        /// <summary>每帧绘制前的清屏色。</summary>
        public Color ClearColor { get; set; } = new Color(12, 14, 24);

        protected Game(string canvasSelector = "#game", string contentRoot = "content")
        {
            GraphicsDevice = new GraphicsDevice(canvasSelector);
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

            try
            {
                if (GraphicsDevice.SyncCanvasSize()) Window.RaiseSizeChanged();

                double elapsed = _lastTimestamp < 0 ? 0d : (timestampMs - _lastTimestamp) / 1000d;
                _lastTimestamp = timestampMs;
                if (elapsed < 0d) elapsed = 0d;

                Input.Poll();

                double target = TargetElapsedTime.TotalSeconds;
                if (target <= 0d) target = 1d / 60d;

                if (IsFixedTimeStep)
                {
                    _accumulator += elapsed;

                    // 螺旋死亡保护：对齐 MonoGame，用 clamp 限制上限，而不是清零丢弃时间。
                    // 官方 Game.cs:553-554
                    //   if (_accumulatedElapsedTime > _maxElapsedTime) _accumulatedElapsedTime = _maxElapsedTime;
                    if (_accumulator > MaxElapsedSeconds) _accumulator = MaxElapsedSeconds;

                    int steps = 0;
                    while (_accumulator >= target && steps < MaxStepsPerFrame)
                    {
                        _totalGameTime += TimeSpan.FromSeconds(target);
                        var stepTime = new GameTime(_totalGameTime, TimeSpan.FromSeconds(target));
                        Update(stepTime);
                        Components.Update(stepTime);
                        // 固定步长下同一帧可能跑多个步，但 Poll 每帧只一次；
                        // 每个步结束后清空按下/抬起边沿，确保一次按键只触发一次（见 Input.ConsumeStepEdges）。
                        Input.ConsumeStepEdges();
                        _accumulator -= target;
                        steps++;
                    }

                    // 剩下的 accumulator 留到下一帧继续补，不要清零（旧实现清零会丢弃时间）。

                    // 对齐 MonoGame「每次 Tick 至少 Update 一次」的保证（官方注释 Game.cs:505-512）。
                    // 官方在 Game.cs:537-550 用 Sleep + goto RetryTick 等待到时间够为止；
                    // 浏览器不能阻塞 rAF 线程，故改成「逻辑没推进就跳过本帧渲染」，
                    // 效果同样是渲染与逻辑同频，消除高刷屏上"一帧不动、一帧走两格"造成的跳动。
                    if (steps == 0) return;

                    // Draw 使用逻辑时间：对齐官方 Game.cs:592
                    //   _gameTime.ElapsedGameTime = TimeSpan.FromTicks(TargetElapsedTime.Ticks * stepCount);
                    var drawElapsed = TimeSpan.FromSeconds(target * steps);
                    GraphicsDevice.Clear(ClearColor);
                    var drawTime = new GameTime(_totalGameTime, drawElapsed);
                    Draw(drawTime);
                    Components.Draw(drawTime);
                }
                else
                {
                    var span = TimeSpan.FromSeconds(Math.Min(elapsed, MaxElapsedSeconds));
                    _totalGameTime += span;
                    var frameTime = new GameTime(_totalGameTime, span);
                    Update(frameTime);
                    Components.Update(frameTime);

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
            Content.Dispose();
            GraphicsDevice.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
