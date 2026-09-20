using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.GraphicsManagerTest;

/// <summary>
/// <see cref="GraphicsDeviceManager"/> 用法演示（照 MonoGame 的标准套路），全部操作都是上面的按钮。
/// <para>· 创建：<c>graphics = new GraphicsDeviceManager(this);</c>（见 <see cref="Example1Game"/>）；</para>
/// <para>· 设置：Preferred* / PreferMultiSampling / 呈现间隔（限帧）；</para>
/// <para>· 没有「垂直同步」开关：浏览器强制 VSync（rAF 就是刷新率），WebGL 无此 API。</para>
/// <para>· 生效：改完必须 <see cref="GraphicsDeviceManager.ApplyChanges"/>（每个按钮里都调了）；</para>
/// <para>· 事件：<see cref="GraphicsDeviceManager.DeviceCreated"/>、<see cref="GraphicsDeviceManager.PreparingDeviceSettings"/>。</para>
/// </summary>
/// <remarks>
/// <para>下方铺了几千个精灵当「效果图」：改分辨率时画面范围、像素密度与 FPS 会立刻跟着变，
/// 比看数字直观得多。精灵数用按钮切（1000 / 3000 / 8000），纹理由代码生成，不依赖任何图片资源。</para>
/// <para>数字键 1..6 是同样操作的快捷方式。</para>
/// </remarks>
public sealed class GraphicsManagerTestScene : TestSceneBase
{
    /// <summary>可选精灵数量档位。</summary>
    private static readonly int[] CountOptions = [1000, 3000, 8000];

    private readonly List<IDisposable> _owned = [];

    /// <summary>呈现间隔档位（照 MonoGame：改 PresentationParameters.PresentationInterval 的标准入口是 PreparingDeviceSettings 事件）。</summary>
    private static readonly PresentInterval[] IntervalOptions = [PresentInterval.One, PresentInterval.Two, PresentInterval.Immediate];

    private Texture2D? _ball;
    private int _countIndex = 1;      // 默认 3000
    private int _intervalIndex;       // 默认 One
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

