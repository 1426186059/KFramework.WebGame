using PixiGame;
using PixiJS;

namespace PixiDemo;

/// <summary>
/// FC《坦克大战》（Battle City）风格：13×13 网格地图 + 砖墙/钢墙/水/树/基地。
/// 地图为静态 Graphics（砖墙被破坏时局部重画）；每辆坦克 = Container（车体+炮管），
/// 炮管指向由 Pixi 场景图旋转合成；子弹命中砖墙 → 修改格状态并重画地图。
/// </summary>
public sealed class TankBattleGame : GameScene
{
    private const int MapW = 13, MapH = 13;
    private const float Cell = 36f;
    private const float MapOx = 16f, MapOy = 66f;
    private const float TankSize = 30f;      // 车体边长（碰撞盒）

    // ---- 地图定义（13×13）：' '空  'B'砖墙  'S'钢墙  '~'水  '%'树  'E'基地 ----
    private static readonly string[] MapDef =
    {
        ".............",
        ".#.#.#.#.#.#.",
        ".#..S#S...#..",
        ".#.#.#.#.#.#.",
        "......~......",
        ".#.#.#.#.#.#.",
        ".#..~#~...#..",
        ".#.#.#.#.#.#.",
        "......%......",
        ".#.#.#.#.#.#.",
        "..#...#...#..",
        ".....#.#.....",
        ".....EEE.....",
    };
    private static readonly (int x, int y)[] Spawns = { (1, 0), (6, 0), (11, 0) };

    private sealed class Tank
    {
        public Container Root = null!;
        public Graphics Barrel = null!;
        public float X, Y, DirX = 1, DirY = 0;
        public float Cooldown;
        public bool Player;
        public bool Dead;
        public float AiTimer;

        public void SetPos(float x, float y) { X = x; Y = y; Root.X = x; Root.Y = y; }
        public void SetDir(float dx, float dy)
        {
            DirX = dx; DirY = dy;
            Barrel.Rotation = MathF.Atan2(dy, dx);
        }
    }

    private sealed class Bullet
    {
        public Graphics Gfx = null!;
        public float X, Y, Vx, Vy;
        public bool Player;
        public bool Alive = true;
    }

    private char[,] _map = new char[MapW, MapH];
    private readonly List<Tank> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private Tank? _player;
    private Graphics? _mapGfx;
    private PixiText? _hud;
    private Container? _root;
    private int _score, _lives = 3, _level = 1;
    private int _enemySpawned;
    private float _respawnTimer;
    private bool _gameOver, _win, _initialized;

    public TankBattleGame() : base("tank-battle") { }

    public override void Enter()
    {
        var stage = PixiApp.Instance.Stage;
        _bullets.Clear();
        _enemies.Clear();
        _lives = 3;
        _score = 0;
        _level = 1;
        _gameOver = false;
        _win = false;
        _respawnTimer = 1.5f;

        _root = Container.Create();
        stage.AddChild(_root);

        // 背景
        var bg = Graphics.Create();
        bg.DrawRect(0, 0, 960, 540, new Color(0.08f, 0.08f, 0.08f));
        bg.DrawRect(MapOx - 6, MapOy - 6, MapW * Cell + 12, MapH * Cell + 12, new Color(0.25f, 0.25f, 0.28f));
        _root.AddChild(bg);

        BuildLevel();

        // 玩家（地图左下角出生）
        _player = SpawnTank(true, Cell * 1 + MapOx + Cell / 2, MapOy + (MapH - 1) * Cell + Cell / 2 - Cell, 0, -1);

        // HUD
        _hud = new PixiText("", "bold 16px system-ui, sans-serif", new Color(0.85f, 0.85f, 0.9f), "left");
        _hud.X = 520; _hud.Y = 24;
        _root.AddChild(_hud);

        _initialized = true;
    }

    private void BuildLevel()
    {
        // 读地图定义
        for (int r = 0; r < MapH; r++)
            for (int c = 0; c < MapW; c++)
                _map[c, r] = MapDef[r][c];
        _enemySpawned = 0;
        RedrawMap();
    }

    private void RedrawMap()
    {
        if (_mapGfx is not null) { _mapGfx.Clear(); }
        else
        {
            _mapGfx = Graphics.Create();
            _root!.AddChild(_mapGfx);
        }
        _mapGfx.BeginBatch();
        for (int r = 0; r < MapH; r++)
        {
            for (int c = 0; c < MapW; c++)
            {
                float x = MapOx + c * Cell, y = MapOy + r * Cell;
                switch (_map[c, r])
                {
                    case 'B': DrawBrick(x, y); break;
                    case 'S': DrawSteel(x, y); break;
                    case '~': DrawWater(x, y); break;
                    case '%': DrawTree(x, y); break;
                    case 'E': DrawBase(x, y); break;
                }
            }
        }
        _mapGfx.EndBatch();
    }

