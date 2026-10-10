using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D
{

    /// <summary>
    /// Canvas2D 渲染后端测试宿主（对应 WebGL 工程的 <c>WebGl20TestGame</c>）。
    /// <para>
    /// 固定使用 <see cref="GraphicsBackendKind.Canvas2D"/>：不碰 WebGL / WebGPU，直接用浏览器 Canvas2D
    /// 回放精灵批次。按数字键切换测试页，Esc 回总纲。
    /// </para>
    /// <para>
    /// 页面的取舍：只收录 Canvas2D 真正能做的（精灵合批 / 文字与字形图集 / 纹理上传与采样 / 混合 / 读像素）。
    /// 自定义着色器、GPU 实例化、URP、渲染目标与 MSAA 在本后端会抛 <see cref="NotSupportedException"/>，故不设页面。
    /// </para>
    /// </summary>
    public sealed class Canvas2DTestGame : Game
    {
        public Canvas2DTestGame() : base(GraphicsBackendKind.Canvas2D, "#game", antialias: false)
        {
            ClearColor = new Color(10, 12, 20);
            IsFixedTimeStep = false;
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

        /// <summary>全局按键：数字 1..9 直达对应测试页，Esc 回总纲。</summary>
        private void HandleGlobalKeys()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Escape))
            {
                KSceneMgr.SetMainScene(new Tests.MainScene());
                return;
            }

            Keys[] digits = [Keys.Digit1, Keys.Digit2, Keys.Digit3, Keys.Digit4, Keys.Digit5, Keys.Digit6, Keys.Digit7, Keys.Digit8, Keys.Digit9];
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
