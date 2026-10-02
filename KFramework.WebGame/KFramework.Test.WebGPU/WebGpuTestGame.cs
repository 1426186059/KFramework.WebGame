using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU
{

    /// <summary>
    /// 例子3：WebGPU 渲染专项测试宿主。
    /// <para>
    /// 设备由外部异步创建后传入（<see cref="Game(GraphicsDevice)"/>），按 <c>1 / 2 / 3</c> 切换测试页。
    /// 若浏览器不支持 WebGPU，<see cref="GraphicsDevice.CreateAsync"/> 会回落到 WebGL 2.0，
    /// 此时各页面仍可运行，只是第 1 页显示的后端名会变成 "WebGL2"。
    /// </para>
    /// </summary>
    public sealed class WebGpuTestGame : Game
    {
        private readonly Func<KSceneBase>[] _scenes;
        private int _current;

        public WebGpuTestGame(GraphicsDevice device) : base(device)
        {
            ClearColor = new Color(10, 12, 20);

            _scenes =
            [
                static () => new Tests.BackendInfoScene(),
                static () => new Tests.SpriteBlendScene(),
                static () => new Tests.UnsupportedScene(),
            ];
        }

        protected override Task LoadContentAsync()
        {
            KInputMgr.Init();
            Input_KeyBoard.Activate(bUseCanvasListener: true);
            KSceneMgr.Init(this);
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
