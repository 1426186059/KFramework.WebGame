using Client;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;


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

    /// <summary>选中目标脚下显示的脚底选中环（动画）。
    /// 真实资源 = 原始传奇 CDN 的脚底特效图集：model 15033 / action 1 / dir 4 → 15033014.png（12 帧脉动动画），
    /// 网页版选中 NPC / 怪物 / 玩家通用同一套脚底环。图集未就绪（加载中或缺失）时回落到程序化红环。</summary>
    private const int RingModel = 15033;                  // 脚底选中环模型号
    private const int RingAction = 1;                     // stand（与网页版 15033014 一致）
    private const int RingDir = 4;                        // 该图集仅有 dir4 一帧方向集
    private const int SelectionRingFrameMs = 90;          // 每帧时长(ms)，12 帧 ≈ 1.08s 循环
    private static bool _ringRequested;
    private static KFramework.MonoGame.Texture2D _proceduralRingTex;

    /// <summary>程序化生成的白色圆环贴图，作为 CDN 图集未就绪时的回落。</summary>
    private static KFramework.MonoGame.Texture2D ProceduralSelectionRingTexture
    {
        get
        {
            if (_proceduralRingTex == null)
                _proceduralRingTex = CreateSelectionRingTexture();
            return _proceduralRingTex;
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

    /// <summary>在指定对象脚底绘制脚底选中环动画（不随 HighlightTarget 开关，选中即显示）。
    /// 优先播放 CDN 图集 12 帧脉动动画（环心居中脚底、按帧原比例缩放），否则回落程序化红环。</summary>
    private void DrawSelectionRing(MapObject obj)
    {
        if (obj == null || obj.Dead) return;

        // 脚底世界屏幕坐标：引擎的 DrawLocation 即对象「模型参考点/脚底基准」（水平中心 X、脚底 Y），
        // 直接用，不要再 +CellWidth/2 / +CellHeight（会把环往右下拉偏）。
        int footX = obj.DrawLocation.X;
        int footY = obj.DrawLocation.Y;

        // 触发一次图集异步预取（去重由 Mir2OriginObjectLibrary 内部保证）。
        if (!_ringRequested)
        {
            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, RingModel, RingAction, RingDir, EResLoadPriority.Visible);
            _ringRequested = true;
        }

        // 播放脚底环动画：按时间推进帧，循环 12 帧（帧序已修复为数值顺序）。
        if (Mir2OriginObjectLibrary.TryGetFrameCount(Mir2ObjectKind.Monster, RingModel, RingAction, RingDir, out int frameCount) && frameCount > 0)
        {
            int frame = (int)((CMain.Time / SelectionRingFrameMs) % frameCount);
            if (Mir2OriginObjectLibrary.TryGetSprite(Mir2ObjectKind.Monster, RingModel, RingAction, RingDir, frame, out var sprite) && sprite != null)
            {
                var src = sprite.SourceRectangle;
                bool oldBlend = DXManager.Blending;
                DXManager.SetBlend(false);
                // 原尺寸 1:1 显示（不缩放），环心居中脚底。
                DXManager.Draw(sprite.Texture,
                    new Rectangle(src.X, src.Y, src.Width, src.Height),
                    new RectangleF(footX - src.Width / 2f, footY - src.Height / 2f, src.Width, src.Height),
                    new Color(1f, 1f, 1f, 1f)); // 贴图自带颜色，白色着色不偏色
                DXManager.SetBlend(oldBlend);
                return;
            }
        }

        // 回落：程序化红色等距椭圆环（图集未就绪 / 加载失败）。
        var ptex = ProceduralSelectionRingTexture;
        if (ptex != null)
        {
            bool oldBlend = DXManager.Blending;
            DXManager.SetBlend(false);
            float semiW = CellWidth * 0.9f;
            float semiH = CellHeight * 0.9f;
            DXManager.Draw(ptex,
                new Rectangle(0, 0, ptex.Width, ptex.Height),
                new RectangleF(footX - semiW, footY - semiH, semiW * 2, semiH * 2),
                new Color(1f, 0.15f, 0.15f, 0.9f)); // 经典红色，90% 不透明
            DXManager.SetBlend(oldBlend);
        }
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

    // ===== 自动运行模式：自动战斗 / 自动寻路 / 关闭，三者互斥，统一由 Mode 管理 =====
    public enum EUserAutoMode
    {
        AutoOff,    // 都关闭
        AutoFight,  // 自动战斗
        AutoPath,   // 自动寻路（大地图/点地面）
    }

    private EUserAutoMode _mode = EUserAutoMode.AutoOff;
    public EUserAutoMode Mode
    {
        get { return _mode; }
        set
        {
            if (_mode == value) return;
            EUserAutoMode old = _mode;
            _mode = value;
            // 离开寻路态时清掉路径，避免残留路径被下一帧消费。
            if (old == EUserAutoMode.AutoPath && _mode != EUserAutoMode.AutoPath)
                CurrentPath = null;
            // 任何模式切换都清掉追击路径。
            ClearChasePath();
        }
    }

    // 兼容包装：现有代码大量读写 AutoFight / AutoPath，统一转发到 Mode。
    // 设为 true 会把另一种模式自动关掉（天然互斥）。
    public bool AutoFight
    {
        get { return _mode == EUserAutoMode.AutoFight; }
        set { if (value) Mode = EUserAutoMode.AutoFight; else if (_mode == EUserAutoMode.AutoFight) Mode = EUserAutoMode.AutoOff; }
    }
    public bool AutoPath
    {
        get { return _mode == EUserAutoMode.AutoPath; }
        set { if (value) Mode = EUserAutoMode.AutoPath; else if (_mode == EUserAutoMode.AutoPath) Mode = EUserAutoMode.AutoOff; }
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
            LogMove("目标格不可走(阻挡): " + target + " Flags=0x" + c.Flags.ToString("X2") +
                " IsBlocked=" + c.IsBlocked + " IsCover=" + c.IsCover + " IsSafe=" + c.IsSafe);
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
        _autoPathNextReplan = 0;   // 新的一次寻路，允许遇阻时立即重规划
        LogMove("开始寻路: " + from + " -> " + target + "，路径点数=" + path.Count);

        ShowClickEffect(target);
    }

    /// <summary>
    /// 自动寻路受阻/脱节时，从当前真实位置重新寻路到原目标。带节流避免每帧跑 A*。
    /// PathFinder 的 Node.Walkable 走 Map.EmptyCell（含怪/NPC 动态阻挡），所以重规划会自动绕开障碍。
    /// </summary>
    private bool ReplanAutoPath(Point dest)
    {
        if (PathFinder == null || dest == User.CurrentLocation) return false;
        if (CMain.Time < _autoPathNextReplan) return false;
        _autoPathNextReplan = CMain.Time + AutoPathReplanInterval;

        // 必须与“启动寻路”的规划参数一致：大地图点击用的是 FindPath(start, dest)
        // （MaxNodes=0，不限制长度，见 BigMapViewPort.OnMouseClick），这里传 20 会让
        // 任何剩余路程 >=20 格的长路径重规划直接返回 null，紧接着下面的分支把 AutoPath
        // 关掉 —— 表现就是“大地图点远处，走两步自动寻路自己没了”。
        var path = PathFinder.FindPath(User.CurrentLocation, dest);
        if (path == null || path.Count == 0)
        {
            AutoPath = false;   // 目标被占/不可达，停下，避免原地抽动
            return false;
        }

        CurrentPath = path;     // RetracePath 含起点，下一帧由 while 剔除
        return true;
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
        // 原版落点环（Magic3#500），保留默认发光，不做改动。
        Effects.Add(new Effect(Libraries.Magic3, 500, 10, 600, target) { DrawOffset = offset.Subtract(8, 15) });
    }

    public PathFinder PathFinder;
    public List<Node> CurrentPath = null;

    /// <summary>场景缩放的中心（与 DrawControl 里地图 RT blit 的放大中心一致）。</summary>
    private static PointF SceneScaleCenter
    {
        get
        {
            var fs = DXManager.FullScreenSize;
            return new PointF(fs.Width / 2f, fs.Height / 2f);
        }
    }

    /// <summary>屏幕/UI 逻辑坐标 → 世界像素坐标。地图 RT 在 DrawControl 里以 SceneScaleCenter 为中心放大
    /// Settings.SceneScale 倍，命中需做逆变换 W = C + (S - C)/scale，才能对准视觉位置。UI 层不缩放，不要对 UI 坐标调用此方法。</summary>
    public static Point ScreenToWorld(Point screen)
    {
        float s = Settings.SceneScale;
        if (s <= 0 || Math.Abs(s - 1f) < 0.0001f) return screen;
        var c = SceneScaleCenter;
        return new Point(
            (int)Math.Round(c.X + (screen.X - c.X) / s),
            (int)Math.Round(c.Y + (screen.Y - c.Y) / s));
    }

    public static Point MapLocation
    {
        get { return GameScene.User == null ? Point.Zero : new Point(MouseLocation.X / CellWidth - OffSetX, MouseLocation.Y / CellHeight - OffSetY).Add(GameScene.User.CurrentLocation); }
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
    public bool Lightning, Fire;
    public long LightningTime, FireTime;
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
                GameScene.Scene.ChatDialog.ReceiveChat(value ? LanguageMgr.Get("AutoRunOn"): LanguageMgr.Get("AutoRunOff"),
                    ChatType.System);
        }

    }

    public static bool AutoHit;

    // ========================================================================
    //  自动战斗 · 可调参数（全部集中在这里，直接改数值即可，无需动下面的逻辑）
    //  （原始传奇 autoFightType=1）
    // ========================================================================
    public const int AutoFightSearchRange = 50;      // 搜索怪物的最大距离（格）：超过这个距离的怪不追
    public const int AutoWanderMaxSteps = 12;        // 无怪时朝一个方向连续跑动的最大步数（每步跑 2~3 格），跑满后换方向
    public const int AutoFightScanInterval = 400;     // 每隔多少毫秒重新扫描一次怪物（越小反应越快、越费性能）
    public const long AutoFightGroundHintWindow = 5000; // 点地面提示后，多少秒内再点可解除自动战斗
    public const int AutoPathReplanInterval = 200;      // 自动寻路遇障碍重规划的最小间隔(ms)，防止每帧跑 A*
    // ========================================================================

    // 自动战斗运行时状态：Mode==Fight 时扫描 AutoFightSearchRange 格内怪物自动追击攻击；无怪则定向巡逻。
    private static long _autoFightNextScan;          // 下次扫描怪物的时间
    private static long _autoPathNextReplan;         // 下次允许自动寻路重规划的时间（节流）
    private static MirDirection _autoWanderDir = MirDirection.Up; // 无怪巡逻方向
    private static int _autoWanderSteps;              // 当前方向已走步数
    private static long _autoFightGroundHintTime;     // 首次点击地面、提示"再次点击解除"的时间

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
        BackColour = CommonColor.Black;

        MouseDown += OnMouseDown;
        MouseMove += (o, e) => MouseLocation = ScreenToWorld(e.Location);
        Click += OnMouseClick;
    }

    public void ResetMap(bool bResetCell = true)
    {
        GameScene.Scene.NPCDialog.Hide();

        MapObject.MouseObjectID = 0;
        MapObject.TargetObjectID = 0;
        MapObject.MagicObjectID = 0;
        
        if (M2CellInfo != null)
        {
            for (var i = ObjectsList.Count - 1; i >= 0; i--)
            {
                ObjectsList[i]?.Remove();
            }
        }

        Objects.Clear();
        ObjectsList.Clear();
        Effects.Clear();

        if (User != null)
        {
            Objects[User.ObjectID] = User;
            ObjectsList.Add(User);
        }

        if (bResetCell)
        {
            // 只有真正换图才清门/瓦片缓存。同城回城卷、任务传送等“同 MapIndex 重定位”
            // 走 ResetMap(false)，必须保留已下载的瓦片，否则地图会从第 0 张重新开始刷新。
            Doors.Clear();
            _mapTileCache.Clear();
            _mapTileLoading.Clear();

            M2CellInfo = null;
            Width = 0;
            Height = 0;
            _pendingAdd.Clear();
        }
    }

    public async Task LoadMapAsync()
    {
        ResetMap();
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
            return;
        }

        if (token.IsCancellationRequested || gen != _mapLoadGeneration)
        {
            string staleName = Path.GetFileNameWithoutExtension(fileName);
            NewResConfig.CurrentMapName = Path.GetFileNameWithoutExtension(FileName);
            return;
        }

        M2CellInfo = Map.MapCells;
        foreach (var ob in _pendingAdd)
        {
            AddObject(ob);
        }
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
        _floorWaitFrames = 0;
        _floorDirty = true;
        _lastUserCell = new Point(-1, -1);

        var pfT0 = DateTime.Now;
        PathFinder = new PathFinder(this);

        try
        {
            string mapStem = Path.GetFileNameWithoutExtension(fileName);
            int mapBgmId = int.TryParse(mapStem, out int mid) ? SoundManager.GetOriginMapBgm(mid) : 0;
            SoundManager.PlayOriginMusic(mapBgmId, true);
        }
        catch (Exception)
        {
        }

        SetMusic = Music;
        SoundList.Music = Music;

        // 当前地图的 .map 已就绪、瓦片开始按 Map 优先级加载；此时才放行任务目标地图的后台预载，
        // 避免登录阶段预载先灌满瓦片闸门把当前地图瓦片挤到队尾（延迟 5 秒+）。
        QuestMapTilePrefetcher.SetMapReady();
    }


    private long _nextPrefetch;

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

        // 后台静默预取（懒队列）：每 500ms 让各实体低优先级暖自己当前动作的各方向，转身/换动作不跳帧；
        // 已缓存/已缺失的 key 由 Request 直接短路，稳态零网络开销。
        if (CMain.Time >= _nextPrefetch)
        {
            _nextPrefetch = CMain.Time + 500;
            for (int i = ObjectsList.Count - 1; i >= 0; i--)
                ObjectsList[i].PrefetchBackground();
        }

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
                    // CMain.MPoint 是 UI 逻辑坐标；对象命中按世界像素比较，需先逆场景缩放（不改 CMain.MPoint，UI 仍用原值）。
                    if (ob == MapObject.User || !ob.MouseOver(ScreenToWorld(CMain.MPoint))) continue;

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
            DXManager.Draw(DXManager.FloorTexture, new Rectangle(0, 0, rtW, rtH), Vector3.Zero, CommonColor.White);
        }

        DrawObjects();

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

        // 上屏：强制单位变换，避免遗留 RenderTransform 二次缩放。目标矩形按 Settings.SceneScale 以屏幕中心放大
        // （原始传奇 setRootScale 默认 1.05），地图+角色一起等比放大，UI 层不受影响。
        // 命中侧在 ScreenToWorld 做完全相同的逆变换，保证点击/悬停对准视觉位置。
        // 不能走 PresentToScreen（其内部清空变换 → 1:1 左上角，地图只显示局部/四周洋红）。
        var savedTransform = DXManager.RenderTransform;
        DXManager.RenderTransform = null;
        var tex = ControlTexture.RenderTarget;
        var fs = DXManager.FullScreenSize;

        float sc = Settings.SceneScale;
        if (sc <= 0) sc = 1f;
        float drawW = fs.Width * sc, drawH = fs.Height * sc;
        var drawRect = new RectangleF((fs.Width - drawW) / 2f, (fs.Height - drawH) / 2f, drawW, drawH);

        DXManager.Draw(tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            drawRect,
            CommonColor.White);
        DXManager.RenderTransform = savedTransform;

        if (MapObject.User.Dead) DXManager.SetGrayscale(false);

        CleanTime = CMain.Time + Settings.CleanDelay;
    }

    private void DrawFloor(int vpW, int vpH)
    {
        int fw = vpW, fh = vpH;
        if (DXManager.FloorTexture == null || 
            DXManager.FloorTexture.Disposed || 
            DXManager.FloorTexture.Width != fw || 
            DXManager.FloorTexture.Height != fh)
        {
            DXManager.FloorTexture = new Texture(DXManager.Device, fw, fh, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
            DXManager.FloorSurface = DXManager.FloorTexture.GetSurfaceLevel(0);
        }

        if (User == null) 
        { 
            FloorValid = true; 
            return; 
        }

        var floorT0 = DateTime.Now;   // 采样：单次地板烘焙耗时
        Surface oldSurface = DXManager.CurrentSurface;
        DXManager.SetSurface(DXManager.FloorSurface);
        DXManager.Device.Clear(ClearFlags.Target, CommonColor.Empty, 0, 0);

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
                        DXManager.Draw(tex, new Rectangle(0, 0, tex.Width, tex.Height), new RectangleF(tileDrawX, tileDrawY, TileTexWidth, TileTexHeight), new Color(1f, 1f, 1f, 1f));
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
            var tex = await Mir2OriginResource.GetMapTileTextureAsync(MapImg, index).ConfigureAwait(false);
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

        // 脚底选中环属于“地面层”，必须在实体之前画：本引擎无深度缓冲、纯按提交顺序作画，
        // 若放在实体之后，环的上半部分会盖住 NPC/怪物/玩家的下半身。先画环，后画的实体自然压住它。
        // 跟随“当前选中对象”SelectionTarget，走路/施法期间回落到可攻击对象，保证边走边打时环不丢。
        var sel = MapObject.SelectionTarget ?? MapObject.CanAttackTargetObject;
        if (sel != null && !sel.Dead) DrawSelectionRing(sel);

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

        DXManager.FlushSprite();
        float oldOpacity = DXManager.Opacity;
        DXManager.SetOpacity(0.4F);

        // 纯线上：本地玩家同样走 origin（User.Draw 已只绘制 origin 模型），不再用旧 .Lib 分层绘制。
        MapObject.User.Draw();

        DXManager.SetOpacity(oldOpacity);

        if (Settings.HighlightTarget)
        {
            if (MapObject.MouseObject != null && !MapObject.MouseObject.Dead && MapObject.MouseObject != MapObject.TargetObject && MapObject.MouseObject.Blend)
                MapObject.MouseObject.DrawBlend();

            if (MapObject.TargetObject != null)
                MapObject.TargetObject.DrawBlend();
        }

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

            if (ob is PlayerObject) ob.DrawChat();

            //ob.DrawHealth();
            ob.DrawPoison();
            ob.DrawDamages();
        }

        foreach (var ob in Objects.Values)
        {
            ob.DrawHealth();
        }
    }



    /// <summary>
    /// 自动战斗中点击地面：首次点击冒泡提示"再次点击解除"，5 秒内的第二次点击真正解除自动战斗。
    /// 通过 GameScene.Scene.MapControl 访问实例字段 AutoFight（本类多处逻辑为 static）。
    /// </summary>
    private static void HandleAutoFightGroundClick()
    {
        MapControl map = GameScene.Scene == null ? null : GameScene.Scene.MapControl;
        if (map == null || !map.AutoFight)
        {
            _autoFightGroundHintTime = 0;
            return;
        }

        long now = CMain.Time;
        if (_autoFightGroundHintTime > 0 && now - _autoFightGroundHintTime <= AutoFightGroundHintWindow)
        {
            // 5 秒内第二次点击地面 → 解除
            map.AutoFight = false;
            _autoFightGroundHintTime = 0;
            return;
        }

        // 首次点击：冒泡提示并记录时间
        _autoFightGroundHintTime = now;
        MirBubbleToast.Show("再次点击地面，解除自动战斗状态");
    }

    /// <summary>开关自动战斗时调用，清掉"再次点击地面解除"的计时，避免下次开启后残留计时误触。</summary>
    public static void ResetAutoFightGroundHint()
    {
        _autoFightGroundHintTime = 0;
        _autoFightNextScan = 0;
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
                        HandleAutoFightGroundClick();
                        // 但落点特效要照放：原版这个特效挂在"右键 + Settings.NewMove"的寻路分支里，
                        // 而 NewMove 默认 false，等于平时点地面完全没有反馈；网页版靠它确认"点到了哪一格"。
                        if (GameScene.Scene != null && GameScene.Scene.MapControl != null)
                            GameScene.Scene.MapControl.ShowClickEffect(MapLocation);
                        return;
                    }
                    NPCObject npc = MapObject.MouseObject as NPCObject;
                    if (npc != null)
                    {
                        MapObject.SelectTarget(npc); // 选定 NPC：统一入口，脚底环移到 NPC、攻击目标按 NPC 语义清空
                        // 城市传送员：自定义分类传送对话框（开关 TeleporterData.UseCustomDialog，默认关，暂用原 NPC 脚本菜单）。
                        if (TeleporterData.UseCustomDialog && TeleporterData.IsCityTeleporter(npc.Name))
                        {
                            GameScene.NPCID = npc.ObjectID;
                            GameScene.NPCTime = CMain.Time + 5000;
                            if (!GameScene.Scene.NPCTeleporterDialog.Visible)
                            {
                                // 先发 @Main 在服务端“开起”与传送员的对话（设置 NPCObjectID/NPCScriptID）；
                                // 否则后续点目的地发 [@mv_xxx] 时服务端对话上下文不对，会落到随机传送。
                                // 该回包仅用于建立对话，SuppressNpcDialog 让 NPCResponse 忽略它，不弹原菜单。
                                NPCTeleporterDialog.SuppressNpcDialog = true;
                                GameScene.Scene.NPCDialog.Hide();
                                Network.Enqueue(new C.CallNPC { ObjectID = npc.ObjectID, Key = "[@Main]" });
                                GameScene.Scene.NPCTeleporterDialog.Open("传送员", TeleporterData.GetDestinations(), npc.ObjectID);
                            }
                            return;
                        }

                        if (npc.ObjectID == GameScene.NPCID &&
                            (CMain.Time <= GameScene.NPCTime || GameScene.Scene.NPCDialog.Visible))
                        {
                            return;
                        }

                        GameScene.NPCTime = CMain.Time + 5000;
                        GameScene.NPCID = npc.ObjectID;
                        GameScene.Scene.NPCTeleporterDialog.Hide();
                        Network.Enqueue(new C.CallNPC { ObjectID = npc.ObjectID, Key = "[@Main]" });
                    }
                }
                break;
            case MouseButtons.Right:
                {
                    AutoRun = false;
                    if (MapObject.MouseObject == null)
                    {
                        HandleAutoFightGroundClick();
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
        // 逆场景缩放：e.Location 是 UI 逻辑坐标，MouseLocation 必须是世界像素。
        MouseLocation = ScreenToWorld(e.Location);

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
                MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("YouCannotDrop", cell.Item.FriendlyName), MirMessageBoxButtons.OK);
                messageBox.Show();
                GameScene.SelectedCell = null;
                return;
            }
            if (cell.Item.Count == 1)
            {
                MirMessageBox messageBox = new MirMessageBox(LanguageMgr.Get("DropTip", cell.Item.FriendlyName), MirMessageBoxButtons.YesNo);

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
                MirAmountBox amountBox = new MirAmountBox(LanguageMgr.Get("DropAmount"), (int)cell.Item.Info.Image, cell.Item.Count);

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
            MirAmountBox amountBox = new MirAmountBox(LanguageMgr.Get("DropAmount"), 116, GameScene.Gold);

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
            MapObject.SelectTarget(MapObject.MouseObject); // 统一入口：脚底环/攻击目标/法术目标一次设齐（互斥）
        }
        else
        {
            // 点地面：不清空已选目标（保留脚底环/攻击对象），但停止自动攻击与追击它。
            MapObject.bAutoAttackSelectTarget = false;
        }
    }

    private void CheckInput()
    {
        if (AwakeningAction == true) return;

        if ((MouseControl == this) && (MapButtons != MouseButtons.None)) AutoHit = false;//mouse actions stop mining even when frozen!
        if (!CanRideAttack()) AutoHit = false;

        if (CMain.Time < InputDelay || User.Poison.HasFlag(PoisonType.Paralysis) || User.Poison.HasFlag(PoisonType.LRParalysis) || User.Poison.HasFlag(PoisonType.Frozen) || User.Fishing) return;

        // 自动战斗接管移动/攻击（在手动输入之前），避免与鼠标操作冲突。
        if (AutoFight)
        {
            ProcessAutoFight();
            return;
        }

        if (User.NextMagic != null && !User.RidingMount)
        {
            UseMagic(User.NextMagic, User);
            return;
        }

        if (CMain.Time < User.BlizzardStopTime || CMain.Time < User.ReincarnationStopTime) return;

        // 点地面后 bAutoAttackSelectTarget=false：保留目标选中，但不再自动攻击它（要再打需重新点怪）。
        if (MapObject.bAutoAttackSelectTarget && MapObject.TargetObject != null && !MapObject.TargetObject.Dead)
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
                            GameScene.Scene.OutputMessage(LanguageMgr.Get("TargetTooFar"));
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
                    // Shift/普通攻击一律以"可攻击对象"为准，不看鼠标悬停（MouseObject）。
                    if (MapObject.CanAttackTargetObject is NPCObject || (MapObject.CanAttackTargetObject is PlayerObject && MapObject.CanAttackTargetObject != User)) break;
                    if (MapObject.CanAttackTargetObject is MonsterObject && MapObject.CanAttackTargetObject.AI == 70) break;

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
                            if (MapObject.CanAttackTargetObject is MonsterObject || MapObject.CanAttackTargetObject is PlayerObject) target = MapObject.CanAttackTargetObject;

                            if (User.Class == MirClass.Archer && User.HasClassWeapon && !User.RidingMount && !User.Poison.HasFlag(PoisonType.Dazed))
                            {
                                if (target != null)
                                {
                                    if (!Functions.InRange(target.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                                    {
                                        if (CMain.Time >= OutputDelay)
                                        {
                                            OutputDelay = CMain.Time + 1000;
                                            GameScene.Scene.OutputMessage(LanguageMgr.Get("TargetTooFar"));
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

                    if (MapObject.CanAttackTargetObject is MonsterObject && User.Class == MirClass.Archer && !MapObject.CanAttackTargetObject.Dead && User.HasClassWeapon && !User.RidingMount) //ArcherTest - range attack
                    {
                        if (Functions.InRange(MapObject.CanAttackTargetObject.CurrentLocation, User.CurrentLocation, Globals.MaxAttackRange))
                        {
                            if (CMain.Time > GameScene.AttackTime)
                            {
                                User.QueuedAction = new QueuedAction { Action = MirAction.AttackRange1, Direction = direction, Location = User.CurrentLocation, Params = new List<object>() };
                                User.QueuedAction.Params.Add(MapObject.CanAttackTargetObject.ObjectID);
                                User.QueuedAction.Params.Add(MapObject.CanAttackTargetObject.CurrentLocation);
                            }
                        }
                        else
                        {
                            if (CMain.Time >= OutputDelay)
                            {
                                OutputDelay = CMain.Time + 1000;
                                GameScene.Scene.OutputMessage(LanguageMgr.Get("TargetTooFar"));
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

            // 自动寻路：路径只在“点击时”算一次（见 BigMapViewPort.OnMouseClick / TryAutoPath），
            // 这里每帧只做轻量工作——剔除已走过的节点、算到下一节点的方向并前进。
            // 仅在“预测落点脱离路径”或“前方被动态阻挡”时才重算（有节流，不会每帧跑 A*）。

            // 原目标始终是路径最后一个节点，重规划都朝它。
            Point dest = CurrentPath[CurrentPath.Count - 1].Location;
            if (dest == User.CurrentLocation)
            {
                AutoPath = false;   // 到达终点
                return;
            }

            // 情况一：跑步跨 2 格转弯落点不在逐格节点上 / 被服务端 S.UserLocation 拉回，
            // 当前位置已不在路径里。继续朝旧路径头走会指向身后 → 短距离来回跑，必须重规划。
            if (!CurrentPath.Any(x => x.Location == User.CurrentLocation))
            {
                if (ReplanAutoPath(dest)) return;   // 本帧只重算，下一帧再走
                return;                             // 节流中/失败：本帧不动，避免乱走
            }

            // 关键：绝不在这里删除路径节点。QueuedAction 只是“预约”，真正移动由
            // PlayerObject.SetAction 消费，而且受 MapControl.NextAction 锁（走路后 +2500ms，
            // 要等服务端 S.UserLocation 回包才清零）。如果动作还没执行就删掉当前节点，
            // 下一帧 CurrentLocation 还在原地却已不在路径里，会被误判“脱节”反复重规划
            // —— 表现就是跑一步停一步。改成按真实位置查索引，动作执行后索引自然前移。
            int pathIndex = CurrentPath.FindIndex(x => x.Location == User.CurrentLocation);
            if (pathIndex < 0 || pathIndex + 1 >= CurrentPath.Count)
            {
                if (ReplanAutoPath(dest)) return;
                return;
            }

            {
                MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, CurrentPath[pathIndex + 1].Location);

                // 情况二：下一节点出现怪物/动态障碍（A* 快照里可走、现在被占），
                // CanRun/CanWalk 都失败 → 立刻重规划绕开，绝不原地停住。
                if (!CanWalk(dir) && !CanRun(dir))
                {
                    ReplanAutoPath(dest);
                    return;
                }

                // 与手动移动（本文件 MouseButtons.Left/Right 分支）以及 PlayerObject.SetAction 的
                // steps = (Sprint && !Sneaking ? 3 : 2) 完全对齐：落点步数漏掉 Sprint 时，
                // 服务端实际跑 3 格、客户端只预测 2 格，S.UserLocation 会不断把人拉回，
                // 路径节点全部对不上 → 一走一停 → 最终被服务端判为异常移动而掉线。
                int runDistance = (User.RidingMount || (User.Sprint && !User.Sneaking)) ? 3 : 2;
                if (GameScene.CanRun && CanRun(dir) && CMain.Time > GameScene.NextRunTime && User.HP >= 10
                    && (!User.Sneaking || (User.Sneaking && User.Sprint))
                    && (CurrentPath.Count - pathIndex - 1) > runDistance)
                {
                    // 手动移动会逐格检查门，自动寻路也必须检查：门未开时服务端拒绝移动。
                    bool doorBlocked = false;
                    for (int i = 0; i <= runDistance; i++)
                    {
                        if (!CheckDoorOpen(Functions.PointMove(User.CurrentLocation, dir, i)))
                        {
                            doorBlocked = true;
                            break;
                        }
                    }
                    if (!doorBlocked)
                    {
                        User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, runDistance) };
                        return;
                    }
                }

                if (CanWalk(dir) && CheckDoorOpen(Functions.PointMove(User.CurrentLocation, dir, 1)))
                {
                    User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, 1) };
                    return;
                }
            }
        }

        // 点地面后 bAutoAttackSelectTarget=false：保留目标但不再自动走向它。
        if (!MapObject.bAutoAttackSelectTarget) return;
        if (MapObject.TargetObject == null || MapObject.TargetObject.Dead) return;
        if (((!MapObject.TargetObject.Name.EndsWith(")") && !(MapObject.TargetObject is PlayerObject)) || !CMain.Shift) &&
            (MapObject.TargetObject.Name.EndsWith(")") || !(MapObject.TargetObject is MonsterObject))) return;
        if (Functions.InRange(MapObject.TargetObject.CurrentLocation, User.CurrentLocation, 1)) return;
        if (User.Class == MirClass.Archer && User.HasClassWeapon && (MapObject.TargetObject is MonsterObject || MapObject.TargetObject is PlayerObject)) return; //ArcherTest - stop walking
        direction = Functions.DirectionFromPoint(User.CurrentLocation, MapObject.TargetObject.CurrentLocation);

        if (!CanWalk(direction, out direction)) return;

        User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = Functions.PointMove(User.CurrentLocation, direction, 1) };
    }

    /// <summary>
    /// 自动战斗核心：AutoFightSearchRange 格内找最近可攻击怪 → 追击并攻击；无怪则定向巡逻（防原地往返）。
    /// 由 CheckInput 在手动输入之前调用；一切移动/攻击仍走引擎标准 QueuedAction 流程。
    /// </summary>
    private void ProcessAutoFight()
    {
        if (User == null || User.Dead || User.RidingMount) { AutoFight = false; return; }
        // 安全区不自动打怪（避免站桩），但也不动。
        if (M2CellInfo == null) return;

        // 移动/攻击指令每帧重新压入 QueuedAction（与手动寻路一致）。
        // 绝不能限频（如 120ms）：UserObject.ProcessFrames 在动画每推进一帧就会清掉 QueuedAction，
        // 限频会让指令在角色尚未走完当前步时被清空、下一步丢失，表现为“走一步卡一下”。
        // 攻击频率另有 GameScene.AttackTime 约束，怪物扫描另有 _autoFightNextScan 约束，均不会每帧执行。

        // ===== 目标锁定 =====
        // 只要当前目标还活着、且在搜索范围内，就一直打它，绝不因为“附近有更近的怪”而换目标。
        // 换目标会清掉追击路径；在墙边绕路时换目标正是来回震荡的元凶。
        // 只有目标丢失/死亡/超出搜索范围，或 A* 反复判定不可达（见 ChaseTarget）时才重选。
        MapObject target = MapObject.CanAttackTargetObject;
        if (target == null || target.Dead ||
            !Functions.InRange(target.CurrentLocation, User.CurrentLocation, AutoFightSearchRange))
        {
            target = FindAutoFightTarget();
            if (target != null)
                MapObject.SelectTarget(target); // 统一设置攻击目标+法术目标+脚底环
            else
            {
                // 无怪：清掉攻击目标与追击路径，进入巡逻
                MapObject.SelectTarget(null);
                ClearChasePath();
            }
        }

        if (target != null && !target.Dead && Functions.InRange(target.CurrentLocation, User.CurrentLocation, AutoFightSearchRange))
        {
            // 已在攻击范围（近战 1 格 / 弓手最大射程）→ 面向目标并攻击。
            int attackRange = (User.Class == MirClass.Archer && User.HasClassWeapon) ? Globals.MaxAttackRange : 1;
            if (Functions.InRange(target.CurrentLocation, User.CurrentLocation, attackRange))
            {
                if (CMain.Time > GameScene.AttackTime)
                {
                    MirDirection face = Functions.DirectionFromPoint(User.CurrentLocation, target.CurrentLocation);
                    User.QueuedAction = new QueuedAction { Action = MirAction.Attack1, Direction = face, Location = User.CurrentLocation };
                }
                return;
            }

            // 未在攻击范围 → 统一交给 ChaseTarget：用一条有记忆的 A* 路径追到底，
            // 不再每帧用无记忆的 CanWalk 贪心贴墙（那会在墙角左右偏交替、两格间来回震荡）。
            ChaseTarget(target);
            return;
        }

        // ===== 无怪：清掉追击路径并定向巡逻（防原地往返）=====
        ClearChasePath();
        AutoWander();
    }

    // ===== 自动战斗追击路径（有记忆，杜绝墙角来回震荡）=====
    private System.Collections.Generic.List<Point> _chasePath;   // 到目标的路径点（不含起点）
    private uint _chaseTargetId;                                // 当前路径对应的目标
    private long _chaseNextBuild;                               // 下次允许重算路径的时间（节流）

    private void ClearChasePath()
    {
        _chasePath = null;
        _chaseTargetId = 0;
    }

    /// <summary>
    /// 自动战斗追击：用一条有记忆的 A* 路径走到底。
    /// 路径一旦建立就锁定走完，不因玩家/怪的小幅移动而重算（重算会让墙两侧路径翻转 → 来回走）。
    /// 只有三种情况重算：目标变了、路径走完、路径下一格被堵。
    /// A* 一次即可判断有无路：有路就走；无路（真不可达）才由外层换目标。
    /// </summary>
    private void ChaseTarget(MapObject target)
    {
        if (PathFinder == null || target == null || target.Dead) return;
        Point me = User.CurrentLocation;

        // 目标变了 → 丢弃旧路径。
        if (_chaseTargetId != target.ObjectID)
            ClearChasePath();

        // 沿已有路径走（优先于任何 CanWalk）。
        if (_chasePath != null)
        {
            // 找到 me 在路径中的位置，剔除它及之前所有点。
            // 跑 2 格会一次跳过 2 个节点，第 1 个节点不会被 me 精确命中，必须按索引整体剔除，
            // 否则残留的旧节点会让人往回走（沿墙来回）。
            int reached = -1;
            for (int i = 0; i < _chasePath.Count; i++)
            {
                if (_chasePath[i] == me) { reached = i; break; }
            }

            if (reached >= 0)
            {
                _chasePath.RemoveRange(0, reached + 1);
            }
            // 若 me 未精确命中任何节点，但【紧邻】路径首点（下一步就能走到），也算有效，继续走。
            // 只有 me 离路径首点 > 1 格（服务端拉回/跑动严重脱节）才判定脱节、清路径重算，
            // 避免墙边因斜向节点与正交实际位置差一格而频繁重算、卡在节流上。
            else if (_chasePath.Count > 0 &&
                     Math.Max(Math.Abs(_chasePath[0].X - me.X), Math.Abs(_chasePath[0].Y - me.Y)) > 1)
            {
                ClearChasePath();
            }

            if (_chasePath != null && _chasePath.Count == 0)
            {
                ClearChasePath();
                return;   // 到达怪邻格，下一帧走正常攻击判定
            }

            if (_chasePath != null && _chasePath.Count > 0)
            {
                Point next = _chasePath[0];
                if (EmptyCell(next))
                {
                    ChaseMoveAlongPath(me);
                    return;
                }
                // 下一格被动态对象占了 → 清掉，稍后重算。
                ClearChasePath();
            }
        }

        // 需要建路径：带节流，A* 一次判断有无路。
        if (CMain.Time < _chaseNextBuild) return;
        _chaseNextBuild = CMain.Time + AutoPathReplanInterval;

        _chasePath = BuildPathToMonster(me, target.CurrentLocation);
        _chaseTargetId = target.ObjectID;

        if (_chasePath == null || _chasePath.Count == 0)
        {
            // 真不可达（A* 找不到路）→ 换目标，让下次扫描换一只，不在这里死磕。
            MapObject.SelectTarget(null);
            ClearChasePath();
            return;
        }

        ChaseMoveAlongPath(me);
    }

    /// <summary>
    /// 沿追击路径移动一步：能跑就跑（跨 runDistance 格），否则走一格。
    /// 注意：这里【不主动删除路径节点】。动作要下一帧才执行、CurrentLocation 才会更新；
    /// 路径节点的剔除统一由 ChaseTarget 开头的 while(_chasePath[0]==me) 根据实际位置驱动，
    /// 跑 2 格后下一帧会自动剔除 2 个节点，走 1 格剔除 1 个，始终与实际落点对齐。
    /// </summary>
    private void ChaseMoveAlongPath(Point me)
    {
        if (_chasePath == null || _chasePath.Count == 0) return;

        Point next = _chasePath[0];
        MirDirection dir = Functions.DirectionFromPoint(me, next);
        int runDistance = (User.RidingMount || (User.Sprint && !User.Sneaking)) ? 3 : 2;

        // 能跑：引擎节奏允许 + 前方 runDistance 格都是空且沿同一方向（路径直线段），
        // 且落点正好是路径第 runDistance 个节点——这样下一帧按实际位置剔除不会错位。
        if (GameScene.CanRun && CMain.Time > GameScene.NextRunTime && User.HP >= 10
            && CanRun(dir) && _chasePath.Count >= runDistance)
        {
            bool straight = true;
            for (int i = 0; i < runDistance; i++)
            {
                if (_chasePath[i] != Functions.PointMove(me, dir, i + 1) || !EmptyCell(_chasePath[i]))
                {
                    straight = false;
                    break;
                }
            }
            if (straight)
            {
                User.QueuedAction = new QueuedAction
                {
                    Action = MirAction.Running,
                    Direction = dir,
                    Location = Functions.PointMove(me, dir, runDistance)
                };
                return;
            }
        }

        // 走一格（同样不删节点，由 while 按实际位置剔除）。
        if (CanWalk(dir))
        {
            User.QueuedAction = new QueuedAction
            {
                Action = MirAction.Walking,
                Direction = dir,
                Location = Functions.PointMove(me, dir, 1)
            };
        }
    }

    /// <summary>
    /// 建一条从 from 到怪物某个可走邻格的路径（不含起点）；A* 不可达返回 null。
    /// 终点优先选“朝向玩家”那一侧的邻格：怪物不动时该格固定，重算不会在墙两侧翻转。
    /// </summary>
    private System.Collections.Generic.List<Point> BuildPathToMonster(Point from, Point monsterCell)
    {
        // 按“离 from 最近的优先”收集所有可走邻格作为候选。
        System.Collections.Generic.List<Point> candidates = new System.Collections.Generic.List<Point>();
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Point cell = new Point(monsterCell.X + dx, monsterCell.Y + dy);
                if (!EmptyCell(cell)) continue;
                candidates.Add(cell);
            }
        }
        if (candidates.Count == 0) return null;   // 怪四周全被堵死

        // 依次尝试候选邻格做 A*，取第一条能走通的路径（A* 一次成功即返回）。
        // 先试朝向玩家那个方向，让终点稳定、少绕路。
        MirDirection towardMe = Functions.DirectionFromPoint(monsterCell, from);
        candidates.Sort((a, b) =>
        {
            int dirA = (int)Functions.DirectionFromPoint(monsterCell, a);
            int dirB = (int)Functions.DirectionFromPoint(monsterCell, b);
            int da = Math.Min(Math.Abs(dirA - (int)towardMe), 8 - Math.Abs(dirA - (int)towardMe));
            int db = Math.Min(Math.Abs(dirB - (int)towardMe), 8 - Math.Abs(dirB - (int)towardMe));
            if (da != db) return da.CompareTo(db);
            return Math.Max(Math.Abs(a.X - from.X), Math.Abs(a.Y - from.Y))
                 .CompareTo(Math.Max(Math.Abs(b.X - from.X), Math.Abs(b.Y - from.Y)));
        });

        foreach (Point goal in candidates)
        {
            var pathPoints = PathFinder.FindPath(from, goal, 20);
            if (pathPoints == null || pathPoints.Count <= 1) continue;

            var list = new System.Collections.Generic.List<Point>();
            for (int i = 1; i < pathPoints.Count; i++)
                list.Add(pathPoints[i].Location);
            return list;
        }
        return null;   // 所有邻格都不可达 → 真无路
    }

    /// <summary>扫描 AutoFightSearchRange 格内最近的可攻击怪物，排除宝宝/宠物/特殊 AI。</summary>
    private MapObject FindAutoFightTarget()
    {
        MapObject best = null;
        int bestDist = int.MaxValue;
        Point me = User.CurrentLocation;
        foreach (var ob in ObjectsList)
        {
            if (!(ob is MonsterObject monster) || monster.Dead) continue;
            if (monster.AI == 64 || monster.AI == 70 || monster.AI == 6) continue; // 宠物/宝宝/特殊
            if (monster.MasterObjectId != 0) continue;                            // 有主人的召唤物不打
            if (monster.Name != null && monster.Name.EndsWith(")")) continue;    // 宝宝类命名（如"神兽(宝宝)"）
            int d = Math.Max(Math.Abs(monster.CurrentLocation.X - me.X), Math.Abs(monster.CurrentLocation.Y - me.Y));
            if (d > AutoFightSearchRange || d >= bestDist) continue;
            bestDist = d;
            best = monster;
        }
        return best;
    }

    /// <summary>无怪巡逻：同一方向连续走若干步再换方向，换方向时排除反方向，杜绝原地来回。</summary>
    private void AutoWander()
    {
        // 当前方向走满步数 -> 换一个非反方向的新方向。
        // 从当前方向左右偏转找可走方向，明显排除正后方（反方向），杜绝 A→B→A 来回。
        if (_autoWanderSteps <= 0 || !CanWalkDir(_autoWanderDir))
        {
            int cur = (int)_autoWanderDir;
            int opposite = (cur + 4) % 8;
            // 优先小角度偏转（±1、±2、±3），最后才考虑其它。
            int[] offsets = { -1, 1, -2, 2, -3, 3 };
            MirDirection pick = _autoWanderDir;
            bool found = false;
            for (int i = 0; i < offsets.Length; i++)
            {
                int nd = (cur + offsets[i] + 8) % 8;
                if (nd == opposite) continue;              // 绝不走回头路
                if (CanWalkDir((MirDirection)nd)) { pick = (MirDirection)nd; found = true; break; }
            }
            if (!found) pick = _autoWanderDir;             // 极端全堵：原地保持（下一步仍会被阻挡，不会来回）
            _autoWanderDir = pick;
            _autoWanderSteps = CMain.Random.Next(6, AutoWanderMaxSteps + 1);
        }

        if (CanWalkDir(_autoWanderDir))
        {
            AutoMoveStep(_autoWanderDir);
            _autoWanderSteps--;
        }
    }

    /// <summary>
    /// 自动战斗移动一步：能跑就跑（与手动寻路 AutoPath 完全相同的跑步条件），否则走一格。
    /// 跑步距离非骑乘 2 格 / 骑乘 3 格，与引擎 Running 动作一致。
    /// </summary>
    private void AutoMoveStep(MirDirection dir)
    {
        // 必须和服务端 HumanObject.Run 的 steps 一致：RidingMount || ActiveSwiftFeet(客户端 Sprint) ? 3 : 2。
        // 少算 Sprint 时服务端跑 3 格、客户端只预测 2 格 → 每次都被 S.UserLocation 拉回并锁输入 400ms。
        int runDistance = (User.RidingMount || (User.Sprint && !User.Sneaking)) ? 3 : 2;
        // 条件与手动寻路 AutoPath 完全一致（含 GameScene.CanRun 的跑/走节奏锁）：
        // 少任何一项都会让客户端预测格数与服务端判定不一致，服务端回 S.UserLocation 把人拉回原位。
        if (GameScene.CanRun && CanRun(dir) && CMain.Time > GameScene.NextRunTime && User.HP >= 10)
        {
            User.QueuedAction = new QueuedAction
            {
                Action = MirAction.Running,
                Direction = dir,
                Location = Functions.PointMove(User.CurrentLocation, dir, runDistance)
            };
            return;
        }

        if (CanWalk(dir))
        {
            User.QueuedAction = new QueuedAction
            {
                Action = MirAction.Walking,
                Direction = dir,
                Location = Functions.PointMove(User.CurrentLocation, dir, 1)
            };
        }
    }

    private bool CanWalkDir(MirDirection dir)
    {
        return !User.InTrapRock && EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1));
    }

    /// <summary>攻击类技能必须点击选中目标：NeedTarget（服务端按原始 releaseType==1 计算）或旧引擎单体攻击技能。</summary>
    private static bool RequiresSelectedTarget(ClientMagic magic)
    {
        if (magic == null) return false;
        // NeedTarget 由服务端按 releaseType==1 下发；不能用 TargetType（召唤与火符都是1）。
        if (magic.NeedTarget) return true;

        switch (magic.Spell)
        {
            case Spell.FireBall: // 火球术
            case Spell.GreatFireBall: // 大火球
            case Spell.ElectricShock: // 电击
            case Spell.Poisoning: // 施毒术
            case Spell.ThunderBolt: // 雷电术
            case Spell.FlameDisruptor: // 灭天火
            case Spell.SoulFireBall: // 灵魂火符
            case Spell.TurnUndead: // 驱尸术（复活死尸）
            case Spell.FrostCrunch: // 寒冰掌
            case Spell.Vampirism: // 噬血术
            case Spell.FireBounce: // 火焰弹
            case Spell.MeteorShower: // 流星雨
                return true;
            default:
                return false;
        }
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
                GameScene.Scene.OutputMessage(LanguageMgr.Get("CannotCastSpellSeconds", magic.Name,
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
                GameScene.Scene.OutputMessage(LanguageMgr.Get("LowMana"));
            }
            actor.ClearMagic();
            return;
        }

        //bool isTargetSpell = true;

        MapObject target = null;

        //Targeting
        switch (magic.Spell)
        {
            case Spell.FireBall: // 火球术
            case Spell.GreatFireBall: // 大火球
            case Spell.ElectricShock: // 电击
            case Spell.Poisoning: // 施毒术
            case Spell.ThunderBolt: // 雷电术
            case Spell.FlameDisruptor: // 灭天火
            case Spell.SoulFireBall: // 灵魂火符
            case Spell.TurnUndead: // 驱尸术（复活死尸）
            case Spell.FrostCrunch: // 寒冰掌
            case Spell.Vampirism: // 噬血术
            case Spell.Revelation: // 心灵启示
            case Spell.Entrapment: // 困魔咒
            case Spell.Hallucination: // 幻象术
            case Spell.DarkBody: // 暗影术
            case Spell.FireBounce: // 火焰弹
            case Spell.MeteorShower: // 流星雨
                // 统一取"可攻击对象"：点怪物后持续保持，不受鼠标移到技能栏影响。
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.StraightShot: // 直射击
            case Spell.DoubleShot: // 双重射击
            case Spell.ElementalShot: // 元素箭
            case Spell.DelayedExplosion: // 延迟爆炸
            case Spell.BindingShot: // 束缚箭
            case Spell.VampireShot: // 吸血箭
            case Spell.PoisonShot: // 毒箭
            case Spell.CrippleShot: // 残废箭
            case Spell.NapalmShot: // 燃烧箭
            case Spell.SummonVampire: // 召唤吸血鬼
            case Spell.SummonToad: // 召唤蟾蜍
            case Spell.SummonSnakes: // 召唤蛇
                if (!actor.HasClassWeapon)
                {
                    GameScene.Scene.OutputMessage(LanguageMgr.Get("MustWearBowForSkill"));
                    actor.ClearMagic();
                    return;
                }
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.Stonetrap: // 石陷阱
                if (!User.HasClassWeapon)
                {
                    GameScene.Scene.OutputMessage(LanguageMgr.Get("MustWearBowForSkill"));
                    User.ClearMagic();
                    return;
                }
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;

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
            case Spell.Purification: // 净化术
            case Spell.Healing: // 治愈术
            case Spell.UltimateEnhancer: // 强化术
            case Spell.EnergyShield: // 能量护盾
            case Spell.PetEnhancer: // 宠物强化
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant &&
                    !(MapObject.CanAttackTargetObject is MonsterObject monster && monster.MasterObjectId == 0))
                    target = MapObject.CanAttackTargetObject;

                if (target == null) target = User;
                break;
            case Spell.FireBang: // 爆裂火焰
            case Spell.MassHiding: // 集体隐身术
            case Spell.FireWall: // 火墙
            case Spell.TrapHexagon: // 困魔咒
            case Spell.HealingCircle: // 治愈术（范围）
            case Spell.CatTongue: // 猫舌
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.PoisonCloud: // 毒云术
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.Blizzard: // 暴风雪
            case Spell.MeteorStrike: // 流星火雨
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.Reincarnation: // 重生术
                // 复活是唯一的例外：目标是死亡玩家，无法被点击选中（选中排除 Dead），
                // 只能取鼠标悬停的尸体。这里用 MouseObject 是复活专用，不是攻击目标。
                {
                    MapObject reviveTarget = actor == Hero ? MapObject.MouseObject ?? User : MapObject.MouseObject;
                    if (reviveTarget != null && reviveTarget.Dead && reviveTarget.Race == ObjectType.Player)
                        target = reviveTarget;
                }
                break;
            case Spell.Trap: // 陷阱
                if (MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
            case Spell.FlashDash: // 十步一杀
                if (actor.GetMagic(Spell.FlashDash).Level <= 1 && actor.IsDashAttack() == false)
                {
                    actor.ClearMagic();
                    return;
                }
                //isTargetSpell = false;
                break;
            default:
                // 原始传奇技能（灵魂火符等）：统一取"可攻击对象"。
                if ((int)magic.Spell > 218 && MapObject.CanAttackTargetObject != null && !MapObject.CanAttackTargetObject.Dead &&
                    MapObject.CanAttackTargetObject.Race != ObjectType.Item && MapObject.CanAttackTargetObject.Race != ObjectType.Merchant)
                    target = MapObject.CanAttackTargetObject;
                break;
        }

        // 攻击类技能必须先点击选中目标：未选中则提示并取消，不发包、不记 CD。
        if (RequiresSelectedTarget(magic) && target == null)
        {
            GameScene.Scene.OutputMessage("请先选中目标");
            actor.ClearMagic();
            return;
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
                GameScene.Scene.OutputMessage(LanguageMgr.Get("TargetTooFar"));
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
        PointF b = MouseLocation.ToVector2();
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

    /// <summary>
    /// 该格子是否"空"（可落脚/可通行）：地形不阻挡且格子上没有阻挡型对象。
    /// 用于寻路、走位等判断能否进入某格。
    /// 地形阻挡来自 map.dat 解码进 CellInfo.Flags 的不可走位(bit3 → IsBlocked)；
    /// 对象阻挡来自 MapObject.Blocking（怪/玩家/NPC 等）。
    /// </summary>
    public bool EmptyCell(Point p)
    {
        if (M2CellInfo == null) return false;
        if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height)
            return false;
        // 用 map.dat 解码进 CellInfo.Flags 的不可走位(bit3)，替代旧的 BackImage/FrontImage 位判断
        //（那两个字段在 CDN 地图下恒为 0，位判断永远为 false）。
        if (M2CellInfo[p.X, p.Y].IsBlocked)
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


    /// <summary>
    /// 该格地形是否有效（可通行）：只看地图本身，不判断格子上是否有对象。
    /// 与 EmptyCell 的区别：EmptyCell 还要求格子上没有阻挡型对象（怪/玩家/NPC）。
    /// 地形有效性来自 map.dat 解码进 CellInfo.Flags 的不可走位(bit3 → IsBlocked)。
    /// </summary>
    public bool ValidPoint(Point p)
    {
        if (M2CellInfo == null) return false;
        if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height)
            return false;
        return !M2CellInfo[p.X, p.Y].IsBlocked;
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
            MouseLocation = Point.Zero;
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
            FloorValid = false;
            LightsValid = false;
            Music = 0;

            AnimationCount = 0;
            Effects.Clear();
        }

        base.Dispose(disposing);
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
