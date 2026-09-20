using Client;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using KFramework.MonoGame;
using MG = KFramework.MonoGame;

namespace WebGame.Mir2.MonoGame.Client
{
    // 浏览器端游戏主机：替代原 WinForms.Forms/CMain 窗体。
    // 保留 Mir2 代码引用的静态成员（Time/Now/MPoint/Random/DPSCounter/BytesReceived/BytesSent...）
    // 以及被 CMain.Instance 引用的"窗体"成员（Controls/ActiveControl/Close/...）。
    // 原 Program 入口类（Init/Frame/Step + 输入桥接）已并入本类。
    public partial class CMain
    {
        public static long Time;
        public static DateTime Now;
        public static MirEngine.Point MPoint;
        public static Random Random = new Random();
        public static int DPSCounter;
        public static KeyBindSettings InputKeys = new KeyBindSettings();
        public static long BytesReceived, BytesSent;
        public static int FPS;
        public static int TotalBytesReceived, TotalBytesSent;

        // 原 Program 入口类的单例窗体及启动状态（已并入 CMain）。
        public static readonly CMain Instance = new CMain();
        public static bool Launch, Restart;
        private static bool _bootstrapped;

        public static GraphicsStub Graphics = new GraphicsStub();

        // 被 CMain.Instance 引用的"窗体"成员（交互由 DOM 桥接，这里仅占位）。
        public List<object> Controls = new List<object>();
        public object ActiveControl;
        public string Text = "";
        public Size ClientSize = new Size(1024, 768);
        public MirEngine.Rectangle ClientRectangle => new MirEngine.Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        public FormBorderStyle FormBorderStyle;
        public bool TopMost;
        // 完全限定 MirEngine.Cursors：本类存在 static Cursor[] Cursors 字段，会遮蔽同名类型。
        public Cursor Cursor = MirEngine.Cursors.Default;
        public void Close() { }
        public void Focus() { }
        public void Activate() { }
        public void CenterToScreen() { }
        public MirEngine.Point PointToClient(MirEngine.Point p) => p;
        public MirEngine.Point PointToScreen(MirEngine.Point p) => p;
        public MirEngine.Rectangle RectangleToScreen(MirEngine.Rectangle r) => r;
        public void CreateScreenShot() { }

        // 原 WinForms CMain 中被逻辑代码引用的静态成员（浏览器端用占位/轻量实现）。
        public static bool Shift, Alt, Ctrl, Tilde, SpellTargetLock;
        public static Cursor[] Cursors = new Cursor[16];
        public static MirControl DebugBaseLabel, HintBaseLabel;
        public static string DebugText = "";
        public static long PingTime;
        public static long NextPing = 10000;

        static CMain()
        {
            for (int i = 0; i < Cursors.Length; i++) Cursors[i] = new Cursor();
        }


        // ---- 原 Program 入口逻辑（已并入 CMain）----
        // 约束：本项目禁止任何 [JSExport] —— JS 互操作基础设施全部由 KFramework.MonoGame 提供
        // （JSBind_GameHost / JSBind_*）。帧循环由引擎的 JSBind_GameHost.Frame → Game.TickFrame
        // → MirGame.Draw → CMain.Loop 驱动并上屏；CMain 只作为纯 C# 宿主暴露 Loop() 与 Init()，
        // 不向 JS 暴露任何入口。昔日挂在 CMain 上的 [JSExport] Init/Frame/Step 已移除：Frame/Step
        // 是纯 JS 入口，会被引擎 findHost 的递归兜底误判为帧宿主，从而绕过 Game.TickFrame/上屏导致黑屏。

        public static async Task Init()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;

            // 资源基址：指向 Crystal 客户端资源目录对应的本地 HTTP 服务
            // （D:\OpenSource\Crystal\Build\Client\Debug），避免把 GB 级资源复制进 wwwroot。
            // 同时创建一个指向它的 ContentManager，统一经 KFramework.MonoGame 异步加载远程资源。
            MirEngine.BrowserResource.Configure("http://127.0.0.1:5080/");

            await Settings.Load();
            await CMain.InputKeys.LoadAsync().ConfigureAwait(false);
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

            // 等待首屏库（登录/选角界面立即需要的 ChrSel/Prguse/UI_32bit/Title 等）全部就绪后再继续：
            // 渲染循环在 LoadContentAsync 完成后才启动，避免首屏库尚未下载完就开画导致的"隔几帧黑屏"闪烁。
            // 其余库（地图/怪物/装备等）不在启动时加载，绘制时按需 InitializeAsync，就绪后 LibraryLoaded 触发重绘。
            await Libraries.LoadAsync();