        if (Input_KeyBoard.GetKeyDown(Keys.D1)) CycleInterval();
        // 多重采样(antialias) 运行时不可切换（WebGL2 限制），PreferMultiSampling 设了会直接抛异常；
        // 这里只提示限制，不调用 setter。
        if (Input_KeyBoard.GetKeyDown(Keys.D2)) PrintTool.Log("[GraphicsManagerTest] 按 D2：多重采样(antialias) 只能在 Game 构造时指定，运行时切换会抛 NotSupportedException；改 Example1Game.Antialias 后重启对比。");
        if (Input_KeyBoard.GetKeyDown(Keys.D3)) ApplyPreset(1280, 720);
        if (Input_KeyBoard.GetKeyDown(Keys.D4)) ApplyPreset(800, 480);
    }

    // ---------- 布局与按钮 ----------

    private void Layout()
    {
        BeginButtons(72);

        GraphicsDeviceManager g = Manager;

        // ① 开关：点一下切换状态并立即 ApplyChanges
        // 多重采样：WebGL2 的 antialias 是「创建上下文」时定死的，运行时改不了（见 GraphicsDeviceManager.PreferMultiSampling 的异常）。
        // 这里只展示「真实生效状态」（来自 GraphicsDevice.Antialias，由 Example1Game.Antialias 在启动时决定），
        // 按钮点击只打日志提示限制，不调用 setter —— 否则运行时设 PreferMultiSampling 会直接抛 NotSupportedException。
        bool msaaOn = Device.Antialias;
        AddUiButton(msaaOn ? "多重采样(antialias)：开·仅启动时" : "多重采样(antialias)：关·仅启动时",
                    static () => PrintTool.Log("[GraphicsManagerTest] 多重采样(antialias) 只能在 Game/GraphicsDevice 构造时指定，运行时切换无效；改 Example1Game.Antialias 后重启对比。"),
                    msaaOn);

        // ② 分辨率：改 PreferredBackBuffer* 后 ApplyChanges
        AddUiButton("1280×720", () => ApplyPreset(1280, 720));
        AddUiButton("800×480", () => ApplyPreset(800, 480));
        // 高亮 = 当前正处于「未指定尺寸、跟随页面布局」的状态
        AddUiButton("填满整个 HTML 页面(不钉死)", ReleaseToViewport, !g.HasPreferredBackBufferSize);
        AddUiButton($"呈现间隔 {IntervalOptions[_intervalIndex]}", CycleInterval, IntervalOptions[_intervalIndex] == PresentInterval.Two);
        AddUiButton($"精灵 {CountOptions[_countIndex]:N0}", CycleCount);
    }

    private void CycleCount() => _countIndex = (_countIndex + 1) % CountOptions.Length;

    /// <summary>
    /// 切呈现间隔：Two 会真的限到半刷新率（主循环每 2 个 rAF 才画一帧），看 FPS 就知道。
    /// 必须走管理器属性的 setter —— 它才会把「待应用」标记置上；
    /// 只改本页自己的字段再调 ApplyChanges 是没用的（ApplyChanges 会直接返回）。
    /// </summary>
    private void CycleInterval()
    {
        _intervalIndex = (_intervalIndex + 1) % IntervalOptions.Length;
        Set(manager => manager.PreferredPresentInterval = IntervalOptions[_intervalIndex]);
    }

    /// <summary>改一个属性然后 ApplyChanges（这就是 MonoGame 的标准顺序）。</summary>
    private void Set(Action<GraphicsDeviceManager> change)
    {
        change(Manager);
        Manager.ApplyChanges();
    }

    private void ApplyPreset(int width, int height)
    {
        Manager.PreferredBackBufferWidth = width;
        Manager.PreferredBackBufferHeight = height;
        Manager.ApplyChanges();
    }

    /// <summary>
    /// 释放「期望的后备缓冲尺寸」：必须走 <see cref="GraphicsDeviceManager.ReleasePreferredBackBufferSize"/>，
    /// 直接把 Preferred* 设成当前尺寸只会把画布钉成另一个固定尺寸（之前这个按钮就是这么写的，所以点了没反应）。
    /// </summary>
    private void ReleaseToViewport() => Manager.ReleasePreferredBackBufferSize();

    // ---------- 绘制 ----------

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        DrawButtons(batch);

        float x = origin.X;
        float y = Math.Max(origin.Y, ContentTop);

        GraphicsDeviceManager g = Manager;
        PresentationParameters pp = Device.PresentationParameters;

        y += DrawSection(batch, "① 管理器当前设置（改完要 ApplyChanges）", new Vector2(x, y));
        y += DrawLine(batch, Font,
                      g.HasPreferredBackBufferSize
                          ? $"PreferredBackBuffer：{g.PreferredBackBufferWidth} x {g.PreferredBackBufferHeight}（已指定 → 画布被钉成这个尺寸）"
                          : "PreferredBackBuffer：未指定（画布跟随页面布局，随浏览器缩放）",
                      new Vector2(x, y), g.HasPreferredBackBufferSize ? Color.LightGray : new Color(150, 220, 255));
        y += DrawLine(batch, Font,
                      $"呈现间隔：{pp.PresentationInterval} → 主循环每 {g.FramesPerPresent} 个 rAF 画一帧（浏览器强制 VSync，无开关）",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font,
                      $"多重采样：{g.PreferMultiSampling} → 上下文 antialias：{Device.Antialias}（只在启动时生效，运行时改不了）",
                      new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "② 已应用到设备与画布的值", new Vector2(x, y));
        y += DrawLine(batch, Font,
                      $"画布 CSS：{(int)Device.CssSize.X} x {(int)Device.CssSize.Y}（DPR {Device.DevicePixelRatio:0.##}）→ 后备缓冲 {pp.BackBufferWidth} x {pp.BackBufferHeight}",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font,
                      $"FPS：{Fps:0.}（真实帧率） / 帧间隔 {KTime.realDeltaTime * 1000f:0.#}ms / 精灵 {_drawn:N0} / DrawCall {Device.Metrics.DrawCount}",
                      new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font,
                      $"多重采样(antialias)：{Device.Antialias} —— 仅 Game 构造时由 Example1Game.Antialias 决定，运行时切换无效（WebGL2 限制）",
                      new Vector2(x, y), new Color(180, 180, 180));

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
