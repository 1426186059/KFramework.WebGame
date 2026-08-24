using PixiGame;
using PixiJS;

namespace PixiDemo;

/// <summary>主菜单：两个 FC 经典游戏入口按钮 + 左下角视频演示（原生 Pixi 对象模型）。</summary>
public sealed class MainMenuGame : GameScene
{
    private readonly (string scene, string title, string desc, float y)[] _entries =
    {
        ("contra",     "魂斗罗", "FC 横版卷轴射击：跳跃 射击 过关", 250f),
        ("tank-battle","坦克大战", "FC 基地守卫战：消灭敌军 守住基地", 380f),
    };

    private readonly List<(Container box, PixiText label)> _buttons = new();
    private Sprite? _video;
    private Container? _root;

    public MainMenuGame() : base("main-menu") { }

    public override void Enter()
    {
        var stage = PixiApp.Instance.Stage;
        _buttons.Clear();

        _root = Container.Create();
        stage.AddChild(_root);

        // 背景（叠几层半透明大色块模拟渐变）
        var bg = Graphics.Create();
        bg.BeginBatch();
        bg.DrawRect(0, 0, 960, 540, new Color(0.04f, 0.06f, 0.10f));
        bg.DrawRect(0, 0, 960, 540, new Color(0.20f, 0.35f, 0.55f, 0.10f));
        bg.DrawRect(0, 430, 960, 110, new Color(0.10f, 0.16f, 0.24f, 0.9f));
        bg.EndBatch();
        _root.AddChild(bg);

        // 标题（PixiText 默认 align="center"，锚点在文本中心 → 须显式给 X=屏幕中心）
        var title = new PixiText("FC 经典游戏合集", "bold 52px system-ui, sans-serif", Color.White);
        title.X = 480;
        title.Y = 78;
        _root.AddChild(title);
        var sub = new PixiText(".NET 10 WASM + PixiJS v8 原生对象模型（WebGPU / WebGL2 自适应）",
            "16px system-ui, sans-serif", new Color(0.55f, 0.65f, 0.80f));
        sub.X = 480;
        sub.Y = 132;
        _root.AddChild(sub);
        var hint = new PixiText("点击按钮进入游戏，Esc 返回菜单", "14px system-ui, sans-serif",
            new Color(0.45f, 0.5f, 0.6f));
        hint.X = 480;
        hint.Y = 505;
        _root.AddChild(hint);

        // 三个入口按钮
        foreach (var e in _entries) BuildButton(e.scene, e.title, e.desc, e.y);

        // 左下角视频演示（异步加载，失败静默）
        _ = LoadVideoAsync();
    }

    private void BuildButton(string scene, string title, string desc, float y)
    {
        var box = Container.Create();
        box.X = 360; box.Y = y;
        _root!.AddChild(box);

        var panel = Graphics.Create();
        panel.DrawRoundedRect(0, 0, 240, 74, 12, new Color(0.12f, 0.17f, 0.26f));
        panel.DrawRoundedRect(0, 0, 240, 74, 12, new Color(0.35f, 0.55f, 0.90f, 0.35f));
        panel.DrawLine(0, 73, 240, 73, 2, new Color(0.35f, 0.55f, 0.90f, 0.8f));
        box.AddChild(panel);

        var label = new PixiText(title, "bold 26px system-ui, sans-serif", Color.White);
        label.X = 120;   // 面板 (0,0,240,74) 的水平中心
        label.Y = -10;
        box.AddChild(label);
        var dlabel = new PixiText(desc, "13px system-ui, sans-serif", new Color(0.55f, 0.62f, 0.75f));
        dlabel.X = 120;
        dlabel.Y = 24;
        box.AddChild(dlabel);

        _buttons.Add((box, label));
    }

    private async Task LoadVideoAsync()
    {
        var tex = await PixiTexture.LoadVideoAsync("media/demo.mp4");
        if (tex is null || _root is null) return;
        var sprite = Sprite.Create();
        sprite.SetTexture(tex);
        float scale = 150f / tex.Height;
        sprite.Width = tex.Width * scale;
        sprite.Height = 150f;
        sprite.X = 20; sprite.Y = 360;
        _video = sprite;
        _root.AddChild(sprite);
    }

    public override void Update(float dt)
    {
        if (!Input.IsMousePressed()) return;
        float mx = Input.MouseX(), my = Input.MouseY();
        for (int i = 0; i < _buttons.Count; i++)
        {
            var (box, _) = _buttons[i];
            if (mx >= box.X && mx <= box.X + 240 && my >= box.Y && my <= box.Y + 74)
            {
                GameApp.Instance.Start(_entries[i].scene);
                return;
            }
        }
    }

    public override void Exit()
    {
        if (_root is not null) { _root.Destroy(); _root = null; }
    }

    public override void Render() { }
}
