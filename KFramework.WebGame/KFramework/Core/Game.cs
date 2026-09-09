using KFramework.Content;
using KFramework.Graphics;

namespace KFramework;

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
        Content = new ContentManager(GraphicsDevice, contentRoot);
        Components = new GameComponentCollection();
        GameHost.Current = this;
    }

    /// <summary>启动主循环；返回的 Task 在 <see cref="Exit"/> 后完成。</summary>
    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            Initialize();
            Components.Initialize();
            await LoadContentAsync().ConfigureAwait(false);
            Components.LoadContent();
            _initialized = true;
        }

        Platform.StartRenderLoop();
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
            else if (elapsed > MaxElapsedSeconds) elapsed = MaxElapsedSeconds;

            Input.Poll();

            double target = TargetElapsedTime.TotalSeconds;
            if (target <= 0d) target = 1d / 60d;

            if (IsFixedTimeStep)
            {
                _accumulator += elapsed;
                int steps = 0;
                while (_accumulator >= target && steps < MaxStepsPerFrame)
                {
                    _totalGameTime += TimeSpan.FromSeconds(target);
                    var stepTime = new GameTime(_totalGameTime, TimeSpan.FromSeconds(target));
                    Update(stepTime);
                    Components.Update(stepTime);
                    _accumulator -= target;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) _accumulator = 0d;
            }
            else
            {
                var span = TimeSpan.FromSeconds(elapsed);
                _totalGameTime += span;
                var frameTime = new GameTime(_totalGameTime, span);
                Update(frameTime);
                Components.Update(frameTime);
            }

            GraphicsDevice.Clear(ClearColor);
            var drawTime = new GameTime(_totalGameTime, TimeSpan.FromSeconds(elapsed));
            Draw(drawTime);
            Components.Draw(drawTime);
        }
        catch (Exception ex)
        {
            // 浏览器里异常会打断 rAF 链，这里就地截获并停止循环，避免持续刷屏
            _frameFaulted = true;
            Console.Error.WriteLine($"[KFramework] 运行时异常，主循环已停止：{ex}");
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
