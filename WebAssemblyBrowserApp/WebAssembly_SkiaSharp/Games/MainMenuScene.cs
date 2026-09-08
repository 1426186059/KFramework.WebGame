using System;
using System.Collections.Generic;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 主菜单：展示游戏列表，↑↓ 键或鼠标选择，回车 / 空格 / 点击进入。
/// 游戏中按 ESC 回到本菜单。
/// </summary>
public sealed class MainMenuScene : GameScene
{
    private sealed record GameItem(string DisplayName, string SceneName, string Accent, string Hint);

    private static readonly GameItem[] Games = new[]
    {
        new GameItem("BREAKOUT", "breakout", "#4dabf7", "ARROWS / MOUSE MOVE PADDLE · SPACE LAUNCH"),
        new GameItem("BATTLE CITY", "tank", "#f6c445", "ARROWS MOVE · SPACE FIRE"),
    };

    private readonly struct Sparkle
    {
        public readonly float X, Y, Speed, Size, Life;
        public Sparkle(float x, float y, float s, float size, float life)
        { X = x; Y = y; Speed = s; Size = size; Life = life; }
    }

    private readonly List<Sparkle> _bg = new();

    private int _selected;
    private float _stateTime;

    public MainMenuScene() : base("main-menu") { }

    public override void Update(float dt)
    {
        _stateTime += dt;

        if (Input.IsKeyPressed("ArrowUp") || Input.IsKeyPressed(Input.KeyW))
        {
            _selected = (_selected - 1 + Games.Length) % Games.Length;
            Audio.Beep(440, 0.05f, "square", 0.05f);
        }
        else if (Input.IsKeyPressed("ArrowDown") || Input.IsKeyPressed(Input.KeyS))
        {
            _selected = (_selected + 1) % Games.Length;
            Audio.Beep(360, 0.05f, "square", 0.05f);
        }
        else if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
        {
            var picked = PickGameByMouse();
            if (picked.HasValue) _selected = picked.Value;

            Audio.Beep(523, 0.08f, "square", 0.07f);
            Audio.Beep(659, 0.10f, "square", 0.07f);
            StartSelected();
        }
    }

    private void StartSelected()
    {
        var g = Games[_selected];
        try
        {
            GameEngine.Instance.Start(g.SceneName);
        }
        catch (Exception ex)
        {
            Storage.Set("main_menu.last_error", ex.Message);
        }
    }

    private int? PickGameByMouse()
    {
        float mx = Input.MouseX();
        float my = Input.MouseY();
        float cx = GameEngine.Width / 2;
        float startY = GameEngine.Height / 2 - 40;
        float itemH = 84;
        for (int i = 0; i < Games.Length; i++)
        {
            float y = startY + i * itemH;
            float w = 560, h = 68;
            if (mx >= cx - w / 2 && mx <= cx + w / 2 && my >= y - h / 2 && my <= y + h / 2)
                return i;
        }
        return null;
    }

    public override void Render()
    {
        SkiaCanvas.GradientRoundRect(0, 0, GameEngine.Width, GameEngine.Height, 0, "#132033", "#0d1117");

        float cx = GameEngine.Width / 2;
        float cy = GameEngine.Height / 2;

        RenderBgSparkles();

        SkiaCanvas.Shadow("#4dabf7", 22);
        SkiaCanvas.FillText("SKIASHARP 2D GAMES", cx, 110, "bold 44px system-ui", "#e6edf3", "center");
        SkiaCanvas.NoShadow();
        SkiaCanvas.FillText("POWERED BY .NET 10 WEBASSEMBLY + SKIASHARP",
            cx, 150, "14px system-ui", "#8b949e", "center");

        float startY = cy - 20;
        float itemH = 96;
        for (int i = 0; i < Games.Length; i++)
            RenderGameItem(cx, startY + i * itemH, Games[i], i == _selected);

        if ((int)(_stateTime * 2) % 2 == 0)
        {
            SkiaCanvas.FillText("UP / DOWN SELECT   ·   ENTER OR CLICK TO START   ·   ESC BACK TO MENU",
                cx, GameEngine.Height - 40, "13px system-ui", "#6e7681", "center");
        }
    }

    private void RenderBgSparkles()
    {
        for (int i = _bg.Count - 1; i >= 0; i--)
        {
            var s = _bg[i];
            float life = s.Life - _stateTime;
            if (life < 0) { _bg.RemoveAt(i); continue; }
            float y = s.Y + s.Speed * _stateTime;
            if (y > GameEngine.Height + 20) { _bg.RemoveAt(i); continue; }
            SkiaCanvas.Alpha(Math.Clamp(life * 2, 0, 1));
            SkiaCanvas.FillRect(s.X, y, s.Size, s.Size, i % 3 == 0 ? "#4d6bff" : (i % 3 == 1 ? "#f6c445" : "#ff6b6b"));
        }
        SkiaCanvas.Alpha(1);

        if (_bg.Count < 80 && Random.Shared.NextDouble() < _stateTime * 1e-1)
        {
            _bg.Add(new Sparkle((float)Random.Shared.NextDouble() * GameEngine.Width, -10,
                30 + (float)Random.Shared.NextDouble() * 40,
                2 + (float)Random.Shared.NextDouble() * 3,
                _stateTime + 4));
        }
    }

    private void RenderGameItem(float cx, float y, GameItem g, bool selected)
    {
        float w = 560, h = 72;
        float x = cx - w / 2;
        float top = y - h / 2;

        if (selected)
        {
            SkiaCanvas.Shadow(g.Accent, 28);
            SkiaCanvas.GradientRoundRect(x, top, w, h, 14, g.Accent + "44", "#161b22");
            SkiaCanvas.NoShadow();
            SkiaCanvas.StrokeRect(x - 2, top - 2, w + 4, h + 4, g.Accent, 3);
        }
        else
        {
            SkiaCanvas.RoundedRect(x, top, w, h, 14, "#161b22");
            SkiaCanvas.StrokeRect(x, top, w, h, "#30363d", 1.5f);
        }

        SkiaCanvas.FillText(g.DisplayName, x + 28, y - 8, "bold 22px system-ui",
            selected ? "#e6edf3" : "#c9d1d9", "left");
        SkiaCanvas.FillText(g.Hint, x + 28, y + 20, "13px system-ui", "#8b949e", "left");

        SkiaCanvas.FillText(">", x + w - 40, y, "bold 26px system-ui",
            selected ? g.Accent : "#30363d", "center");
    }
}
