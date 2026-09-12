using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using MirClient.Assets;
using MirClient.Rendering;
using MirEngine;

namespace MirClient;

/// <summary>
/// 客户端主循环。渲染真实 Mir3 地图（.map + 多层 .Zl 图块），支持点击移动与八方向行走动画。
/// </summary>
public static partial class MirGame
{
    private const int CellW = 48;
    private const int CellH = 32;

    private const int ViewW = 1024;
    private const int ViewH = 768;

    // 与 MapControl.cs:145-148 一致
    private const int OffSetX = ViewW / 2 / CellW;   // 10
    private const int OffSetY = ViewH / 2 / CellH;   // 12
    private const int PixelOffsetX = (ViewW - CellW) / 2 - OffSetX * CellW;  // 8
    private const int PixelOffsetY = (ViewH - CellH) / 2 - OffSetY * CellH;  // -16

    private const int BackgroundArgb = unchecked((int)0xFF0B0E14);

    // 动画：直接取自原版移植的 LibraryCore/FrameSet.Players（Standing=0,4,10,500 / Walking=80,6,10,100）
    private static readonly Frame PlayerStand = FrameSet.Players[MirAnimation.Standing];
    private static readonly Frame PlayerWalk = FrameSet.Players[MirAnimation.Walking];
    private static int StandingStart => PlayerStand.StartIndex;
    private static int StandingCount => PlayerStand.FrameCount;
    private static int StandingOffset => PlayerStand.OffSet;
    private static int StandingDelay => (int)PlayerStand.Delays[0].TotalMilliseconds;
    private static int WalkingStart => PlayerWalk.StartIndex;
    private static int WalkingCount => PlayerWalk.FrameCount;
    private static int WalkingOffset => PlayerWalk.OffSet;
    private static int WalkingDelay => (int)PlayerWalk.Delays[0].TotalMilliseconds;

    private const double MoveMsPerCell = 260;

    private const string MapUrl = "MyRes/Data/Map/11.map";
    private const string HumanUrl = "MyRes/Data/M-Hum.Zl";

    /// <summary>地图用到的资源库：KrOrder 编号 → 相对 data 的路径。</summary>
    private static readonly (int FileId, string Path)[] MapLibraries =
    {
        (0, "MyRes/Data/MapData/Tilesc.Zl"),
        (1, "MyRes/Data/MapData/Tiles30c.Zl"),
        (5, "MyRes/Data/MapData/Cliffsc.Zl"),
        (9, "MyRes/Data/MapData/Wallsc.Zl"),
        (10, "MyRes/Data/MapData/SmObjectsc.Zl"),
        (23, "MyRes/Data/MapData/Wood/Furnituresc.Zl"),
        (25, "MyRes/Data/MapData/Wood/SmObjectsc.Zl"),
    };

    private sealed class Actor
    {
        public float X, Y;            // 插值后的浮点格坐标
        public int CellX, CellY;      // 逻辑格
        public int FromX, FromY;
        public int Direction = 4;     // 默认朝下
        public bool Moving;
        public double Progress;
        public int TargetX, TargetY;
        public bool HasTarget;
        public double FrameTime;
        public int FrameIndex;
    }

    private static MapFile? _map;
    private static readonly AssetManager Assets = LibraryManager.Assets;
    private static bool _ready;

    private static readonly Actor Player = new();
    private static readonly List<Actor> Monsters = new();

    private static int _camX, _camY;
    private static bool _followPlayer = true;

    private static bool _dragging;
    private static int _lastMouseX, _lastMouseY;

    private static double _lastTime;
    private static int _animCounter;
    private static double _animTick;

    private static int _frameCount;
    private static double _fpsElapsed;
    private static int _fps;
    private static double _frameMsAvg;
    private static bool _stress;
    private static long _initMs;

    // ===================== 生命周期 =====================

    [JSExport]
    public static string[] GetAssetList()
    {
        string[] list = new string[MapLibraries.Length + 2];
        list[0] = MapUrl;
        list[1] = HumanUrl;
        for (int i = 0; i < MapLibraries.Length; i++)
            list[i + 2] = MapLibraries[i].Path;
        return list;
    }