    private void DrawBrick(float x, float y)
    {
        _mapGfx!.DrawRect(x, y, Cell, Cell, new Color(0.62f, 0.40f, 0.22f));
        _mapGfx.DrawRect(x, y, Cell, Cell / 2, new Color(0.72f, 0.47f, 0.26f));
        _mapGfx.DrawRect(x + Cell / 2, y + Cell / 2, Cell / 2, Cell / 2, new Color(0.72f, 0.47f, 0.26f));
        _mapGfx.DrawLine(x + Cell / 2, y, x + Cell / 2, y + Cell / 2, 1.5f, new Color(0.42f, 0.26f, 0.14f));
        _mapGfx.DrawLine(x, y + Cell / 2, x + Cell, y + Cell / 2, 1.5f, new Color(0.42f, 0.26f, 0.14f));
    }

    private void DrawSteel(float x, float y)
    {
        _mapGfx!.DrawRect(x, y, Cell, Cell, new Color(0.72f, 0.75f, 0.80f));
        _mapGfx.DrawLine(x + 4, y + Cell, x + Cell, y + 4, 3, new Color(0.90f, 0.92f, 0.95f));
        _mapGfx.DrawLine(x, y + Cell - 8, x + 8, y + Cell, 3, new Color(0.90f, 0.92f, 0.95f));
        _mapGfx.DrawLine(x + Cell - 8, y, x + Cell, y + 8, 3, new Color(0.55f, 0.58f, 0.62f));
    }

    private void DrawWater(float x, float y)
    {
        _mapGfx!.DrawRect(x, y, Cell, Cell, new Color(0.15f, 0.35f, 0.65f));
        for (int i = 0; i < 3; i++)
            _mapGfx.DrawLine(x + 4 + i * 6, y + Cell - 6 - i * 10, x + Cell - 6 + i * 6, y + Cell - 6 - i * 10, 2, new Color(0.40f, 0.60f, 0.85f));
    }

    private void DrawTree(float x, float y)
    {
        _mapGfx!.DrawRect(x, y, Cell, Cell, new Color(0.08f, 0.09f, 0.08f));
        _mapGfx.DrawEllipse(x + Cell / 2, y + Cell / 2, Cell * 0.42f, Cell * 0.42f, new Color(0.20f, 0.45f, 0.20f));
        _mapGfx.DrawCircle(x + Cell / 2, y + Cell / 2, 4, new Color(0.32f, 0.60f, 0.28f));
    }

    private void DrawBase(float x, float y)
    {
        // 基地 2×2 格整体绘制（鹰徽）
        _mapGfx!.DrawRect(x, y, Cell, Cell, new Color(0.92f, 0.90f, 0.82f));
        _mapGfx.DrawTriangle(x + Cell / 2, y + 6, x + Cell - 6, y + Cell - 8, x + 6, y + Cell - 8, new Color(0.80f, 0.55f, 0.15f));
        _mapGfx.DrawRect(x + Cell / 2 - 3, y + Cell - 12, 6, 6, new Color(0.60f, 0.40f, 0.10f));
    }

    private Tank SpawnTank(bool player, float x, float y, float dx, float dy)
    {
        var t = new Tank { Player = player };
        t.Root = Container.Create();
        t.Root.X = x; t.Root.Y = y;

        var body = Graphics.Create();
        body.BeginBatch();
        if (player)
        {
            body.DrawRoundedRect(-TankSize / 2, -TankSize / 2, TankSize, TankSize, 4, new Color(0.95f, 0.80f, 0.15f));
            body.DrawRoundedRect(-TankSize / 2, -TankSize / 2 + 2, 7, TankSize - 4, 2, new Color(0.75f, 0.60f, 0.10f));
            body.DrawRoundedRect(TankSize / 2 - 7, -TankSize / 2 + 2, 7, TankSize - 4, 2, new Color(0.75f, 0.60f, 0.10f));
            body.DrawRoundedRect(-6, -6, 12, 12, 3, new Color(0.85f, 0.30f, 0.15f));
        }
        else
        {
            body.DrawRoundedRect(-TankSize / 2, -TankSize / 2, TankSize, TankSize, 4, new Color(0.80f, 0.82f, 0.84f));
            body.DrawRoundedRect(-TankSize / 2, -TankSize / 2 + 2, 7, TankSize - 4, 2, new Color(0.55f, 0.57f, 0.60f));
            body.DrawRoundedRect(TankSize / 2 - 7, -TankSize / 2 + 2, 7, TankSize - 4, 2, new Color(0.55f, 0.57f, 0.60f));
            body.DrawRoundedRect(-6, -6, 12, 12, 3, new Color(0.95f, 0.95f, 0.95f));
        }
        body.EndBatch();
        t.Root.AddChild(body);

        t.Barrel = Graphics.Create();
        t.Barrel.DrawRoundedRect(-3, -TankSize / 2 - 6, 6, 12, 2,
            player ? new Color(0.60f, 0.30f, 0.12f) : new Color(0.40f, 0.42f, 0.45f));
        t.Root.AddChild(t.Barrel);

        t.SetPos(x, y);
        t.SetDir(dx, dy);
        _root!.AddChild(t.Root);
        return t;
    }

