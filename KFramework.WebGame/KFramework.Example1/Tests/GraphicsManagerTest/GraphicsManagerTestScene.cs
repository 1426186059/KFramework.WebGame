using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.GraphicsManagerTest;

/// <summary>
/// <see cref="GraphicsDeviceManager"/> 用法演示（照 MonoGame 的标准套路），全部操作都是上面的按钮。
/// <para>· 创建：<c>graphics = new GraphicsDeviceManager(this);</c>（见 <see cref="Example1Game"/>）；</para>
/// <para>· 设置：Preferred* / IsFullScreen / HardwareModeSwitch / SynchronizeWithVerticalRetrace / PreferMultiSampling；</para>
/// <para>· 生效：改完必须 <see cref="GraphicsDeviceManager.ApplyChanges"/>（每个按钮里都调了）；</para>
/// <para>· 事件：<see cref="GraphicsDeviceManager.DeviceCreated"/>、<see cref="GraphicsDeviceManager.PreparingDeviceSettings"/>。</para>
/// </summary>
/// <remarks>
/// <para>下方铺了几千个精灵当「效果图」：改分辨率 / 全屏时画面范围、像素密度与 FPS 会立刻跟着变，
/// 比看数字直观得多。精灵数用按钮切（1000 / 3000 / 8000），纹理由代码生成，不依赖任何图片资源。</para>
/// <para>数字键 1..6 是同样操作的快捷方式。全屏按钮必须在用户手势里触发，点按钮正好满足浏览器的要求。</para>
/// </remarks>
public sealed class GraphicsManagerTestScene : TestSceneBase
{
    /// <summary>可选精灵数量档位。</summary>
    private static readonly int[] CountOptions = [1000, 3000, 8000];

    private readonly List<IDisposable> _owned = [];

    private Texture2D? _ball;
    private int _countIndex = 1;      // 默认 3000
    private float _time;
    private int _drawn;

    public override string Title => "GraphicsDeviceManager：点按钮改参数 + ApplyChanges（下方几千个精灵看效果）";

    private GraphicsDeviceManager Manager => ((Example1Game)KSceneMgr.Game).Graphics;

    public override void LoadContent()
    {
        _ball = Own(TestSpriteTexture.MakeBall(Device, 32));
    }

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        // 动画时间用逻辑步长（与帧率无关，动画速度才稳定）；FPS 一律读基类的真实帧率
        _time += KTime.deltaTime;

        Layout();
        ClickButtons();