            MirScene.ActiveScene = new LoginScene();
            await SoundManager.CreateAsync().ConfigureAwait(false);
            ConfigureInput();
        }

        private static string _lastLoopError;

        public static void Loop()
        {
            try
            {
                Time = Environment.TickCount & 0x7FFFFFFF;
                Now = DateTime.Now;

                Network.Process();

                if (MirScene.ActiveScene != null)
                {
                    MirScene.ActiveScene.Process();
                    DXManager.RenderFrame(() => MirScene.ActiveScene.Draw());
                }

                DXManager.Clean();
            }
            catch (Exception ex)
            {
                // 帧循环里的未处理异常会终止渲染进程且拿不到堆栈；这里打印完整堆栈（去重）到控制台，
                // 便于定位真正的出错行（如连接阶段的 NullReferenceException）。
                string s = ex.ToString();
                if (s != _lastLoopError)
                {
                    _lastLoopError = s;
                    SaveError(s);
                }
            }
        }

        // 原版用 Win32 .CUR 文件切换窗体光标；浏览器端无法加载 .CUR，改为切换 canvas 的 CSS cursor。
        // 之前的空实现会让游戏内光标永远停在 canvas 默认样式（攻击/NPC对话/文本等状态都不变化）。
        // 传的是语义名，具体图片/样式在 tsengine/src/core/cursor.ts 里定义（改成自己的图片只需改那里）。
        public static void SetMouseCursor(MouseCursor cursor)
        {
            string name;
            switch (cursor)
            {
                case MouseCursor.Attack:
                case MouseCursor.AttackRed:
                    name = "attack";
                    break;
                case MouseCursor.NPCTalk:
                case MouseCursor.Upgrade:
                    name = "npc";
                    break;
                case MouseCursor.TextPrompt:
                    name = "text";
                    break;
                case MouseCursor.Trash:
                    name = "trash";
                    break;
                default:
                    name = "default";
                    break;
            }

            MirEngine.BrowserCursor.Set(name);
        }

        // 浏览器端全屏方案：游戏以【固定逻辑分辨率】(Settings.ScreenWidth/Height，默认 1024x768)渲染，
        // 再由 MirScene.DrawControl → DXManager.PresentToScreen 把整帧场景纹理拉伸铺满画布(Viewport)。
        // 因此这里【不再】把 Settings 改成画布物理尺寸——否则场景离屏纹理与实际显示尺寸脱节、
        // UI 命中坐标错配；只负责在窗口缩放时刷新地板/光照离屏纹理并令当前场景重烘焙。
        // （该回调由 MirGame 在 Window.SizeChanged 时触发，传入的 width/height 即画布尺寸，此处不再使用。）
        public static void OnWindowSizeChanged(int width, int height)
        {
            // 游戏内场景的地板/光照是离屏 RenderTarget，分辨率变化时先释放，
            // DrawFloor/DrawLights 检测到 null/Disposed 后会用逻辑分辨率(Settings)重建。
            if (GameScene.Scene != null)
            {
                GameScene.Scene.MapControl.FloorValid = false;
                DXManager.FloorTexture?.Dispose(); DXManager.FloorTexture = null;
                DXManager.FloorSurface = null;
                DXManager.LightTexture?.Dispose(); DXManager.LightTexture = null;
                DXManager.LightSurface = null;
            }

            // 当前活动场景（登录/选人/游戏）按逻辑分辨率重烘焙（库加载完成也会触发，见 Init）。
            MirScene.ActiveScene?.Refresh();
        }

        public static void ToggleFullScreen() { }
        public static bool IsKeyLocked(MirEngine.Keys key) => false;