    private bool BlockedAt(float x, float y)
    {
        int cx = (int)((x - MapOx) / Cell), cy = (int)((y - MapOy) / Cell);
        if (cx < 0 || cx >= MapW || cy < 0 || cy >= MapH) return true;
        char t = _map[cx, cy];
        return t == 'B' || t == 'S' || t == '~' || t == 'E';
    }

    private bool TankCollides(Tank t, float nx, float ny)
    {
        float h = TankSize / 2 - 2;
        if (BlockedAt(nx - h, ny - h) || BlockedAt(nx + h, ny - h) ||
            BlockedAt(nx - h, ny + h) || BlockedAt(nx + h, ny + h) || BlockedAt(nx, ny))
            return true;
        foreach (var o in _enemies)
        {
            if (o.Dead || ReferenceEquals(o, t)) continue;
            if (MathF.Abs(o.X - nx) < TankSize && MathF.Abs(o.Y - ny) < TankSize) return true;
        }
        if (_player is not null && !_player.Dead && !ReferenceEquals(_player, t) &&
            MathF.Abs(_player.X - nx) < TankSize && MathF.Abs(_player.Y - ny) < TankSize)
            return true;
        return false;
    }

    private void Fire(Tank t)
    {
        var b = new Bullet { Player = t.Player };
        b.Gfx = Graphics.Create();
        b.Gfx.DrawRoundedRect(-3, -3, 6, 6, 2, b.Player ? new Color(1.0f, 0.95f, 0.7f) : new Color(1.0f, 0.6f, 0.5f));
        _root!.AddChild(b.Gfx);
        b.X = t.X + t.DirX * (TankSize / 2 + 4);
        b.Y = t.Y + t.DirY * (TankSize / 2 + 4);
        float sp = t.Player ? 330f : 240f;
        b.Vx = t.DirX * sp; b.Vy = t.DirY * sp;
        _bullets.Add(b);
        Audio.Beep(300, 0.05, "square", 0.04);
    }

    private void GameOver()
    {
        _gameOver = true;
        ShowOverlay("基地被摧毁！按 Enter 重来");
    }

    private void ShowOverlay(string msg)
    {
        var o = new PixiText(msg, "bold 28px system-ui, sans-serif", new Color(1.0f, 0.45f, 0.35f));
        o.Y = 240;
        _root!.AddChild(o);
        _overlay = o;
    }

    private PixiText? _overlay;

