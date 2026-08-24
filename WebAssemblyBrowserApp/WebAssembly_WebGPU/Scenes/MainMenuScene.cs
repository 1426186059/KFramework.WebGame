using System;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Scenes;

/// <summary>
/// 主菜单场景：展示游戏列表，玩家可以用 ↑↓ 键或鼠标选择要进入的游戏。
/// 同时按 ESC 退出当前游戏回到主菜单。
/// </summary>
public sealed class MainMenuScene : GameScene
{
    private sealed record GameItem(string DisplayName, string SceneName, string Accent, string Hint);

    private static readonly GameItem[] Games = new[]
    {
        new GameItem("打砖块 BREAKOUT", "breakout",  "#4dabf7", "← → 挡板，空格发射"),
        new GameItem("坦克大战 BATTLE CITY", "tank",  "#f6c445", "↑↓←→ 移动，空格开炮"),
        new GameItem("FC 西游记 · MONKEY KING", "xyj",  "#f08c00", "←→ 移动，SPACE 跳，J 攻击"),
    };

    private int _selected;
    private float _stateTime;

    public MainMenuScene() : base("main-menu") { }

    public override void Update(float dt)
    {
        _stateTime += dt;

        if (Input.IsKeyPressed("ArrowUp") || Input.IsKeyPressed("w") || Input.IsKeyPressed("W"))
        {
            _selected = (_selected - 1 + Games.Length) % Games.Length;
            Audio.Beep(440, 0.05f, "square", 0.05f);
        }
        else if (Input.IsKeyPressed("ArrowDown") || Input.IsKeyPressed("s") || Input.IsKeyPressed("S"))
        {
            _selected = (_selected + 1) % Games.Length;
            Audio.Beep(360, 0.05f, "square", 0.05f);
        }
        else if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
        {
            // 鼠标点击时选中鼠标指向的条目
            var picked = PickGameByMouse();
            if (picked.HasValue) _selected = picked.Value;

            Audio.Beep(523, 0.08f, "square", 0.07f);
            Audio.Beep(659, 0.10f, "square", 0.07f);
            // 延迟 160ms 让音效播完，用 stateTime 下一帧开始切换
            StartSelected();
            return;
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
            // 场景未注册：留在主菜单，打条日志
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
        WebGPU.Clear("#0d1117");

        float cx = GameEngine.Width / 2;
        float cy = GameEngine.Height / 2;

        // 背景装饰粒子：屏幕顶部缓慢飘落小方块
        RenderBgSparkles();

        // 标题
        WebGPU.Shadow("#4dabf7", 22);
        WebGPU.FillText("KFramework 2D Games", cx, 110, "bold 46px system-ui, sans-serif", "#e6edf3", "center");
        WebGPU.NoShadow();
        WebGPU.FillText("选择一个游戏开始  ·  基于 .NET 10 WebAssembly + WebGPU",
            cx, 150, "16px system-ui, sans-serif", "#8b949e", "center");

        float startY = cy - 40;
        float itemH = 84;
        for (int i = 0; i < Games.Length; i++)
        {
            float y = startY + i * itemH;
            bool selected = i == _selected;
            RenderGameItem(cx, y, Games[i], selected);
        }

        // 底部说明
        if ((int)(_stateTime * 2) % 2 == 0)
        {
            WebGPU.FillText("↑ ↓ 选择  ·  回车 / 空格 / 点击 进入  ·  游戏中按 ESC 回到菜单",
                cx, GameEngine.Height - 40, "14px system-ui, sans-serif", "#6e7681", "center");
        }
    }

    private readonly struct Sparkle
    {
        public readonly float X, Y, Speed, Size, Life;
        public Sparkle(float x, float y, float s, float size, float life) { X = x; Y = y; Speed = s; Size = size; Life = life; }
    }
    private readonly System.Collections.Generic.List<Sparkle> _bg = new();

    private void RenderBgSparkles()
    {
        for (int i = _bg.Count - 1; i >= 0; i--)
        {
            var s = _bg[i];
            float life = s.Life - _stateTime;
            if (life < 0) { _bg.RemoveAt(i); continue; }
            float y = s.Y + s.Speed * _stateTime;
            if (y > GameEngine.Height + 20) { _bg.RemoveAt(i); continue; }
            float a = Math.Clamp(life * 2, 0, 1);
            WebGPU.Alpha(a);
            WebGPU.FillRect(s.X, y, s.Size, s.Size, i % 3 == 0 ? "#4d6bff" : (i % 3 == 1 ? "#f6c445" : "#ff6b6b"));
        }
        WebGPU.Alpha(1);
        // 生成
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
        float w = 560, h = 68;
        float x = cx - w / 2;
        float top = y - h / 2;

        if (selected)
        {
            WebGPU.Shadow(g.Accent, 28);
            WebGPU.RoundedRect(x, top, w, h, 14, g.Accent + "33");
            WebGPU.StrokeRect(x - 2, top - 2, w + 4, h + 4, 3, g.Accent);
            WebGPU.NoShadow();
        }
        else
        {
            WebGPU.RoundedRect(x, top, w, h, 14, "#161b22");
            WebGPU.StrokeRect(x, top, w, h, 1.5f, "#30363d");
        }

        WebGPU.FillText(g.DisplayName, x + 28, y - 8, "bold 22px system-ui, sans-serif", selected ? "#e6edf3" : "#c9d1d9", "left");
        WebGPU.FillText(g.Hint, x + 28, y + 20, "14px system-ui, sans-serif", "#8b949e", "left");

        WebGPU.FillText("▶", x + w - 40, y, "bold 26px system-ui, sans-serif",
            selected ? g.Accent : "#30363d", "center");
    }
}
