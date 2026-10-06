using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20
{

    /// <summary>
    /// 例子2：WebGL 2.0 渲染专项测试宿主。
    /// <para>
    /// 固定使用 WebGL 2.0 后端（preferWebGpu: false，设备由 Game 在 Run 时同步创建），
    /// 按 <c>1 / 2 / 3</c> 切换测试页；测试页放在 Tests/ 下，一个文件一页。
    /// </para>
    /// </summary>
    public sealed class WebGl20TestGame : Game
    {
        /// <summary>WebGL2 上下文是否开 MSAA（只能在创建上下文时决定）。</summary>
        private const bool Antialias = true;

        private readonly GraphicsDeviceManager _graphics;

        public WebGl20TestGame() : base("#game", Antialias, preferWebGpu: false)
        {
            ClearColor = new Color(10, 12, 20);
            IsFixedTimeStep = false;

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24,
                PreferMultiSampling = Antialias,
                GraphicsProfile = GraphicsProfile.Reach,
            };
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
            Input_Mouse.Activate();   // 总纲页与各页的「← 总纲」都要用鼠标点击，必须激活
            KSceneMgr.Init(this);
            // 测试界面用程序化生成的系统字体，不依赖任何字体资源。
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 20f);

            KSceneMgr.SetMainScene(new Tests.MainScene());
            return Task.CompletedTask;
        }

        protected override void Update(GameTime gameTime)
        {
            KInputMgr.Update(gameTime);
            HandleGlobalKeys();
            KSceneMgr.Update(gameTime);
        }

        /// <summary>
        /// 全局按键：在任何测试页都能切换，不必先退回总纲。
        /// 数字 1..4 直达对应测试页，Esc 回总纲（各页左上角的返回逻辑同样有效）。
        /// </summary>
        private void HandleGlobalKeys()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Escape))
            {
                KSceneMgr.SetMainScene(new Tests.MainScene());
                return;
            }

            Keys[] digits = [Keys.Digit1, Keys.Digit2, Keys.Digit3, Keys.Digit4, Keys.Digit5];
            for (int i = 0; i < Tests.TestRegistry.Entries.Count && i < digits.Length; i++)
            {
                if (Input_KeyBoard.GetKeyDown(digits[i]))
                {
                    KSceneMgr.SetMainScene(Tests.TestRegistry.Entries[i].Factory());
                    return;
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            KSceneMgr.Draw(gameTime);
        }
    }

}
