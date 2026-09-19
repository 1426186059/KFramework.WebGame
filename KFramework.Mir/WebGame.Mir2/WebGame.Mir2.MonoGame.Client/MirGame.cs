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
            // 把 KFramework.MonoGame 的 GraphicsDevice / SpriteBatch 交给 DXManager（渲染后端）。
            DXManager.Initialize(GraphicsDevice, new SpriteBatch(GraphicsDevice));

            // 把"取画布（后备缓冲）物理像素尺寸"的能力注入 CMain（供需要时查询；
            // 当前全屏由 MirScene.DrawControl 把固定逻辑分辨率的场景纹理拉伸到画布实现，不再改 Settings）。
            CMain.GetCanvasSize = () => (Window.Width, Window.Height);

            // 初始刷新一次；窗口缩放时同样刷新当前场景（地板/光照离屏纹理重建 + 场景重烘焙）。
            // 实际"铺满窗口"由 MirScene 将场景纹理拉伸到 Viewport 完成。
            CMain.SetResolution(Window.Width, Window.Height);
            Window.SizeChanged += () => CMain.SetResolution(Window.Width, Window.Height);
        }

        protected override async Task LoadContentAsync()
        {
            // 引擎引导：设资源基址、加载设置、建 DXManager、登记库、建登录场景、声音、输入。
            // 放在异步加载阶段：声音索引表（SoundList）需经资源服务器异步读取，避免同步网络冻结主线程。
            await CMain.Init().ConfigureAwait(false);
        }

        protected override void Update(GameTime gameTime)
        {
            CMain.MPoint = Input_Mouse.Position;
            CMain.CMain_MouseMove(null, new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, 0));
            SoundManager.ProcessDelayedSounds();
        }

        protected override void Draw(GameTime gameTime)
        {
            // CMain.Loop 内部已做清屏 + 绘制 + 提交；Game.TickFrame 在调用 Draw 前也会清一次屏，
            // 但 RenderFrame 会再次清成黑色并重绘，最终画面以 DXManager 输出的为准。
            CMain.Loop();
        }
    }
}