    [JSExport]
    public static void Init()
    {
        Stopwatch sw = Stopwatch.StartNew();

        _map = MapFile.Load(GetBytesImpl(MapUrl));
        MirCanvas.Log($"[Mir] 地图载入: {_map.Width}x{_map.Height} = {_map.Width * _map.Height:N0} 格");

        foreach ((int fileId, string path) in MapLibraries)
        {
            byte[] data = GetBytesImpl(path);
            Assets.AddLibrary(fileId, data);
            MirCanvas.Log($"[Mir] 资源库 {KrOrder.GetName(fileId),-18} 图片 {Assets.LibraryImageCount(fileId),7:N0}");
        }

        Assets.AddLibrary(AssetManager.HumanLibrary, GetBytesImpl(HumanUrl));

        // 出生点：从地图中心螺旋找一个不阻挡的格子
        FindSpawn();
        _camX = Player.CellX;
        _camY = Player.CellY;
        Player.X = Player.CellX;
        Player.Y = Player.CellY;

        // 放几个“怪物”用于验证深度排序
        SpawnMonsters();

        sw.Stop();
        _initMs = sw.ElapsedMilliseconds;
        MirCanvas.Log($"[Mir] 初始化完成 {sw.ElapsedMilliseconds} ms，出生点 ({Player.CellX},{Player.CellY})");

        _ready = true;
        UpdateStatus();
    }

