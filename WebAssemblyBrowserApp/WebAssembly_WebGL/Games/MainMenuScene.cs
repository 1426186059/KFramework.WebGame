#nullable enable
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 游戏选择主菜单：按 1 / 2 / 3 或点击卡片进入对应游戏，游戏内按 Esc 返回。
/// </summary>
public sealed class MainMenuScene : GameScene
{
    private const float CardW = 240, CardH = 130, CardY = 240;
    private const float Card1X = 40, Card2X = 280, Card3X = 520;

    private float _time;
    private int[]? _demoPixels;
    private Texture? _demoVideo;
    private bool _videoLoading;

    public MainMenuScene() : base("main-menu") { }

    public override void Enter() => _time = 0;

    public override void Update(float dt)
    {
        _time += dt;
        EnsureVideoLoaded();

        if (Input.IsKeyPressed(Input.Digit1)) { GameEngine.Instance.Push("breakout"); return; }
        if (Input.IsKeyPressed(Input.Digit2)) { GameEngine.Instance.Push("tank"); return; }
        if (Input.IsKeyPressed("3")) { GameEngine.Instance.Push("xyj"); return; }

        if (Input.IsMousePressed())
        {
            float mx = Input.MouseX(), my = Input.MouseY();
            if (mx >= Card1X && mx <= Card1X + CardW && my >= CardY && my <= CardY + CardH)
                GameEngine.Instance.Push("breakout");
            else if (mx >= Card2X && mx <= Card2X + CardW && my >= CardY && my <= CardY + CardH)
                GameEngine.Instance.Push("tank");
            else if (mx >= Card3X && mx <= Card3X + CardW && my >= CardY && my <= CardY + CardH)
                GameEngine.Instance.Push("xyj");
        }
    }

    public override void Render()
    {
        WebGL.Clear("#0d1117");
        WebGL.Save();

        float cx = GameEngine.Width / 2;

        WebGL.Shadow("#4dabf7", 30);
        WebGL.FillText("WEB GAMES", cx, 118, "bold 56px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.NoShadow();
        WebGL.FillText("基于 .NET 10 + WebAssembly 的 2D 游戏引擎", cx, 158, "17px system-ui, sans-serif", "#8b949e", "center");

        // 卡片 1：打砖块
        WebGL.Shadow("#4dabf7", 14);
        WebGL.RoundedRect(Card1X, CardY, CardW, CardH, 14, "#161b22");
        WebGL.NoShadow();
        WebGL.RoundedRect(Card1X, CardY, CardW, CardH, 14, "#30363d");
        WebGL.FillText("1", Card1X + CardW / 2, CardY + 42, "bold 26px system-ui, sans-serif", "#4dabf7", "center");
        WebGL.FillText("打砖块", Card1X + CardW / 2, CardY + 78, "bold 24px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.FillText("BREAKOUT · 弹球打砖块", Card1X + CardW / 2, CardY + 106, "12px system-ui, sans-serif", "#8b949e", "center");

        // 卡片 2：坦克闯关
        WebGL.Shadow("#ffd43b", 14);
        WebGL.RoundedRect(Card2X, CardY, CardW, CardH, 14, "#161b22");
        WebGL.NoShadow();
        WebGL.RoundedRect(Card2X, CardY, CardW, CardH, 14, "#30363d");
        WebGL.FillText("2", Card2X + CardW / 2, CardY + 42, "bold 26px system-ui, sans-serif", "#ffd43b", "center");
        WebGL.FillText("坦克闯关", Card2X + CardW / 2, CardY + 78, "bold 24px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.FillText("TANK BATTLE · 守护基地", Card2X + CardW / 2, CardY + 106, "12px system-ui, sans-serif", "#8b949e", "center");

        // 卡片 3：西游记
        WebGL.Shadow("#f08c00", 14);
        WebGL.RoundedRect(Card3X, CardY, CardW, CardH, 14, "#161b22");
        WebGL.NoShadow();
        WebGL.RoundedRect(Card3X, CardY, CardW, CardH, 14, "#30363d");
        WebGL.FillText("3", Card3X + CardW / 2, CardY + 42, "bold 26px system-ui, sans-serif", "#f08c00", "center");
        WebGL.FillText("西游记", Card3X + CardW / 2, CardY + 78, "bold 24px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.FillText("MONKEY KING · 横版动作", Card3X + CardW / 2, CardY + 106, "12px system-ui, sans-serif", "#8b949e", "center");

        if ((int)(_time * 2) % 2 == 0)
            WebGL.FillText("按 1 / 2 / 3 或点击卡片进入游戏", cx, GameEngine.Height - 60, "18px system-ui, sans-serif", "#ffe066", "center");

        // 直接上传 GPU 纹理演示：像素数组 → Assets.UploadTexture → 按 id 直接 Draw
        RenderDemoTexture();

        // 视频纹理演示：GPU 硬解，当前解码帧直接进 GPU（WebGL texImage2D(video) 每帧上传）
        if (_demoVideo is { Id: >= 0 })
        {
            Assets.Draw(_demoVideo, 16, GameEngine.Height - 160, 256, 144);
            WebGL.FillText("视频纹理 · GPU 硬解", 16, GameEngine.Height - 172, "12px system-ui, sans-serif", "#8b949e", "left");
        }

        WebGL.Restore();
    }

    // 视频为异步加载，完成前自动跳过绘制
    private async void EnsureVideoLoaded()
    {
        if (_videoLoading || _demoVideo is not null) return;
        _videoLoading = true;
        _demoVideo = await Assets.LoadVideoAsync("/media/demo.mp4");
    }

    // 每帧重绘像素：横向 RGB 渐变 + 纵向滚动 + 半透明（同时验证 alpha 混合）
    private void RenderDemoTexture()
    {
        const int w = 128, h = 128;
        _demoPixels ??= new int[w * h];
        var px = _demoPixels;
        int offset = (int)(_time * 40) % h;
        for (int y = 0; y < h; y++)
        {
            int row = (y + offset) % h;
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = unchecked((int)0xCC000000) // 半透明 A=0xCC
                    | ((x * 255 / w) << 16)               // R 横向渐变
                    | ((row * 255 / h) << 8)              // G 纵向滚动
                    | 160;                                 // B 固定
            }
        }
        Assets.UploadTexture(0, w, h, px);   // 直接上传 GPU 纹理（id=0，'dyn:' 命名空间）
        Assets.Draw(0, GameEngine.Width - 148, GameEngine.Height - 148, w, h); // 按 id 直接绘制
        WebGL.FillText("Assets.UploadTexture", GameEngine.Width - 176, GameEngine.Height - 160, "12px system-ui, sans-serif", "#8b949e", "center");
    }
}
