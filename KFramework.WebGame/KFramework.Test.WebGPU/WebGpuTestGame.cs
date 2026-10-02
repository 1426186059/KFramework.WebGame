using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU
{

    /// <summary>
    /// 例子3：WebGPU 渲染专项测试宿主。
    /// <para>
    /// 设备由外部异步创建后传入（<see cref="Game(GraphicsDevice)"/>）。
    /// 启动进入 <see cref="Tests.MainScene"/> 总纲页，可点条目或按数字键进入各测试页。
    /// 若浏览器不支持 WebGPU，<see cref="GraphicsDevice.CreateAsync"/> 会回落到 WebGL 2.0，
    /// 此时各页面仍可运行，只是总纲页显示的后端名会变成 "WebGL2"。
    /// </para>
    /// </summary>
    public sealed class WebGpuTestGame : Game
    {
        /// <summary>
        /// 图形设备管理器。它不只是"管参数"：<see cref="GraphicsDeviceManager.ApplyChanges"/>
        /// 会按呈现参数摆放画布（尺寸 / 全屏 / 居中）。少了它画布不会被布局，
        /// 实测会停在 1x1 —— 画面什么都看不到。
        /// </summary>
        private readonly GraphicsDeviceManager _graphics;

        public WebGpuTestGame(GraphicsDevice device) : base(device)
        {
            ClearColor = new Color(10, 12, 20);

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24,
                GraphicsProfile = GraphicsProfile.Reach,
            };
        }

        protected override void Initialize()
        {
            // 应用呈现参数并摆放画布（与 WebGL 例子一致）。
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override Task LoadContentAsync()
        {
            KInputMgr.Init();
            // 绑到 window 全局捕获（而非画布）：画布监听要求画布处于焦点，
            // 一旦焦点被页面或开发者工具抢走，键盘就完全收不到事件。
            Input_KeyBoard.Activate(bUseCanvasListener: false);
            // 总纲页与各页的「← 总纲」都要用鼠标点击，必须激活鼠标。
            Input_Mouse.Activate();
            KSceneMgr.Init(this);
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
        /// 数字 1..3 直达对应测试页，Esc 回总纲（各页左上角的「← 总纲」按钮同样有效）。
        /// </summary>
        private void HandleGlobalKeys()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Escape))
            {
                KSceneMgr.SetMainScene(new Tests.MainScene());
                return;
            }

            Keys[] digits = [Keys.Digit1, Keys.Digit2, Keys.Digit3];
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