        public static void CMain_KeyDown(object sender, KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = (MG.Keys)(int)e.KeyCode == (MG.Keys)Enum.Parse(typeof(MG.Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == MirEngine.Keys.Oem8) Tilde = true;
            try
            {
                if (e.Alt && (MG.Keys)(int)e.KeyCode == MG.Keys.Enter) { ToggleFullScreen(); return; }
                if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyDown(e);
            }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_KeyUp(object sender, KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = (MG.Keys)(int)e.KeyCode == (MG.Keys)Enum.Parse(typeof(MG.Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == MirEngine.Keys.Oem8) Tilde = false;
            foreach (KeyBind KeyCheck in CMain.InputKeys.Keylist)
            {
                if (KeyCheck.function != KeybindOptions.Screenshot) continue;
                if (KeyCheck.Key != e.KeyCode) continue;
                if ((KeyCheck.RequireAlt != 2) && (KeyCheck.RequireAlt != (Alt ? 1 : 0))) continue;
                if ((KeyCheck.RequireShift != 2) && (KeyCheck.RequireShift != (Shift ? 1 : 0))) continue;
                if ((KeyCheck.RequireCtrl != 2) && (KeyCheck.RequireCtrl != (Ctrl ? 1 : 0))) continue;
                if ((KeyCheck.RequireTilde != 2) && (KeyCheck.RequireTilde != (Tilde ? 1 : 0))) continue;
                Instance.CreateScreenShot();
                break;
            }
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyUp(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_MouseMove(object sender, MouseEventArgs e)
        {
            // 与 OnMouseDown/OnMouseUp 保持一致：把原始画布坐标换算成逻辑 UI 坐标(1024x768)，
            // 否则命中检测用原始像素对比逻辑 DisplayRectangle 会错位。
            MPoint = KCamera.ScreenToWorldPos(new MirEngine.Point(e.Location.X, e.Location.Y));
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnMouseMove(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void SaveError(string ex)
        {
            try { MirEngine.BrowserResource.Log("[Mir][Error] " + ex); }
            catch { }
        }

        private static void ConfigureInput()
        {
            // 输入改走 KFramework.MonoGame 的 Input 层（KInputMgr 已把指针/键盘事件桥到 GL 画布）。
            // 鼠标移动不提供事件，改为每帧在 MirGame.Update 里轮询 Input_Mouse.Position 并转发 CMain_MouseMove。
            // 字符输入（KeyPress）浏览器端由 MirTextBox 的 DOM 输入层承担，这里不桥接。
            MG.Input_Mouse.ButtonDown += OnMouseDown;
            MG.Input_Mouse.ButtonUp += OnMouseUp;
            MG.Input_Mouse.ScrollWheel += OnScrollWheel;
            MG.Input_KeyBoard.KeyDown += OnKeyDown;
            MG.Input_KeyBoard.KeyUp += OnKeyUp;
            // 首次任意输入即解锁 WebAudio（浏览器自动播放策略要求用户手势）。
            MG.Input_KeyBoard.KeyDown += _ => AudioMaster.Unlock();
            MG.Input_Mouse.ButtonDown += (_, _) => AudioMaster.Unlock();
        }

        private static void OnKeyDown(MG.Keys k) => CMain.CMain_KeyDown(null, ToKeyEventArgs(k));
        private static void OnKeyUp(MG.Keys k) => CMain.CMain_KeyUp(null, ToKeyEventArgs(k));

        private static void OnMouseDown(MG.MouseButton b, MG.Vector2 p)
        {
            var lp = KCamera.ScreenToWorldPos(p);
            CMain.MPoint = new MirEngine.Point((int)lp.X, (int)lp.Y);
            MirScene.ActiveScene?.OnMouseDown(ToMouseEventArgs(b, lp));
        }
        private static void OnMouseUp(MG.MouseButton b, MG.Vector2 p)
        {
            var lp = KCamera.ScreenToWorldPos(p);
            CMain.MPoint = new MirEngine.Point((int)lp.X, (int)lp.Y);
            var e = ToMouseEventArgs(b, lp);
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

        private static KeyEventArgs ToKeyEventArgs(MG.Keys k)
        {
            MirEngine.Keys keyData = (MirEngine.Keys)(int)k;
            if (MG.Input_KeyBoard.Shift) keyData |= MirEngine.Keys.Shift;
            if (MG.Input_KeyBoard.Ctrl) keyData |= MirEngine.Keys.Control;
            if (MG.Input_KeyBoard.Alt) keyData |= MirEngine.Keys.Alt;
            return new KeyEventArgs(keyData);
        }

        private static MouseEventArgs ToMouseEventArgs(MG.MouseButton b, MG.Vector2 p)
        {
            MouseButtons mb = b == MG.MouseButton.Right ? MouseButtons.Right
                                : b == MG.MouseButton.Middle ? MouseButtons.Middle
                                : MouseButtons.Left;
            return new MouseEventArgs(mb, 1, (int)p.X, (int)p.Y, 0);
        }
    }

    public class GraphicsStub
    {
        public float DpiX => 96f;
        public float DpiY => 96f;
    }
}
