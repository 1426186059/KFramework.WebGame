namespace PixiJS;

/// <summary>
/// Pixi 应用单例：初始化画布、持有根舞台（句柄 0 = app.stage）、每帧渲染。
/// </summary>
public sealed partial class PixiApp
{
    public static PixiApp Instance { get; } = new();

    public const float Width = 960;
    public const float Height = 540;

    /// <summary>根舞台（固定句柄 0，对应 PixiJS app.stage）。</summary>
    public Container Stage { get; } = new(0);

    public bool IsInitialized { get; private set; }

    private PixiApp() { }

    public void Initialize(string selector = "#game")
    {
        if (IsInitialized) return;
        PixiApi.Init(selector);
        IsInitialized = true;
    }

    /// <summary>帧末调用一次：让 PixiJS 渲染当前场景图。</summary>
    public void Render() => PixiApi.Render();
}