        if (Input_KeyBoard.GetKeyDown(Keys.D1)) Set(manager => manager.SynchronizeWithVerticalRetrace = !manager.SynchronizeWithVerticalRetrace);
        if (Input_KeyBoard.GetKeyDown(Keys.D2)) Set(manager => manager.PreferMultiSampling = !manager.PreferMultiSampling);
        if (Input_KeyBoard.GetKeyDown(Keys.D3)) Set(manager => manager.IsFullScreen = !manager.IsFullScreen);
        if (Input_KeyBoard.GetKeyDown(Keys.D4)) Manager.ToggleFullScreen();
        if (Input_KeyBoard.GetKeyDown(Keys.D5)) ApplyPreset(1280, 720);
        if (Input_KeyBoard.GetKeyDown(Keys.D6)) ApplyPreset(800, 480);
    }

    // ---------- 布局与按钮 ----------

    private void Layout()
    {
        BeginButtons(72);

        GraphicsDeviceManager g = Manager;

        // ① 开关：点一下切换状态并立即 ApplyChanges
        AddUiButton(g.SynchronizeWithVerticalRetrace ? "垂直同步：开" : "垂直同步：关",
                    () => Set(manager => manager.SynchronizeWithVerticalRetrace = !manager.SynchronizeWithVerticalRetrace),
                    g.SynchronizeWithVerticalRetrace);
        AddUiButton(g.PreferMultiSampling ? "多重采样：开" : "多重采样：关",
                    () => Set(manager => manager.PreferMultiSampling = !manager.PreferMultiSampling),
                    g.PreferMultiSampling);
        AddUiButton(g.HardwareModeSwitch ? "全屏方式：原生(硬)" : "全屏方式：铺满(软)",
                    () => Set(manager => manager.HardwareModeSwitch = !manager.HardwareModeSwitch),
                    g.HardwareModeSwitch);
        AddUiButton(g.IsFullScreen ? "IsFullScreen：开" : "IsFullScreen：关",
                    () => Set(manager => manager.IsFullScreen = !manager.IsFullScreen),
                    g.IsFullScreen);
        AddUiButton("ToggleFullScreen()", () => Manager.ToggleFullScreen(), g.IsFullScreen);

        // ② 分辨率：改 PreferredBackBuffer* 后 ApplyChanges
        AddUiButton("1280×720", () => ApplyPreset(1280, 720));
        AddUiButton("800×480", () => ApplyPreset(800, 480));
        AddUiButton("铺满视口(不钉死)", ReleaseToViewport);
        AddUiButton($"精灵 {CountOptions[_countIndex]:N0}", CycleCount);
    }

    private void CycleCount() => _countIndex = (_countIndex + 1) % CountOptions.Length;

    /// <summary>改一个属性然后 ApplyChanges（这就是 MonoGame 的标准顺序）。</summary>
    private void Set(Action<GraphicsDeviceManager> change)
    {
        change(Manager);
        Manager.ApplyChanges();
    }

    private void ApplyPreset(int width, int height)
    {
        Manager.IsFullScreen = false;
        Manager.PreferredBackBufferWidth = width;
        Manager.PreferredBackBufferHeight = height;
        Manager.ApplyChanges();
    }

    /// <summary>清掉 Preferred* 的影响：把期望尺寸还原成画布当前大小，画布重新随浏览器缩放。</summary>
    private void ReleaseToViewport()
    {
        Manager.PreferredBackBufferWidth = Device.Viewport.Width;
        Manager.PreferredBackBufferHeight = Device.Viewport.Height;
        Manager.ApplyChanges();
    }

    // ---------- 绘制 ----------

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        DrawButtons(batch);

        float x = origin.X;
        float y = Math.Max(origin.Y, ContentTop);

        GraphicsDeviceManager g = Manager;
        PresentationParameters pp = Device.PresentationParameters;

        y += DrawSection(batch, "① 管理器当前设置（改完要 ApplyChanges）", new Vector2(x, y));
        y += DrawLine(batch, Font, $"PreferredBackBuffer：{g.PreferredBackBufferWidth} x {g.PreferredBackBufferHeight}（{g.PreferredBackBufferFormat}）",
                      new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font,
                      $"垂直同步：{g.SynchronizeWithVerticalRetrace} / 多重采样：{g.PreferMultiSampling} / 全屏方式：{(g.HardwareModeSwitch ? "原生" : "铺满")} / IsFullScreen：{g.IsFullScreen}",
                      new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "② 已应用到设备与画布的值", new Vector2(x, y));
        y += DrawLine(batch, Font,
                      $"画布 CSS：{(int)Device.CssSize.X} x {(int)Device.CssSize.Y}（DPR {Device.DevicePixelRatio:0.##}）→ 后备缓冲 {pp.BackBufferWidth} x {pp.BackBufferHeight}",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font,
                      $"FPS：{Fps:0.}（真实帧率） / 帧间隔 {KTime.realDeltaTime * 1000f:0.#}ms / 精灵 {_drawn:N0} / DrawCall {Device.Metrics.DrawCount}",
                      new Vector2(x, y), Color.LightGray);

        // 剩余空间画几千个精灵：改分辨率 / 全屏后，这里的可视范围与密度会立刻变化。
        DrawSpriteField(batch, y + 12f);
    }

    /// <summary>把剩余区域铺满精灵（网格 + 轻微起伏动画），数量由按钮切换。</summary>
    private void DrawSpriteField(SpriteBatch batch, float top)
    {
        _drawn = 0;
        if (_ball == null) return;

        int areaTop = (int)top;
        int areaHeight = Device.Viewport.Height - areaTop - 20;
        int areaWidth = Device.Viewport.Width - 56;
        if (areaWidth < 80 || areaHeight < 80) return;   // 窗口太小就只显示文字

        int want = CountOptions[_countIndex];
        var area = new Rectangle(28, areaTop, areaWidth, areaHeight);

        // 格子边长：让整片区域刚好装得下 want 个（限制在 8..48 像素之间）
        int cell = (int)Math.Clamp(MathF.Sqrt((float)(area.Width * area.Height) / want), 8f, 48f);
        int cols = area.Width / cell;
        int rows = area.Height / cell;

        for (int r = 0; r < rows && _drawn < want; r++)
        {
            float wave = MathF.Sin(_time * 1.6f + r * 0.28f) * cell * 0.25f;

            for (int c = 0; c < cols && _drawn < want; c++)
            {
                var dest = new Rectangle(area.X + c * cell, area.Y + r * cell + (int)wave, cell - 1, cell - 1);
                batch.Draw(_ball, dest, TestSpriteTexture.Hsv((_drawn * 5 + _time * 30f) % 360f, 0.55f, 0.95f));
                _drawn++;
            }
        }
    }

    // ---------- 资源 ----------

    private T Own<T>(T resource) where T : IDisposable
    {
        _owned.Add(resource);
        return resource;
    }

    public override void Dispose()
    {
        foreach (IDisposable resource in _owned) resource.Dispose();
        _owned.Clear();
        _ball = null;
        base.Dispose();
    }
}
