namespace KFramework.Example.Mario
{

    /// <summary>
    /// 超级马里奥（移植自 FCGame_MonoGame）的 Web 宿主：只负责初始化引擎
    /// （输入 / 场景管理器 / 默认字体 / 内容），真正的游戏逻辑都在 MainScene 里。
    /// </summary>
    public sealed class MarioGame : Game
    {
        /// <summary>
        /// 帧耗时 HUD 的开关。马里奥是测试工程，默认开着；
        /// 想看纯净画面（或对比 HUD 自身的开销）把它改成 false 重新运行即可。
        /// </summary>
        private const bool ShowPerfHud = true;

        /// <summary>HUD 专用的 SpriteBatch：独立于 <see cref="KSceneMgr"/> 那一个，互不干扰。</summary>
        private SpriteBatch _hud = null!;

        private PerfHud _perf = null!;

        public MarioGame() : base("#game") { }

        protected override async Task LoadContentAsync()
        {
            // MonoGameExtend 的输入总调度（键盘/鼠标/触摸/指针事件分发）
            KInputMgr.Init();
            // 激活本例需要的输入装置：引擎按各装置的 Active 开关决定是否真正采集；不需要的可不激活。
            // 键盘默认绑到画布（bUseCanvas: true，需画布聚焦才收键）；传 false 则绑到 window 全局捕获。
            Input_KeyBoard.Activate();
            Input_Mouse.Activate();
            Input_Touch.Activate();
            // 场景管理器：创建共享 SpriteBatch 并接管 Update/Draw 的遍历
            KSceneMgr.Init(this);
            // 节点树 UI 文本需要默认字体（程序化生成，避免依赖 .spritefont 内容文件）
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 18f);

            await ContentManager.Default.LoadAsync().ConfigureAwait(false);

            _hud = new SpriteBatch(GraphicsDevice);
            _perf = new PerfHud();

            var scene = new MainScene();
            KSceneMgr.SetMainScene(scene);
        }

        protected override void Update(GameTime gameTime)
        {
            // 输入由引擎在 TickFrame 内统一驱动（Input.Update / Input.LateUpdate）；本例只负责激活所需装置。
            _perf.BeginUpdate();
            KInputMgr.Update(gameTime);
            KSceneMgr.Update(gameTime);
            _perf.EndUpdate();
        }

        protected override void Draw(GameTime gameTime)
        {
            _perf.BeginDraw();
            GraphicsDevice.Clear(Color.CornflowerBlue);
            KSceneMgr.Draw(gameTime);

            // Metrics 每帧由 Clear 重置，故在 Draw 末尾读到的是本帧累计值
            _perf.EndDraw(GraphicsDevice.Metrics);
            _perf.Render(_hud, KDefaultRes.DefaultSpriteFont, ShowPerfHud);
        }
    }

}
