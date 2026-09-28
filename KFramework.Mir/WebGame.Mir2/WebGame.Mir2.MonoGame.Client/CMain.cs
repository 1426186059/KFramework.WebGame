using Client;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
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
        public static MirLabel DebugTextLabel, HintTextLabel;
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

            // 原版 CreateHintLabel() / CreateDebugLabel()（Crystal Client/Forms/CMain.cs:514 / :421）。
            // 逐帧刷新：Hint 浮层跟随鼠标所在控件的 Hint 文本；Debug 浮层由 Settings.DebugMode（F12）开关，
            // 两个浮层都无 Parent，由 MirScene.Draw() 在场景上屏之后单独 Draw（与 MirScene.cs:85-89 对应）。
            CreateHintLabel();

            if (Settings.DebugMode || Input_KeyBoard.GetKey(MG.Keys.Tab))
                CreateDebugLabel();
            else if (DebugBaseLabel != null)
                DisposeDebugLabel();
        }

        // 原版 CMain.CreateDebugLabel()（Crystal Client/Forms/CMain.cs:421）。
        // 原版在"全屏"时建浮层、窗口模式时把文本写进窗体标题；浏览器端没有窗体标题，一律建浮层。
        private static void CreateDebugLabel()
        {
            string text;

            if (MirControl.MouseControl != null)
            {
                text = string.Format("FPS: {0}", FPS);

                text += string.Format(", DPS: {0}", DPS);

                text += string.Format(", Time: {0:HH:mm:ss UTC}", Now);

                if (MirControl.MouseControl is MapControl)
                    text += string.Format(", Co Ords: {0}", MapControl.MapLocation);

                if (MirControl.MouseControl is MirImageControl)
                    text += string.Format(", Control: {0}", MirControl.MouseControl.GetType().Name);

                if (MirScene.ActiveScene is GameScene)
                    text += string.Format(", Objects: {0}", MapControl.Objects.Count);

                if (MirScene.ActiveScene is GameScene && !string.IsNullOrEmpty(DebugText))
                    text += string.Format(", Debug: {0}", DebugText);

                text += MapObject.MouseObject != null
                    ? string.Format(", Target: {0}", MapObject.MouseObject.Name)
                    : string.Format(", Target: none");
            }
            else
                text = string.Format("FPS: {0}", FPS);

            text += string.Format(", Ping: {0}", PingTime);

            text += string.Format(", Sent: {0}, Received: {1}", Functions.ConvertByteSize(BytesSent), Functions.ConvertByteSize(BytesReceived));

            text += string.Format(", TLC: {0}", DXManager.TextureList.Count(x => x.TextureValid));
            text += string.Format(", CLC: {0}", DXManager.ControlList.Count(x => !x.IsDisposed));

            if (DebugBaseLabel == null || DebugBaseLabel.IsDisposed)
            {
                DebugBaseLabel = new MirControl
                {
                    BackColour = MirEngine.Color.FromArgb(50, 50, 50),
                    Border = true,
                    BorderColour = MirEngine.Color.Black,
                    DrawControlTexture = true,
                    Location = new MirEngine.Point(5, 5),
                    NotControl = true,
                    Opacity = 0.5F
                };
            }

            if (DebugTextLabel == null || DebugTextLabel.IsDisposed)
            {
                DebugTextLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = MirEngine.Color.Transparent,
                    ForeColour = MirEngine.Color.White,
                    Parent = DebugBaseLabel,
                };

                DebugTextLabel.SizeChanged += (o, e) => ResizeBaseToText(DebugBaseLabel, DebugTextLabel);
            }

            DebugTextLabel.Text = text;
        }

        // 原版 CMain.CreateHintLabel()（Crystal Client/Forms/CMain.cs:514）：鼠标悬停控件的 Hint 浮层。
        // 原版把 HintBaseLabel 挂在 MirScene.ActiveScene 上；本移植刻意不挂父节点——场景的子控件会被
        // 烘焙进 UI 层纹理（MirScene.DrawControl → UILayer.Bake），再叠加一次 MirScene.Draw 里的显式
        // Draw 就会画两遍，故保持无父、只由 MirScene.Draw 画一次（UI 层是单位变换，屏幕坐标即 UI 坐标）。
        private static void CreateHintLabel()
        {
            if (HintBaseLabel == null || HintBaseLabel.IsDisposed)
            {
                HintBaseLabel = new MirControl
                {
                    BackColour = MirEngine.Color.FromArgb(255, 0, 0, 0),
                    Border = true,
                    DrawControlTexture = true,
                    BorderColour = MirEngine.Color.FromArgb(255, 144, 144, 0),
                    ForeColour = MirEngine.Color.Yellow,
                    NotControl = true,
                    Opacity = 0.5F
                };
            }

            if (HintTextLabel == null || HintTextLabel.IsDisposed)
            {
                HintTextLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = MirEngine.Color.Transparent,
                    ForeColour = MirEngine.Color.Yellow,
                    Parent = HintBaseLabel,
                };

                HintTextLabel.SizeChanged += (o, e) => ResizeBaseToText(HintBaseLabel, HintTextLabel);
            }

            if (MirControl.MouseControl == null || string.IsNullOrEmpty(MirControl.MouseControl.Hint))
            {
                HintBaseLabel.Visible = false;
                return;
            }

            HintBaseLabel.Visible = true;

            HintTextLabel.Text = MirControl.MouseControl.Hint;

            MirEngine.Point point = MPoint.Add(-HintTextLabel.Size.Width, 20);

            if (point.X + HintBaseLabel.Size.Width >= Settings.ScreenWidth)
                point.X = Settings.ScreenWidth - HintBaseLabel.Size.Width - 1;
            if (point.Y + HintBaseLabel.Size.Height >= Settings.ScreenHeight)
                point.Y = Settings.ScreenHeight - HintBaseLabel.Size.Height - 1;

            if (point.X < 0)
                point.X = 0;
            if (point.Y < 0)
                point.Y = 0;

            HintBaseLabel.Location = point;
        }

        // 文本标签是 AutoSize 的，底板要跟着文本尺寸走（原版 HintTextLabel.SizeChanged += ...）。
        // 额外一步 DisposeTexture：MirControl.CreateTexture 只在 ControlTexture 为 null 时建 RT，
        // 尺寸变了不会重建，不显式释放的话底板会一直沿用首次的纹理尺寸。
        private static void ResizeBaseToText(MirControl baseLabel, MirLabel textLabel)
        {
            if (baseLabel == null || baseLabel.IsDisposed || textLabel == null) return;
            if (baseLabel.Size == textLabel.Size) return;

            baseLabel.Size = textLabel.Size;
            baseLabel.DisposeTexture();
        }

        // 关闭 DebugMode（再按一次 F12）时销毁调试浮层；原版只在全屏分支里建、窗口模式里不建，
        // 没有对应的销毁，本移植必须显式销毁，否则关掉后浮层会一直留在屏幕上。
        private static void DisposeDebugLabel()
        {
            if (DebugTextLabel != null)
            {
                if (!DebugTextLabel.IsDisposed) DebugTextLabel.Dispose();
                DebugTextLabel = null;
            }
            if (DebugBaseLabel != null)
            {
                if (!DebugBaseLabel.IsDisposed) DebugBaseLabel.Dispose();
                DebugBaseLabel = null;
            }
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
                // 原版：Settings.DebugMode 取反，浮层由 UpdateEnviroment 里的 CreateDebugLabel 逐帧维护。
                Settings.DebugMode = !Settings.DebugMode;
                if (!Settings.DebugMode) DisposeDebugLabel();
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

        public static void CMain_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (MirScene.ActiveScene != null)
                    MirScene.ActiveScene.OnKeyPress(e);
            }
            catch (Exception ex)
            {
                SaveError(ex.ToString());
            }
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
            MG.Input_KeyBoard.KeyPress += OnKeyPress;

            // 首次任意输入即解锁 WebAudio（浏览器自动播放策略要求用户手势）。
            MG.Input_KeyBoard.KeyDown += _ => AudioMaster.Unlock();
            MG.Input_Mouse.ButtonDown += (_, _) => AudioMaster.Unlock();

            MG.Input_KeyBoard.Activate();
            MG.Input_Mouse.Activate();
            // 对齐原版 Program.Form.ActiveControl：原版靠 WinForms 表单级焦点登记表把按键路由到
            // 当前聚焦的原生 TextBox（Crystal MirTextBox.cs:225-227）。移植版无真实窗口，引擎用
            // TextBox.ActiveOrResolved 决定回车/退格等控制键的目标框：优先引擎焦点链 _active，
            // 退化时回退到本 resolver。这里把“当前激活的文本框”登记到引擎，使聊天框等惰性显示的
            // 框在 _active 因 Blur/焦点互斥被清空、而 MirControl 层 ActiveControl 仍指向它时，
            // 回车仍能正确送达 ChatTextBox_KeyPress 发送（否则输入完回车没反应）。
            KFramework.MonoGame.TextBox.ActiveTextBoxResolver =
                () => CMain.Instance?.ActiveControl as KFramework.MonoGame.TextBox;
        }

        private static void OnKeyDown(MG.Keys k) => CMain.CMain_KeyDown(null, ToKeyEventArgs(k));
        private static void OnKeyUp(MG.Keys k) => CMain.CMain_KeyUp(null, ToKeyEventArgs(k));
        private static void OnKeyPress(MG.Keys k) => CMain.CMain_KeyPress(null, ToKeyPressEventArgs(k));

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

        private static MirEngine.KeyPressEventArgs ToKeyPressEventArgs(MG.Keys k)
        {
            // KeyPressEventArgs 现在携带 Keys（键码 + 修饰键位），与 KeyEventArgs 同构。
            // 修饰键电平必须并进 KeyData：浏览器 keyCode 不区分上档（Shift+2 与 2 都是 50），
            // 否则聊天框无从判断 '@'（Shift+D2）/ '!'（Shift+D1）这类前缀字符。
            MirEngine.Keys keyData = (MirEngine.Keys)(int)k;
            if (MG.Input_KeyBoard.Shift) keyData |= MirEngine.Keys.Shift;
            if (MG.Input_KeyBoard.Ctrl) keyData |= MirEngine.Keys.Control;
            if (MG.Input_KeyBoard.Alt) keyData |= MirEngine.Keys.Alt;
            return new MirEngine.KeyPressEventArgs(keyData);
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
