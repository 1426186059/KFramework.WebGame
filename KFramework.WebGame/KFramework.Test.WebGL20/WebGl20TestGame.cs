using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20
{

    /// <summary>
    /// 例子2：WebGL 2.0 渲染专项测试宿主。
    /// <para>
    /// 固定使用 WebGL 2.0 后端（<see cref="GraphicsDevice"/> 同步构造），
    /// 按 <c>1 / 2 / 3</c> 切换测试页；测试页放在 Tests/ 下，一个文件一页。
    /// </para>
    /// </summary>
    public sealed class WebGl20TestGame : Game
    {
        /// <summary>WebGL2 上下文是否开 MSAA（只能在创建上下文时决定）。</summary>
        private const bool Antialias = true;

        private readonly GraphicsDeviceManager _graphics;
        private readonly Func<KSceneBase>[] _scenes;
        private int _current;

        public WebGl20TestGame() : base("#game", Antialias)
        {
            ClearColor = new Color(10, 12, 20);

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24,
                PreferMultiSampling = Antialias,
                GraphicsProfile = GraphicsProfile.Reach,
            };

            _scenes =
            [
                static () => new Tests.SpriteBatchScene(),
                static () => new Tests.RenderTargetScene(),
                static () => new Tests.MsaaScene(),
            ];
        }

        protected override void Initialize()
        {
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override Task LoadContentAsync()
        {
            KInputMgr.Init();
            Input_KeyBoard.Activate(bUseCanvasListener: true);
            KSceneMgr.Init(this);
            // 测试界面用程序化生成的系统字体，不依赖任何字体资源。
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 20f);

            KSceneMgr.SetMainScene(_scenes[0]());
            return Task.CompletedTask;
        }

        protected override void Update(GameTime gameTime)
        {
            KInputMgr.Update(gameTime);
            SwitchScene();
            KSceneMgr.Update(gameTime);
        }

        private void SwitchScene()
        {
            int target = -1;
            // 键名照浏览器 KeyboardEvent.code 命名（Digit1 / Digit2 / Digit3 …）。
            if (Input_KeyBoard.GetKeyDown(Keys.Digit1)) target = 0;
            else if (Input_KeyBoard.GetKeyDown(Keys.Digit2)) target = 1;
            else if (Input_KeyBoard.GetKeyDown(Keys.Digit3)) target = 2;

            if (target < 0 || target == _current) return;
            _current = target;
            KSceneMgr.SetMainScene(_scenes[target]());
        }

        protected override void Draw(GameTime gameTime)
        {
            KSceneMgr.Draw(gameTime);
        }
    }

}
