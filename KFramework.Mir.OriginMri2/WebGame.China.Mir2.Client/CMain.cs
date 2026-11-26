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

        // ── 主线程卡顿采样（排查“首次进 GameScene 卡 3 秒 → 心跳超时”）──────────────
        // 只测上层游戏逻辑：帧间隔 / Network.Process / 场景 Process / 场景 Draw。
        // 引擎内部（Game.TickFrame、UpdateLoop、DrawPhase、GL 提交）一概不动。
        // 每帧只在耗时超过阈值时打一行，正常帧零输出，不会刷屏。
        private static DateTime _tickStart = DateTime.Now;
        private static int _libLoadedCount;


        public static Point MPoint;
        public static Random Random = new Random();
        public static KeyBindSettings InputKeys = new KeyBindSettings();
        public static long BytesReceived, BytesSent;
        public static int FPS;
        public static long DC;
        public static long SpriteCount;
        private static long _fpsTime;
        private static int _fps;
        public static int DPS;
        public static int DPSCounter;
        private static long _cleanTime;
        private static long _drawTime;

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
        public Rectangle ClientRectangle => new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        public FormBorderStyle FormBorderStyle;
        public bool TopMost;
        // 完全限定 MirEngine.Cursors：本类存在 static Cursor[] Cursors 字段，会遮蔽同名类型。
        public void Close() { }
        public void Focus() { }
        public void Activate() { }
        public void CenterToScreen() { }
        public Point PointToClient(Point p) => p;
        public Point PointToScreen(Point p) => p;
        public Rectangle RectangleToScreen(Rectangle r) => r;
        public void CreateScreenShot() { }

        // 原 WinForms CMain 中被逻辑代码引用的静态成员（浏览器端用占位/轻量实现）。
        public static bool Shift, Alt, Ctrl, Tilde, SpellTargetLock;
        public static MirControl HintBaseLabel;
        public static MirLabel HintTextLabel;
        public static FPSDialog DebugDialog;
        public static string DebugText = "";

        // 进入游戏时的全屏加载蒙层：由 GameScene 触发创建，但【不挂在任何场景控件树下】，
        // 而是由 CMain 在场景绘制之后单独 Draw（RenderEnvironment），从而层级最高、盖在所有 UI/浮层之上。
        public static LoadingDialog LoadingDialog;

        /// <summary>GameScene 进入时调用：创建加载蒙层（已存在则忽略，避免重连重建场景时重复创建）。</summary>
        public static void ShowLoadingDialog()
        {
            if (LoadingDialog != null && !LoadingDialog.IsDisposed) return;
            LoadingDialog = new LoadingDialog();   // 无 Parent：由 CMain 在场景之后 Draw，层级最高
        }

        /// <summary>每帧推进加载蒙层进度；完成后取消后台资源任务并释放。</summary>
        private static void UpdateLoadingDialog()
        {
            if (LoadingDialog == null || LoadingDialog.IsDisposed)
            {
                return;
            }
            LoadingDialog.Tick();
            if (LoadingDialog.Done)
            {
                LoadingDialog.Cancel();
                LoadingDialog.Dispose();
                LoadingDialog = null;
            }
        }

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

        public static async Task InitAsync()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;
            MirScene.ActiveScene = new InitLoadingScene();
        }

        /// <summary>
        /// 真正的游戏初始化重活：配置表 / 图集 / 首屏资源等。由 InitLoadingScene 在渲染循环里异步驱动，
        /// 每完成一步通过 setStatus 更新 Loading 界面文本；全部完成后切换到 LoginScene。
        /// 之所以放在场景里异步跑（而非 CMain.Init 内同步 await），是因为渲染循环在 Init 返回后才启动；
        /// 若在 Init 内 await 全部，Loading 界面无法显示，且重活卡主线程会导致心跳超时掉线。
        /// </summary>
        internal static async Task RunGameInitializationAsync(Action<string> setStatus)
        {
            // 两个 HTTP 资源服务器地址统一从 RemoteWebSetting 取（Web 部署相关配置集中地，无本地概念，写死在该类）
            BrowserResource.Configure(RemoteWebSetting.LibBaseUrl);

            await Settings.Load();
            await RemoteWebSetting.Load();
            await CMain.InputKeys.LoadAsync().ConfigureAwait(false);
            DXManager.Create();

            // 图集/UI 资源走 CDN 哈希布局：manifest key → resource/{hash前2}/{hash}_{size}.ext。
            // 默认 LocalMirrorUsesKeyPath=true 仅匹配「本地镜像按 key 原路径落盘」；默认 base 是 CDN，
            // 故需设为 false，否则 key 会被当成逻辑路径直接拼到 CDN 根而 404（之前就是这么挂的）。
            Mir2OriginResource.LocalMirrorUsesKeyPath = false;
            setStatus?.Invoke("加载配置表…");
            await ExcelTableMgr.InitializeAsync();   // 配置表后台加载，不阻塞进 LoginScene（就绪后由 IsReady 校验）

            setStatus?.Invoke("加载图集/UI 资源索引…");
            await Mir2AtlasLibrary.InitializeAsync();

            _ = Mir2OriginObjectLibrary.InitializeAsync();

            MirScene.ActiveScene = new LoginScene();
            await SoundManager.CreateAsync().ConfigureAwait(false);
            ConfigureInput();
        }

        private static string _lastLoopError;

        public static void Update(GameTime gameTime)
        {
            UpdateTime(gameTime);

            CMain.MPoint = Input_Mouse.Position.ToPoint();
            CMain.CMain_MouseMove(null, new MouseEventArgs(MouseButtons.None, 0, CMain.MPoint.X, CMain.MPoint.Y, 0));
            SoundManager.ProcessDelayedSounds();

            UpdateEnviroment();
        }

        public static void Draw()
        {
            try
            {
                if (IsDrawTime())
                {
                    RenderEnvironment();
                    UpdateFrameTime();
                }
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

        private static bool IsDrawTime()
        {
            int targetUpdates = 1000 / Math.Max(1, Settings.MaxFPS);

            if (Time >= _drawTime)
            {
                _drawTime = Time + targetUpdates;
                return true;
            }
            return false;
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

                // 每秒采样一次真实 Draw Call 数（与 FPS 同节奏），避免 debug 文本每帧跳动。
                // 此时本帧 RenderEnvironment 已执行，Metrics.DrawCount 为本帧值。
                DC = DXManager.GDevice != null ? DXManager.GDevice.Metrics.DrawCount : 0;
                SpriteCount = DXManager.GDevice != null ? DXManager.GDevice.Metrics.SpriteCount : 0;

                // 每秒（与 FPS/DC 同节奏）重建一次 debug 文本：字符串只在这里构建一次，
                // 不再每帧分配，FPSDialog 仅在文本变化时重烘焙离屏纹理。
                UpdateDebugDialogText();
            }
            else
                _fps++;
        }

        // CMain.UpdateEnviroment()。
        // 沿用历史拼写（Enviroment 少了第二个 n），便于与传奇业务代码逐行对照。
        private static void UpdateEnviroment()
        {
            if (Time >= _cleanTime)
            {
                _cleanTime = Time + 1000;
                DXManager.Clean(); // Clean once a second.
            }

            if (MirScene.ActiveScene != null && !(MirScene.ActiveScene is InitLoadingScene))
            {
                Network.Process();
            }

            if (MirScene.ActiveScene != null)
            {
                MirScene.ActiveScene.Process();
            }

            // 冒泡提示统一由主机逻辑帧驱动动画推进与安全释放（不依赖当前场景，也不放绘制栈）。
            MirBubbleToast.Process();

            // 加载蒙层（进图期间存在）：每帧推进进度；完成即释放。不挂场景树，由 CMain.Draw 在场景之后单独绘制。
            UpdateLoadingDialog();

            // 每帧推进动画控件/按钮的帧偏移。移植时漏掉这两段会让动画永远停在第一帧
            //（表现为登录/选人界面动画不播放）。必须在 Draw 之前执行。
            for (int i = 0; i < MirAnimatedControl.Animations.Count; i++)
                MirAnimatedControl.Animations[i].UpdateOffSet();

            for (int i = 0; i < MirAnimatedButton.Animations.Count; i++)
                MirAnimatedButton.Animations[i].UpdateOffSet();

            // MirAnimatedBase 统一驱动图集帧动画与 Texture2D 序列动画（两者共用计时 / 循环逻辑）。
            for (int i = 0; i < MirAnimatedBase.Animations.Count; i++)
                MirAnimatedBase.Animations[i].UpdateOffSet();

            // CreateHintLabel() / CreateDebugLabel()（传奇客户端主机类的两个浮层）。
            // 逐帧刷新：Hint 浮层跟随鼠标所在控件的 Hint 文本；Debug 浮层由 Settings.DebugMode（F12）开关，
            // 两个浮层都无 Parent，由 MirScene.Draw() 在场景上屏之后单独 Draw（与 MirScene.cs:85-89 对应）。
            CreateHintLabel();

            if (Settings.DebugMode || Input_KeyBoard.GetKey(MG.Keys.Tab))
                EnsureDebugDialog();
            else if (DebugDialog != null)
                HideDebugDialog();
        }

        // CMain.CreateDebugLabel() 收敛成独立 FPSDialog 控件。
        // 文本构建只发生在 UpdateDebugDialogText()，由每秒采样点（UpdateFrameTime）与首次创建时调用，
        // 因此不会每帧重建字符串（消除问题 B 的每帧分配 / GC 尖峰）；FPSDialog 自身走离屏纹理缓存，
        // 文本不变时每帧只贴缓存纹理，不重绘文本。
        private static void EnsureDebugDialog()
        {
            if (DebugDialog == null || DebugDialog.IsDisposed)
            {
                DebugDialog = new FPSDialog { Location = new Point(5, 5) };
                UpdateDebugDialogText(); // 立即填一次，避免空窗
            }
        }

        // 关闭 DebugMode（再按一次 F12）时销毁调试浮层，否则关掉后浮层会一直留在屏幕上。
        private static void HideDebugDialog()
        {
            if (DebugDialog != null)
            {
                if (!DebugDialog.IsDisposed) DebugDialog.Dispose();
                DebugDialog = null;
            }
        }

        // 构建 debug 文本并交给 FPSDialog（其 Text setter 仅在文本变化时重烘焙离屏纹理）。
        private static void UpdateDebugDialogText()
        {
            if (DebugDialog == null || DebugDialog.IsDisposed) return;

            string text;

            if (MirControl.MouseControl != null)
            {
                text = string.Format("FPS: {0}, DC: {1}, Sprites: {2}", FPS, DC, SpriteCount);

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
                text = string.Format("FPS: {0}, DC: {1}, Sprites: {2}", FPS, DC, SpriteCount);

            text += string.Format(", Ping: {0}", PingTime);

            text += string.Format(", Sent: {0}, Received: {1}", Functions.ConvertByteSize(BytesSent), Functions.ConvertByteSize(BytesReceived));

            text += string.Format(", CLC: {0}", DXManager.ControlList.Count(x => !x.IsDisposed));

            DebugDialog.Text = text;
        }

        // CMain.CreateHintLabel()：鼠标悬停控件的 Hint 浮层。
        // 传统桌面实现把 HintBaseLabel 挂在 MirScene.ActiveScene 上；本移植刻意不挂父节点——场景的子控件会被
        // 烘焙进场景离屏纹理（MirScene.CreateTexture），再叠加一次 MirScene.Draw 里的显式
        // Draw 就会画两遍，故保持无父、只由 MirScene.Draw 画一次（UI 层是单位变换，屏幕坐标即 UI 坐标）。
        private static void CreateHintLabel()
        {
            if (HintBaseLabel == null || HintBaseLabel.IsDisposed)
            {
                HintBaseLabel = new MirControl
                {
                    BackColour = CommonColor.FromArgb(255, 0, 0, 0),
                    Border = true,
                    DrawControlTexture = true,
                    BorderColour = CommonColor.FromArgb(255, 144, 144, 0),
                    ForeColour = CommonColor.Yellow,
                    NotControl = true,
                    Opacity = 0.5F
                };
            }

            if (HintTextLabel == null || HintTextLabel.IsDisposed)
            {
                HintTextLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = CommonColor.Transparent,
                    ForeColour = CommonColor.Yellow,
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

            if (HintTextLabel.Text != MirControl.MouseControl.Hint)
            {
                HintTextLabel.Text = MirControl.MouseControl.Hint;
            }
            // AutoSize 的 SizeChanged -> ResizeBaseToText 是异步冒泡的，文本刚换的这一帧
            // HintBaseLabel.Size 可能还是上一个提示的旧尺寸，用它定位会让提示框跑到屏幕外
            //（表现为鼠标在部分图标上"看不到提示"）。这里按当前 HintTextLabel 的实际像素宽高重算。
            ResizeBaseToText(HintBaseLabel, HintTextLabel);

            int hx = MPoint.X + 20;
            int hy = MPoint.Y + 20;

            if (hx + HintBaseLabel.Size.Width >= Settings.ScreenWidth)
            {
                hx = MPoint.X - HintBaseLabel.Size.Width - 10;
            }
            if (hy + HintBaseLabel.Size.Height >= Settings.ScreenHeight)
            {
                hy = MPoint.Y - HintBaseLabel.Size.Height - 10;
            }

            if (hx < 0)
            {
                hx = 0;
            }
            if (hy < 0)
            {
                hy = 0;
            }

            HintBaseLabel.Location = new Point(hx, hy);
        }

        private static void ResizeBaseToText(MirControl baseLabel, MirLabel textLabel)
        {
            if (baseLabel == null || baseLabel.IsDisposed || textLabel == null) return;
            if (baseLabel.Size == textLabel.Size) return;

            baseLabel.Size = textLabel.Size;
            baseLabel.DisposeTexture();
        }

        private static void RenderEnvironment()
        {
            try
            {
                // 原版在这里先判 DXManager.DeviceLost（D3D 设备丢失 → AttemptReset 后 return）。
                // WebGL 无「设备丢失」概念，本移植的 DXManager 也无该成员，故略去。

                DXManager.Device.Clear(SlimDX.Direct3D9.ClearFlags.Target, CommonColor.Black, 0, 0);
                DXManager.Device.BeginScene();
                DXManager.BeginSprite(MirBlend.Normal);
                DXManager.SetSurface(null);                          // 原版 DXManager.SetSurface(DXManager.MainSurface)

                if (MirScene.ActiveScene != null)
                {
                    MirScene.ActiveScene.Draw();
                }

                // 加载蒙层（若有）在场景绘制之后单独 Draw —— 层级最高，盖在所有 UI/Debug/Hint 浮层之上。
                if (LoadingDialog != null && !LoadingDialog.IsDisposed)
                    LoadingDialog.Draw();

                DXManager.EndSprite();                               // 原版 DXManager.Sprite.End()
                DXManager.Device.EndScene();
                DXManager.Device.Present();
            }
            catch (Exception ex)
            {
                SaveErrorOnce(ex.ToString());
                DXManager.AttemptRecovery();
            }
        }

        private static void SaveErrorOnce(string text)
        {
            if (text == _lastLoopError) return;
            _lastLoopError = text;
            SaveError(text);
        }

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
        
        public static void OnWindowSizeChanged(int width, int height)
        {
            if (GameScene.Scene != null)
            {
                GameScene.Scene.MapControl.FloorValid = false;
                DXManager.FloorTexture?.Dispose(); DXManager.FloorTexture = null;
                DXManager.FloorSurface = null;
            }

            MirScene.ActiveScene?.ApplyAnchors();
            MirScene.ActiveScene?.Refresh();
        }

        public static void ToggleFullScreen() { }
        public static bool IsKeyLocked(Keys key) => false;

        public static void CMain_KeyDown(object sender, MirEngine.KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;

            // 浏览器长按会持续触发 KeyDown；首次按下正常施放，后续交给 Process 按 CD 重复。
            if (GetSkillHotkeySlot(e.KeyCode, out int heldSkillSlot) && !HeldSkillKeys.TryAdd(e.KeyCode, heldSkillSlot))
            {
                return;
            }

            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = (MG.Keys)(int)e.KeyCode == (MG.Keys)Enum.Parse(typeof(MG.Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == Keys.Backquote) Tilde = true;
            if (e.KeyCode == Keys.F12)
            {
                // 原版：Settings.DebugMode 取反，浮层由 UpdateEnviroment 里的 EnsureDebugDialog 维护，
                Settings.DebugMode = !Settings.DebugMode;
                if (!Settings.DebugMode) HideDebugDialog();
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
            HeldSkillKeys.Remove(e.KeyCode);
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = (MG.Keys)(int)e.KeyCode == (MG.Keys)Enum.Parse(typeof(MG.Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == Keys.Backquote) Tilde = false;
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

        // 技能快捷键在 KeyDown 已消费时置位，令紧随其后的 KeyPress 不落到聊天输入框。
        // Input_KeyBoard 的 KeyDown/KeyPress 是两条独立事件，KeyDown 的 Handled 无法阻止 KeyPress。
        public static bool SuppressNextKeyPress;

        // 浏览器长按技能键会持续发 KeyDown。这里记录“当前按住的技能键 → 技能栏槽位”，
        // 首次 KeyDown 正常施放，后续由 GameScene.Process 按 CD 自动重复施放，松开键才停止。
        private static readonly Dictionary<Keys, int> HeldSkillKeys = new();

        public static IEnumerable<int> HeldSkillSlots
        {
            get { return new HashSet<int>(HeldSkillKeys.Values); }
        }

        private static bool GetSkillHotkeySlot(Keys key, out int slot)
        {
            slot = 0;
            if (InputKeys == null || InputKeys.Keylist == null)
            {
                return false;
            }

            foreach (KeyBind bind in InputKeys.Keylist)
            {
                if (bind.Key != key)
                {
                    continue;
                }

                if (bind.function >= KeybindOptions.Bar1Skill1 && bind.function <= KeybindOptions.Bar1Skill8)
                {
                    slot = (int)(bind.function - KeybindOptions.Bar1Skill1) + 1;
                    return true;
                }

                if (bind.function >= KeybindOptions.Bar2Skill1 && bind.function <= KeybindOptions.Bar2Skill8)
                {
                    slot = (int)(bind.function - KeybindOptions.Bar2Skill1) + 9;
                    return true;
                }

                if (bind.function >= KeybindOptions.HeroSkill1 && bind.function <= KeybindOptions.HeroSkill8)
                {
                    slot = (int)(bind.function - KeybindOptions.HeroSkill1) + 17;
                    return true;
                }

                return false;
            }

            return false;
        }

        public static void CMain_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (SuppressNextKeyPress)
            {
                SuppressNextKeyPress = false;
                e.Handled = true;
                return;
            }

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
            MPoint = KCamera.ScreenToWorldPos(new Point(e.Location.X, e.Location.Y));
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

            KFramework.MonoGame.TextBox.ActiveTextBoxResolver =
                () => CMain.Instance?.ActiveControl as KFramework.MonoGame.TextBox;
        }

        private static void OnKeyDown(MG.Keys k) => CMain.CMain_KeyDown(null, ToKeyEventArgs(k));
        private static void OnKeyUp(MG.Keys k) => CMain.CMain_KeyUp(null, ToKeyEventArgs(k));
        private static void OnKeyPress(MG.Keys k) => CMain.CMain_KeyPress(null, ToKeyPressEventArgs(k));

        private static void OnMouseDown(MG.MouseButton b, MG.Vector2 p)
        {
            var lp = KCamera.ScreenToWorldPos(p.ToPoint());
            CMain.MPoint = new Point((int)lp.X, (int)lp.Y);
            MirScene.ActiveScene?.OnMouseDown(ToMouseEventArgs(b, lp.ToVector2()));
        }
        private static void OnMouseUp(MG.MouseButton b, MG.Vector2 p)
        {
            var lp = KCamera.ScreenToWorldPos(p.ToPoint());
            CMain.MPoint = new Point((int)lp.X, (int)lp.Y);
            var e = ToMouseEventArgs(b, lp.ToVector2());
            if (MapControl.MapButtons == e.Button) MapControl.MapButtons = MouseButtons.None;
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
            return new KeyEventArgs(k, MG.Input_KeyBoard.Shift, MG.Input_KeyBoard.Ctrl, MG.Input_KeyBoard.Alt);
        }

        private static KeyPressEventArgs ToKeyPressEventArgs(MG.Keys k)
        {
            return new KeyPressEventArgs(k, MG.Input_KeyBoard.Shift, MG.Input_KeyBoard.Ctrl, MG.Input_KeyBoard.Alt);
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
