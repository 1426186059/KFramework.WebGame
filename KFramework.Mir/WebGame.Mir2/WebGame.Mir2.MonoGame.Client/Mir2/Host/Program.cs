using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using KFramework.MonoGame;
using MirEngine;

namespace Client
{
    // 浏览器入口：替代原 WinForms Program + CMain 窗体。
    // main.js 在 WASM 启动后调用 Init()，每帧 rAF 调用 Frame()。
    public static partial class Program
    {
        public static CMain Form = new CMain();
        public static bool Launch, Restart;
        private static bool _bootstrapped;

        [JSExport]
        public static async Task Init()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;

            // 资源基址：指向 Crystal 客户端资源目录对应的本地 HTTP 服务
            // （D:\OpenSource\Crystal\Build\Client\Debug），避免把 GB 级资源复制进 wwwroot。
            // 同时创建一个指向它的 ContentManager，统一经 KFramework.MonoGame 异步加载远程资源。
            MirEngine.BrowserResource.Configure("http://127.0.0.1:5080/");

            Settings.Load();
            DXManager.Create();

            // 触发 Libraries 静态构造（登记各库，不加载）
            _ = Libraries.Loaded;

            // 资源（贴图库）是异步加载的，而场景离屏纹理只在首次绘制时烘焙一次。
            // 若烘焙时库尚未就绪，场景会卡在默认背景色（整屏粉红）。因此每当有库加载完成，
            // 就令当前活动场景递归失效、下一帧重新烘焙，纹理就绪后即可正常显示。
            Libraries.LibraryLoaded += () =>
            {
                MirScene.ActiveScene?.Refresh();

                // 地表（Floor）是单独烘焙到离屏 FloorTexture 的：CreateTexture 里只有
                // "if (!FloorValid) DrawFloor();"，FloorValid 一旦为 true 就不再重烘焙。
                // 而地图片库（尤其是几百 MB 的 Tiles）是异步加载的，首屏烘焙时它往往还没下载完，
                // 那一版地板全是马赛克/空洞 —— 若不在这里让它失效，地板会永久停在那一版。
                if (GameScene.Scene != null && GameScene.Scene.MapControl != null)
                    GameScene.Scene.MapControl.FloorValid = false;
            };

            // 启动异步资源加载管线（fire-and-forget，不阻塞 Init）：
            // 首屏库并发优先加载，其余库后台限流加载；未就绪的库在绘制时自动跳过。
            _ = Libraries.LoadAsync();

            MirScene.ActiveScene = new LoginScene();
            await SoundManager.CreateAsync().ConfigureAwait(false);
            ConfigureInput();
        }

        [JSExport]
        public static void Frame(double timeMs)
        {
            CMain.Loop();
        }

        [JSExport]
        public static void Step() => CMain.Loop();

        private static void ConfigureInput()
        {
            // 输入改走 KFramework.MonoGame 的 Input 层（KInputMgr 已把指针/键盘事件桥到 GL 画布）。
            // 鼠标移动不提供事件，改为每帧在 MirGame.Update 里轮询 Input_Mouse.Position 并转发 CMain_MouseMove。
            // 字符输入（KeyPress）浏览器端由 MirTextBox 的 DOM 输入层承担，这里不桥接。
            Input_Mouse.ButtonDown += OnMouseDown;
            Input_Mouse.ButtonUp += OnMouseUp;
            Input_Mouse.ScrollWheel += OnScrollWheel;
            Input_KeyBoard.KeyDown += OnKeyDown;
            Input_KeyBoard.KeyUp += OnKeyUp;
            // 首次任意输入即解锁 WebAudio（浏览器自动播放策略要求用户手势）。
            Input_KeyBoard.KeyDown += _ => AudioMaster.Unlock();
            Input_Mouse.ButtonDown += (_, _) => AudioMaster.Unlock();
        }

        private static void OnKeyDown(KFramework.MonoGame.Keys k) => CMain.CMain_KeyDown(null, ToKeyEventArgs(k));
        private static void OnKeyUp(KFramework.MonoGame.Keys k) => CMain.CMain_KeyUp(null, ToKeyEventArgs(k));

        private static void OnMouseDown(KFramework.MonoGame.MouseButton b, Vector2 p)
        {
            CMain.MPoint = new MirEngine.Point((int)p.X, (int)p.Y);
            MirScene.ActiveScene?.OnMouseDown(ToMouseEventArgs(b, p));
        }
        private static void OnMouseUp(KFramework.MonoGame.MouseButton b, Vector2 p)
        {
            CMain.MPoint = new MirEngine.Point((int)p.X, (int)p.Y);
            var e = ToMouseEventArgs(b, p);
            // 复刻 WinForms 原版 CMain_MouseUp：松开按键必须清掉 MapControl.MapButtons，否则后续点击会错位。
            MapControl.MapButtons &= ~e.Button;
            if (e.Button != MouseButtons.Right || !Settings.NewMove)
                GameScene.CanRun = false;
            MirScene.ActiveScene?.OnMouseUp(e);
            MirScene.ActiveScene?.OnMouseClick(e);
        }
        private static void OnScrollWheel(int delta)
        {
            MirScene.ActiveScene?.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, delta));
        }

        private static KeyEventArgs ToKeyEventArgs(KFramework.MonoGame.Keys k)
        {
            MirEngine.Keys keyData = (MirEngine.Keys)(int)k;
            if (Input_KeyBoard.Shift) keyData |= MirEngine.Keys.Shift;
            if (Input_KeyBoard.Ctrl) keyData |= MirEngine.Keys.Control;
            if (Input_KeyBoard.Alt) keyData |= MirEngine.Keys.Alt;
            return new KeyEventArgs(keyData);
        }

        private static MouseEventArgs ToMouseEventArgs(KFramework.MonoGame.MouseButton b, Vector2 p)
        {
            MouseButtons mb = b == KFramework.MonoGame.MouseButton.Right ? MouseButtons.Right
                            : b == KFramework.MonoGame.MouseButton.Middle ? MouseButtons.Middle
                            : MouseButtons.Left;
            return new MouseEventArgs(mb, 1, (int)p.X, (int)p.Y, 0);
        }
    }
}
