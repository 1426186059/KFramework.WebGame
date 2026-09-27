namespace KFramework.Example3;

/// <summary>
/// 超级马里奥（移植自 FCGame_MonoGame）的 Web 宿主：只负责初始化引擎
/// （输入 / 场景管理器 / 默认字体 / 内容），真正的游戏逻辑都在 MainScene 里。
/// </summary>
public sealed class MarioGame : Game
{
    public MarioGame() : base("#game", "hot_update_res") { }

    protected override async Task LoadContentAsync()
    {
        // MonoGameExtend 的输入总调度（键盘/鼠标/触摸/指针事件分发）
        KInputMgr.Init();
        // 场景管理器：创建共享 SpriteBatch 并接管 Update/Draw 的遍历
        KSceneMgr.Init(this);
        // 节点树 UI 文本需要默认字体（程序化生成，避免依赖 .spritefont 内容文件）
        KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 18f);

        await Content.LoadAsync().ConfigureAwait(false);

        var scene = new MainScene();
        KSceneMgr.SetMainScene(scene);
    }

    protected override void Update(GameTime gameTime)
    {
        // 本例子选择 Input 门面模式：引擎不再代劳，由游戏自行每步取回键盘/鼠标/触摸/IME 事件
        Input.Update();
        KInputMgr.Update(gameTime);
        KSceneMgr.Update(gameTime);
        // 固定步长下每步结束清空按下/抬起边沿，确保一次按键 / 一次点击只触发一次
        Input.LateUpdate();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        KSceneMgr.Draw(gameTime);
    }
}
