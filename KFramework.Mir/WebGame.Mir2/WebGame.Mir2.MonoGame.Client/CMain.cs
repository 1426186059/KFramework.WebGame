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
        public readonly static DateTime StartTime = DateTime.UtcNow;
        public static long Time;
        public static DateTime Now { get { return StartTime.AddMilliseconds(Time); } }
        public static MirEngine.Point MPoint;
        public static Random Random = new Random();
        public static KeyBindSettings InputKeys = new KeyBindSettings();
        public static long BytesReceived, BytesSent;
        public static int FPS;
        private static long _fpsTime;
        private static int _fps;
        public static int DPS;
        public static int DPSCounter;

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
        public MirEngine.Size ClientSize = new MirEngine.Size(1024, 768);
        public MirEngine.Rectangle ClientRectangle => new MirEngine.Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        public FormBorderStyle FormBorderStyle;
        public bool TopMost;
        // 完全限定 MirEngine.Cursors：本类存在 static Cursor[] Cursors 字段，会遮蔽同名类型。
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
        public static MirControl DebugBaseLabel, HintBaseLabel;
        public static string DebugText = "";
        public static long PingTime;
        public static long NextPing = 10000;

        static CMain()
        {
           
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

            // 两个 HTTP 资源服务器地址统一从 RemoteWebSetting 取（Web 部署相关配置集中地，无本地概念，写死在该类）
            BrowserResource.Configure(RemoteWebSetting.LibBaseUrl);

            await Settings.Load();
            await RemoteWebSetting.Load();
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

            KFramework.MonoGame.PrintTool.Log($"[Scene] 切换: {(MirScene.ActiveScene == null ? "null" : MirScene.ActiveScene.GetType().Name)} → LoginScene  时刻={DateTime.Now:HH:mm:ss.fff}");
            MirScene.ActiveScene = new LoginScene();
            await SoundManager.CreateAsync().ConfigureAwait(false);
            ConfigureInput();
        }

        private static string _lastLoopError;
        private static long _cleanTime;
            
        public static void Update(GameTime gameTime)
        {
            UpdateTime(gameTime);

            CMain.MPoint = Input_Mouse.Position;
            CMain.CMain_MouseMove(null, new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, 0));
            SoundManager.ProcessDelayedSounds();

            UpdateEnviroment();
        }

        public static void Draw()
        {
            try
            {
                RenderEnvironment();
                UpdateFrameTime();
            }
            catch (Exception ex)
            {
                // 帧循环里的未处理异常会终止渲染进程且拿不到堆栈；这里打印完整堆栈（去重）到控制台，
                // 便于定位真正的出错行（如连接阶段的 NullReferenceException）。
                SaveErrorOnce(ex.ToString());
            }
        }

        private static void UpdateTime(GameTime gameTime)
        {
            Time = (long)gameTime.TotalGameTime.TotalMilliseconds;
        }

        private static void UpdateFrameTime()
        {
            if (Time >= _fpsTime)
            {
                _fpsTime = Time + 1000;
                FPS = _fps;
                _fps = 0;

                DPS = DPSCounter;
                DPSCounter = 0;
            }
            else
                _fps++;
        }

        // 原版 CMain.UpdateEnviroment()（Crystal Client/Forms/CMain.cs:357）。
        // 沿用原版拼写（Enviroment 少了第二个 n），便于与原版逐行对照。
        private static void UpdateEnviroment()
        {
            if (Time >= _cleanTime)
            {
                _cleanTime = Time + 1000;

                DXManager.Clean(); // Clean once a second.
            }

            Network.Process();

            if (MirScene.ActiveScene != null)
                MirScene.ActiveScene.Process();

            // 每帧推进动画控件/按钮的帧偏移。移植时漏掉这两段会让动画永远停在第一帧
            //（表现为登录/选人界面动画不播放）。必须在 Draw 之前执行。
            for (int i = 0; i < MirAnimatedControl.Animations.Count; i++)
                MirAnimatedControl.Animations[i].UpdateOffSet();

            for (int i = 0; i < MirAnimatedButton.Animations.Count; i++)
                MirAnimatedButton.Animations[i].UpdateOffSet();

            // 原版此处还有 CreateHintLabel()，以及 Settings.DebugMode 为真时的 CreateDebugLabel()。
            // 本移植未搬这两个方法：
            //   - Hint 悬浮提示依赖 HintBaseLabel/HintTextLabel，本移植未实现；
            //   - Debug 浮层已由 GameScene 负责（见 GameScene.cs 中 CMain.DebugBaseLabel 的处理），此处不重复创建。
        }

        // 原版 CMain.RenderEnvironment()（Crystal Client/Forms/CMain.cs:385）。
        private static void RenderEnvironment()
        {
            try
            {
                // 原版在这里先处理 DXManager.DeviceLost（D3D 设备丢失 → AttemptReset 后返回）。
                // WebGL 没有「设备丢失」概念，本移植的 DXManager 也没有该成员，故略去。

                // 对应原版的 Device.Clear + BeginScene + Sprite.Begin + ActiveScene.Draw + Sprite.End + EndScene + Present。
                DXManager.RenderFrame(() =>
                {
                    if (MirScene.ActiveScene != null)
                        MirScene.ActiveScene.Draw();
                });
            }
            catch (Exception ex)
            {
                SaveErrorOnce(ex.ToString());
                DXManager.AttemptRecovery();
            }
        }

        // 帧循环里同一异常会每帧重复抛出，去重后再打印，避免刷屏。
        private static void SaveErrorOnce(string text)
        {
            if (text == _lastLoopError) return;
            _lastLoopError = text;
            SaveError(text);
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
                    name = MouseCursorFunc.Default;
                    break;
                case MouseCursor.NPCTalk:
                case MouseCursor.Upgrade:
                    name = MouseCursorFunc.Pointer;
                    break;
                case MouseCursor.TextPrompt:
                    name = MouseCursorFunc.Pointer;
                    break;
                default:
                    name = MouseCursorFunc.Default;
                    break;
            }

            BrowserCursor.Set(name);
        }

        // 窗口尺寸变化的处理。UI 以画布原生分辨率布局（UI 层恒等变换、不做非等比拉伸），
        // 世界层用 FullScreenSize 原生渲染，故这里只需三步：
        //   1) 释放地板/光照离屏纹理，让它们按新尺寸重建（MapControl 每帧用 FullScreenSize
        //      调 UpdateViewPort，会同步自身 Size 与可视范围）；
        //   2) RelayoutAll：按锚点重排 UI 顶层控件，内部子控件随父移动；
        //   3) Refresh：令当前场景重新烘焙。
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

            // UI 以原生分辨率布局，窗口变化后已构造控件的位置不会自动更新，
            // 故先重新结算顶层控件位置，再重新烘焙（库加载完成也会触发 Refresh，见 Init）。
            MirScene.ActiveScene?.ApplyAnchors();

            // 当前活动场景（登录/选人/游戏）重烘焙。
            MirScene.ActiveScene?.Refresh();
        }

        public static void ToggleFullScreen() { }
        public static bool IsKeyLocked(MirEngine.Keys key) => false;

        public static void CMain_KeyDown(object sender, MirEngine.KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = (MG.Keys)(int)e.KeyCode == (MG.Keys)Enum.Parse(typeof(MG.Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == MirEngine.Keys.Oem8) Tilde = true;
            if (e.KeyCode == MirEngine.Keys.F12)
            {
                Settings.DebugMode = !Settings.DebugMode;
                if (!Settings.DebugMode && CMain.DebugBaseLabel != null)
                {
                    CMain.DebugBaseLabel.Dispose();
                    CMain.DebugBaseLabel = null;
                }
                return;
            }
            try
            {
                if (e.Alt && (MG.Keys)(int)e.KeyCode == MG.Keys.Enter) { ToggleFullScreen(); return; }
                if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyDown(e);
            }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_KeyUp(object sender, MirEngine.KeyEventArgs e)
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

        public static void CMain_MouseMove(object sender, MirEngine.MouseEventArgs e)
        {
            // 与 OnMouseDown/OnMouseUp 保持一致：把原始画布坐标换算成逻辑 UI 坐标(1024x768)，
            // 否则命中检测用原始像素对比逻辑 DisplayRectangle 会错位。
            MPoint = KCamera.ScreenToWorldPos(new MirEngine.Point(e.Location.X, e.Location.Y));
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnMouseMove(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void SaveError(string ex)
        {
            try { KFramework.MonoGame.PrintTool.LogError("[Mir][Error] " + ex); }
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

        private static MirEngine.KeyEventArgs ToKeyEventArgs(MG.Keys k)
        {
            MirEngine.Keys keyData = (MirEngine.Keys)(int)k;
            if (MG.Input_KeyBoard.Shift) keyData |= MirEngine.Keys.Shift;
            if (MG.Input_KeyBoard.Ctrl) keyData |= MirEngine.Keys.Control;
            if (MG.Input_KeyBoard.Alt) keyData |= MirEngine.Keys.Alt;
            return new KeyEventArgs(keyData);
        }

        private static MirEngine.MouseEventArgs ToMouseEventArgs(MG.MouseButton b, MG.Vector2 p)
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
