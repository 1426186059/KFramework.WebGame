using Client.MirControls;
using Client.MirGraphics;
using Client.MirGraphics.Particles;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;

using SlimDX;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using S = ServerPackets;


namespace Client.MirScenes
{
    public sealed class MapControl : MirControl
    {
        public static UserObject User
        {
            get { return MapObject.User; }
            set { MapObject.User = value; }
        }

        public static UserHeroObject Hero
        {
            get { return MapObject.Hero; }
            set { MapObject.Hero = value; }
        }

        public static Dictionary<uint, MapObject> Objects = new Dictionary<uint, MapObject>();
        public static List<MapObject> ObjectsList = new List<MapObject>();

        public const int CellWidth = 48;
        public const int CellHeight = 32;

        // 原始传奇 zipmap 真实瓦片纹理尺寸（jpg），每张覆盖 512×256 地图像素
        public const int TileTexWidth = 512;
        public const int TileTexHeight = 256;

        /// <summary>选中目标脚下显示的「选择环」贴图（程序化生成的白色圆环，绘制时染色）。
        /// 按传奇经典风格新增：在所选敌人/玩家脚底画一个等距椭圆环。</summary>
        private static KFramework.MonoGame.Texture2D _selectionRingTex;
        private static KFramework.MonoGame.Texture2D SelectionRingTexture
        {
            get
            {
                if (_selectionRingTex == null)
                    _selectionRingTex = CreateSelectionRingTexture();
                return _selectionRingTex;
            }
        }

