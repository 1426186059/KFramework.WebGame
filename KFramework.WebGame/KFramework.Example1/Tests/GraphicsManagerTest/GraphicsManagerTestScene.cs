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
/// 数字键 1..4 是同样操作的快捷方式。全屏按钮必须在用户手势里触发，点按钮正好满足浏览器的要求。
/// </remarks>
public sealed class GraphicsManagerTestScene : TestSceneBase
{
    public override string Title => "GraphicsDeviceManager：点按钮改参数 + ApplyChanges";

    private GraphicsDeviceManager Manager => ((Example1Game)KSceneMgr.Game).Graphics;

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

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
    }

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
        y += DrawLine(batch, Font, $"GraphicsProfile：{g.GraphicsProfile} / PreferredDepthStencilFormat：{g.PreferredDepthStencilFormat}",
                      new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font,
                      $"垂直同步：{g.SynchronizeWithVerticalRetrace} / 多重采样：{g.PreferMultiSampling} / 全屏方式：{(g.HardwareModeSwitch ? "原生" : "铺满")} / IsFullScreen：{g.IsFullScreen}",
                      new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "② 已应用到设备与画布的值", new Vector2(x, y));
        y += DrawLine(batch, Font, $"画布 CSS：{(int)Device.CssSize.X} x {(int)Device.CssSize.Y}（DPR {Device.DevicePixelRatio:0.##}）",
                      new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"后备缓冲：{pp.BackBufferWidth} x {pp.BackBufferHeight}（{pp.BackBufferFormat}）",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font,
                      $"DepthStencil：{pp.DepthStencilFormat} / MultiSampleCount：{pp.MultiSampleCount} / PresentInterval：{pp.PresentationInterval} / IsFullScreen：{pp.IsFullScreen}",
                      new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "③ 现在真生效 / 仍然只记录", new Vector2(x, y));
        y += DrawLine(batch, Font, "生效：PreferredBackBuffer* 按 ÷DPR 改画布大小、IsFullScreen / ToggleFullScreen",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font, "生效：全屏方式=原生时走 canvas.requestFullscreen（需点击 / 按键手势）",
                      new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font, "只记录：BackBufferFormat / DepthStencilFormat / 多重采样 / HalfPixelOffset",
                      new Vector2(x, y), new Color(255, 190, 140));
        y += DrawLine(batch, Font, "只记录：不重建设备、不触发 DeviceReset（浏览器只有一个 WebGL2 上下文）",
                      new Vector2(x, y), new Color(255, 190, 140));
    }
}
