using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战的宿主：只负责初始化引擎（输入 / 场景管理器 / 字体 / 内容），
/// 真正的游戏逻辑与界面都在 <see cref="GameScene"/> 里，由 MonoGameExtend 的节点树驱动。
/// 对应 PixiJS 版的 CreationEngine + GameScene。
/// </summary>
public sealed class TankGame : Game
{
    public TankGame() : base("#game", "content") { }

    protected override async Task LoadContentAsync()
    {
        // MonoGameExtend 的输入总调度（键盘/鼠标/触摸/指针事件分发）
        KInputMgr.Init();
        // 场景管理器：创建共享 SpriteBatch 并接管 Update/Draw 的遍历
        KSceneMgr.Init(this);
        // 节点树 UI 文本需要默认字体
        KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 40f);

        await Content.LoadAsync().ConfigureAwait(false);

        var scene = new GameScene();
        KSceneMgr.SetMainScene(scene);
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