        /// <summary>程序化生成一张正方形白色圆环贴图（透明底），绘制时按比例压扁成地面等距椭圆。</summary>
        private static KFramework.MonoGame.Texture2D CreateSelectionRingTexture()
        {
            const int size = 64;
            byte[] rgba = new byte[size * size * 4];
            float cx = (size - 1) / 2f;
            float cy = (size - 1) / 2f;
            float radius = size / 2f - 3f;
            float thickness = 4f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d <= radius && d >= radius - thickness)
                    {
                        int i = (y * size + x) * 4;
                        rgba[i] = 255; rgba[i + 1] = 255; rgba[i + 2] = 255; rgba[i + 3] = 255;
                    }
                }
            }
            return DXManager.GDevice.CreateTexture(size, size, rgba);
        }

        /// <summary>在指定对象脚底绘制等距椭圆选中环（不随 HighlightTarget 开关，选中即显示）。</summary>
        private void DrawSelectionRing(MapObject obj)
        {
            if (obj == null || obj.Dead) return;
            var tex = SelectionRingTexture;
            if (tex == null) return;

            // 脚底世界屏幕坐标：对象所在格左下中心（角色站立点）。
            int footX = obj.DrawLocation.X + CellWidth / 2;
            int footY = obj.DrawLocation.Y + CellHeight;

            // 等距椭圆半轴：地面圆环宽:高 = CellWidth:CellHeight。
            float semiW = CellWidth * 0.9f;
            float semiH = CellHeight * 0.9f;

            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(false);
            DXManager.Draw(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new RectangleF(footX - semiW, footY - semiH, semiW * 2, semiH * 2),
                new SlimDX.Color4(0.9f, 1f, 0.15f, 0.15f)); // 经典红色，90% 不透明
            DXManager.SetBlend(oldBlend);
        }

        public static int OffSetX;
        public static int OffSetY;

        public static int ViewRangeX;
        public static int ViewRangeY;

        // 当前地图渲染所用的视口尺寸缓存：仅在视口变化时重算相机、可见范围并重建地板/光照纹理。
        private static int _vpW, _vpH;
        /// <summary>
        /// 让地图相机/可见范围/控件尺寸跟随真实视口：玩家始终居于视口中心，世界坐标 1:1 渲染，
        /// 视口越大看到的地图越多（分辨率无关）。
        /// </summary>
        private void UpdateViewPort(int w, int h)
        {
            if (w == _vpW && h == _vpH) return;
            _vpW = w; _vpH = h;
            OffSetX = w / 2 / CellWidth;
            OffSetY = h / 2 / CellHeight - 1;
            ViewRangeX = OffSetX + 6;
            ViewRangeY = OffSetY + 6;
            Size = new Size(w, h);
            // 视口变化：地板需按新尺寸重新烘焙（DrawFloor 仅在 !FloorValid 时执行）。
            FloorValid = false;
        }

        private bool _autoPath;
        public bool AutoPath
        {
            get
            {
                return _autoPath;
            }
            set
            {
                if (_autoPath == value) return;
                _autoPath = value;

                if (!_autoPath)
                    CurrentPath = null;
            }
        }

        // 限频输出移动诊断日志：每次点击只打一条，便于判断是"点击没送到"、
        // "坐标算错"还是"目标格被阻挡"，而不是靠猜。
        private static long _lastMoveLog;
        private static void LogMove(string msg)
        {
            if (CMain.Time < _lastMoveLog) return;
            _lastMoveLog = CMain.Time + 400;
            //KFramework.MonoGame.PrintTool.Log("[Move] " + msg);
        }

        // 走到鼠标所指的格子（左/右键点空地都会调用）。
        // 原先只有"右键 + Settings.NewMove 为真"才寻路，而 NewMove 默认 false，
        // 且左键点空地直接 return —— 这就是"点击鼠标人物不动"的原因。
        private void TryAutoPath()
        {
            if (MapObject.User == null) return;
            // 地图尚未加载完成（M2CellInfo 为空）时忽略寻路点击，避免索引空网格。
            if (M2CellInfo == null) return;

            Point target = MapLocation;
            Point from = MapObject.User.CurrentLocation;

            // 目标必须在地图范围内：EmptyCell 内部直接索引 M2CellInfo[x,y]，
            // 越界会抛 IndexOutOfRangeException，表现为"点击毫无反应"。
            if (target.X < 0 || target.Y < 0 || target.X >= Width || target.Y >= Height)
            {
                LogMove("点击越界: " + target + " 地图=" + Width + "x" + Height);
                return;
            }

            if (target == from)
            {
                LogMove("点击到自己所在格: " + target);
                return;
            }

            if (!GameScene.Scene.MapControl.EmptyCell(target))
            {
                CellInfo c = M2CellInfo[target.X, target.Y];
                LogMove("目标格不可走(阻挡): " + target + " BackImage=0x" + c.BackImage.ToString("X8") +
                    " FrontImage=0x" + c.FrontImage.ToString("X4"));
                return;
            }

            var path = GameScene.Scene.MapControl.PathFinder.FindPath(from, target, 20);
            if (path == null || path.Count == 0)
            {
                LogMove("寻路失败: " + from + " -> " + target + "，目标可走=True");
                return;
            }

            GameScene.Scene.MapControl.CurrentPath = path;
            GameScene.Scene.MapControl.AutoPath = true;
            LogMove("开始寻路: " + from + " -> " + target + "，路径点数=" + path.Count);

            ShowClickEffect(target);
        }

        // 落点特效（Magic3 第 500 帧起的 10 帧动画，画在鼠标所指格子上）。
        // 传统实现只在"右键 + Settings.NewMove 为真"的寻路分支里放；本移植把它抽出来，
        // 让点空地（不论是否真正起寻路）都有落点反馈。
        public void ShowClickEffect(Point target)
        {
            if (M2CellInfo == null) return;
            // 越界格子 ToMouseLocation 会算出屏幕外的坐标，特效画出去看不见，且毫无意义。
            if (target.X < 0 || target.Y < 0 || target.X >= Width || target.Y >= Height) return;
            // Magic3 是"绘制时按需异步加载"的库：未就绪时 CheckImage 会画马赛克占位（首次点击常见），
            // 加载完成后再点即为真实特效。库对象本身恒不为 null，这里的判断只是兜底防 NRE。
            if (Libraries.Magic3 == null) return;

            var offset = MouseLocation.Subtract(ToMouseLocation(target));
            // 原版落点环（Magic3#500）默认 Light=6，会被 DrawLights 当成光源画光晕进光照 RT，
            // 使点击区域被照亮——这是原版既有行为，故保留默认发光，不做改动。
            Effects.Add(new Effect(Libraries.Magic3, 500, 10, 600, target) { DrawOffset = offset.Subtract(8, 15) });
        }

        public PathFinder PathFinder;
        public List<Node> CurrentPath = null;

        public static Point MapLocation
        {
            get { return GameScene.User == null ? Point.Empty : new Point(MouseLocation.X / CellWidth - OffSetX, MouseLocation.Y / CellHeight - OffSetY).Add(GameScene.User.CurrentLocation); }
        }

        public static Point ToMouseLocation(Point p)
        {
            return new Point((p.X - MapObject.User.Movement.X + OffSetX) * CellWidth, (p.Y - MapObject.User.Movement.Y + OffSetY) * CellHeight).Add(MapObject.User.OffSetMove);
        }

        public static MouseButtons MapButtons;
        public static Point MouseLocation;
        public static long InputDelay;

        private static long nextAction;
        public static long NextAction
        {
            get { return nextAction; }
            set
            {
                if (GameScene.Observing) return;
                nextAction = value;
            }
        }

        public CellInfo[,] M2CellInfo;
        // 地图异步加载完成前（M2CellInfo 尚未就绪），暂存需要加入地图格的对象，
        // 待 LoadMapAsync 赋值 M2CellInfo 后再补加，避免登录竞态导致的 NullReferenceException。
        private readonly List<MapObject> _pendingAdd = new List<MapObject>();
        // 切图加载代次：LoadMapAsync 是 fire-and-forget 调用的，连续/并发切图时
        // 只有「最后一次发起」的加载才允许落地，先完成的旧地图必须丢弃（见 LoadMapAsync）。
        private int _mapLoadGeneration;
        // 上一次切图加载的取消源：发起新加载时先 Cancel 旧的，让它尽快停下。
        // 不依赖「后续是否还有 MapChanged 包」——只要再来一次切图请求就中断上一次。
        private CancellationTokenSource _mapLoadCts;
        public List<Door> Doors = new List<Door>();
        public int Width, Height;

        public int Index;

        // 原始传奇 zipmap 真实瓦片渲染：从 CDN 的 assets/resource/zipmap/{MapImg}/{index}.jpg 拼图，
        // 取代旧的 .map 瓦片索引(Libraries.MapLibs)渲染。
        public int MapImg;
        public int PixelWidth, PixelHeight;
        public int TileCols, TileRows;
        private readonly Dictionary<int, KFramework.MonoGame.Texture2D> _mapTileCache = new Dictionary<int, KFramework.MonoGame.Texture2D>();
        private readonly HashSet<int> _mapTileLoading = new HashSet<int>();
        private Point _lastUserCell = new Point(-1, -1);
        private bool _floorDirty;
        public string FileName = String.Empty;

        public string Title = String.Empty;
        public ushort MiniMap, BigMap, Music, SetMusic;
        public LightSetting Lights;
        public bool Lightning, Fire;
        public byte MapDarkLight;
        public long LightningTime, FireTime;
        public WeatherSetting Weather = WeatherSetting.None;
        public bool FloorValid, LightsValid;

        public long OutputDelay;

        private static bool _awakeningAction;
        public static bool AwakeningAction
        {
            get { return _awakeningAction; }
            set
            {
                if (_awakeningAction == value) return;
                _awakeningAction = value;
            }
        }

        private static bool _autoRun;
        public static bool AutoRun
        {
            get { return _autoRun; }
            set
            {
                if (_autoRun == value) return;
                _autoRun = value;
                if (GameScene.Scene != null)
                    GameScene.Scene.ChatDialog.ReceiveChat(value ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AutoRunOn) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AutoRunOff),
                        ChatType.System);
            }

        }
        // [DayNight] 当前地图昼夜状态（仅变化时打印，避免每帧刷屏）
        internal static LightSetting _dnLastSetting = (LightSetting)0xFF;

        public static bool AutoHit;

        public int AnimationCount;

        public static List<Effect> Effects = new List<Effect>();

        public MapControl()
        {
            MapButtons = MouseButtons.None;

            OffSetX = DXManager.GDevice.Viewport.Width / 2 / CellWidth;
            OffSetY = DXManager.GDevice.Viewport.Height / 2 / CellHeight - 1;

            ViewRangeX = OffSetX + 6;
            ViewRangeY = OffSetY + 6;

            Size = new Size(DXManager.GDevice.Viewport.Width, DXManager.GDevice.Viewport.Height);
            DrawControlTexture = true;
            BackColour = Color.Black;

            MouseDown += OnMouseDown;
            MouseMove += (o, e) => MouseLocation = e.Location;
            Click += OnMouseClick;
        }

        public void ResetMap(bool bResetCell = true)
        {
            GameScene.Scene.NPCDialog.Hide();

            MapObject.MouseObjectID = 0;
            MapObject.TargetObjectID = 0;
            MapObject.MagicObjectID = 0;

            // 先卸载旧地图上的全部对象：Remove 需要从当前网格移除对象，
            // 必须在本方法置空 M2CellInfo 之前执行，否则旧 NPC/怪物残留导致重影跟随玩家。
            if (M2CellInfo != null)
                for (var i = ObjectsList.Count - 1; i >= 0; i--)
                    ObjectsList[i]?.Remove();

            Objects.Clear();
            ObjectsList.Clear();
            Effects.Clear();
            Doors.Clear();
            _mapTileCache.Clear();
            _mapTileLoading.Clear();

            if (User != null)
            {
                Objects[User.ObjectID] = User;
                ObjectsList.Add(User);
            }

            // 切图置空网格（须在对象卸载之后）：异步加载完成前，新地图的 User/NPC/怪物对象
            // 会经由 AddObject/RemoveObject 落入 _pendingAdd 缓冲，待 LoadMapAsync 加载完本图后统一回放，
            // 避免用“旧地图(可能更小)网格”按“新地图坐标”索引导致 IndexOutOfRangeException。
            if (bResetCell)
            {
                M2CellInfo = null;
                Width = 0;
                Height = 0;
                _pendingAdd.Clear();
            }
        }

        public async Task LoadMapAsync()
        {
            ResetMap();

            // 记录本次加载代次并快照文件名：LoadMapAsync 是 fire-and-forget 调用的，
            // 连续/并发切图时（如登录时连着收到两张地图包），先完成的旧地图若照常落地，
            // 会被后完成的覆盖，最终显示成「另一张地图的同一坐标区域」。
            // 中断上一次尚未完成的加载：新切图一来旧的立即作废，不再继续
            // 解析 / 建 PathFinder / 预加载库 / 播音乐。
            _mapLoadCts?.Cancel();
            _mapLoadCts = new CancellationTokenSource();
            var token = _mapLoadCts.Token;

            int gen = ++_mapLoadGeneration;
            string fileName = FileName;

            MapObject.MouseObjectID = 0;
            MapObject.TargetObjectID = 0;
            MapObject.MagicObjectID = 0;

            MapReader Map = new MapReader(fileName);
            try
            {
                await Map.LoadAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;   // 被新切图取消，正常退出
            }

            // 期间已有更新的切图请求：本次结果作废，绝不能覆盖新地图的数据。
            if (token.IsCancellationRequested || gen != _mapLoadGeneration)
            {
                // MapReader 内部会顺带把 NewResConfig.CurrentMapName 设成它自己这张（旧）地图，
                // 作废时要改回当前最新的那张，否则后续地图片库会按旧地图名去取。
                string staleName = Path.GetFileNameWithoutExtension(fileName);
                NewResConfig.CurrentMapName = Path.GetFileNameWithoutExtension(FileName);
                //KFramework.MonoGame.PrintTool.Log($"[Map] 丢弃过期地图加载: {staleName}（已有更新的切图请求，当前={NewResConfig.CurrentMapName}）");
                return;
            }

            M2CellInfo = Map.MapCells;
            // 地图就绪后补加竞态窗口内暂存的对象（如登录时先于地图加载完成的 UserObject）
            foreach (var ob in _pendingAdd) AddObject(ob);
            _pendingAdd.Clear();
            Width = Map.Width;
            Height = Map.Height;

            MapImg = Map.MapImg;
            PixelWidth = Map.PixelWidth;
            PixelHeight = Map.PixelHeight;
            TileCols = (int)Math.Ceiling(PixelWidth / (double)TileTexWidth);
            TileRows = (int)Math.Ceiling(PixelHeight / (double)TileTexHeight);
            _mapTileCache.Clear();
            _mapTileLoading.Clear();
            _tilesReady = 0;            // 采样计数随切图重置
            _tileGridLogged = false;
            _floorWaitFrames = 0;
            _floorDirty = true;
            _lastUserCell = new Point(-1, -1);

            // 首次进图卡顿采样：PathFinder 构造会为整张地图逐格 new Node（Width*Height 个对象），
            // 与上面 CellInfo 网格同量级，同样跑在 await 之后的主线程上。
            var pfT0 = DateTime.Now;
            PathFinder = new PathFinder(this);

            try
            {
                if (SetMusic != Music)
                {
                    SoundManager.Music?.Dispose();
                    SoundManager.PlayMusic(Music, true);
                }
            }
            catch (Exception)
            {
                // Do nothing. index was not valid.
            }

            SetMusic = Music;
            SoundList.Music = Music;

            // 采样：从收到地图包 → 网格/寻路全部就绪的总耗时（瓦片另算，见 DrawFloor 日志）
            //KFramework.MonoGame.PrintTool.Log($"[Map] 地图加载链路总耗时（收包 → 网格+寻路就绪）：{(DateTime.Now - GameScene.MapLoadStart).TotalMilliseconds:F0}ms  时刻={DateTime.Now:HH:mm:ss.fff}");

            UpdateWeather();
        }


        public void Process()
        {
            Processdoors();
            User.Process();
            for (int i = ObjectsList.Count - 1; i >= 0; i--)
            {
                if (ObjectsList[i] == User) continue;
                ObjectsList[i].Process();
            }

            for (int i = Effects.Count - 1; i >= 0; i--)
                Effects[i].Process();

            if (MapObject.TargetObject != null && MapObject.TargetObject is MonsterObject && MapObject.TargetObject.AI == 64)
                MapObject.TargetObjectID = 0;
            if (MapObject.MagicObject != null && MapObject.MagicObject is MonsterObject && MapObject.MagicObject.AI == 64)
                MapObject.MagicObjectID = 0;

            CheckInput();

            MapObject bestmouseobject = null;
            for (int y = MapLocation.Y + 2; y >= MapLocation.Y - 2; y--)
            {
                if (y >= Height) continue;
                if (y < 0) break;
                for (int x = MapLocation.X + 2; x >= MapLocation.X - 2; x--)
                {
                    if (x >= Width) continue;
                    if (x < 0) break;
                    CellInfo cell = M2CellInfo[x, y];
                    if (cell.CellObjects == null) continue;

                    for (int i = cell.CellObjects.Count - 1; i >= 0; i--)
                    {
                        MapObject ob = cell.CellObjects[i];
                        if (ob == MapObject.User || !ob.MouseOver(CMain.MPoint)) continue;

                        if (MapObject.MouseObject != ob)
                        {
                            if (ob.Dead)
                            {
                                if (!Settings.TargetDead && GameScene.TargetDeadTime <= CMain.Time) continue;

                                bestmouseobject = ob;
                                //continue;
                            }
                            MapObject.MouseObjectID = ob.ObjectID;
                            Redraw();
                        }
                        if (bestmouseobject != null && MapObject.MouseObject == null)
                        {
                            MapObject.MouseObjectID = bestmouseobject.ObjectID;
                            Redraw();
                        }
                        return;
                    }
                }
            }

            if (MapObject.MouseObject != null)
            {
                MapObject.MouseObjectID = 0;
                Redraw();
            }
        }

        public static MapObject GetObject(uint targetID)
        {
            Objects.TryGetValue(targetID, out var ob);
            return ob;
        }

        public override void Draw()
        {
            // 与原版 Crystal 一致：MapControl 不向场景 RT 绘制。
            // 地图整张烤进 MapControl 自己的 ControlTexture（见 CreateTexture），由 GameScene.DrawControl
            // 单独 blit 上屏；场景 RT 只装 UI——半透明 UI 叠在不透明地图之上不漏层，且 UI 不再随地图动画每帧重烤。
        }

        // MapControl 只允许挂在 MirScene 上（不再有独立的世界层容器）。
        // 一旦不在场景里，场景烘焙不到任何内容——地图不上屏（黑屏）。

        protected override void CreateTexture()
        {
            if (User == null) return;

            // MapControl 按【全屏/窗口原生分辨率】烘焙世界（世界坐标），地板 1:1 铺满、无黑边、无放大。
            // 场景 RT 为 1:1 透传（单位矩阵），所以这里直接用窗口像素作为世界区域：
            // 窗口更大只是看到更多世界（以 48x32 世界像素为单位的视口更大），而非放大世界坐标。
            // 命中：MapControl 内鼠标即世界像素（与屏幕 1:1），与 OffSetX/Y、ViewRangeX/Y 一致。
            // 用 DXManager.FullScreenSize（后备缓冲真实像素）而非 GDevice.Viewport，避免某些宿主下
            // Viewport 被临时缩小成逻辑尺寸，导致地图只铺左上角。
            var fs = DXManager.FullScreenSize;
            int rtW = fs.Width, rtH = fs.Height;
            UpdateViewPort(rtW, rtH);

            // 视口/相机变化导致的地板重烤，改由 DrawControl 每帧检测并置 _floorDirty（见 DrawControl 顶部），
            // 此处不再判断——否则“门控为 false 时 CreateTexture 不跑、内部移动判断也跳过”会让地板不随角色滚动。

            bool floorNeedsResize = DXManager.FloorTexture == null || DXManager.FloorTexture.Disposed
                || DXManager.FloorTexture.Width != rtW || DXManager.FloorTexture.Height != rtH;
            if (!FloorValid || floorNeedsResize || _floorDirty)
                DrawFloor(rtW, rtH);

            if (TextureSize.Width != rtW || TextureSize.Height != rtH)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new Texture(DXManager.Device, rtW, rtH, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                TextureSize = new Size(rtW, rtH);
            }

            Surface oldSurface = DXManager.CurrentSurface;
            Surface surface = ControlTexture.GetSurfaceLevel(0);
            DXManager.SetSurface(surface);
            DXManager.Device.Clear(ClearFlags.Target, BackColour, 0, 0);

            // 内容按全屏(窗口原生分辨率) 1:1 烘焙（不缩放）。世界层为单位变换，DrawControl 直接 1:1 合成。
            var savedRenderTransform = DXManager.RenderTransform;
            DXManager.RenderTransform = null;

            DrawBackground();

            if (FloorValid && DXManager.FloorTexture != null)
            {
                // 地板纹理(世界区域尺寸)与 ControlTexture 同尺寸，1:1 原尺寸贴图。
                DXManager.Draw(DXManager.FloorTexture, new Rectangle(0, 0, rtW, rtH), Vector3.Zero, Color.White);
            }

            DrawObjects();

            //render weather
            foreach (ParticleEngine engine in GameScene.Scene.ParticleEngines)
            {
                engine.Draw();
            }

            //Render Death, 

            LightSetting setting = Lights == LightSetting.Normal ? GameScene.Scene.Lights : Lights;

            if (_dnLastSetting != setting)
            {
                _dnLastSetting = setting;
                string dnName = setting switch
                {
                    LightSetting.Day => "白天",
                    LightSetting.Normal => "白天",
                    LightSetting.Night => "黑夜",
                    LightSetting.Evening => "黄昏",
                    LightSetting.Dawn => "黎明",
                    _ => setting.ToString()
                };
                //KFramework.MonoGame.PrintTool.Log($"[DayNight] 当前地图: {dnName}");
            }

            if (setting != LightSetting.Day || GameScene.User.Poison.HasFlag(PoisonType.Blindness))
            {
                DrawLights(setting);
            }

            if (Settings.DropView || GameScene.DropViewTime > CMain.Time)
            {
                foreach (var ob in Objects.Values.OfType<ItemObject>())
                {
                    if (!ob.MouseOver(MouseLocation))
                        ob.DrawName();
                }
            }

            if (MapObject.MouseObject != null && !(MapObject.MouseObject is ItemObject))
                MapObject.MouseObject.DrawName();

            int offSet = 0;

            if (Settings.DisplayBodyName)
            {
                foreach (var ob in Objects.Values.OfType<MonsterObject>())
                {
                    if (ob.MouseOver(MouseLocation))
                        ob.DrawName();
                }
            }

            foreach (var ob in Objects.Values.OfType<ItemObject>())
            {
                if (ob.MouseOver(MouseLocation))
                {
                    ob.DrawName(offSet);
                    offSet -= ob.NameLabel.Size.Height + (ob.NameLabel.Border ? 1 : 0);
                }
            }

            if (MapObject.User.MouseOver(MouseLocation))
                MapObject.User.DrawName();

            // 恢复世界层变换，供外层 DrawControl 合成时等比缩放（只缩放一次）。
            DXManager.RenderTransform = savedRenderTransform;
            DXManager.SetSurface(oldSurface);
            surface.Dispose();
            TextureValid = true;

        }
        protected internal override void DrawControl()
        {
            if (!DrawControlTexture)
                return;

            // 相机所在格变化 → 暴露出新的瓦片范围，需重新烘焙地板（按需从 CDN 拉取新瓦片）。
            // 这必须在 DrawControl 每帧判断：否则 CreateTexture 门控为 false 时内部判断被跳过，地板不随角色滚动。
            if (User != null && (User.Movement.X != _lastUserCell.X || User.Movement.Y != _lastUserCell.Y))
            {
                _floorDirty = true;
                _lastUserCell = new Point(User.Movement.X, User.Movement.Y);
            }

            // 地板是异步烘焙的：进图首帧 MapImg 尚未就绪会烤出空地板并把 TextureValid 置 true，
            // 此后 LoadMapAsync 设 _floorDirty / MapImg 时，必须允许 CreateTexture 再次执行
            // （DrawFloor 只在 CreateTexture 内部被调用），否则空地板永远留着 → 所有地图黑屏。
            // 故门控不能只看 !TextureValid，还要在地板需要重烤(!FloorValid)或地图刚就绪(_floorDirty)时放行。
            if (!TextureValid || _floorDirty || !FloorValid)
                CreateTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
                return;

            float oldOpacity = DXManager.Opacity;

            if (MapObject.User.Dead) DXManager.SetGrayscale(true);

            // 全屏 1:1 上屏：强制单位变换，保证地板铺满、绝不被任何遗留 RenderTransform 缩放/偏移；
            // 目标矩形取实时窗口尺寸（DXManager.FullScreenSize），确保铺满整个窗口——
            // 窗口更大只是看到更多世界，而非放大世界像素（世界坐标本不放大）。
            // 不能走 PresentToScreen（其内部清空变换 → 1:1 左上角，地图只显示局部/四周洋红）。
            var savedTransform = DXManager.RenderTransform;
            DXManager.RenderTransform = null;
            var tex = ControlTexture.RenderTarget;
            var fs = DXManager.FullScreenSize;

            DXManager.Draw(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new RectangleF(0, 0, fs.Width, fs.Height),
                Color.White);
            DXManager.RenderTransform = savedTransform;

            if (MapObject.User.Dead) DXManager.SetGrayscale(false);

            CleanTime = CMain.Time + Settings.CleanDelay;
        }

        private void DrawFloor(int vpW, int vpH)
        {
            int fw = vpW, fh = vpH;
            if (DXManager.FloorTexture == null || DXManager.FloorTexture.Disposed
                || DXManager.FloorTexture.Width != fw || DXManager.FloorTexture.Height != fh)
            {
                DXManager.FloorTexture = new Texture(DXManager.Device, fw, fh, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                DXManager.FloorSurface = DXManager.FloorTexture.GetSurfaceLevel(0);
            }

            // 还没进图（User 为空）时直接判定有效，避免空烘焙
            if (User == null) { FloorValid = true; return; }

            var floorT0 = DateTime.Now;   // 采样：单次地板烘焙耗时
            Surface oldSurface = DXManager.CurrentSurface;
            DXManager.SetSurface(DXManager.FloorSurface);
            DXManager.Device.Clear(ClearFlags.Target, Color.Empty, 0, 0);

            // 原版传奇地板：直接由 CDN 的 assets/resource/zipmap/{MapImg}/{index}.jpg 真实瓦片拼出，
            // 取代旧 Libraries.MapLibs 瓦片索引渲染——原始传奇官方就是这么画地图的。
            // 瓦片网格：cols=ceil(PixelWidth/TileTexWidth)，rows=ceil(PixelHeight/TileTexHeight)，index=r*cols+c+1；
            // 每瓦片在地图像素空间占 [c*TileTexWidth,(c+1)*TileTexWidth]x[r*TileTexHeight,(r+1)*TileTexHeight]，映射到 1:1 地板 RT 后整张贴入。
            int pending = 0;
            if (MapImg > 0 && TileCols > 0 && TileRows > 0)
            {
                int startX = User.Movement.X - ViewRangeX;
                int endX = User.Movement.X + ViewRangeX;
                int startY = User.Movement.Y - ViewRangeY;
                int endYExtended = User.Movement.Y + ViewRangeY + 5;

                int colMin = (int)Math.Floor((startX * CellWidth) / (double)TileTexWidth) - 1;
                int colMax = (int)Math.Floor((endX * CellWidth) / (double)TileTexWidth) + 1;
                int rowMin = (int)Math.Floor((startY * CellHeight) / (double)TileTexHeight) - 1;
                int rowMax = (int)Math.Floor((endYExtended * CellHeight) / (double)TileTexHeight) + 1;
                if (colMin < 0) colMin = 0;
                if (rowMin < 0) rowMin = 0;
                if (colMax >= TileCols) colMax = TileCols - 1;
                if (rowMax >= TileRows) rowMax = TileRows - 1;

                // 采样：本次切图一次性要拉多少张瓦片（只在切图后第一帧打一次）
                if (!_tileGridLogged)
                {
                    _tileGridLogged = true;
                    int tc = colMax - colMin + 1, tr = rowMax - rowMin + 1;
                    //KFramework.MonoGame.PrintTool.Log($"[Map] 地板瓦片请求规模: {tc}x{tr}={tc * tr} 张（TileCols={TileCols} TileRows={TileRows} 地图像素={PixelWidth}x{PixelHeight}）");
                }

                // 地板 RT 是 1:1 像素，瓦片用单位变换贴，避免被外层遗留 RenderTransform 二次缩放/偏移
                var savedRT = DXManager.RenderTransform;
                DXManager.RenderTransform = null;
                try
                {
                    // 第一阶段：仅可见区。绘制已就绪瓦片、对缺失瓦片发起加载并计入 pending。
                    for (int r = rowMin; r <= rowMax; r++)
                    {
                        for (int c = colMin; c <= colMax; c++)
                        {
                            int index = r * TileCols + c + 1;
                            var tex = GetMapTile(index);
                            if (tex == null) { tex = MosaicManager.GetFloorMosaic(TileTexWidth, TileTexHeight); pending++; } // 缺失瓦片用土/金马赛克占位（仿原始传奇），不再留黑/留空

                            int tileDrawX = c * TileTexWidth - User.Movement.X * CellWidth + OffSetX * CellWidth - OffSetX + User.OffSetMove.X;
                            int tileDrawY = r * TileTexHeight - User.Movement.Y * CellHeight + OffSetY * CellHeight + User.OffSetMove.Y;
                            DXManager.Draw(tex, new Rectangle(0, 0, tex.Width, tex.Height), new RectangleF(tileDrawX, tileDrawY, TileTexWidth, TileTexHeight), new SlimDX.Color4(1f, 1f, 1f, 1f));
                        }
                    }
                }
                finally
                {
                    DXManager.RenderTransform = savedRT;
                }
            }

            DXManager.SetSurface(oldSurface);
            // 地板只要烘过一次就视为有效、应当上屏：缺的瓦片只留空（透明），
            // 绝不能因“还有图没下完”就 FloorValid=false 让整张地板不绘制而全黑。
            // 仍缺瓦片(pending>0)时保持 _floorDirty=true，下一帧继续重烘把陆续下载完的瓦片补上；
            // 全部到位后 _floorDirty=false，停止重烘。
            FloorValid = true;
            _floorDirty = pending > 0;

            // 采样：地板重烘焙的频次与总等待时长（pending>0 期间每帧都会重烤一次）
            if (pending > 0)
            {
                if (_floorWaitFrames == 0) _floorWaitStart = DateTime.Now;
                _floorWaitFrames++;
                if (_floorWaitFrames % 30 == 1)
                {
                    //KFramework.MonoGame.PrintTool.Log($"[Map] 地板重烘焙第 {_floorWaitFrames} 帧，仍缺 {pending} 张瓦片（已就绪 {_tilesReady} 张）");
                }
            }
            else if (_floorWaitFrames > 0)
            {
                //KFramework.MonoGame.PrintTool.Log($"[Map] 地板瓦片全部到位：共重烘焙 {_floorWaitFrames} 帧 / {(DateTime.Now - _floorWaitStart).TotalMilliseconds:F0}ms，就绪 {_tilesReady} 张");
                _floorWaitFrames = 0;
            }
        }

        private KFramework.MonoGame.Texture2D GetMapTile(int index)
        {
            if (_mapTileCache.TryGetValue(index, out var t)) return t;
            if (!_mapTileLoading.Contains(index))
            {
                _mapTileLoading.Add(index);
                _ = LoadMapTileAsync(index);
            }
            return null;
        }

        private async Task LoadMapTileAsync(int index)
        {
            var tileT0 = DateTime.Now;
            try
            {
                var tex = await MapTileLoadGate.RunAsync(() =>
                    Mir2OriginResource.GetMapTileTextureAsync(MapImg, index, DXManager.GDevice)).ConfigureAwait(false);
                if (tex != null) _mapTileCache[index] = tex;
                _tilesReady++;
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.Log("[Map] 瓦片加载失败 img=" + MapImg + " idx=" + index + " " + ex.Message);
            }
            finally
            {
                _mapTileLoading.Remove(index);
            }
        }

        // ── 以下仅为「新地图加载方式」的耗时采样，不参与任何渲染逻辑 ──
        private int _tilesReady;              // 本次切图已就绪瓦片数
        private bool _tileGridLogged;         // 瓦片请求规模是否已打过日志
        private DateTime _floorWaitStart;     // 地板开始等待瓦片的时刻
        private int _floorWaitFrames;         // 地板重烘焙帧数
        private void DrawBackground()
        {
            string cleanFilename = FileName.Replace(Settings.MapPath, "");

            if (cleanFilename.StartsWith("ID1") || cleanFilename.StartsWith("ID2"))
            {
                Libraries.Background.Draw(10, 0, 0); //mountains
            }
            else if (cleanFilename.StartsWith("ID3_013"))
            {
                Libraries.Background.Draw(22, 0, 0); //desert
            }
            else if (cleanFilename.StartsWith("ID3_015"))
            {
                Libraries.Background.Draw(23, 0, 0); //greatwall
            }
            else if (cleanFilename.StartsWith("ID3_023") || cleanFilename.StartsWith("ID3_025"))
            {
                Libraries.Background.Draw(21, 0, 0); //village entrance
            }
        }

        private void DrawObjects()
        {
            if (Settings.Effect)
            {
                for (int i = Effects.Count - 1; i >= 0; i--)
                {
                    if (!Effects[i].DrawBehind) continue;
                    Effects[i].Draw();
                }
            }

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 25; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    M2CellInfo[x, y].DrawDeadObjects();
                }
            }

            for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY + 25; y++)
            {
                if (y <= 0) continue;
                if (y >= Height) break;
                // 原始传奇地图整体由 CDN zipmap 瓦片拼出（见 DrawFloor），已包含地面/中层/前层/动画，
                // 不再用 Libraries.MapLibs 逐层叠加绘制。这里只画随格子挂的动态对象（角色/NPC/怪物/物品等）。
                for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
                {
                    if (x < 0) continue;
                    if (x >= Width) break;
                    M2CellInfo[x, y].DrawObjects();
                }
            }

            DXManager.Sprite.Flush();
            float oldOpacity = DXManager.Opacity;
            DXManager.SetOpacity(0.4F);

            //MapObject.User.DrawMount();

            MapObject.User.DrawBody();

            if ((MapObject.User.Direction == MirDirection.Up) ||
                (MapObject.User.Direction == MirDirection.UpLeft) ||
                (MapObject.User.Direction == MirDirection.UpRight) ||
                (MapObject.User.Direction == MirDirection.Right) ||
                (MapObject.User.Direction == MirDirection.Left))
            {
                MapObject.User.DrawHead();
                MapObject.User.DrawWings();
            }
            else
            {
                MapObject.User.DrawWings();
                MapObject.User.DrawHead();
            }

            DXManager.SetOpacity(oldOpacity);

            if (Settings.HighlightTarget)
            {
                if (MapObject.MouseObject != null && !MapObject.MouseObject.Dead && MapObject.MouseObject != MapObject.TargetObject && MapObject.MouseObject.Blend)
                    MapObject.MouseObject.DrawBlend();

                if (MapObject.TargetObject != null)
                    MapObject.TargetObject.DrawBlend();
            }

            // 选中目标脚下显示经典选择环（敌人/玩家均可，不依赖 HighlightTarget 开关）。
            // 走位时左键点地面会清空 TargetObject，但雷电术等远程攻击仍走 MagicObject，
            // 因此这里以"实际战斗目标"= TargetObject ?? MagicObject 作为环的载体，保证边走边打时环不丢。
            var sel = MapObject.TargetObject;
            if (sel == null || sel.Dead) sel = MapObject.MagicObject;
            if (sel != null && !sel.Dead) DrawSelectionRing(sel);

            if (Settings.Effect)
            {
                for (int i = Effects.Count - 1; i >= 0; i--)
                {
                    if (Effects[i].DrawBehind) continue;
                    Effects[i].Draw();
                }
            }

            foreach (var ob in Objects.Values)
            {
                ob.DrawEffects(Settings.Effect);

                if (Settings.NameView && !(ob is ItemObject) && !ob.Dead)
                    ob.DrawName();

                ob.DrawChat();
                //ob.DrawHealth();
                ob.DrawPoison();
                ob.DrawDamages();
            }

            foreach (var ob in Objects.Values)
            {
                ob.DrawHealth();
            }
        }

        private Color GetBlindLight(Color light)
        {
            if (MapObject.User.BlindTime <= CMain.Time && MapObject.User.BlindCount < 25)
            {
                MapObject.User.BlindTime = CMain.Time + 100;
                MapObject.User.BlindCount++;
            }

            int count = MapObject.User.BlindCount;
            light = Color.FromArgb(255, Math.Max(20, light.R - (count * 10)), Math.Max(20, light.G - (count * 10)), Math.Max(20, light.B - (count * 10)));

            return light;
        }

        private void DrawLights(LightSetting setting)
        {
            // 灯贴图（DXManager.Lights）未就绪时直接跳过昼夜压暗，避免整屏黑；CreateLights 填充后自然走原版压暗+火把逻辑。
            if (DXManager.Lights == null || DXManager.Lights.Count == 0) return;

            var vp = DXManager.GDevice.Viewport;
            int lw = vp.Width, lh = vp.Height;
            if (DXManager.LightTexture == null || DXManager.LightTexture.Disposed
                || DXManager.LightTexture.Width != lw || DXManager.LightTexture.Height != lh)
            {
                DXManager.LightTexture = new Texture(DXManager.Device, lw, lh, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                DXManager.LightSurface = DXManager.LightTexture.GetSurfaceLevel(0);
            }

            Surface oldSurface = DXManager.CurrentSurface;
            DXManager.SetSurface(DXManager.LightSurface);

            // DrawLights 全部在屏幕空间绘制（光照 RT 与合成全屏矩形都不应被世界相机变换影响）。
            // DXManager.Draw 会把 RenderTransform 套到顶点，故此处临时清空，方法结束再恢复。
            var _savedRT = DXManager.RenderTransform;
            DXManager.RenderTransform = null;

            Color darkness;

            switch (setting)
            {
                case LightSetting.Night:
                    {
                        switch (MapDarkLight)
                        {
                            case 1:
                                darkness = Color.FromArgb(255, 20, 20, 20);
                                break;
                            case 2:
                                darkness = Color.LightSlateGray;
                                break;
                            case 3:
                                darkness = Color.SkyBlue;
                                break;
                            case 4:
                                darkness = Color.Goldenrod;
                                break;
                            default:
                                darkness = Color.Black;
                                break;
                        }
                    }
                    break;
                case LightSetting.Evening:
                case LightSetting.Dawn:
                    darkness = Color.FromArgb(255, 50, 50, 50);
                    break;
                default:
                case LightSetting.Day:
                    darkness = Color.FromArgb(255, 255, 255, 255);
                    break;
            }

            if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
            {
                darkness = GetBlindLight(darkness);
            }

            // 光照 RT：照原版传奇，先清空为「暗度色」darkness（而非全透明）；
            // 下面的发光体（火把/法术/地图灯）再以加法叠加到这层暗度之上。
            DXManager.Device.Clear(ClearFlags.Target, darkness, 0, 0);

            // 灯贴图（火把/法术/地图灯）在浏览器端暂未加载（DXManager.Lights 为空）。此时仍要执行上面的环境昼夜压暗，
            // 仅跳过下面的"发光体"绘制以免 Lights[-1] 越界崩溃；日后补齐灯光资源后这段会自动生效。
            if (DXManager.Lights.Count > 0)
            {
                int light;
                Point p;
                DXManager.SetBlend(true);
                DXManager.Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
                DXManager.Device.SetRenderState(RenderState.DestinationBlend, Blend.One);

                foreach (var ob in Objects.Values)
                {
                    if (ob.Light > 0 && (!ob.Dead || ob == MapObject.User || ob.Race == ObjectType.Spell))
                    {
                        light = ob.Light;

                        int lightRange = light % 15;
                        if (lightRange >= DXManager.Lights.Count)
                            lightRange = DXManager.Lights.Count - 1;

                        p = ob.DrawLocation;

                        Color lightColour = ob.LightColour;

                        if (ob.Race == ObjectType.Player)
                        {
                            switch (light / 15)
                            {
                                case 0://no light source
                                    lightColour = Color.FromArgb(255, 60, 60, 60);
                                    break;
                                case 1:
                                    lightColour = Color.FromArgb(255, 120, 120, 120);
                                    break;
                                case 2://Candle
                                    lightColour = Color.FromArgb(255, 180, 180, 180);
                                    break;
                                case 3://Torch
                                    lightColour = Color.FromArgb(255, 240, 240, 240);
                                    break;
                                default://Peddler Torch
                                    lightColour = Color.FromArgb(255, 255, 255, 255);
                                    break;
                            }
                        }
                        else if (ob.Race == ObjectType.Merchant)
                        {
                            lightColour = Color.FromArgb(255, 120, 120, 120);
                        }

                        if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                        {
                            lightColour = GetBlindLight(lightColour);
                        }

                        if (DXManager.Lights[lightRange] != null && !DXManager.Lights[lightRange].Disposed)
                        {
                            p.Offset(-(DXManager.LightSizes[lightRange].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[lightRange].Y / 2) - (CellHeight / 2) - 5);
                            DXManager.Draw(DXManager.Lights[lightRange], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                        }
                    }

                    if (!Settings.Effect) continue;
                    for (int e = 0; e < ob.Effects.Count; e++)
                    {
                        Effect effect = ob.Effects[e];
                        if (!effect.Blend || CMain.Time < effect.Start || (!(effect is Missile) && effect.Light < ob.Light)) continue;

                        light = effect.Light;

                        p = effect.DrawLocation;

                        var lightColour = effect.LightColour;

                        if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                        {
                            lightColour = GetBlindLight(lightColour);
                        }

                        if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                        {
                            p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                            DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                        }

                    }
                }

                if (Settings.Effect)
                {
                    for (int e = 0; e < Effects.Count; e++)
                    {
                        Effect effect = Effects[e];
                        if (!effect.Blend || CMain.Time < effect.Start) continue;

                        light = effect.Light;
                        if (light == 0) continue;

                        p = effect.DrawLocation;

                        var lightColour = Color.White;

                        if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                        {
                            lightColour = GetBlindLight(lightColour);
                        }

                        if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                        {
                            p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2), -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                            DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightColour);
                        }
                    }
                }

                for (int y = MapObject.User.Movement.Y - ViewRangeY - 24; y <= MapObject.User.Movement.Y + ViewRangeY + 24; y++)
                {
                    if (y < 0) continue;
                    if (y >= Height) break;
                    for (int x = MapObject.User.Movement.X - ViewRangeX - 24; x < MapObject.User.Movement.X + ViewRangeX + 24; x++)
                    {
                        if (x < 0) continue;
                        if (x >= Width) break;
                        int imageIndex = (M2CellInfo[x, y].FrontImage & 0x7FFF) - 1;
                        if (imageIndex == -1) continue;
                        int fileIndex = M2CellInfo[x, y].FrontIndex;
                        if (fileIndex == -1) continue;
                        if (M2CellInfo[x, y].Light <= 0 || M2CellInfo[x, y].Light >= 10) continue;
                        if (M2CellInfo[x, y].Light == 0) continue;

                        Color lightIntensity;

                        light = (M2CellInfo[x, y].Light % 10) * 3;

                        switch (M2CellInfo[x, y].Light / 10)
                        {
                            case 1:
                                lightIntensity = Color.FromArgb(255, 255, 255, 255);
                                break;
                            case 2:
                                lightIntensity = Color.FromArgb(255, 120, 180, 255);
                                break;
                            case 3:
                                lightIntensity = Color.FromArgb(255, 255, 180, 120);
                                break;
                            case 4:
                                lightIntensity = Color.FromArgb(255, 22, 160, 5);
                                break;
                            default:
                                lightIntensity = Color.FromArgb(255, 255, 255, 255);
                                break;
                        }

                        if (MapObject.User.Poison.HasFlag(PoisonType.Blindness))
                        {
                            lightIntensity = GetBlindLight(lightIntensity);
                        }

                        p = new Point(
                            (x + OffSetX - MapObject.User.Movement.X) * CellWidth + MapObject.User.OffSetMove.X,
                            (y + OffSetY - MapObject.User.Movement.Y) * CellHeight + MapObject.User.OffSetMove.Y + 32);

                        if (light >= DXManager.Lights.Count)
                            light = DXManager.Lights.Count - 1;

                        if (DXManager.Lights[light] != null && !DXManager.Lights[light].Disposed)
                        {
                            p.Offset(-(DXManager.LightSizes[light].X / 2) - (CellWidth / 2) + 10, -(DXManager.LightSizes[light].Y / 2) - (CellHeight / 2) - 5);
                            DXManager.Draw(DXManager.Lights[light], null, new Vector3((float)p.X, (float)p.Y, 0.0F), lightIntensity);
                        }
                    }
                }
            } // 关闭 if (DXManager.Lights.Count > 0)

            DXManager.SetBlend(false);
            DXManager.SetSurface(oldSurface);

            // —— 合成（照原版传奇）——
            // 光照 RT 此刻 = 暗度色 darkness + 加法叠加的发光体（火把/法术/地图灯）。
            // 原版用 D3D9 固定功能混合 SourceBlend=Zero, DestinationBlend=SourceColor，即
            // Final = 主画面 × 光照图，逐像素乘法压暗回主画面；这里用等价的 MirBlend.Multiply。
            var vpSize = DXManager.GDevice.Viewport;
            DXManager.Draw(DXManager.LightTexture, new Rectangle(0, 0, vpSize.Width, vpSize.Height), Vector3.Zero, Color.White, MirBlend.Multiply.ToBlendState());

            DXManager.Sprite.End();
            DXManager.Sprite.Begin(SpriteFlags.AlphaBlend);
            DXManager.RenderTransform = _savedRT;
        }

        private static void OnMouseClick(object sender, EventArgs e)
        {
            if (!(e is MouseEventArgs me)) return;

            if (MapObject.User != null)
                LogMove("Click button=" + me.Button + " 鼠标格=" + MapLocation + " 人物格=" + MapObject.User.CurrentLocation +
                    " MouseObject=" + (MapObject.MouseObject == null ? "null" : MapObject.MouseObject.Name));

            if (AwakeningAction == true) return;
            switch (me.Button)
            {
                case MouseButtons.Left:
                    {
                        AutoRun = false;
                        // 左键 = 按住不放持续走/跑（真正的连续移动在 CheckInput 的 MouseButtons.Left 分支里）。
                        // 这里不再对空地启动寻路（寻路是"到点即停"的有界移动，一次点击只走一小段，
                        // 会被误认为"按住走一会儿就停住了"）。点空地寻路保留给右键（见 MouseButtons.Right 分支）。
                        GameScene.Scene.MapControl.AutoPath = false;
                        if (MapObject.MouseObject == null)
                        {
                            // 但落点特效要照放：原版这个特效挂在"右键 + Settings.NewMove"的寻路分支里，
                            // 而 NewMove 默认 false，等于平时点地面完全没有反馈；网页版靠它确认"点到了哪一格"。
                            if (GameScene.Scene != null && GameScene.Scene.MapControl != null)
                                GameScene.Scene.MapControl.ShowClickEffect(MapLocation);
                            return;
                        }
                        NPCObject npc = MapObject.MouseObject as NPCObject;
                        if (npc != null)
                        {
                            if (npc.ObjectID == GameScene.NPCID &&
                                (CMain.Time <= GameScene.NPCTime || GameScene.Scene.NPCDialog.Visible))
                            {
                                return;
                            }

                            //GameScene.Scene.NPCDialog.Hide();

                            GameScene.NPCTime = CMain.Time + 5000;
                            GameScene.NPCID = npc.ObjectID;
                            Network.Enqueue(new C.CallNPC { ObjectID = npc.ObjectID, Key = "[@Main]" });
                        }
                    }
                    break;
                case MouseButtons.Right:
                    {
                        AutoRun = false;
                        if (MapObject.MouseObject == null)
                        {
                            if (GameScene.Scene != null && GameScene.Scene.MapControl != null)
                                GameScene.Scene.MapControl.TryAutoPath();
                            return;
                        }

                        if (CMain.Ctrl)
                        {
                            HeroObject hero = MapObject.MouseObject as HeroObject;

                            if (hero != null &&
                                hero.ObjectID != (Hero is null ? 0 : Hero.ObjectID) &&
                                CMain.Time >= GameScene.InspectTime)
                            {
                                GameScene.InspectTime = CMain.Time + 500;
                                InspectDialog.InspectID = hero.ObjectID;
                                Network.Enqueue(new C.Inspect { ObjectID = hero.ObjectID, Hero = true });
                                return;
                            }

                            PlayerObject player = MapObject.MouseObject as PlayerObject;

                            if (player != null &&
                                player != User &&
                                CMain.Time >= GameScene.InspectTime)
                            {
                                GameScene.InspectTime = CMain.Time + 500;
                                InspectDialog.InspectID = player.ObjectID;
                                Network.Enqueue(new C.Inspect { ObjectID = player.ObjectID });
                                return;
                            }
                        }
                    }
                    break;
                case MouseButtons.Middle:
                    AutoRun = !AutoRun;
                    break;
            }
        }

        private static void OnMouseDown(object sender, MouseEventArgs e)
        {
            // 立即同步一次坐标：点击（按下->抬起->Click）期间若中间没有 mousemove，
            // MouseLocation 会停留在旧位置，导致 MapLocation 算到别的格子上。
            MouseLocation = e.Location;

            // MouseButtons 现在是对齐 HTML MouseEvent.button 的顺序枚举（Left=0），不再是 WinForms 那种标志位枚举，
            // 因此不能再做 |= / &=~ 位运算（位运算在 Left=0 时根本置不上位）。直接记录当前按下的键即可。
            MapButtons = e.Button;
            if (e.Button != MouseButtons.Right || !Settings.NewMove)
                GameScene.CanRun = false;

            if (AwakeningAction == true) return;

            if (e.Button != MouseButtons.Left) return;

            if (GameScene.SelectedCell != null)
            {
                if (GameScene.SelectedCell.GridType != MirGridType.Inventory && GameScene.SelectedCell.GridType != MirGridType.HeroInventory)
                {
                    GameScene.SelectedCell = null;
                    return;
                }

                MirItemCell cell = GameScene.SelectedCell;
                if (cell.Item.Info.Bind.HasFlag(BindMode.DontDrop))
                {
                    MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.YouCannotDrop), cell.Item.FriendlyName), MirMessageBoxButtons.OK);
                    messageBox.Show();
                    GameScene.SelectedCell = null;
                    return;
                }
                if (cell.Item.Count == 1)
                {
                    MirMessageBox messageBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DropTip), cell.Item.FriendlyName), MirMessageBoxButtons.YesNo);

                    messageBox.YesButton.Click += (o, a) =>
                    {
                        Network.Enqueue(new C.DropItem
                        {
                            UniqueID = cell.Item.UniqueID,
                            Count = 1,
                            HeroInventory = cell.GridType == MirGridType.HeroInventory
                        });

                        cell.Locked = true;
                    };
                    messageBox.Show();
                }
                else
                {
                    MirAmountBox amountBox = new MirAmountBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DropAmount), cell.Item.Info.Image, cell.Item.Count);

                    amountBox.OKButton.Click += (o, a) =>
                    {
                        if (amountBox.Amount <= 0) return;
                        Network.Enqueue(new C.DropItem
                        {
                            UniqueID = cell.Item.UniqueID,
                            Count = (ushort)amountBox.Amount,
                            HeroInventory = cell.GridType == MirGridType.HeroInventory
                        });

                        cell.Locked = true;
                    };

                    amountBox.Show();
                }
                GameScene.SelectedCell = null;

                return;
            }

            if (GameScene.PickedUpGold)
            {
                MirAmountBox amountBox = new MirAmountBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DropAmount), 116, GameScene.Gold);

                amountBox.OKButton.Click += (o, a) =>
                {
                    if (amountBox.Amount > 0)
                    {
                        Network.Enqueue(new C.DropGold { Amount = amountBox.Amount });
                    }
                };

                amountBox.Show();
                GameScene.PickedUpGold = false;
            }

            if (MapObject.MouseObject != null && !MapObject.MouseObject.Dead && !(MapObject.MouseObject is ItemObject) &&
                !(MapObject.MouseObject is NPCObject) && !(MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 64)
                 && !(MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 70))
            {
                MapObject.TargetObjectID = MapObject.MouseObject.ObjectID;
                if (MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI != 6)
                    MapObject.MagicObjectID = MapObject.TargetObject.ObjectID;
            }
            else
                MapObject.TargetObjectID = 0;
        }

        private void CheckInput()
        {
            if (AwakeningAction == true) return;

            if ((MouseControl == this) && (MapButtons != MouseButtons.None)) AutoHit = false;//mouse actions stop mining even when frozen!
            if (!CanRideAttack()) AutoHit = false;

            if (CMain.Time < InputDelay || User.Poison.HasFlag(PoisonType.Paralysis) || User.Poison.HasFlag(PoisonType.LRParalysis) || User.Poison.HasFlag(PoisonType.Frozen) || User.Fishing) return;

            if (User.NextMagic != null && !User.RidingMount)
            {
                UseMagic(User.NextMagic, User);
                return;
            }

            if (CMain.Time < User.BlizzardStopTime || CMain.Time < User.ReincarnationStopTime) return;

            if (MapObject.TargetObject != null && !MapObject.TargetObject.Dead)
            {
                if (((MapObject.TargetObject.Name.EndsWith(")") || MapObject.TargetObject is PlayerObject) && CMain.Shift) ||
                    (!MapObject.TargetObject.Name.EndsWith(")") && MapObject.TargetObject is MonsterObject))
                {
                    GameScene.LogTime = CMain.Time + Globals.LogDelay;

                    if (User.Class == MirClass.Archer && User.HasClassWeapon && !User.RidingMount && !User.Fishing)//ArcherTest - non aggressive targets (player / pets)
                    {
                        if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                        {
                            if (CMain.Time > GameScene.AttackTime)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.AttackRange1, Direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation), Location = User.CurrentLocation, Params = new List<object>() };
                                User.QueuedAction.Params.Add(MapObject.TargetObject != null ? MapObject.TargetObject.ObjectID : (uint)0);
                                User.QueuedAction.Params.Add(MapObject.TargetObject.CurrentLocation);

                                // MapObject.TargetObject = null; //stop constant attack when close up
                            }
                        }
                        else
                        {
                            if (CMain.Time >= OutputDelay)
                            {
                                OutputDelay = CMain.Time + 1000;
                                GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TargetTooFar));
                            }
                        }
                        //  return;
                    }

                    else if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, 1))
                    {
                        if (CMain.Time > GameScene.AttackTime && CanRideAttack() && !User.Poison.HasFlag(PoisonType.Dazed))
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Attack1, Direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation), Location = User.CurrentLocation };
                            return;
                        }
                    }
                }
            }
            if (AutoHit && !User.RidingMount)
            {
                if (CMain.Time > GameScene.AttackTime)
                {
                    User.QueuedAction = new QueuedAction { Action = MirAction.Mine, Direction = User.Direction, Location = User.CurrentLocation };
                    return;
                }
            }


            MirDirection direction;
            if (MouseControl == this)
            {
                direction = MouseDirection();
                if (AutoRun)
                {
                    if (GameScene.CanRun && CanRun(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint))) //slow remove
                    {
                        int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                        bool fail = false;
                        for (int i = 1; i <= distance; i++)
                        {
                            if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i)))
                                fail = true;
                        }
                        if (!fail)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, distance) };
                            return;
                        }
                    }
                    if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                        return;
                    }
                    if (direction != User.Direction)
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.Standing, Direction = direction, Location = User.CurrentLocation };
                        return;
                    }
                    return;
                }

                switch (MapButtons)
                {
                    case MouseButtons.Left:
                        if (MapObject.MouseObject is NPCObject || (MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User)) break;
                        if (MapObject.MouseObject is MonsterObject && MapObject.MouseObject.AI == 70) break;

                        if (CMain.Alt && !User.RidingMount)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Harvest, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }

                        if (CMain.Shift)
                        {
                            if (CMain.Time > GameScene.AttackTime && CanRideAttack()) //ArcherTest - shift click
                            {
                                MapObject target = null;
                                if (MapObject.MouseObject is MonsterObject || MapObject.MouseObject is PlayerObject) target = MapObject.MouseObject;

                                if (User.Class == MirClass.Archer && User.HasClassWeapon && !User.RidingMount && !User.Poison.HasFlag(PoisonType.Dazed))
                                {
                                    if (target != null)
                                    {
                                        if (!Functions.InRange(MapObject.MouseObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                                        {
                                            if (CMain.Time >= OutputDelay)
                                            {
                                                OutputDelay = CMain.Time + 1000;
                                                GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TargetTooFar));
                                            }
                                            return;
                                        }
                                    }

                                    User.QueuedAction = new QueuedAction { Action = MirAction.AttackRange1, Direction = MouseDirection(), Location = User.CurrentLocation, Params = new List<object>() };
                                    User.QueuedAction.Params.Add(target != null ? target.ObjectID : (uint)0);
                                    User.QueuedAction.Params.Add(Functions.PointMove(User.CurrentLocation, MouseDirection(), 9));
                                    return;
                                }

                                //stops double slash from being used without empty hand or assassin weapon (otherwise bugs on second swing)
                                if (GameScene.User.DoubleSlash && (!User.HasClassWeapon && User.Weapon > -1)) return;
                                if (User.Poison.HasFlag(PoisonType.Dazed)) return;

                                User.QueuedAction = new QueuedAction { Action = MirAction.Attack1, Direction = direction, Location = User.CurrentLocation };
                            }
                            return;
                        }

                        if (MapObject.MouseObject is MonsterObject && User.Class == MirClass.Archer && MapObject.TargetObject != null && !MapObject.TargetObject.Dead && User.HasClassWeapon && !User.RidingMount) //ArcherTest - range attack
                        {
                            if (Functions.InRange(MapObject.MouseObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                            {
                                if (CMain.Time > GameScene.AttackTime)
                                {
                                    User.QueuedAction = new QueuedAction { Action = MirAction.AttackRange1, Direction = direction, Location = User.CurrentLocation, Params = new List<object>() };
                                    User.QueuedAction.Params.Add(MapObject.TargetObject.ObjectID);
                                    User.QueuedAction.Params.Add(MapObject.TargetObject.CurrentLocation);
                                }
                            }
                            else
                            {
                                if (CMain.Time >= OutputDelay)
                                {
                                    OutputDelay = CMain.Time + 1000;
                                    GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TargetTooFar));
                                }
                            }
                            return;
                        }

                        if (MapLocation == User.CurrentLocation)
                        {
                            if (CMain.Time > GameScene.PickUpTime)
                            {
                                GameScene.PickUpTime = CMain.Time + 200;
                                Network.Enqueue(new C.PickUp());
                            }
                            return;
                        }

                        //mine
                        if (!ValidPoint(Functions.PointMove(User.CurrentLocation, direction, 1)))
                        {
                            if ((MapObject.User.Equipment[(int)EquipmentSlot.Weapon] != null) && (MapObject.User.Equipment[(int)EquipmentSlot.Weapon].Info.CanMine))
                            {
                                if (direction != User.Direction)
                                {
                                    User.QueuedAction = new QueuedAction { Action = MirAction.Standing, Direction = direction, Location = User.CurrentLocation };
                                    return;
                                }
                                AutoHit = true;
                                return;
                            }
                        }
                        // 新移动方式（Settings.NewMove）下右键不再负责跑，跑改由左键按住触发：
                        // 第一步先走，随后 GameScene.CanRun 变真（PlayerObject 的 Walking 分支会置真），
                        // 于是"按住左键 = 走两步后自动变跑"，和常见网页版传奇一致。旧移动方式下保持原样（跑在右键）。
                        if (Settings.NewMove)
                        {
                            GameScene.CanRun = User.FastRun ? true : GameScene.CanRun;

                            if (GameScene.CanRun && CanRun(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint)))
                            {
                                int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                                bool fail = false;
                                for (int i = 0; i <= distance; i++)
                                {
                                    if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i)))
                                        fail = true;
                                }
                                if (!fail)
                                {
                                    User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, distance) };
                                    return;
                                }
                            }
                        }
                        if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                        {

                            User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                            return;
                        }
                        if (direction != User.Direction)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Standing, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }

                        if (CanFish(direction))
                        {
                            User.FishingTime = CMain.Time;
                            Network.Enqueue(new C.FishingCast { CastOut = true });
                            return;
                        }

                        break;
                    case MouseButtons.Right:
                        if (MapObject.MouseObject is PlayerObject && MapObject.MouseObject != User && CMain.Ctrl) break;
                        if (Settings.NewMove) break;

                        if (Functions.InRange(MapLocation, User.CurrentLocation, 2))
                        {
                            if (direction != User.Direction)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.Standing, Direction = direction, Location = User.CurrentLocation };
                            }
                            return;
                        }

                        GameScene.CanRun = User.FastRun ? true : GameScene.CanRun;

                        if (GameScene.CanRun && CanRun(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint))) //slow removed
                        {
                            int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                            bool fail = false;
                            for (int i = 0; i <= distance; i++)
                            {
                                if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, i)))
                                    fail = true;
                            }
                            if (!fail)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, User.RidingMount || (User.Sprint && !User.Sneaking) ? 3 : 2) };
                                return;
                            }
                        }
                        if ((CanWalk(direction, out direction)) && (CheckDoorOpen(Functions.PointMove(User.CurrentLocation, direction, 1))))
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
                            return;
                        }
                        if (direction != User.Direction)
                        {
                            User.QueuedAction = new QueuedAction { Action = MirAction.Standing, Direction = direction, Location = User.CurrentLocation };
                            return;
                        }
                        break;
                }
            }

            if (AutoPath)
            {
                if (CurrentPath == null || CurrentPath.Count == 0)
                {
                    AutoPath = false;
                    return;
                }

                var path = GameScene.Scene.MapControl.PathFinder.FindPath(MapObject.User.CurrentLocation, CurrentPath.Last().Location);

                if (path != null && path.Count > 0)
                    GameScene.Scene.MapControl.CurrentPath = path;
                else
                {
                    AutoPath = false;
                    return;
                }

                Node currentNode = CurrentPath.SingleOrDefault(x => User.CurrentLocation == x.Location);
                if (currentNode != null)
                {
                    while (true)
                    {
                        Node first = CurrentPath.First();
                        CurrentPath.Remove(first);

                        if (first == currentNode)
                            break;
                    }
                }

                if (CurrentPath.Count > 0)
                {
                    MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, CurrentPath.First().Location);

                    if (GameScene.CanRun && CanRun(dir) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && CurrentPath.Count > (User.RidingMount ? 2 : 1))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, User.RidingMount ? 3 : 2) };
                        return;
                    }
                    if (CanWalk(dir))
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, 1) };

                        return;
                    }
                }
            }

            if (MapObject.TargetObject == null || MapObject.TargetObject.Dead) return;
            if (((!MapObject.TargetObject.Name.EndsWith(")") && !(MapObject.TargetObject is PlayerObject)) || !CMain.Shift) &&
                (MapObject.TargetObject.Name.EndsWith(")") || !(MapObject.TargetObject is MonsterObject))) return;
            if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, 1)) return;
            if (User.Class == MirClass.Archer && User.HasClassWeapon && (MapObject.TargetObject is MonsterObject || MapObject.TargetObject is PlayerObject)) return; //ArcherTest - stop walking
            direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation);

            if (!CanWalk(direction, out direction)) return;

            User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
        }

        public void UseMagic(ClientMagic magic, UserObject actor)
        {
            if (CMain.Time < GameScene.SpellTime || actor.Poison.HasFlag(PoisonType.Stun))
            {
                actor.ClearMagic();
                return;
            }

            if ((CMain.Time <= magic.CastTime + magic.Delay))
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CannotCastSpellSeconds), magic.Name,
                        ((magic.CastTime + magic.Delay) - CMain.Time - 1) / 1000 + 1));
                }

                actor.ClearMagic();
                return;
            }

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;

            if (magic.Spell == Spell.Teleport || magic.Spell == Spell.Blink || magic.Spell == Spell.StormEscape)
            {
                if (actor.Stats[Stat.TeleportManaPenaltyPercent] > 0)
                {
                    cost += (cost * actor.Stats[Stat.TeleportManaPenaltyPercent]) / 100;
                }
            }

            if (actor.Stats[Stat.ManaPenaltyPercent] > 0)
            {
                cost += (cost * actor.Stats[Stat.ManaPenaltyPercent]) / 100;
            }

            if (cost > actor.MP)
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LowMana));
                }
                actor.ClearMagic();
                return;
            }

            //bool isTargetSpell = true;

            MapObject target = null;

            //Targeting
            switch (magic.Spell)
            {
                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.ElectricShock:
                case Spell.Poisoning:
                case Spell.ThunderBolt:
                case Spell.FlameDisruptor:
                case Spell.SoulFireBall:
                case Spell.TurnUndead:
                case Spell.FrostCrunch:
                case Spell.Vampirism:
                case Spell.Revelation:
                case Spell.Entrapment:
                case Spell.Hallucination:
                case Spell.DarkBody:
                case Spell.FireBounce:
                case Spell.MeteorShower:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = MapObject.MagicObject;

                    if (target != null && target.Race == ObjectType.Monster) MapObject.MagicObjectID = target.ObjectID;
                    break;
                case Spell.StraightShot:
                case Spell.DoubleShot:
                case Spell.ElementalShot:
                case Spell.DelayedExplosion:
                case Spell.BindingShot:
                case Spell.VampireShot:
                case Spell.PoisonShot:
                case Spell.CrippleShot:
                case Spell.NapalmShot:
                case Spell.SummonVampire:
                case Spell.SummonToad:
                case Spell.SummonSnakes:
                    if (!actor.HasClassWeapon)
                    {
                        GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.MustWearBowForSkill));
                        actor.ClearMagic();
                        return;
                    }
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = MapObject.MagicObject;

                    if (target != null && target.Race == ObjectType.Monster) MapObject.MagicObjectID = target.ObjectID;
                    break;
                case Spell.Stonetrap:
                    if (!User.HasClassWeapon)
                    {
                        GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.MustWearBowForSkill));
                        User.ClearMagic();
                        return;
                    }
                    if (User.NextMagicObject != null)
                    {
                        if (!User.NextMagicObject.Dead && User.NextMagicObject.Race != ObjectType.Item && User.NextMagicObject.Race != ObjectType.Merchant)
                            target = User.NextMagicObject;
                    }

                    //if(magic.Spell == Spell.ElementalShot)
                    //{
                    //    isTargetSpell = User.HasElements;
                    //}

                    //switch(magic.Spell)
                    //{
                    //    case Spell.SummonVampire:
                    //    case Spell.SummonToad:
                    //    case Spell.SummonSnakes:
                    //        isTargetSpell = false;
                    //        break;
                    //}

                    break;
                case Spell.Purification:
                case Spell.Healing:
                case Spell.UltimateEnhancer:
                case Spell.EnergyShield:
                case Spell.PetEnhancer:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant && !(actor.NextMagicObject is MonsterObject monster && monster.MasterObjectId == 0))
                            target = actor.NextMagicObject;
                    }

                    if (target == null) target = User;
                    break;
                case Spell.FireBang:
                case Spell.MassHiding:
                case Spell.FireWall:
                case Spell.TrapHexagon:
                case Spell.HealingCircle:
                case Spell.CatTongue:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.PoisonCloud:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.Blizzard:
                case Spell.MeteorStrike:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && actor.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.Reincarnation:
                    if (actor == Hero && actor.NextMagicObject == null)
                        actor.NextMagicObject = User;
                    if (actor.NextMagicObject != null)
                    {
                        if (actor.NextMagicObject.Dead && actor.NextMagicObject.Race == ObjectType.Player)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.Trap:
                    if (actor.NextMagicObject != null)
                    {
                        if (!actor.NextMagicObject.Dead && User.NextMagicObject.Race != ObjectType.Item && actor.NextMagicObject.Race != ObjectType.Merchant)
                            target = actor.NextMagicObject;
                    }
                    break;
                case Spell.FlashDash:
                    if (actor.GetMagic(Spell.FlashDash).Level <= 1 && actor.IsDashAttack() == false)
                    {
                        actor.ClearMagic();
                        return;
                    }
                    //isTargetSpell = false;
                    break;
                default:
                    //isTargetSpell = false;
                    break;
            }

            MirDirection dir = (target == null || target == User) ? actor.NextMagicDirection : Functions.DirectionFromPoint(actor.CurrentLocation, target.CurrentLocation);

            Point location = target != null ? target.CurrentLocation : actor.NextMagicLocation;

            uint targetID = target != null ? target.ObjectID : 0;

            if (magic.Spell == Spell.FlashDash)
                dir = actor.Direction;

            if ((magic.Range != 0) && (!Functions.InRange(actor.CurrentLocation, location, magic.Range)))
            {
                if (CMain.Time >= OutputDelay)
                {
                    OutputDelay = CMain.Time + 1000;
                    GameScene.Scene.OutputMessage(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TargetTooFar));
                }
                actor.ClearMagic();
                return;
            }

            GameScene.LogTime = CMain.Time + Globals.LogDelay;

            if (actor == User)
            {
                User.QueuedAction = new QueuedAction { Action = MirAction.Spell, Direction = dir, Location = User.CurrentLocation, Params = new List<object>() };
                User.QueuedAction.Params.Add(magic.Spell);
                User.QueuedAction.Params.Add(targetID);
                User.QueuedAction.Params.Add(location);
                User.QueuedAction.Params.Add(magic.Level);
            }
            else
            {
                Network.Enqueue(new C.Magic { ObjectID = actor.ObjectID, Spell = magic.Spell, Direction = dir, TargetID = targetID, Location = location, SpellTargetLock = CMain.SpellTargetLock });
            }
        }

        public static MirDirection MouseDirection(float ratio = 45F) //22.5 = 16
        {
            Point p = new Point(MouseLocation.X / CellWidth, MouseLocation.Y / CellHeight);
            if (Functions.InRange(new Point(OffSetX, OffSetY), p, 2))
                return Functions.DirectionFromPoint(new Point(OffSetX, OffSetY), p);

            PointF c = new PointF(OffSetX * CellWidth + CellWidth / 2F, OffSetY * CellHeight + CellHeight / 2F);
            PointF a = new PointF(c.X, 0);
            PointF b = MouseLocation;
            float bc = (float)Distance(c, b);
            float ac = bc;
            b.Y -= c.Y;
            c.Y += bc;
            b.Y += bc;
            float ab = (float)Distance(b, a);
            double x = (ac * ac + bc * bc - ab * ab) / (2 * ac * bc);
            double angle = Math.Acos(x);

            angle *= 180 / Math.PI;

            if (MouseLocation.X < c.X) angle = 360 - angle;
            angle += ratio / 2;
            if (angle > 360) angle -= 360;

            return (MirDirection)(angle / ratio);
        }

        public static int Direction16(Point source, Point destination)
        {
            PointF c = new PointF(source.X, source.Y);
            PointF a = new PointF(c.X, 0);
            PointF b = new PointF(destination.X, destination.Y);
            float bc = (float)Distance(c, b);
            float ac = bc;
            b.Y -= c.Y;
            c.Y += bc;
            b.Y += bc;
            float ab = (float)Distance(b, a);
            double x = (ac * ac + bc * bc - ab * ab) / (2 * ac * bc);
            double angle = Math.Acos(x);

            angle *= 180 / Math.PI;

            if (destination.X < c.X) angle = 360 - angle;
            angle += 11.25F;
            if (angle > 360) angle -= 360;

            return (int)(angle / 22.5F);
        }

        public static double Distance(PointF p1, PointF p2)
        {
            double x = p2.X - p1.X;
            double y = p2.Y - p1.Y;
            return Math.Sqrt(x * x + y * y);
        }

        public bool EmptyCell(Point p)
        {
            if (M2CellInfo == null) return false;
            if ((M2CellInfo[p.X, p.Y].BackImage & 0x20000000) != 0 || (M2CellInfo[p.X, p.Y].FrontImage & 0x8000) != 0)
                return false;

            foreach (var ob in Objects.Values)
                if (ob.CurrentLocation == p && ob.Blocking)
                    return false;

            return true;
        }


        private bool CanWalk(MirDirection dir)
        {
            return EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)) && !User.InTrapRock;
        }

        private bool CanWalk(MirDirection dir, out MirDirection outDir)
        {
            outDir = dir;
            if (User.InTrapRock) return false;

            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
                return true;

            dir = Functions.NextDir(outDir);
            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
            {
                outDir = dir;
                return true;
            }

            dir = Functions.PreviousDir(outDir);
            if (EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1)))
            {
                outDir = dir;
                return true;
            }

            return false;
        }

        private bool CheckDoorOpen(Point p)
        {
            if (M2CellInfo == null) return false;
            if (M2CellInfo[p.X, p.Y].DoorIndex == 0) return true;
            Door DoorInfo = GetDoor(M2CellInfo[p.X, p.Y].DoorIndex);
            if (DoorInfo == null) return false;//if the door doesnt exist then it isnt even being shown on screen (and cant be open lol)
            if ((DoorInfo.DoorState == DoorState.Closed) || (DoorInfo.DoorState == DoorState.Closing))
            {
                if (CMain.Time > _doorTime)
                {
                    _doorTime = CMain.Time + 4000;
                    Network.Enqueue(new C.Opendoor() { DoorIndex = DoorInfo.index });
                }

                return false;
            }
            if ((DoorInfo.DoorState == DoorState.Open) && (DoorInfo.LastTick + 4000 > CMain.Time))
            {
                if (CMain.Time > _doorTime)
                {
                    _doorTime = CMain.Time + 4000;
                    Network.Enqueue(new C.Opendoor() { DoorIndex = DoorInfo.index });
                }
            }
            return true;
        }

        private long _doorTime = 0;


        private bool CanRun(MirDirection dir)
        {
            if (User.InTrapRock) return false;
            if (User.CurrentBagWeight > User.Stats[Stat.BagWeight]) return false;
            if (User.CurrentWearWeight > User.Stats[Stat.BagWeight]) return false;
            if (CanWalk(dir) && EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 2)))
            {
                if (User.RidingMount || User.Sprint && !User.Sneaking)
                {
                    return EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 3));
                }

                return true;
            }

            return false;
        }

        private bool CanRideAttack()
        {
            if (GameScene.User.RidingMount)
            {
                UserItem item = GameScene.User.Equipment[(int)EquipmentSlot.Mount];
                if (item == null || item.Slots.Length < 4 || item.Slots[(int)MountSlot.Bells] == null) return false;
            }

            return true;
        }

        public bool CanFish(MirDirection dir)
        {
            if (!GameScene.User.HasFishingRod || GameScene.User.FishingTime + 1000 > CMain.Time) return false;
            if (GameScene.User.CurrentAction != MirAction.Standing) return false;
            if (GameScene.User.Direction != dir) return false;
            if (GameScene.User.TransformType >= 6 && GameScene.User.TransformType <= 9) return false;

            Point point = Functions.PointMove(User.CurrentLocation, dir, 3);

            if (M2CellInfo == null || !M2CellInfo[point.X, point.Y].FishingCell) return false;

            return true;
        }

        public bool CanFly(Point target)
        {
            Point location = User.CurrentLocation;
            while (location != target)
            {
                MirDirection dir = Functions.DirectionFromPoint(location, target);

                location = Functions.PointMove(location, dir, 1);

                if (location.X < 0 || location.Y < 0 || location.X >= GameScene.Scene.MapControl.Width || location.Y >= GameScene.Scene.MapControl.Height) return false;

                if (!GameScene.Scene.MapControl.ValidPoint(location)) return false;
            }

            return true;
        }


        public bool ValidPoint(Point p)
        {
            if (M2CellInfo == null) return false;
            //GameScene.Scene.ChatDialog.ReceiveChat(string.Format("cell: {0}", (M2CellInfo[p.X, p.Y].BackImage & 0x20000000)), ChatType.System);
            return (M2CellInfo[p.X, p.Y].BackImage & 0x20000000) == 0;
        }
        public bool HasTarget(Point p)
        {
            foreach (var ob in Objects.Values)
                if (ob.CurrentLocation == p && ob.Blocking)
                    return true;
            return false;
        }

        public bool CanHalfMoon(Point p, MirDirection d)
        {
            d = Functions.PreviousDir(d);
            for (int i = 0; i < 4; i++)
            {
                if (HasTarget(Functions.PointMove(p, d, 1))) return true;
                d = Functions.NextDir(d);
            }
            return false;
        }
        public bool CanCrossHalfMoon(Point p)
        {
            MirDirection dir = MirDirection.Up;
            for (int i = 0; i < 8; i++)
            {
                if (HasTarget(Functions.PointMove(p, dir, 1))) return true;
                dir = Functions.NextDir(dir);
            }
            return false;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Objects.Clear();

                MapButtons = MouseButtons.None;
                MouseLocation = Point.Empty;
                InputDelay = 0;
                NextAction = 0;

                M2CellInfo = null;
                Width = 0;
                Height = 0;

                Index = 0;
                FileName = String.Empty;
                Title = String.Empty;
                MiniMap = 0;
                BigMap = 0;
                Lights = 0;
                FloorValid = false;
                LightsValid = false;
                MapDarkLight = 0;
                Music = 0;

                AnimationCount = 0;
                Effects.Clear();
            }

            base.Dispose(disposing);
        }


        public void UpdateWeather()
        {
            for (int i = GameScene.Scene.ParticleEngines.Count - 1; i > 0; i--)
                GameScene.Scene.ParticleEngines[i].Dispose();

            GameScene.Scene.ParticleEngines.Clear();
            List<ParticleImageInfo> textures = new List<ParticleImageInfo>();
            foreach (WeatherSetting itemWeather in Enum.GetValues(typeof(WeatherSetting)).Cast<object>().ToArray())
            {
                //if not enabled skip
                if ((Weather & itemWeather) != itemWeather)
                    continue;

                //foreach (WeatherSetting itemWeather in Weather)
                //{
                switch (itemWeather)
                {
                    case WeatherSetting.Leaves:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 170, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 55, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 587, 200, 50));


                        ParticleEngine LeavesEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.Leaves);
                        Vector2 lVelocity = new Vector2(0F, 0F);
                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = LeavesEngine2.GenerateNewParticle(ParticleType.Leaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = lVelocity;
                            }
                        LeavesEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(LeavesEngine2);
                        break;
                    case WeatherSetting.FireyLeaves:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 170, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 55, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 587, 200, 50));


                        ParticleEngine FLeavesEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.FireyLeaves);
                        Vector2 FlVelocity = new Vector2(0F, 0F);
                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = FLeavesEngine2.GenerateNewParticle(ParticleType.FireyLeaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = FlVelocity;
                            }
                        FLeavesEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(FLeavesEngine2);
                        break;
                    case WeatherSetting.Rain:
                        textures = new List<ParticleImageInfo>();
                        //Rain
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 164, 150, 50));


                        ParticleEngine RainEngine2 = new ParticleEngine(textures, new Vector2(2f, 0), ParticleType.Rain);
                        Vector2 rsevelocity = new Vector2(0F, 0F);
                        var xVar = 512;
                        var yVar = 512;
                        for (int y = yVar * -1; y < Settings.ScreenHeight + yVar; y += yVar)
                            for (int x = xVar * -1; x < Settings.ScreenWidth + xVar; x += xVar)
                            {
                                Particle part = RainEngine2.GenerateNewParticle(ParticleType.Rain);
                                part.Position = new Vector2(x, y);
                                part.Velocity = rsevelocity;
                            }
                        RainEngine2.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(RainEngine2);
                        break;

                    case WeatherSetting.Snow:
                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 43, 20, 50));

                        ParticleEngine RainEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.Snow);
                        Vector2 rsvelocity = new Vector2(1F, -1F);

                        for (int y = -400; y < Settings.ScreenHeight + 400; y += 400)
                            for (int x = -400; x < Settings.ScreenWidth + 400; x += 400)
                            {
                                Particle part = RainEngine.GenerateNewParticle(ParticleType.Snow);
                                part.Position = new Vector2(x, y);
                                part.Velocity = rsvelocity;
                            }
                        RainEngine.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(RainEngine);

                        break;
                    case WeatherSetting.Fog:
                        List<ParticleImageInfo> ftextures = new List<ParticleImageInfo>();
                        ftextures.Add(new ParticleImageInfo(Libraries.Weather, 0));
                        ParticleEngine fengine = new ParticleEngine(ftextures, new Vector2(0, 0), ParticleType.Fog);
                        fengine.UpdateDelay = TimeSpan.FromMilliseconds(20);

                        Vector2 fvelocity = new Vector2(2F, -2F);
                        for (int y = -512; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = -512; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = fengine.GenerateNewParticle(ParticleType.Fog);
                                part.Position = new Vector2(x, y);
                                part.Velocity = fvelocity;
                            }


                        fengine.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(fengine);
                        break;
                    case WeatherSetting.RedEmber:
                        var rtextures = new List<ParticleImageInfo>();
                        rtextures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 150));

                        var rengine = new ParticleEngine(rtextures, new Vector2(0, 0), ParticleType.RedFogEmber);
                        GameScene.Scene.ParticleEngines.Add(rengine);
                        break;
                    case WeatherSetting.WhiteEmber:

                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 150));
                        var whiteEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.WhiteEmber);
                        GameScene.Scene.ParticleEngines.Add(whiteEmberEngine);
                        break;
                    case WeatherSetting.PurpleLeaves:

                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 359, 170, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 531, 55, 50));
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 587, 200, 50));
                        //textures.Add(new ParticleImageInfo(Libraries.Weather, 10, 20, 50));

                        var pEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.PurpleLeaves);

                        for (int y = 512 * -1; y < Settings.ScreenHeight + 512; y += 512)
                            for (int x = 512 * -1; x < Settings.ScreenWidth + 512; x += 512)
                            {
                                Particle part = pEmberEngine.GenerateNewParticle(ParticleType.PurpleLeaves);
                                part.Position = new Vector2(x, y);
                                part.Velocity = new Vector2(0, 0);
                            }
                        pEmberEngine.GenerateParticles = false;
                        GameScene.Scene.ParticleEngines.Add(pEmberEngine);
                        break;

                    case WeatherSetting.YellowEmber:

                        textures = new List<ParticleImageInfo>();
                        textures.Add(new ParticleImageInfo(Libraries.Weather, 1, 9, 100));

                        var yellowEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.YellowEmber);
                        GameScene.Scene.ParticleEngines.Add(yellowEmberEngine);
                        break;
                    case WeatherSetting.FireParticle:

                        textures = new List<ParticleImageInfo>();
                        //textures.Add(new ParticleImageInfo(Libraries.StateEffect, 640)); << TODO - Win
                        //   textures.Add(new ParticleImageInfo(Libraries.Prguse4, 642));
                        var fEmberEngine = new ParticleEngine(textures, new Vector2(0, 0), ParticleType.Bird);
                        GameScene.Scene.ParticleEngines.Add(fEmberEngine);
                        break;


                }

            }

        }

        private bool InMapBounds(int x, int y)
        {
            return M2CellInfo != null && x >= 0 && y >= 0 && x < M2CellInfo.GetLength(0) && y < M2CellInfo.GetLength(1);
        }

        public void RemoveObject(MapObject ob)
        {
            if (M2CellInfo == null)
            {
                _pendingAdd.Remove(ob);
                return;
            }
            if (!InMapBounds(ob.MapLocation.X, ob.MapLocation.Y)) return;
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].RemoveObject(ob);
        }
        public void AddObject(MapObject ob)
        {
            if (M2CellInfo == null)
            {
                // 异步加载期间同一 ObjectID 的包可能多次到达（重连/重发）。此刻对象尚未进入
                // MapControl.Objects 字典，MapObject 构造函数的去重（按 Objects 字典命中）不会触发，
                // 直接缓冲会累积多份，待 LoadMapAsync 回放时全部加入地图 → NPC 重影且跟随玩家移动。
                // 故在缓冲阶段即按 ObjectID 去重，只保留最后一次，对齐原版同步加载下的“单实例”语义。
                for (int i = _pendingAdd.Count - 1; i >= 0; i--)
                    if (_pendingAdd[i].ObjectID == ob.ObjectID)
                        _pendingAdd.RemoveAt(i);
                _pendingAdd.Add(ob);
                return;
            }
            if (!InMapBounds(ob.MapLocation.X, ob.MapLocation.Y)) return;
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].AddObject(ob);
        }
        public MapObject FindObject(uint ObjectID, int x, int y)
        {
            if (!InMapBounds(x, y)) return null;
            return M2CellInfo[x, y].FindObject(ObjectID);
        }
        public void SortObject(MapObject ob)
        {
            if (M2CellInfo == null) return;
            if (!InMapBounds(ob.MapLocation.X, ob.MapLocation.Y)) return;
            M2CellInfo[ob.MapLocation.X, ob.MapLocation.Y].Sort();
        }

        public Door GetDoor(byte Index)
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                if (Doors[i].index == Index)
                    return Doors[i];
            }
            return null;
        }
        public void Processdoors()
        {
            for (int i = 0; i < Doors.Count; i++)
            {
                if ((Doors[i].DoorState == DoorState.Opening) || (Doors[i].DoorState == DoorState.Closing))
                {
                    if (Doors[i].LastTick + 50 < CMain.Time)
                    {
                        Doors[i].LastTick = CMain.Time;
                        Doors[i].ImageIndex++;

                        if (Doors[i].ImageIndex == 1)//change the 1 if you want to animate doors opening/closing
                        {
                            Doors[i].ImageIndex = 0;
                            Doors[i].DoorState = (DoorState)Enum.ToObject(typeof(DoorState), ((byte)++Doors[i].DoorState % 4));
                        }

                        FloorValid = false;
                    }
                }
                if (Doors[i].DoorState == DoorState.Open)
                {
                    if (Doors[i].LastTick + 5000 < CMain.Time)
                    {
                        Doors[i].LastTick = CMain.Time;
                        Doors[i].DoorState = DoorState.Closing;
                        FloorValid = false;
                    }
                }
            }
        }
        public void OpenDoor(byte Index, bool closed)
        {
            Door Info = GetDoor(Index);
            if (Info == null) return;
            Info.DoorState = (closed ? DoorState.Closing : Info.DoorState == DoorState.Open ? DoorState.Open : DoorState.Opening);
            Info.ImageIndex = 0;
            Info.LastTick = CMain.Time;
        }
    }
}
