using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.GraphicsManagerTest;

/// <summary>
/// <see cref="GraphicsDeviceManager"/> 用法演示（照 MonoGame 的标准套路）。
/// <para>· 创建：<c>graphics = new GraphicsDeviceManager(this);</c>（在 Game 派生类的构造函数里，见 <see cref="Example1Game"/>）。</para>
/// <para>· 设置：Preferred* / IsFullScreen / SynchronizeWithVerticalRetrace / PreferMultiSampling / GraphicsProfile。</para>
/// <para>· 生效：改完这些属性后必须调用 <see cref="GraphicsDeviceManager.ApplyChanges"/>。</para>
/// <para>· 事件：<see cref="GraphicsDeviceManager.DeviceCreated"/>、<see cref="GraphicsDeviceManager.PreparingDeviceSettings"/>。</para>
/// </summary>
/// <remarks>
/// <para>操作：1 切垂直同步，2 切多重采样，3 切 IsFullScreen，4 调 ToggleFullScreen()；每次改完都会 ApplyChanges。</para>
/// <para>Web 差异：后备缓冲尺寸由画布（CSS 尺寸 × DPR）决定，PreferredBackBufferWidth/Height 只作为期望值记录，
/// 不改变画布大小；IsFullScreen / HardwareModeSwitch / PreferMultiSampling 也只写入 PresentationParameters。</para>
/// </remarks>
public sealed class GraphicsManagerTestScene : TestSceneBase
{
    public override string Title => "GraphicsDeviceManager：设备参数管理（1/2/3/4 切换 + ApplyChanges）";

    private GraphicsDeviceManager Manager => ((Example1Game)KSceneMgr.Game).Graphics;

    public override void Update()
    {
        base.Update();
        if (!ReferenceEquals(KSceneMgr.Main, this)) return;

        if (Input_KeyBoard.GetKeyDown(Keys.D1))
        {
            Manager.SynchronizeWithVerticalRetrace = !Manager.SynchronizeWithVerticalRetrace;
            Manager.ApplyChanges();
        }

        if (Input_KeyBoard.GetKeyDown(Keys.D2))
        {
            Manager.PreferMultiSampling = !Manager.PreferMultiSampling;
            Manager.ApplyChanges();
        }

        if (Input_KeyBoard.GetKeyDown(Keys.D3))
        {
            Manager.IsFullScreen = !Manager.IsFullScreen;
            Manager.ApplyChanges();
        }

        if (Input_KeyBoard.GetKeyDown(Keys.D4))
            Manager.ToggleFullScreen();
    }

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float x = origin.X;
        float y = origin.Y;

        GraphicsDeviceManager g = Manager;
        PresentationParameters pp = Device.PresentationParameters;

        y += DrawSection(batch, "① 管理器当前设置（改完要 ApplyChanges）", new Vector2(x, y));
        y += DrawLine(batch, Font, $"PreferredBackBuffer：{g.PreferredBackBufferWidth} x {g.PreferredBackBufferHeight}（{g.PreferredBackBufferFormat}）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"PreferredDepthStencilFormat：{g.PreferredDepthStencilFormat}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"GraphicsProfile：{g.GraphicsProfile}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"SynchronizeWithVerticalRetrace：{g.SynchronizeWithVerticalRetrace}（按 1 切换）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"PreferMultiSampling：{g.PreferMultiSampling}（按 2 切换）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"IsFullScreen：{g.IsFullScreen}（按 3 切换 / 按 4 走 ToggleFullScreen）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"HardwareModeSwitch：{g.HardwareModeSwitch}", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "② 已应用到 GraphicsDevice.PresentationParameters 的值", new Vector2(x, y));
        y += DrawLine(batch, Font, $"BackBuffer：{pp.BackBufferWidth} x {pp.BackBufferHeight}（{pp.BackBufferFormat}，跟随画布，每帧 SyncCanvasSize）", new Vector2(x, y), new Color(150, 220, 255));
        y += DrawLine(batch, Font, $"DepthStencilFormat：{pp.DepthStencilFormat} / MultiSampleCount：{pp.MultiSampleCount}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"PresentationInterval：{pp.PresentationInterval} / IsFullScreen：{pp.IsFullScreen}", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, $"DisplayOrientation：{pp.DisplayOrientation} / RenderTargetUsage：{pp.RenderTargetUsage}", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "③ 事件", new Vector2(x, y));
        y += DrawLine(batch, Font, "DeviceCreated：设备就绪后触发（宿主里打印了一行日志，见控制台）", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "PreparingDeviceSettings：应用参数前最后一次改写机会", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "④ Web 与桌面端的差异", new Vector2(x, y));
        y += DrawLine(batch, Font, "· 画布尺寸由 CSS × DPR 决定，PreferredBackBufferWidth/Height 只作为期望值记录", new Vector2(x, y), new Color(255, 190, 140));
        y += DrawLine(batch, Font, "· 浏览器只有一个 WebGL2 上下文：不重建设备，也不触发 DeviceReset", new Vector2(x, y), new Color(255, 190, 140));
        y += DrawLine(batch, Font, "· 全屏需要浏览器 requestFullscreen，IsFullScreen 目前只记录不生效", new Vector2(x, y), new Color(255, 190, 140));
    }
}
