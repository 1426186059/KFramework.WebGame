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
    public Example1Game() : base("#game", "hot_update_res")
    {
        ClearColor = new Color(10, 12, 20);
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