    private static readonly (float, float)[] _dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    public override void Update(float dt)
    {
        if (!_initialized) return;
        if (Input.IsKeyPressed(Input.Escape)) { GameApp.Instance.Start("main-menu"); return; }
        if (_gameOver || _win)
        {
            if (Input.IsKeyPressed(Input.Enter)) { GameApp.Instance.Start("tank-battle"); }
            return;
        }

        // ---- 玩家 ----
        if (_player is { Dead: false })
        {
            float dx = 0, dy = 0;
            if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) dx = -1;
            else if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) dx = 1;
            else if (Input.IsKeyDown(Input.ArrowUp) || Input.IsKeyDown(Input.KeyW)) dy = -1;
            else if (Input.IsKeyDown(Input.ArrowDown) || Input.IsKeyDown(Input.KeyS)) dy = 1;
            if (dx != 0 || dy != 0)
            {
                _player.SetDir(dx, dy);
                float nx = _player.X + dx * 135 * dt, ny = _player.Y + dy * 135 * dt;
                if (!TankCollides(_player, nx, ny)) _player.SetPos(nx, ny);
            }
            _player.Cooldown -= dt;
            if (Input.IsKeyPressed(Input.Space) && _player.Cooldown <= 0) { Fire(_player); _player.Cooldown = 0.3f; }
        }

        // ---- 敌军重生 ----
        if (_enemySpawned < 12)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0 && _enemies.Count < 4)
            {
                var (sx, sy) = Spawns[Random.Shared.Next(Spawns.Length)];
                float ex = MapOx + sx * Cell + Cell / 2, ey = MapOy + sy * Cell + Cell / 2;
                bool clash = _player is { Dead: false } && MathF.Abs(_player.X - ex) < TankSize * 2 && MathF.Abs(_player.Y - ey) < TankSize * 2;
                if (!clash)
                {
                    var e = SpawnTank(false, ex, ey, 0, 1);
                    _enemies.Add(e);
                    _enemySpawned++;
                    _respawnTimer = 3f;
                }
                else _respawnTimer = 0.6f;
            }
        }

        // ---- 敌军 AI ----
        foreach (var e in _enemies)
        {
            if (e.Dead) continue;
            e.Cooldown -= dt;
            e.AiTimer -= dt;
            float nx = e.X + e.DirX * 95 * dt, ny = e.Y + e.DirY * 95 * dt;
            if (TankCollides(e, nx, ny) || e.AiTimer <= 0)
            {
                // 撞墙 / 到点 → 随机转向；避免反复横跳用 AiTimer
                if (e.AiTimer <= 0) e.AiTimer = 1.5f;
                var (dx, dy) = _dirs[Random.Shared.Next(_dirs.Length)];
                e.SetDir(dx, dy);
            }
            else e.SetPos(nx, ny);
            if (e.Cooldown <= 0 && Random.Shared.NextSingle() < 0.35f) { Fire(e); e.Cooldown = 1.8f; }
        }

        // ---- 子弹 ----
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.X += b.Vx * dt; b.Y += b.Vy * dt;
            b.Gfx.X = b.X; b.Gfx.Y = b.Y;
            bool dead = false;

            // 出界
            if (b.X < MapOx - 8 || b.X > MapOx + MapW * Cell + 8 || b.Y < MapOy - 8 || b.Y > MapOy + MapH * Cell + 8)
                dead = true;

            // 地图格
            if (!dead)
            {
                int cx = (int)((b.X - MapOx) / Cell), cy = (int)((b.Y - MapOy) / Cell);
                if (cx >= 0 && cx < MapW && cy >= 0 && cy < MapH)
                {
                    char t = _map[cx, cy];
                    if (t == 'B') { _map[cx, cy] = ' '; RedrawMap(); dead = true; Audio.Beep(200, 0.04, "square", 0.05); }
                    else if (t == 'S') dead = true;
                    else if (t == 'E') { GameOver(); dead = true; }
                }
            }

            // 命中坦克
            if (!dead && b.Player)
            {
                foreach (var e in _enemies)
                {
                    if (e.Dead) continue;
                    if (MathF.Abs(e.X - b.X) < TankSize / 2 && MathF.Abs(e.Y - b.Y) < TankSize / 2)
                    {
                        dead = true;
                        _root!.RemoveChild(e.Root); e.Root.Destroy();
                        e.Dead = true;
                        _score += 100;
                        Audio.Beep(500, 0.08, "square", 0.05);
                        break;
                    }
                }
            }
            else if (!dead && !b.Player && _player is { Dead: false })
            {
                if (MathF.Abs(_player.X - b.X) < TankSize / 2 && MathF.Abs(_player.Y - b.Y) < TankSize / 2)
                {
                    dead = true;
                    _lives--;
                    _player.Dead = true;
                    _root!.RemoveChild(_player.Root); _player.Root.Destroy();
                    if (_lives <= 0)
                    {
                        _gameOver = true;
                        ShowOverlay("GAME OVER  得分 " + _score + "  按 Enter 重来");
                    }
                }
            }

            if (dead)
            {
                _root!.RemoveChild(b.Gfx); b.Gfx.Destroy();
                _bullets.RemoveAt(i);
            }
        }

        // ---- 玩家重生（2 秒后，检查点=左下出生点） ----
        if (_player is { Dead: true } && !_gameOver)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= -1.5f)
            {
                var p = SpawnTank(true, Cell * 1 + MapOx + Cell / 2, (MapH - 1) * Cell + MapOy + Cell / 2 - Cell, 0, -1);
                _player = p;
            }
        }

        // ---- 过关 ----
        if (_enemySpawned >= 12 && _enemies.TrueForAll(e => e.Dead))
        {
            _win = true;
            _score += 500;
            ShowOverlay("关卡完成！得分 " + _score + "  按 Enter 再来一局");
        }

        _hud!.Text = $"得分 {_score:0000}\n\n生命 {_lives}\n\n关卡 {_level}\n\n敌军 {Math.Max(0, 12 - _enemySpawned) + _enemies.Count}";
    }

    public override void Exit()
    {
        if (_root is not null) { _root.Destroy(); _root = null; }
        _player = null;
        _enemies.Clear();
    }

    public override void Render() { }
}
