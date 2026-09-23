using Client.MirGraphics;
using Client.MirSounds;
using Client;
using KFramework.MonoGame;

namespace WebGame.Mir2.MonoGame.Client
{
    /// <summary>
    /// KFramework.MonoGame 宿主：取代原 WinForms.Forms/CMain 窗体与旧 main.js 的 rAF 循环。
    /// 构造里会把 JSBind_GameHost.Current 指向本实例，并由 KFramework.TSEngine 的 jsengine/main.js
    /// 经 requestAnimationFrame 每帧回调 JSBind_GameHost.Frame → Game.TickFrame。
    /// 每帧：Update 跑输入/声音延迟；Draw 跑 CMain.Loop（网络收发包 + 场景逻辑 + DXManager 渲染 + 纹理回收）。
    /// 采用非固定步长（IsFixedTimeStep=false），保证每帧都渲染（Mir2 自绘，避免跳帧）。
    /// </summary>
    public sealed class MirGame : Game
    {
        public MirGame() : base("#game", "hot_update_res")
        {
            IsFixedTimeStep = false;
        }

        protected override void Initialize()
        {
            DXManager.Initialize(GraphicsDevice, new SpriteBatch(GraphicsDevice));
            Window.SizeChanged += () => CMain.OnWindowSizeChanged(Window.Width, Window.Height);
        }

        protected override async Task LoadContentAsync()
        {
            await CMain.Init().ConfigureAwait(false);
        }

        protected override void Update(GameTime gameTime)
        {
            CMain.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            CMain.Draw();
        }
    }
}
