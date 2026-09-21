using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.CanvasTest;

/// <summary>
/// 画布（<c>&lt;canvas&gt;</c>）位置与尺寸控制：<see cref="HTML_Canvas"/> ↔ <c>html_canvas.ts</c>。
/// 全部操作都是上面的按钮，点一下就生效。
/// </summary>
/// <remarks>
/// <para>居中用的是「浏览器视口尺寸」，不是画布自身尺寸 —— 后者在改过一次画布之后就不再是屏幕大小了。</para>
/// <para>C# 只改 CSS：下面的 CSS 尺寸 / 后备缓冲 / 视口三行会在下一次对画布操作时立即同步
///（<see cref="GraphicsDevice.SyncCanvasSize"/>），拖动浏览器窗口时则由每帧自动同步。</para>
/// </remarks>
public sealed class CanvasTestScene : TestSceneBase
{
    private static readonly (string Name, int Width, int Height)[] Presets =
    [
        ("640×360", 640, 360),
        ("960×540", 960, 540),
        ("1280×720", 1280, 720),
    ];

    private int _presetIndex = 1;
    private string _hint = "点击上面的按钮切换画布布局";

    public override string Title => "画布位置与尺寸：点按钮切换（HTML_Canvas）";

    private GameWindow Window => KSceneMgr.Game.Window;

    private Point Preset
    {
        get
        {
            (string _, int width, int height) = Presets[_presetIndex];
            return new Point(width, height);
        }
    }

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        Layout();
        ClickButtons();

        if (Input_KeyBoard.GetKeyDown(Keys.D1)) SetCentered();
        if (Input_KeyBoard.GetKeyDown(Keys.D2)) Fullscreen();
        if (Input_KeyBoard.GetKeyDown(Keys.D3)) CyclePreset(1);
    }

    // ---------- 布局与按钮 ----------

    private void Layout()
    {
        BeginButtons(72);

        Point preset = Preset;
        AddUiButton($"预设 {preset.X}×{preset.Y} [3]", () => CyclePreset(1));
        AddUiButton("居中 [1]", SetCentered);
        AddUiButton("左上角", TopLeft);
        AddUiButton("填满整个 HTML 页面 [2]", Fullscreen);
        AddUiButton("恢复页面布局", Restore);
        AddUiButton("重新读回 Rect", Reread);
    }

    private void CyclePreset(int delta)
    {
        _presetIndex = (_presetIndex + delta + Presets.Length) % Presets.Length;
        SetCentered();
    }

    /// <summary>居中：位置由 html_canvas.ts 按 window.innerWidth/innerHeight 算，且窗口缩放时自动保持居中。</summary>
    private void SetCentered()
    {
        Point size = Preset;
        bool ok = Window.SetCanvasCentered(size.X, size.Y);
        SyncNow();
        Say(ok ? $"居中 {size.X}×{size.Y}（CSS 像素，窗口缩放会自动重新居中）" : "居中失败：找不到画布");
    }

    private void TopLeft()
    {
        Point size = Preset;
        bool ok = Window.SetCanvasRect(24, 96, size.X, size.Y);
        SyncNow();
        Say(ok ? $"摆到左上角 (24, 96)，{size.X}×{size.Y}" : "设置失败：找不到画布");
    }

    private void Fullscreen()
    {
        bool ok = Window.SetCanvasFullscreen();
        SyncNow();
        Say(ok ? "填满整个 HTML 页面（软全屏，不改显示模式）" : "设置失败：找不到画布");
    }

    private void Restore()
    {
        bool ok = Window.RestoreCanvasLayout();
        SyncNow();
        Say(ok ? "已清掉引擎写的内联样式，恢复页面自带的 CSS 布局" : "恢复失败：找不到画布");
    }

    /// <summary>画布 CSS 改完后立即同步后备缓冲 / 视口，不用等下一帧。</summary>
    private void Reread()
    {
        Window.Canvas.Refresh();
        Say($"已从浏览器读回 Rect：{Window.Canvas.Rect.X}, {Window.Canvas.Rect.Y} {Window.Canvas.Rect.Width}×{Window.Canvas.Rect.Height}");
    }

    /// <summary>
    /// GameWindow 的各 Set* 方法内部已经同步过后备缓冲与视口了；
    /// 这里再补一次，是为了覆盖「浏览器 resize 自动重新居中」这类不经过 C# 的尺寸变化。
    /// </summary>
    private void SyncNow()
    {
        _ = Device.SyncCanvasSize();
    }

    private void Say(string text) => _hint = text;

    // ---------- 绘制 ----------

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        DrawButtons(batch);

        float x = origin.X;
        float y = Math.Max(origin.Y, ContentTop);

        Point pageSize = Window.HTMLPageSize;

        y += DrawLine(batch, Font, $"提示：{_hint}", new Vector2(x, y), new Color(126, 200, 255));
        y += 10f;

        y += DrawSection(batch, "① 画布 / 视口尺寸", new Vector2(x, y));
        y += DrawLine(batch, Font, $"DOM id：{Window.CanvasId} / 存在：{Window.Canvas.Exists}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"画布 Rect：({Window.Canvas.Rect.X}, {Window.Canvas.Rect.Y}) {Window.Canvas.Rect.Width}×{Window.Canvas.Rect.Height}（HTML 元素实际矩形）",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font, $"HTML 页面尺寸：{pageSize.X} x {pageSize.Y}（居中算法用的就是它）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"可设最大尺寸：{pageSize.X} x {pageSize.Y}（超出就跑到可见区域外了）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"画布 CSS：{(int)Device.CssSize.X} x {(int)Device.CssSize.Y}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font,
                      $"后备缓冲：{Device.Viewport.Width} x {Device.Viewport.Height}（= CSS × DPR {Device.DevicePixelRatio:0.##}）",
                      new Vector2(x, y), new Color(150, 220, 255));

        y += 16f;
        y += DrawSection(batch, "② 按钮对应的 API", new Vector2(x, y));
        y += DrawLine(batch, Font, "居中 → GameWindow.SetCanvasCentered(w, h)（自动跟随窗口缩放）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "左上角 → GameWindow.SetCanvasRect(x, y, w, h)", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "填满整个 HTML 页面 → SetCanvasFullscreen（软全屏，不改显示模式）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "恢复页面布局 → GameWindow.RestoreCanvasLayout()", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "③ 注意", new Vector2(x, y));
        y += DrawLine(batch, Font, "· 改尺寸会重建 backing buffer，别把它放进每帧的 Update / Draw", new Vector2(x, y), new Color(255, 190, 140));
    }
}
