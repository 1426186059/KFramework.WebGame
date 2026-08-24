using WebAssemblyBrowserApp.Engine;
using WebAssemblyBrowserApp.Games;

namespace WebAssemblyBrowserApp.Scenes;

/// <summary>
/// FC《西游记》风格横版动作游戏场景（WebGPU 渲染）。
/// 游戏逻辑与绘制全部在共享的 XyWorld 中，本场景只做渲染器适配。
/// </summary>
public sealed class XiYouJiScene : GameScene
{
    private readonly XyWorld _world = new(GameEngine.Width, GameEngine.Height);
    private readonly GpuRenderer _r = new();

    public XiYouJiScene() : base("xyj") { }

    public override void Enter() => _world.Reset();

    public override void Update(float dt)
    {
        if (Input.IsKeyPressed(Input.Escape))
        {
            GameEngine.Instance.Start("main-menu");
            return;
        }
        _world.Update(dt);
    }

    public override void Render() => _world.DrawScene(_r);

    private sealed class GpuRenderer : IXyRenderer
    {
        public void Clear(string color) => Pixi.Clear(color);
        public void FillRect(float x, float y, float w, float h, string color) => Pixi.FillRect(x, y, w, h, color);
        public void FillCircle(float cx, float cy, float r, string color) => Pixi.FillCircle(cx, cy, r, color);
        public void FillText(string text, float x, float y, string font, string color, string align) => Pixi.FillText(text, x, y, font, color, align);
        public void Alpha(float a) => Pixi.Alpha(a);
    }
}
