using KFramework.MonoGame;
using KFramework.MonoGameExtend;

using MirGame.Tests;

namespace MirGame;

/// <summary>
/// 例子1：引擎功能测试宿主。
/// 只负责初始化引擎（输入 / 场景管理器 / 默认字体）与加载内容包，
/// 具体的测试项在 Tests/ 下「一个模块一个文件夹」地添加，由 <see cref="TestMainScene"/> 入口进入。
/// </summary>
public sealed class Example1Game : Game
{
    /// <summary>
    /// WebGL2 上下文是否开 MSAA（antialias）。
    /// 它只能在创建上下文时决定（Game / GraphicsDevice 构造），运行时改不了 ——
    /// 想对比 MSAA 开 / 关，改这里的值重新运行即可。
    /// </summary>
    private const bool Antialias = true;

    /// <summary>
    /// 图形设备管理器（MonoGame 的标准用法：每个 Game 在构造函数里创建一个并持有它）。
    /// 完整演示见 <see cref="MirGame.Tests.GraphicsManagerTest.GraphicsManagerTestScene"/>。
    /// </summary>
    public GraphicsDeviceManager Graphics { get; }

    public Example1Game() : base("#game", "hot_update_res", Antialias)
    {
        ClearColor = new Color(10, 12, 20);

        Graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferFormat = SurfaceFormat.Color,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            PreferMultiSampling = Antialias,
            GraphicsProfile = GraphicsProfile.Reach,
        };

        // 设备就绪回调：MonoGame 里通常在这里做与设备绑定的初始化（例如重建 RenderTarget、恢复纹理）。
        Graphics.DeviceCreated += static (sender, _) =>
        {
            var manager = (GraphicsDeviceManager)sender!;
            Console.WriteLine($"[Example1] DeviceCreated：{manager.GraphicsDevice.Viewport.Width} x {manager.GraphicsDevice.Viewport.Height}");
        };

        // 创建设备前的最后一次改参数机会（例如按设备能力调整 PresentInterval / 后备缓冲格式）。
        Graphics.PreparingDeviceSettings += static (_, args) =>
        {
            args.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.DiscardContents;
        };
    }

    protected override void Initialize()
    {
        // 把上面设置的偏好应用到设备（Game 在进入 Initialize 之前已让管理器接管 GraphicsDevice）。
        Graphics.ApplyChanges();
        base.Initialize();
    }

    protected override async Task LoadContentAsync()
    {
        // MonoGameExtend 的输入总调度（键盘 / 鼠标 / 触摸 / 指针事件分发）
        KInputMgr.Init();
        // 场景管理器：创建共享 SpriteBatch 并接管 Update / Draw 的遍历
        KSceneMgr.Init(this);
        // 测试界面用程序化生成的系统字体，不依赖任何字体资源
        KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 20f);

        // 加载全部 AssetBundle（含 Content/raw/Bundles/fonts 下的 ttf 字体）
        await Content.LoadAsync().ConfigureAwait(false);

        KSceneMgr.SetMainScene(new TestMainScene());
    }

    protected override void Update(GameTime gameTime)
    {
        KInputMgr.Update(gameTime);
        KSceneMgr.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        KSceneMgr.Draw(gameTime);
    }
}
