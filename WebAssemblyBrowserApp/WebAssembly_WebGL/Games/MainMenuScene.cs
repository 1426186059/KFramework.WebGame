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
    private Texture2D _demoTex;

    public MainMenuScene() : base("main-menu") { }

    public override void Enter() => _time = 0;

    public override void Update(float dt)
    {
        _time += dt;

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

        // Texture2D 演示：动态纹理全链路（逐像素写入 → Commit 重传 GPU → Draw）
        _demoTex ??= new Texture2D(128, 128);
        RenderDemoTexture();

        WebGL.Restore();
    }

    // 每帧重绘像素：横向 RGB 渐变 + 纵向滚动 + 半透明（同时验证 alpha 混合）
    private void RenderDemoTexture()
    {
        var tex = _demoTex;
        int w = tex.Width, h = tex.Height;
        var px = tex.Pixels;
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
        tex.Commit();
        tex.Draw(GameEngine.Width - 148, GameEngine.Height - 148, 128, 128);
        WebGL.FillText("Texture2D", GameEngine.Width - 164, GameEngine.Height - 160, "12px system-ui, sans-serif", "#8b949e", "center");
    }
}
