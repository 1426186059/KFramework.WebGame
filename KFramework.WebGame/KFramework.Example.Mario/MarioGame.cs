namespace KFramework.Example.Mario
{

    /// <summary>
    /// 超级马里奥（移植自 FCGame_MonoGame）的 Web 宿主：组件式接入 MonoGameExtend —— 只挂一个
    /// <see cref="KEngineComponent"/>，输入、KTime、场景树、KTween / KTimer 都交给它驱动，
    /// 本类只负责默认字体与内容加载，真正的游戏逻辑都在 MainScene 里。
    /// </summary>
    public sealed class MarioGame : Game
    {
        public MarioGame() : base(GraphicsBackendKind.WebGL20)
        {
            // 原来的 Draw 里额外 Clear(CornflowerBlue)；引擎每帧已按 ClearColor 清屏，等价改成这里设置。
            ClearColor = Color.CornflowerBlue;

            Components.Add(new KEngineComponent(this));
        }

        protected override async Task LoadContentAsync()
        {
            // 节点树 UI 文本需要默认字体（程序化生成，避免依赖 .spritefont 内容文件）
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 18f);

            await ContentManager.Default.LoadAsync().ConfigureAwait(false);

            KSceneMgr.SetMainScene(new MainScene());
        }
    }

}
