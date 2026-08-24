using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 游戏选择主菜单：按 1 / 2 或点击卡片进入对应游戏，游戏内按 Esc 返回。
/// </summary>
public sealed class MainMenuScene : GameScene
{
    private const float Card1X = 60, Card2X = 440, CardY = 240, CardW = 300, CardH = 130;

    private float _time;

    public MainMenuScene() : base("menu") { }

    public override void Enter() => _time = 0;

    public override void Update(float dt)
    {
        _time += dt;

        if (Input.IsKeyPressed(Input.Digit1)) { GameEngine.Instance.Push("breakout"); return; }
        if (Input.IsKeyPressed(Input.Digit2)) { GameEngine.Instance.Push("tank"); return; }

        if (Input.IsMousePressed())
        {
            float mx = Input.MouseX(), my = Input.MouseY();
            if (mx >= Card1X && mx <= Card1X + CardW && my >= CardY && my <= CardY + CardH)
                GameEngine.Instance.Push("breakout");
            else if (mx >= Card2X && mx <= Card2X + CardW && my >= CardY && my <= CardY + CardH)
                GameEngine.Instance.Push("tank");
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

        // 卡片 1：打砖子
        WebGL.Shadow("#4dabf7", 14);
        WebGL.RoundedRect(Card1X, CardY, CardW, CardH, 14, "#161b22");
        WebGL.NoShadow();
        WebGL.RoundedRect(Card1X, CardY, CardW, CardH, 14, "#30363d");
        WebGL.FillText("1", Card1X + CardW / 2, CardY + 42, "bold 26px system-ui, sans-serif", "#4dabf7", "center");
        WebGL.FillText("打砖子", Card1X + CardW / 2, CardY + 78, "bold 26px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.FillText("BREAKOUT · 弹球打砖块", Card1X + CardW / 2, CardY + 106, "13px system-ui, sans-serif", "#8b949e", "center");

        // 卡片 2：坦克闯关
        WebGL.Shadow("#ffd43b", 14);
        WebGL.RoundedRect(Card2X, CardY, CardW, CardH, 14, "#161b22");
        WebGL.NoShadow();
        WebGL.RoundedRect(Card2X, CardY, CardW, CardH, 14, "#30363d");
        WebGL.FillText("2", Card2X + CardW / 2, CardY + 42, "bold 26px system-ui, sans-serif", "#ffd43b", "center");
        WebGL.FillText("坦克闯关", Card2X + CardW / 2, CardY + 78, "bold 26px system-ui, sans-serif", "#e6edf3", "center");
        WebGL.FillText("TANK BATTLE · 守护基地", Card2X + CardW / 2, CardY + 106, "13px system-ui, sans-serif", "#8b949e", "center");

        if ((int)(_time * 2) % 2 == 0)
            WebGL.FillText("按 1 / 2 或点击卡片进入游戏", cx, GameEngine.Height - 60, "18px system-ui, sans-serif", "#ffe066", "center");

        WebGL.Restore();
    }
}
