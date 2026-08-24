using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// FC《西游记》风格横版动作游戏场景（WebGL 渲染）。
/// 游戏逻辑与绘制全部在共享的 XyWorld 中，本场景只做渲染器适配。
/// </summary>
public sealed class XiYouJiScene : GameScene
{
    private readonly XyWorld _world = new(WebGL.LogicalWidth, WebGL.LogicalHeight);
    private readonly GlRenderer _r = new();

    public XiYouJiScene() : base("xyj") { }

    public override void Enter() => _world.Reset();

    public override void Update(float dt)
    {
        if (Input.IsKeyPressed(Input.Escape))
        {
            GameEngine.Instance.Pop();
            return;
        }
        _world.Update(dt);
    }

    public override void Render() => _world.DrawScene(_r);

    private sealed class GlRenderer : IXyRenderer
    {
        public void Clear(string color) => WebGL.Clear(color);
        public void FillRect(float x, float y, float w, float h, string color) => WebGL.FillRect(x, y, w, h, color);
        public void FillCircle(float cx, float cy, float r, string color) => WebGL.FillCircle(cx, cy, r, color);
        public void FillText(string text, float x, float y, string font, string color, string align) => WebGL.FillText(text, x, y, font, color, align);
        public void Alpha(float a) => WebGL.Alpha(a);
    }
}
