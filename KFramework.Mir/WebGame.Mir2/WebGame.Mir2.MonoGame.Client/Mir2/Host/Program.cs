using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using MirEngine;

namespace Client
{
    // 浏览器入口：替代原 WinForms Program + CMain 窗体。
    // main.js 在 WASM 启动后调用 Init()，每帧 rAF 调用 Frame()。
    public static partial class Program
    {
        public static CMain Form = new CMain();
        public static bool Launch, Restart;

        [JSExport]
        public static void Init()
        {
            // 资源基址：指向 Crystal 客户端资源目录对应的本地 HTTP 服务
            // （D:\OpenSource\Crystal\Build\Client\Debug），避免把 GB 级资源复制进 wwwroot。
            MirEngine.BrowserResource.BaseUrl = "http://127.0.0.1:5080/";

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
            SoundManager.Create();
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
            BrowserMouse.Attach();
            BrowserMouse.MouseDown += (s, e) =>
            {
                try
                {
                    // 命中测试依赖 CMain.MPoint：按下时先同步坐标，避免“无前置移动直接点击”时仍用旧坐标打偏。
                    CMain.MPoint = e.Location;
                    MirScene.ActiveScene?.OnMouseDown(e);
                }
                catch (Exception ex) { CMain.SaveError(ex.ToString()); }
            };
            // 关键：鼠标移动必须经由 CMain.CMain_MouseMove，它负责更新 CMain.MPoint（控件命中测试依赖该坐标）。
            // 若直接转发到 ActiveScene.OnMouseMove，CMain.MPoint 永远停在 (0,0)，所有点击都会打偏，表现为“鼠标/键盘失效”。
            BrowserMouse.MouseMove += CMain.CMain_MouseMove;
            BrowserMouse.MouseUp += (s, e) =>
            {
                try
                {
                    // 复刻 WinForms 原版 CMain_MouseUp：松开按键必须清掉 MapControl.MapButtons。
                    // 浏览器端此前漏了这一步，导致按下后 MapButtons 永久保留：
                    // 左键点过再右键点过就会变成 Left|Right，MapControl.CheckInput 的 switch(MapButtons)
                    // 两个分支都不匹配 —— 这正是"点击鼠标人物不移动"的原因。
                    MapControl.MapButtons &= ~e.Button;
                    if (e.Button != MouseButtons.Right || !Settings.NewMove)
                        GameScene.CanRun = false;

                    MirScene.ActiveScene?.OnMouseUp(e);
                    // 复刻 WinForms 原版 CMain_MouseClick：抬起后派发单击，按钮（如连接框 Cancel）与双击逻辑才能触发。
                    // 缺失此步会导致所有按钮点击无反应。
                    MirScene.ActiveScene?.OnMouseClick(e);
                }
                catch (Exception ex) { CMain.SaveError(ex.ToString()); }
            };
            BrowserMouse.MouseWheel += (s, e) =>
            {
                try { MirScene.ActiveScene?.OnMouseWheel(e); }
                catch (Exception ex) { CMain.SaveError(ex.ToString()); }
            };

            BrowserKeyboard.Attach();
            BrowserKeyboard.KeyDown += (s, e) => CMain.CMain_KeyDown(s, e);
            BrowserKeyboard.KeyUp += (s, e) => CMain.CMain_KeyUp(s, e);
            BrowserKeyboard.KeyPress += (s, e) => CMain.CMain_KeyPress(s, e);
        }
    }
}