    private static void FindSpawn()
    {
        if (_map == null) return;

        int cx = _map.Width / 2, cy = _map.Height / 2;
        for (int r = 0; r < Math.Max(_map.Width, _map.Height); r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!_map.InBounds(x, y) || _map.IsBlocking(x, y)) continue;

                    Player.CellX = x;
                    Player.CellY = y;
                    return;
                }
            }
        }

        Player.CellX = cx;
        Player.CellY = cy;
    }

    private static void SpawnMonsters()
    {
        if (_map == null) return;

        Random rnd = new(12345);
        for (int i = 0; i < 12; i++)
        {
            int x = Player.CellX + rnd.Next(-12, 13);
            int y = Player.CellY + rnd.Next(-12, 13);
            if (!_map.InBounds(x, y) || _map.IsBlocking(x, y)) continue;

            Monsters.Add(new Actor
            {
                CellX = x, CellY = y, X = x, Y = y,
                Direction = rnd.Next(8),
                FrameIndex = rnd.Next(StandingCount),
            });
        }
    }

    // ===================== 每帧 =====================

    [JSExport]
    public static void Frame(double timeMs)
    {
        if (!_ready) return;

        double dt = _lastTime == 0 ? 16.0 : timeMs - _lastTime;
        _lastTime = timeMs;
        if (dt > 250) dt = 250; // 切标签页回来时不要瞬移

        _animTick += dt;
        if (_animTick >= 100)
        {
            _animCounter += (int)(_animTick / 100);
            _animTick %= 100;
        }

        UpdatePlayer(dt);
        UpdateMonsters(dt);

        if (_followPlayer)
        {
            _camX = Player.CellX;
            _camY = Player.CellY;
        }

        Stopwatch sw = Stopwatch.StartNew();

        MirCanvas.BeginFrame();
        MirCanvas.Clear(BackgroundArgb);

        if (_stress) DrawStress();
        else DrawMap();

        MirCanvas.Flush();
        sw.Stop();

        _frameCount++;
        _fpsElapsed += dt;
        _frameMsAvg += (sw.Elapsed.TotalMilliseconds - _frameMsAvg) * 0.1;
        if (_fpsElapsed >= 500)
        {
            _fps = (int)(_frameCount * 1000 / _fpsElapsed);
            _frameCount = 0;
            _fpsElapsed = 0;
            UpdateStatus();
        }
    }

    private static void UpdatePlayer(double dt)
    {
        if (Player.Moving)
        {
            Player.Progress += dt / MoveMsPerCell;
            if (Player.Progress >= 1)
            {
                Player.Progress = 0;
                Player.Moving = false;
                Player.X = Player.CellX;
                Player.Y = Player.CellY;
            }
            else
            {
                Player.X = Player.FromX + (Player.CellX - Player.FromX) * (float)Player.Progress;
                Player.Y = Player.FromY + (Player.CellY - Player.FromY) * (float)Player.Progress;
            }
        }
        else if (Player.HasTarget)
        {
            StepAlongField(Player);
        }

        Player.FrameTime += dt;
        int delay = Player.Moving ? WalkingDelay : StandingDelay;
        if (Player.FrameTime >= delay)
        {
            Player.FrameTime = 0;
            Player.FrameIndex++;
        }
    }

    private static void UpdateMonsters(double dt)
    {
        foreach (Actor m in Monsters)
        {
            m.FrameTime += dt;
            if (m.FrameTime >= StandingDelay)
            {
                m.FrameTime = 0;
                m.FrameIndex++;
            }
        }
    }

    /// <summary>
    /// 沿 BFS 距离场走向目标：每次选八邻域中距离最小且严格更小的一格。
    /// 斜向移动要求两条正交边都不阻挡，避免穿墙角。
    /// </summary>
    private static void StepAlongField(Actor actor)
    {
        if (_map == null || _dist == null) return;

        int h = _map.Height;
        int curIndex = actor.CellX * h + actor.CellY;
        if (curIndex < 0 || curIndex >= _dist.Length) { actor.HasTarget = false; return; }

        int curDist = _dist[curIndex];
        if (curDist <= 0) { actor.HasTarget = false; return; } // 已到达或不可达

        int bestDir = -1, bestDist = int.MaxValue;

        for (int i = 0; i < 8; i++)
        {
            int nx = actor.CellX + DirX[i];
            int ny = actor.CellY + DirY[i];

            if (!_map.InBounds(nx, ny) || _map.IsBlocking(nx, ny)) continue;

            // 斜向：正交两侧必须都能走
            if (DirX[i] != 0 && DirY[i] != 0)
            {
                if (_map.IsBlocking(actor.CellX + DirX[i], actor.CellY)) continue;
                if (_map.IsBlocking(actor.CellX, actor.CellY + DirY[i])) continue;
            }

            int d = _dist[nx * h + ny];
            if (d < 0 || d >= curDist || d >= bestDist) continue;

            bestDist = d;
            bestDir = i;
        }

        if (bestDir < 0) { actor.HasTarget = false; return; }

        actor.Direction = bestDir;
        actor.FromX = actor.CellX;
        actor.FromY = actor.CellY;
        actor.CellX += DirX[bestDir];
        actor.CellY += DirY[bestDir];
        actor.Moving = true;
        actor.Progress = 0;
    }

    // 0=上 1=右上 2=右 3=右下 4=下 5=左下 6=左 7=左上
    private static readonly int[] DirX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] DirY = { -1, -1, 0, 1, 1, 1, 0, -1 };

    private static int[] _dist = Array.Empty<int>();
    private static int[] _queue = Array.Empty<int>();

    /// <summary>从目标点做 BFS，生成"每格到目标的距离场"。</summary>
    private static void BuildDistanceField(int targetX, int targetY)
    {
        if (_map == null) return;

        int w = _map.Width, h = _map.Height;
        int size = w * h;

        if (_dist.Length != size)
        {
            _dist = new int[size];
            _queue = new int[size];
        }

        Array.Fill(_dist, -1);

        int start = targetX * h + targetY;
        _dist[start] = 0;

        int head = 0, tail = 0;
        _queue[tail++] = start;

        const int MaxNodes = 30000; // 限制最坏情况开销

        while (head < tail && tail - head < MaxNodes)
        {
            int cur = _queue[head++];
            int cx = cur / h, cy = cur % h;
            int next = _dist[cur] + 1;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + DirX[i];
                int ny = cy + DirY[i];

                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int ni = nx * h + ny;
                if (_dist[ni] != -1) continue;
                if (_map.IsBlocking(nx, ny)) continue;

                // 斜向只从正交可达的格子扩展，保证走法与实际可走一致
                if (DirX[i] != 0 && DirY[i] != 0)
                {
                    if (_map.IsBlocking(cx + DirX[i], cy)) continue;
                    if (_map.IsBlocking(cx, cy + DirY[i])) continue;
                }

                _dist[ni] = next;
                _queue[tail++] = ni;
            }
        }
    }

    // ===================== 绘制 =====================

    private static void DrawMap()
    {
        if (_map == null) return;

        int minX = Math.Max(0, _camX - OffSetX - 4);
        int maxX = Math.Min(_map.Width - 1, _camX + OffSetX + 4);
        int minY = Math.Max(0, _camY - OffSetY - 4);
        int maxY = Math.Min(_map.Height - 1, _camY + OffSetY + 25);

        // ---- 背景层：巨型图块（如 Tiles30c 的 30x30 格）锚点可能远在屏幕外，
        //      需向左上外扩扫描，否则屏幕边缘会出现黑带。
        //      但扫描不等于绘制——先用尺寸做屏幕裁剪，只画真正覆盖画面的块。 ----
        int backMinX = Math.Max(0, (minX - 32) & ~1);
        int backMinY = Math.Max(0, (minY - 32) & ~1);

        for (int y = backMinY; y <= maxY + 1; y += 2)
        {
            int drawYTop = (y - _camY + OffSetY) * CellH + PixelOffsetY;
            if (drawYTop > ViewH) break; // 后面的行只会更靠下

            for (int x = backMinX; x <= maxX + 1; x += 2)
            {
                int drawX = (x - _camX + OffSetX) * CellW + PixelOffsetX;
                if (drawX > ViewW) break; // 后面的列只会更靠右

                MapCell cell = _map[x, y];
                if (cell.BackFile == KrOrder.NoImage) continue;

                // 宽度/高度来自索引，无需解码像素；巨型块锚点常在屏幕外，这里直接剔除
                if (!Assets.TryGetSize(cell.BackFile, cell.BackImage, out short bw, out short bh)) continue;
                if (drawX + bw <= 0 || drawYTop + bh <= 0) continue;

                DrawLayer(cell.BackFile, cell.BackImage, drawX, drawYTop, bottomAligned: false);
            }
        }

        // ---- 中景 / 前景 / 角色（按行扫，行尾插角色实现深度排序）----
        for (int y = minY; y <= maxY; y++)
        {
            int drawYBottom = (y - _camY + OffSetY + 1) * CellH + PixelOffsetY;

            for (int x = minX; x <= maxX; x++)
            {
                int drawX = (x - _camX + OffSetX) * CellW + PixelOffsetX;
                MapCell cell = _map[x, y];

                // ---- 中景层（Tilesc 由背景层负责，这里跳过）----
                if (KrOrder.IsDrawable(cell.MiddleFile))
                {
                    int index = cell.MiddleImage - 1;
                    if (cell.MiddleAnimationFrame > 1 && cell.MiddleAnimationFrame < KrOrder.NoImage)
                    {
                        int count = cell.MiddleAnimationCount;
                        if (count > 0) index += _animCounter % count;
                    }
                    DrawLayer(cell.MiddleFile, index, drawX, drawYBottom, bottomAligned: true, frontLayer: false);
                }

                // ---- 前景层 ----
                if (KrOrder.IsDrawable(cell.FrontFile))
                {
                    int index = cell.FrontImage - 1;
                    if (cell.FrontAnimationFrame > 1 && cell.FrontAnimationFrame < KrOrder.NoImage)
                    {
                        int count = cell.FrontAnimationCount;
                        if (count > 0) index += _animCounter % count;
                    }
                    DrawLayer(cell.FrontFile, index, drawX, drawYBottom, bottomAligned: true, frontLayer: true);
                }
            }

            // ---- 该行的角色（深度排序）----
            int playerRow = (int)Math.Round(Player.Y);
            if (playerRow == y) DrawActor(Player);

            foreach (Actor m in Monsters)
            {
                if (m.CellY == y) DrawActor(m);
            }
        }
    }

    /// <summary>
    /// 绘制一个地图图块。
    /// - 背景层（bottomAligned=false）：顶对齐，画在格顶。
    /// - 中景 / 前景（bottomAligned=true）：底对齐，图片底边贴锚点 y。
    ///   与原版 Zircon <c>MapControl.DrawObjects</c> 一致：中景一律 <c>drawY - h</c>；
    ///   前景仅当「整格尺寸」(48x32 或 96x64) 时才用 <c>drawY - CellH</c>，否则也是 <c>drawY - h</c>。
    ///   （差异点：原版中景 96x64 画在 drawY-64，前景 96x64 画在 drawY-32。）
    /// </summary>
    private static void DrawLayer(int fileId, int index, int drawX, int drawY, bool bottomAligned, bool frontLayer = false)
    {
        if (index < 0) return;

        int key = Assets.GetTexture(fileId, index);
        if (key == 0) return;

        if (!Assets.TryGetSize(fileId, index, out short w, out short h)) return;

        int y = drawY;
        if (bottomAligned)
        {
            bool cellSized = (w == CellW && h == CellH) || (w == CellW * 2 && h == CellH * 2);
            // 中景一律 drawY - h；前景整格尺寸才 drawY - CellH
            y = (frontLayer && cellSized) ? drawY - CellH : drawY - h;
        }

        // 屏幕裁剪：省掉完全在可视区外的绘制调用
        if (drawX + w <= 0 || y + h <= 0 || drawX >= ViewW || y >= ViewH) return;

        MirCanvas.Draw(key, 0, 0, w, h, drawX, y, w, h);
    }

    private static void DrawActor(Actor actor)
    {
        bool walking = actor.Moving;
        int start = walking ? WalkingStart : StandingStart;
        int count = walking ? WalkingCount : StandingCount;
        int offset = walking ? WalkingOffset : StandingOffset;

        int frame = walking ? actor.FrameIndex % count : actor.FrameIndex % count;
        int index = start + actor.Direction * offset + frame;

        int key = Assets.GetTexture(AssetManager.HumanLibrary, index);
        if (key == 0) return;
        if (!Assets.TryGetSize(AssetManager.HumanLibrary, index, out short w, out short h)) return;

        // 角色锚点 = 格子左上角，再套用 .Zl 里的 OffSet（角色图以此居中）
        int drawX = (int)((actor.X - _camX + OffSetX) * CellW + PixelOffsetX);
        int drawY = (int)((actor.Y - _camY + OffSetY) * CellH + PixelOffsetY);

        Assets.TryGetOffset(AssetManager.HumanLibrary, index, out short offX, out short offY);

        MirCanvas.Draw(key, 0, 0, w, h, drawX + offX, drawY + offY, w, h);
    }

    private static void DrawStress()
    {
        int index = WalkingStart + (_animCounter % WalkingCount);
        int key = Assets.GetTexture(AssetManager.HumanLibrary, index);
        if (key == 0) return;
        if (!Assets.TryGetSize(AssetManager.HumanLibrary, index, out short w, out short h)) return;

        for (int y = 0; y < 42; y++)
        {
            for (int x = 0; x < 51; x++)
            {
                MirCanvas.Draw(key, 0, 0, w, h, x * 20, y * 18, w, h);
            }
        }
    }

    // ===================== 输入 =====================

    [JSExport]
    public static void OnMouseDown(int button, int x, int y)
    {
        if (button != 0 || !_ready) return;
        _dragging = true;
        _lastMouseX = x;
        _lastMouseY = y;
    }

    [JSExport]
    public static void OnMouseMove(int x, int y)
    {
        if (!_dragging) return;

        _camX -= (x - _lastMouseX) / CellW;
        _camY -= (y - _lastMouseY) / CellH;
        _lastMouseX = x;
        _lastMouseY = y;

        if (_map != null)
        {
            _camX = Math.Clamp(_camX, 0, _map.Width - 1);
            _camY = Math.Clamp(_camY, 0, _map.Height - 1);
        }
    }

    [JSExport]
    public static void OnMouseUp(int button, int x, int y)
    {
        if (button != 0 || !_dragging) return;
        _dragging = false;

        int dx = Math.Abs(x - _lastMouseX);
        int dy = Math.Abs(y - _lastMouseY);

        // 位移很小 → 视为点击移动；否则视为拖动视角
        if (dx <= 3 && dy <= 3)
            SetPlayerTarget(x, y);
        else
            _followPlayer = false;
    }

    private static void SetPlayerTarget(int screenX, int screenY)
    {
        if (_map == null) return;

        int cellX = (int)Math.Floor((screenX - PixelOffsetX) / (double)CellW + _camX - OffSetX);
        int cellY = (int)Math.Floor((screenY - PixelOffsetY) / (double)CellH + _camY - OffSetY);

        if (!_map.InBounds(cellX, cellY)) return;

        // 点到树/石头/建筑上时，改为走到它旁边、且离玩家最近的可通行格
        if (_map.IsBlocking(cellX, cellY))
        {
            int bestX = -1, bestY = -1, bestDist = int.MaxValue;

            for (int r = 1; r <= 8; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cellX + dx, y = cellY + dy;
                        if (!_map.InBounds(x, y) || _map.IsBlocking(x, y)) continue;

                        // 选离「点击点」最近的可通行格，而不是离玩家最近
                        int d = Math.Abs(dx) + Math.Abs(dy);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestX = x;
                            bestY = y;
                        }
                    }
                }
                if (bestX >= 0 && r >= 3) break; // 前三圈扫完即可，避免大幅偏离点击点
            }

            if (bestX < 0) return;

            MirCanvas.Log($"[Mir] 点击格 ({cellX},{cellY}) 阻挡，改走 ({bestX},{bestY})");
            cellX = bestX;
            cellY = bestY;
        }

        MirCanvas.Log($"[Mir] 目标 cell=({cellX},{cellY}) 玩家=({Player.CellX},{Player.CellY})");

        Player.TargetX = cellX;
        Player.TargetY = cellY;
        Player.HasTarget = true;

        BuildDistanceField(cellX, cellY);

        int playerDist = _dist[Player.CellX * _map.Height + Player.CellY];
        MirCanvas.Log($"[Mir] 距离场目标 ({cellX},{cellY})，玩家 ({Player.CellX},{Player.CellY}) 距离={playerDist}");
    }

    [JSExport]
    public static void OnKey(string key)
    {
        switch (key)
        {
            case "b":
                MirCanvas.Batched = !MirCanvas.Batched;
                break;
            case "s":
                _stress = !_stress;
                break;
            case "f":
                _followPlayer = true;
                break;
            case "r":
                _followPlayer = true;
                Player.HasTarget = false;
                Player.Moving = false;
                break;
        }
        UpdateStatus();
    }

    // ===================== 状态 =====================

    private static void UpdateStatus()
    {
        string mapInfo = _map == null ? "-" : $"{_map.Width} x {_map.Height}";

        string mode = MirCanvas.Batched
            ? "<b class='ok'>批命令</b>（每帧 1 次跨边界调用）"
            : "<b class='warn'>直调</b>（每帧 N 次跨边界调用）";

        MirCanvas.SetStatus($@"
<div class='row'><span>FPS</span><b>{_fps}</b></div>
<div class='row'><span>绘制调用 / 帧</span><b>{MirCanvas.DrawCalls:N0}</b></div>
<div class='row'><span>C# 帧耗时</span><b>{_frameMsAvg:F2} ms</b></div>
<div class='row'><span>互操作模式</span>{mode}</div>
<div class='row'><span>地图</span><b>11.map</b> <span>{mapInfo}</span></div>
<div class='row'><span>玩家坐标</span><b>{Player.CellX}, {Player.CellY}</b> <span>朝向 {Player.Direction}</span></div>
<div class='row'><span>目标</span><b>{(Player.HasTarget ? $"{Player.TargetX}, {Player.TargetY}" : "无")}</b></div>
<div class='row'><span>已上传纹理</span><b>{Assets.TextureCount:N0}</b> <span>解码失败 {Assets.FailedCount:N0}</span></div>
<div class='row'><span>初始化耗时</span><b>{_initMs} ms</b></div>
<div class='row'><span>摄像机</span><b>{_camX}, {_camY}</b> <span>{(_followPlayer ? "跟随" : "自由")}</span></div>
<div class='hint'>点击地面移动 · 拖动 = 自由视角 · F 回到跟随 · B 切换互操作 · S 压力测试 · R 停止</div>");
    }

    [JSImport("mir.getBytes", "main.js")]
    private static partial byte[] GetBytesImpl(string url);
}
