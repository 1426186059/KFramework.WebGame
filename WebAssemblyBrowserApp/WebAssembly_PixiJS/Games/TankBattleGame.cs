using PixiGame;
using PixiJS;

namespace PixiDemo;

/// <summary>
/// 坦克大战（参考 Battle City / WebGL 版 TankScene）：
/// 三关 + 居中战场 + 敌人寻的 AI + 砖墙 2×2 子块 + 5 种道具 + 粒子爆炸 + 屏幕震动 + 出生闪光 + 完整 HUD。
/// </summary>
public sealed class TankBattleGame : GameScene
{
    private const float Cell = 36f;
    private const int MapW = 13, MapH = 13;
    private const float MapSize = MapW * Cell;                 // 468
    private const float MapOx = (960f - MapSize) / 2f;         // 246，战场水平居中
    private const float MapOy = 40f;                            // 顶部留 HUD
    private const float TankSize = 30f;
    private const float TankHalf = TankSize / 2f;
    private const int BaseCol = 6, BaseRow = 12;

    private static readonly int[] DirX = { 0, 1, 0, -1 };
    private static readonly int[] DirY = { -1, 0, 1, 0 };

    private static readonly string[][] Levels =
    {
        new[]
        {
            ".............",
            "..#..#..#..#.",
            "..#..#..#..#.",
            "..#..#..#..#.",
            "......~......",
            "..#####.####.",
            ".....#..#.#..",
            ".....#..#.#..",
            "..B..........",
            "..........B..",
            ".....#B#.....",
            ".....#B#.....",
            ".B..B.E.B..B.",
        },
        new[]
        {
            ".............",
            "..S....S.....",
            "..#.#..B.#.#.",
            "..#.#..B.#.#.",
            "....#..B.....",
            "..S.###.##..S",
            "....#....#...",
            "....#....#...",
            "..B....#..B..",
            "..B....#..B..",
            ".....#S#.....",
            ".....#S#.....",
            "BB..B..E.B..B",
        },
        new[]
        {
            ".............",
            "..S......S...",
            "..#.#####.#..",
            "..#.#~#~#.#..",
            "..###~#~###..",
            "....##.##....",
            "..B.##.##.B..",
            "..B.##.##.B..",
            "....##.##....",
            "....#SSS#....",
            "....#SSS#....",
            "....#SSS#....",
            "BB..B.E.B..B",
        },
    };

    private static readonly (int x, int y)[] Spawns = { (0, 0), (6, 0), (12, 0) };

    private enum TankType { Basic, Fast, Heavy, Armor }
    private enum PowerKind { Star, Grenade, Shovel, Helmet, Life }
    private enum State { Intro, Playing, Over }

    private sealed class Tank
    {
        public Container Root = null!;
        public float X, Y;
        public int Dir;
        public float Speed;
        public float Cooldown;
        public bool Player;
        public TankType Type;
        public int Hp = 1;
        public int Score = 100;
        public bool Dead;
        public float SpawnInvul;
        public float Immortal;

        public void SetPos(float x, float y) { X = x; Y = y; Root.X = x; Root.Y = y; }
        public void SetDir(int d) { Dir = d; Root.Rotation = d * MathF.PI / 2f; }
    }

    private sealed class Bullet
    {
        public Graphics Gfx = null!;
        public float X, Y, Vx, Vy;
        public bool Player;
        public bool Alive = true;
    }

    private sealed class Particle
    {
        public float X, Y, Vx, Vy, Life, Size;
        public Color Color;
        public bool Alive = true;
    }

    private sealed class PowerUp
    {
        public Container Root = null!;
        public float X, Y;
        public PowerKind Kind;
        public float Life = 9f;
    }

    private Container _root = null!;
    private Container _world = null!;          // 战场（含震动偏移）
    private Graphics _mapGfx = null!;
    private Graphics _treeGfx = null!;
    private Graphics _fxGfx = null!;           // 粒子
    private Graphics _flashGfx = null!;        // 出生闪光
    private Graphics _hudLivesGfx = null!;
    private PixiText _hudStage = null!;
    private PixiText _hudStatus = null!;
    private PixiText _hudScore = null!;
    private PixiText _hudHigh = null!;
    private PixiText _hudEnemy = null!;
    private Container _overlay = null!;
    private PixiText _overlayTitle = null!;
    private PixiText _overlaySub = null!;

    private char[,] _map = new char[MapW, MapH];
    private bool[,,,] _brickBits = new bool[MapW, MapH, 2, 2];
    private readonly char?[,] _steelSave = new char?[3, 3];
    private readonly List<Tank> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Particle> _particles = new();
    private readonly List<PowerUp> _powerUps = new();
    private readonly List<(float x, float y, float t)> _spawnFlashes = new();
    private Tank? _player;

    private State _state;
    private float _stateTime;
    private int _level = 1;
    private int _score;
    private int _high;
    private int _lives = 3;
    private int _power = 1;
    private int _enemiesTotal;
    private int _enemiesLeft;
    private int _enemiesSpawned;
    private int _maxEnemies = 4;
    private float _spawnTimer;
    private float _shovelTimer;
    private float _shock;
    private bool _won;

    public TankBattleGame() : base("tank-battle") { }

    public override void Enter()
    {
        var stage = PixiApp.Instance.Stage;
        _root = Container.Create();
        stage.AddChild(_root);

        var bg = Graphics.Create();
        bg.DrawRect(0, 0, 960, 540, new Color(0.035f, 0.05f, 0.08f));
        _root.AddChild(bg);

        // ---- 战场（可震动） ----
        _world = Container.Create();
        _root.AddChild(_world);

        var border = Graphics.Create();
        border.BeginBatch();
        border.DrawRect(MapOx - 6, MapOy - 6, MapSize + 12, MapSize + 12, new Color(0.10f, 0.14f, 0.19f));
        border.DrawRect(MapOx - 3, MapOy - 3, MapSize + 6, MapSize + 6, new Color(0.45f, 0.55f, 0.65f, 0.35f));
        border.EndBatch();
        _world.AddChild(border);

        _mapGfx = Graphics.Create();
        _world.AddChild(_mapGfx);
        _treeGfx = Graphics.Create();
        _world.AddChild(_treeGfx);
        _fxGfx = Graphics.Create();
        _world.AddChild(_fxGfx);
        _flashGfx = Graphics.Create();
        _world.AddChild(_flashGfx);

        // ---- HUD ----
        BuildHud();
        BuildOverlay();

        // ---- 初始数据 ----
        _high = ParseInt(Storage.Get("tank.high", "0"), 0);
        _level = 1;
        _score = 0;
        _lives = 3;
        _power = 1;
        _enemies.Clear();
        _bullets.Clear();
        _particles.Clear();
        _powerUps.Clear();
        _spawnFlashes.Clear();
        _shovelTimer = 0;

        StartLevel(1);
    }

    public override void Exit()
    {
        if (_root is not null) { _root.Destroy(); _root = null!; }
    }

    // =====================================================================
    //  开局 / 关卡
    // =====================================================================
    private void StartLevel(int level)
    {
        _level = level;
        _state = State.Intro;
        _stateTime = 0;

        // 清场
        foreach (var e in _enemies) { _world.RemoveChild(e.Root); e.Root.Destroy(); }
        _enemies.Clear();
        foreach (var b in _bullets) { _world.RemoveChild(b.Gfx); b.Gfx.Destroy(); }
        _bullets.Clear();
        foreach (var p in _powerUps) { _world.RemoveChild(p.Root); p.Root.Destroy(); }
        _powerUps.Clear();
        _particles.Clear();
        _spawnFlashes.Clear();
        if (_player is not null && !_player.Dead) { _world.RemoveChild(_player.Root); _player.Root.Destroy(); }
        _player = null;
        _shovelTimer = 0;

        // 载入地图
        var rows = Levels[level - 1];
        for (int r = 0; r < MapH; r++)
            for (int c = 0; c < MapW; c++)
                _map[c, r] = rows[r][c];
        for (int r = 0; r < MapH; r++)
            for (int c = 0; c < MapW; c++)
                for (int sr = 0; sr < 2; sr++)
                    for (int sc = 0; sc < 2; sc++)
                        _brickBits[c, r, sr, sc] = true;
        RedrawMap();
        RedrawTrees();

        // 敌军配额
        _enemiesTotal = 10 + level * 2;
        _enemiesLeft = _enemiesTotal;
        _enemiesSpawned = 0;
        _maxEnemies = level == 3 ? 5 : 4;
        _spawnTimer = 1.0f;

        _player = SpawnPlayer();
        ShowIntro();
    }

    private void ShowIntro()
    {
        _overlay.Visible = true;
        _overlayTitle.Text = $"STAGE {_level}";
        _overlaySub.Text = "消灭所有敌军，守住基地！";
    }

    // =====================================================================
    //  Update
    // =====================================================================
    public override void Update(float dt)
    {
        _stateTime += dt;
        _shock = MathF.Max(0, _shock - dt);
        if (_shovelTimer > 0)
        {
            _shovelTimer -= dt;
            if (_shovelTimer <= 0) SteelAroundBase(false);
        }

        UpdateParticles(dt);
        UpdateSpawnFlashes(dt);

        switch (_state)
        {
            case State.Intro:
                if (_stateTime > 2.2f) { _state = State.Playing; _overlay.Visible = false; }
                else if (_stateTime > 0.4f && (Input.IsMousePressed() || Input.IsKeyPressed(Input.Enter))) { _state = State.Playing; _overlay.Visible = false; }
                break;

            case State.Playing:
                UpdatePlayer(dt);
                UpdateEnemies(dt);
                UpdateBullets(dt);
                UpdateSpawning(dt);
                UpdatePowerUps(dt);
                UpdateHud();
                CheckStageResult();
                break;

            case State.Over:
                if (Input.IsKeyPressed(Input.Escape)) { GameApp.Instance.Start("main-menu"); return; }
                if (Input.IsKeyPressed(Input.Enter) || Input.IsMousePressed())
                    GameApp.Instance.Start("tank-battle");
                break;
        }
    }

    private void UpdatePlayer(float dt)
    {
        var p = _player;
        if (p is null || p.Dead) return;
        if (p.SpawnInvul > 0) p.SpawnInvul -= dt;
        if (p.Immortal > 0) p.Immortal -= dt;

        int nd = p.Dir;
        if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) nd = 3;
        else if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) nd = 1;
        else if (Input.IsKeyDown(Input.ArrowUp) || Input.IsKeyDown(Input.KeyW)) nd = 0;
        else if (Input.IsKeyDown(Input.ArrowDown) || Input.IsKeyDown(Input.KeyS)) nd = 2;
        if (nd != p.Dir) p.SetDir(nd);

        float nx = p.X + DirX[p.Dir] * p.Speed * dt;
        float ny = p.Y + DirY[p.Dir] * p.Speed * dt;
        if (CanMove(p, nx, ny)) p.SetPos(nx, ny);
        else if (CanMove(p, nx, p.Y)) p.SetPos(nx, p.Y);
        else if (CanMove(p, p.X, ny)) p.SetPos(p.X, ny);
        p.SetPos(
            ClampF(p.X, MapOx + TankHalf, MapOx + MapSize - TankHalf),
            ClampF(p.Y, MapOy + TankHalf, MapOy + MapSize - TankHalf));

        p.Cooldown -= dt;
        if ((Input.IsKeyDown(Input.Space) || Input.IsKeyDown(Input.KeyJ) || Input.IsMouseDown()) && p.Cooldown <= 0)
        {
            int active = 0;
            foreach (var b in _bullets) if (b.Alive && b.Player) active++;
            if (active < _power)
            {
                Fire(p, true);
                p.Cooldown = 0.24f;
            }
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in _enemies)
        {
            if (e.Dead) continue;
            if (e.SpawnInvul > 0) e.SpawnInvul -= dt;
            e.Cooldown -= dt;

            // 随机换向 + 偶尔朝玩家方向
            if (Random.Shared.NextDouble() < dt * 0.7)
                e.SetDir(Random.Shared.NextDouble() < 0.45f ? DirToward(e) : Random.Shared.Next(4));

            float nx = e.X + DirX[e.Dir] * e.Speed * dt;
            float ny = e.Y + DirY[e.Dir] * e.Speed * dt;
            if (CanMove(e, nx, ny)) e.SetPos(nx, ny);
            else if (CanMove(e, nx, e.Y)) e.SetPos(nx, e.Y);
            else if (CanMove(e, e.X, ny)) e.SetPos(e.X, ny);
            else e.SetDir(Random.Shared.Next(4));
            e.SetPos(
                ClampF(e.X, MapOx + TankHalf, MapOx + MapSize - TankHalf),
                ClampF(e.Y, MapOy + TankHalf, MapOy + MapSize - TankHalf));

            if (e.Cooldown <= 0 && Random.Shared.NextDouble() < dt * 2.2)
            {
                int active = 0;
                foreach (var b in _bullets) if (b.Alive && !b.Player) active++;
                if (active < 3)
                {
                    Fire(e, false);
                    e.Cooldown = 1.2f + 1.3f * Random.Shared.NextSingle();
                }
            }
        }
        _enemies.RemoveAll(e => e.Dead);
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            if (!b.Alive) { _world.RemoveChild(b.Gfx); b.Gfx.Destroy(); _bullets.RemoveAt(i); continue; }

            float dist = MathF.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy) * dt;
            int steps = Math.Max(1, (int)MathF.Ceiling(dist / 8f));
            float px = b.Vx * dt / steps, py = b.Vy * dt / steps;
            for (int s = 0; s < steps && b.Alive; s++)
            {
                b.X += px;
                b.Y += py;
                StepBullet(b);
            }
            b.Gfx.X = b.X;
            b.Gfx.Y = b.Y;
        }
    }

    private void StepBullet(Bullet b)
    {
        // 出界
        if (b.X < MapOx - 12 || b.X > MapOx + MapSize + 12 || b.Y < MapOy - 12 || b.Y > MapOy + MapSize + 12)
        {
            b.Alive = false;
            return;
        }

        int cx = (int)((b.X - MapOx) / Cell);
        int cy = (int)((b.Y - MapOy) / Cell);
        if (cx >= 0 && cx < MapW && cy >= 0 && cy < MapH)
        {
            char t = _map[cx, cy];
            if (t == 'B')
            {
                if (HitBrick(cx, cy, b.X, b.Y)) { b.Alive = false; return; }
                // 已打穿 → 本步穿透，继续飞
            }
            else if (t == 'S')
            {
                SpawnBurst(b.X, b.Y, new Color(0.85f, 0.87f, 0.92f), 8, 140);
                Audio.Beep(320, 0.03, "square", 0.04);
                b.Alive = false;
                return;
            }
            else if (t == 'E')
            {
                DestroyBase();
                b.Alive = false;
                return;
            }
        }

        // 打坦克
        if (b.Player)
        {
            foreach (var e in _enemies)
            {
                if (e.Dead) continue;
                if (PointInTank(b.X, b.Y, e))
                {
                    HitEnemy(e, b);
                    b.Alive = false;
                    return;
                }
            }
        }
        else if (_player is not null && !_player.Dead && PointInTank(b.X, b.Y, _player))
        {
            HitPlayer(b);
            b.Alive = false;
        }
    }

    private void UpdateSpawning(float dt)
    {
        if (_enemiesSpawned >= _enemiesTotal) return;
        int alive = 0;
        foreach (var e in _enemies) if (!e.Dead) alive++;
        if (alive >= _maxEnemies) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0) return;
        foreach (var s in Spawns)
        {
            float x = MapOx + s.x * Cell + Cell / 2;
            float y = MapOy + s.y * Cell + Cell / 2;
            bool occupied = false;
            foreach (var e in _enemies)
                if (!e.Dead && MathF.Abs(e.X - x) < TankSize && MathF.Abs(e.Y - y) < TankSize) { occupied = true; break; }
            if (!occupied && _player is not null && !_player.Dead &&
                MathF.Abs(_player.X - x) < TankSize && MathF.Abs(_player.Y - y) < TankSize) occupied = true;
            if (occupied) continue;

            var e2 = CreateTank(false, x, y);
            e2.SpawnInvul = 1f;
            e2.SetDir(2);
            _enemies.Add(e2);
            _spawnFlashes.Add((x, y, 0.8f));
            _enemiesSpawned++;
            Audio.Beep(300, 0.18, "square", 0.05);
            _spawnTimer = 2.2f;
            return;
        }
        _spawnTimer = 0.5f;
    }

    private void UpdatePowerUps(float dt)
    {
        var p = _player;
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var pu = _powerUps[i];
            pu.Life -= dt;
            pu.Root.Alpha = 0.72f + 0.28f * MathF.Sin(GameApp.Instance.Time * 6f + pu.X);
            bool collected = p is not null && !p.Dead &&
                MathF.Abs(p.X - pu.X) < TankSize && MathF.Abs(p.Y - pu.Y) < TankSize;
            if (collected) ApplyPowerUp(pu.Kind);
            if (collected || pu.Life <= 0)
            {
                _world.RemoveChild(pu.Root);
                pu.Root.Destroy();
                _powerUps.RemoveAt(i);
            }
        }
    }

    private void CheckStageResult()
    {
        if (_state != State.Playing) return;
        bool anyEnemy = false;
        foreach (var e in _enemies) if (!e.Dead) { anyEnemy = true; break; }
        if (_enemiesLeft <= 0 && !anyEnemy)
        {
            if (_level >= Levels.Length) { GameOver(true); }
            else { StartLevel(_level + 1); }
        }
    }

    // =====================================================================
    //  战斗
    // =====================================================================
    private void Fire(Tank t, bool player)
    {
        float sp = player ? 340f : 240f;
        float vx = DirX[t.Dir] * sp, vy = DirY[t.Dir] * sp;
        int count = player ? _power : 1;
        for (int i = 0; i < count; i++)
        {
            float off = (i - (count - 1) / 2f) * 7f;
            float ox = -DirY[t.Dir] * off, oy = DirX[t.Dir] * off;
            var b = new Bullet { Player = player, X = t.X + DirX[t.Dir] * 18 + ox, Y = t.Y + DirY[t.Dir] * 18 + oy, Vx = vx, Vy = vy };
            b.Gfx = Graphics.Create();
            b.Gfx.DrawRoundedRect(-3.5f, -3.5f, 7, 7, 2, player ? new Color(1f, 0.88f, 0.40f) : new Color(1f, 0.42f, 0.42f));
            b.Gfx.X = b.X;
            b.Gfx.Y = b.Y;
            _world.AddChild(b.Gfx);
            _bullets.Add(b);
        }
        Audio.Beep(player ? 520 : 300, 0.05, "square", player ? 0.04 : 0.03);
    }

    private bool HitBrick(int cx, int cy, float x, float y)
    {
        float ox = x - (MapOx + cx * Cell);
        float oy = y - (MapOy + cy * Cell);
        int sc = ox < Cell / 2 ? 0 : 1;
        int sr = oy < Cell / 2 ? 0 : 1;
        if (!_brickBits[cx, cy, sr, sc]) return false;   // 已打穿
        _brickBits[cx, cy, sr, sc] = false;
        SpawnBurst(x, y, new Color(0.83f, 0.62f, 0.30f), 8, 150);
        Audio.Beep(240, 0.04, "square", 0.05);
        RedrawMap();
        return true;
    }

    private void HitEnemy(Tank e, Bullet b)
    {
        if (e.SpawnInvul > 0)
        {
            SpawnBurst(b.X, b.Y, new Color(1f, 1f, 1f), 6, 100);
            return;
        }
        e.Hp--;
        if (e.Hp <= 0)
        {
            e.Dead = true;
            _score += e.Score;
            _enemiesLeft = Math.Max(0, _enemiesLeft - 1);
            _world.RemoveChild(e.Root);
            e.Root.Destroy();
            var c = EnemyColor(e.Type);
            SpawnBurst(e.X, e.Y, c, 16, 200);
            SpawnBurst(e.X, e.Y, new Color(1f, 0.83f, 0.23f), 8, 140);
            Audio.Beep(420, 0.1, "square", 0.08);
            _shock = 0.08f;
            MaybeDropPowerUp(e.X, e.Y);
        }
        else
        {
            SpawnBurst(b.X, b.Y, new Color(0.85f, 0.87f, 0.9f), 8, 120);
            Audio.Beep(340, 0.05, "square", 0.05);
        }
    }

    private void HitPlayer(Bullet b)
    {
        var p = _player!;
        if (p.SpawnInvul > 0 || p.Immortal > 0)
        {
            SpawnBurst(b.X, b.Y, new Color(0.4f, 0.6f, 1f), 8, 120);
            return;
        }
        p.Dead = true;
        _lives--;
        _world.RemoveChild(p.Root);
        p.Root.Destroy();
        SpawnBurst(p.X, p.Y, new Color(1f, 0.83f, 0.23f), 20, 240);
        Audio.Beep(160, 0.4, "sawtooth", 0.12);
        _shock = 0.25f;
        if (_lives <= 0) GameOver(false);
        else
        {
            _player = SpawnPlayer();
            Audio.Beep(440, 0.15, "triangle", 0.09);
        }
    }

    private void DestroyBase()
    {
        _map[BaseCol, BaseRow] = ' ';
        RedrawMap();
        float bx = MapOx + BaseCol * Cell + Cell / 2;
        float by = MapOy + BaseRow * Cell + Cell / 2;
        SpawnBurst(bx, by, new Color(0.9f, 0.25f, 0.2f), 26, 280);
        SpawnBurst(bx, by, new Color(1f, 0.83f, 0.23f), 18, 200);
        Audio.Beep(120, 0.6, "sawtooth", 0.15);
        _shock = 0.3f;
        GameOver(false);
    }

    private void GameOver(bool won)
    {
        _won = won;
        _state = State.Over;
        _stateTime = 0;
        _overlay.Visible = true;
        if (won)
        {
            _overlayTitle.Text = "全部通关！";
            _overlaySub.Text = $"得分 {_score}   按 Enter 重新开始";
        }
        else
        {
            _overlayTitle.Text = "基地被毁…";
            _overlaySub.Text = $"得分 {_score}   按 Enter 重新开始";
        }
        if (_score > _high)
        {
            _high = _score;
            Storage.Set("tank.high", _high.ToString());
        }
        Audio.Beep(180, 0.5, "sawtooth", 0.1);
    }

    // =====================================================================
    //  移动 / 碰撞
    // =====================================================================
    private bool CanMove(Tank t, float nx, float ny) => !TankBlocked(nx, ny) && !TankHitsTank(t, nx, ny);

    private bool TankBlocked(float nx, float ny)
    {
        float h = TankHalf - 2;
        return TileBlockedAt(nx - h, ny - h) || TileBlockedAt(nx + h, ny - h) ||
               TileBlockedAt(nx - h, ny + h) || TileBlockedAt(nx + h, ny + h);
    }

    private bool TileBlockedAt(float x, float y)
    {
        int cx = (int)((x - MapOx) / Cell);
        int cy = (int)((y - MapOy) / Cell);
        if (cx < 0 || cx >= MapW || cy < 0 || cy >= MapH) return true;   // 出战场 = 阻挡
        char t = _map[cx, cy];
        if (t == 'B')
            return _brickBits[cx, cy, 0, 0] || _brickBits[cx, cy, 0, 1] ||
                   _brickBits[cx, cy, 1, 0] || _brickBits[cx, cy, 1, 1];
        return t == 'S' || t == '~' || t == 'E';
    }

    private bool TankHitsTank(Tank t, float nx, float ny)
    {
        if (_player is not null && _player != t && !_player.Dead &&
            MathF.Abs(_player.X - nx) < TankSize && MathF.Abs(_player.Y - ny) < TankSize) return true;
        foreach (var o in _enemies)
            if (o != t && !o.Dead && MathF.Abs(o.X - nx) < TankSize && MathF.Abs(o.Y - ny) < TankSize) return true;
        return false;
    }

    private static bool PointInTank(float x, float y, Tank t)
    {
        return x > t.X - TankHalf + 3 && x < t.X + TankHalf - 3 &&
               y > t.Y - TankHalf + 3 && y < t.Y + TankHalf - 3;
    }

    private int DirToward(Tank e)
    {
        float tx = 480, ty = 270;   // 默认朝屏幕中心
        if (_player is not null && !_player.Dead) { tx = _player.X; ty = _player.Y; }
        float dx = tx - e.X, dy = ty - e.Y;
        if (MathF.Abs(dx) > MathF.Abs(dy)) return dx > 0 ? 1 : 3;
        return dy > 0 ? 2 : 0;
    }

    // =====================================================================
    //  道具
    // =====================================================================
    private void MaybeDropPowerUp(float x, float y)
    {
        if (Random.Shared.NextDouble() > 0.16) return;
        var pu = new PowerUp { Kind = (PowerKind)Random.Shared.Next(5), X = x, Y = y };
        pu.Root = Container.Create();
        pu.Root.X = x;
        pu.Root.Y = y;
        BuildPowerUpVisual(pu);
        _world.AddChild(pu.Root);
        _powerUps.Add(pu);
    }

    private void BuildPowerUpVisual(PowerUp pu)
    {
        var g = Graphics.Create();
        g.BeginBatch();
        Color panel = pu.Kind switch
        {
            PowerKind.Star => new Color(0.85f, 0.70f, 0.20f),
            PowerKind.Grenade => new Color(0.85f, 0.30f, 0.25f),
            PowerKind.Shovel => new Color(0.40f, 0.70f, 0.40f),
            PowerKind.Helmet => new Color(0.35f, 0.55f, 0.85f),
            _ => new Color(0.90f, 0.45f, 0.60f),
        };
        g.DrawRoundedRect(-16, -16, 32, 32, 6, new Color(0, 0, 0, 0.35f));
        g.DrawRoundedRect(-14, -14, 28, 28, 5, panel);
        switch (pu.Kind)
        {
            case PowerKind.Star:
                g.DrawRect(-2, -9, 4, 18, Color.White);
                g.DrawRect(-9, -2, 18, 4, Color.White);
                g.DrawRect(-2, -2, 4, 4, new Color(1f, 0.8f, 0.3f));
                break;
            case PowerKind.Grenade:
                g.DrawCircle(0, 2, 6, new Color(0.15f, 0.15f, 0.18f));
                g.DrawRect(-1, -8, 2, 5, new Color(0.25f, 0.25f, 0.3f));
                g.DrawCircle(0, 2, 3, new Color(1f, 0.85f, 0.4f));
                break;
            case PowerKind.Shovel:
                g.DrawRect(-8, -2, 16, 5, new Color(0.80f, 0.84f, 0.88f));
                g.DrawRect(-4, 3, 8, 9, new Color(0.55f, 0.62f, 0.68f));
                break;
            case PowerKind.Helmet:
                g.DrawCircle(0, 2, 7, new Color(0.18f, 0.28f, 0.42f));
                g.DrawRect(-7, 1, 14, 3, new Color(0.4f, 0.5f, 0.66f));
                break;
            case PowerKind.Life:
                g.DrawRect(-2, -8, 4, 16, Color.White);
                g.DrawRect(-8, -2, 16, 4, Color.White);
                break;
        }
        g.EndBatch();
        pu.Root.AddChild(g);
    }

    private void ApplyPowerUp(PowerKind kind)
    {
        switch (kind)
        {
            case PowerKind.Star:
                if (_power < 3) _power++;
                else _score += 500;
                Audio.Beep(660, 0.08, "square", 0.07);
                Audio.Beep(880, 0.1, "square", 0.07);
                break;
            case PowerKind.Grenade:
                foreach (var e in _enemies)
                {
                    if (e.Dead) continue;
                    e.Dead = true;
                    _score += e.Score;
                    _enemiesLeft = Math.Max(0, _enemiesLeft - 1);
                    _world.RemoveChild(e.Root);
                    e.Root.Destroy();
                    SpawnBurst(e.X, e.Y, EnemyColor(e.Type), 12, 200);
                }
                Audio.Beep(140, 0.5, "sawtooth", 0.15);
                _shock = 0.2f;
                break;
            case PowerKind.Shovel:
                if (_shovelTimer > 0) SteelAroundBase(false);
                SteelAroundBase(true);
                _shovelTimer = 10f;
                Audio.Beep(440, 0.1, "square", 0.07);
                Audio.Beep(554, 0.12, "square", 0.07);
                break;
            case PowerKind.Helmet:
                if (_player is not null) _player.Immortal = 10f;
                Audio.Beep(330, 0.12, "triangle", 0.08);
                Audio.Beep(494, 0.12, "triangle", 0.08);
                break;
            case PowerKind.Life:
                _lives++;
                Audio.Beep(523, 0.09, "square", 0.07);
                Audio.Beep(659, 0.09, "square", 0.07);
                Audio.Beep(784, 0.12, "square", 0.07);
                break;
        }
    }

    private void SteelAroundBase(bool on)
    {
        for (int r = BaseRow - 2; r <= BaseRow; r++)
            for (int c = BaseCol - 2; c <= BaseCol; c++)
            {
                if (c < 0 || c >= MapW || r < 0 || r >= MapH) continue;
                if (_map[c, r] == 'E') continue;
                if (on)
                {
                    _steelSave[c - (BaseCol - 2), r - (BaseRow - 2)] = _map[c, r];
                    _map[c, r] = 'S';
                }
                else
                {
                    var orig = _steelSave[c - (BaseCol - 2), r - (BaseRow - 2)];
                    if (orig is null) continue;
                    _map[c, r] = orig.Value;
                    _steelSave[c - (BaseCol - 2), r - (BaseRow - 2)] = null;
                    if (orig.Value == 'B')
                        for (int sr = 0; sr < 2; sr++)
                            for (int sc = 0; sc < 2; sc++)
                                _brickBits[c, r, sr, sc] = true;
                }
            }
        RedrawMap();
    }

    // =====================================================================
    //  坦克 / 特效
    // =====================================================================
    private Tank SpawnPlayer()
    {
        var t = CreateTank(true, MapOx + 2 * Cell + Cell / 2, MapOy + BaseRow * Cell + Cell / 2);
        t.SpawnInvul = 2f;
        t.SetDir(0);
        return t;
    }

    private Tank CreateTank(bool player, float x, float y)
    {
        var t = new Tank { Player = player, X = x, Y = y };
        if (player)
        {
            t.Speed = 140f;
            t.Type = TankType.Basic;
            t.Hp = 1;
            t.Score = 0;
        }
        else
        {
            double r = Random.Shared.NextDouble();
            if (_level == 1) t.Type = r < 0.6 ? TankType.Basic : r < 0.85 ? TankType.Fast : TankType.Heavy;
            else if (_level == 2) t.Type = r < 0.4 ? TankType.Basic : r < 0.7 ? TankType.Fast : TankType.Heavy;
            else t.Type = r < 0.3 ? TankType.Basic : r < 0.55 ? TankType.Fast : r < 0.8 ? TankType.Heavy : TankType.Armor;
            t.Speed = t.Type switch
            {
                TankType.Fast => 110f,
                TankType.Heavy => 45f,
                TankType.Armor => 65f,
                _ => 60f,
            };
            t.Hp = t.Type switch { TankType.Heavy => 4, TankType.Armor => 2, _ => 1 };
            t.Score = t.Type switch
            {
                TankType.Fast => 200,
                TankType.Heavy => 400,
                TankType.Armor => 300,
                _ => 100,
            };
        }

        t.Root = Container.Create();
        t.Root.X = x;
        t.Root.Y = y;
        var body = Graphics.Create();
        body.BeginBatch();
        var c = t.Player ? new Color(1f, 0.83f, 0.23f) : EnemyColor(t.Type);
        body.DrawRoundedRect(-15, -15, 8, 30, 2, new Color(0.05f, 0.05f, 0.08f));
        body.DrawRoundedRect(7, -15, 8, 30, 2, new Color(0.05f, 0.05f, 0.08f));
        body.DrawRoundedRect(-14, -14, 28, 28, 4, c);
        body.DrawRoundedRect(-9, -9, 18, 18, 3, new Color(1f, 1f, 1f, 0.15f));
        body.DrawCircle(0, 0, 7, c);
        body.DrawCircle(0, 0, 4, new Color(1f, 1f, 1f, 0.18f));
        body.DrawRoundedRect(-2.5f, -20, 5, 14, 2, c);
        body.EndBatch();
        t.Root.AddChild(body);
        _world.AddChild(t.Root);
        return t;
    }

    private static Color EnemyColor(TankType type) => type switch
    {
        TankType.Fast => new Color(0.87f, 0.89f, 0.90f),
        TankType.Heavy => new Color(0.45f, 0.48f, 0.52f),
        TankType.Armor => new Color(0.91f, 0.77f, 0.42f),
        _ => new Color(0.68f, 0.71f, 0.74f),
    };

    private void SpawnBurst(float x, float y, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = Random.Shared.NextSingle() * MathF.PI * 2;
            float spd = speed * (0.3f + 0.7f * Random.Shared.NextSingle());
            _particles.Add(new Particle
            {
                X = x, Y = y,
                Vx = MathF.Cos(ang) * spd, Vy = MathF.Sin(ang) * spd,
                Life = 0.25f + 0.35f * Random.Shared.NextSingle(),
                Size = 2f + 3f * Random.Shared.NextSingle(),
                Color = color,
            });
        }
        if (_particles.Count > 400) _particles.RemoveRange(0, _particles.Count - 400);
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0) { _particles.RemoveAt(i); continue; }
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Vx *= (1f - 2.5f * dt);
            p.Vy *= (1f - 2.5f * dt);
        }
    }

    private void UpdateSpawnFlashes(float dt)
    {
        for (int i = _spawnFlashes.Count - 1; i >= 0; i--)
        {
            var f = _spawnFlashes[i];
            f.t -= dt;
            if (f.t <= 0) _spawnFlashes.RemoveAt(i);
            else _spawnFlashes[i] = f;
        }
    }

    // =====================================================================
    //  Render
    // =====================================================================
    public override void Render()
    {
        // 屏幕震动
        if (_shock > 0)
        {
            float s = _shock * 10f;
            _world.X = Random.Shared.NextSingle() * 2 * s - s;
            _world.Y = Random.Shared.NextSingle() * 2 * s - s;
        }
        else { _world.X = 0; _world.Y = 0; }

        // 粒子
        _fxGfx.Clear();
        _fxGfx.BeginBatch();
        foreach (var p in _particles)
        {
            float r = p.Size * (p.Life / 0.6f) + 0.5f;
            if (r <= 0) continue;
            _fxGfx.DrawCircle(p.X, p.Y, r, p.Color);
        }
        _fxGfx.EndBatch();

        // 出生闪光
        _flashGfx.Clear();
        _flashGfx.BeginBatch();
        foreach (var f in _spawnFlashes)
        {
            float a = MathF.Min(1f, f.t * 3f);
            var col = new Color(1f, 1f, 1f, a);
            _flashGfx.DrawLine(f.x - 10, f.y - 10, f.x + 10, f.y + 10, 3, col);
            _flashGfx.DrawLine(f.x + 10, f.y - 10, f.x - 10, f.y + 10, 3, col);
        }
        _flashGfx.EndBatch();

        // 生命图标
        _hudLivesGfx.Clear();
        _hudLivesGfx.BeginBatch();
        for (int i = 0; i < _lives; i++)
        {
            float x = 20 + i * 26, y = 98;
            _hudLivesGfx.DrawRoundedRect(x, y, 20, 20, 3, new Color(1f, 0.83f, 0.23f));
            _hudLivesGfx.DrawRoundedRect(x + 8, y - 7, 4, 9, 1, new Color(1f, 0.83f, 0.23f));
        }
        _hudLivesGfx.EndBatch();

        // 无敌闪烁（出生护盾）
        if (_player is not null && !_player.Dead)
        {
            bool blink = ((int)(_stateTime * 8) & 1) == 0;
            _player.Root.Visible = _player.SpawnInvul <= 0 || blink;
        }

        // Overlay 提示闪烁
        if (_state == State.Over)
        {
            bool blink = ((int)(_stateTime * 2) & 1) == 0;
            _overlaySub.Visible = blink;
        }
    }

    // =====================================================================
    //  地图 / HUD 绘制
    // =====================================================================
    private void RedrawMap()
    {
        _mapGfx.Clear();
        _mapGfx.BeginBatch();
        for (int cy = 0; cy < MapH; cy++)
            for (int cx = 0; cx < MapW; cx++)
            {
                float x = MapOx + cx * Cell, y = MapOy + cy * Cell;
                switch (_map[cx, cy])
                {
                    case 'B': DrawBrick(cx, cy, x, y); break;
                    case 'S':
                        _mapGfx.DrawRect(x + 2, y + 2, Cell - 4, Cell - 4, new Color(0.80f, 0.83f, 0.88f));
                        _mapGfx.DrawRect(x + 2, y + 2, Cell - 4, 6, new Color(1f, 1f, 1f, 0.4f));
                        break;
                    case '~':
                        _mapGfx.DrawRect(x + 2, y + 2, Cell - 4, Cell - 4, new Color(0.13f, 0.42f, 0.68f));
                        _mapGfx.DrawLine(x + 6, y + 5, x + 12, y + 5, 1.5f, new Color(0.45f, 0.70f, 0.90f, 0.6f));
                        _mapGfx.DrawLine(x + 20, y + 14, x + 28, y + 14, 1.5f, new Color(0.45f, 0.70f, 0.90f, 0.6f));
                        _mapGfx.DrawLine(x + 8, y + 26, x + 16, y + 26, 1.5f, new Color(0.45f, 0.70f, 0.90f, 0.6f));
                        _mapGfx.DrawLine(x + 24, y + 31, x + 31, y + 31, 1.5f, new Color(0.45f, 0.70f, 0.90f, 0.6f));
                        break;
                    case 'E': DrawBase(x, y); break;
                }
            }
        _mapGfx.EndBatch();
    }

    private void DrawBrick(int cx, int cy, float x, float y)
    {
        for (int sr = 0; sr < 2; sr++)
            for (int sc = 0; sc < 2; sc++)
            {
                if (!_brickBits[cx, cy, sr, sc]) continue;
                float bx = x + sc * (Cell / 2), by = y + sr * (Cell / 2);
                _mapGfx.DrawRect(bx + 1, by + 1, Cell / 2 - 2, Cell / 2 - 2, new Color(0.78f, 0.55f, 0.28f));
                _mapGfx.DrawRect(bx + 1, by + 1, Cell / 2 - 2, 3, new Color(0.95f, 0.75f, 0.45f, 0.8f));
            }
    }

    private void DrawBase(float x, float y)
    {
        _mapGfx.DrawRect(x + 4, y + 4, Cell - 8, Cell - 8, new Color(0.85f, 0.25f, 0.22f));
        _mapGfx.DrawRect(x + 4, y + 4, Cell - 8, 5, new Color(1f, 0.6f, 0.5f, 0.8f));
        _mapGfx.DrawLine(x + Cell / 2, y + 8, x + Cell / 2, y + Cell - 8, 2, Color.White);
        _mapGfx.DrawTriangle(x + Cell / 2, y + 8, x + Cell / 2 + 9, y + 13, x + Cell / 2, y + 18, new Color(0.95f, 0.9f, 0.4f));
    }

    private void RedrawTrees()
    {
        _treeGfx.Clear();
        _treeGfx.BeginBatch();
        for (int cy = 0; cy < MapH; cy++)
            for (int cx = 0; cx < MapW; cx++)
                if (_map[cx, cy] == '%')
                {
                    float x = MapOx + cx * Cell, y = MapOy + cy * Cell;
                    var g = new Color(0.16f, 0.45f, 0.20f, 0.92f);
                    _treeGfx.DrawCircle(x + 10, y + 10, 7, g);
                    _treeGfx.DrawCircle(x + 26, y + 10, 7, g);
                    _treeGfx.DrawCircle(x + 10, y + 26, 7, g);
                    _treeGfx.DrawCircle(x + 26, y + 26, 7, g);
                }
        _treeGfx.EndBatch();
    }

    private void BuildHud()
    {
        var title = new PixiText("坦克大战", "bold 22px system-ui, sans-serif", new Color(0.90f, 0.93f, 0.97f), "left");
        title.X = 20; title.Y = 24;
        _root.AddChild(title);

        _hudStage = new PixiText("", "bold 14px system-ui, sans-serif", new Color(0.55f, 0.58f, 0.65f), "left");
        _hudStage.X = 20; _hudStage.Y = 52;
        _root.AddChild(_hudStage);

        _hudStatus = new PixiText("", "bold 12px system-ui, sans-serif", new Color(1f, 0.83f, 0.23f), "left");
        _hudStatus.X = 20; _hudStatus.Y = 74;
        _root.AddChild(_hudStatus);

        var livesLabel = new PixiText("生命", "bold 12px system-ui, sans-serif", new Color(0.45f, 0.5f, 0.6f), "left");
        livesLabel.X = 20; livesLabel.Y = 86;
        _root.AddChild(livesLabel);

        _hudLivesGfx = Graphics.Create();
        _root.AddChild(_hudLivesGfx);

        var scoreLabel = new PixiText("得分", "bold 12px system-ui, sans-serif", new Color(0.45f, 0.5f, 0.6f), "right");
        scoreLabel.X = 940; scoreLabel.Y = 20;
        _root.AddChild(scoreLabel);

        _hudScore = new PixiText("0", "bold 28px system-ui, sans-serif", new Color(1f, 0.88f, 0.40f), "right");
        _hudScore.X = 940; _hudScore.Y = 42;
        _root.AddChild(_hudScore);

        _hudHigh = new PixiText("", "bold 12px system-ui, sans-serif", new Color(0.96f, 0.77f, 0.27f), "right");
        _hudHigh.X = 940; _hudHigh.Y = 72;
        _root.AddChild(_hudHigh);

        _hudEnemy = new PixiText("", "bold 12px system-ui, sans-serif", new Color(0.55f, 0.58f, 0.65f), "right");
        _hudEnemy.X = 940; _hudEnemy.Y = 92;
        _root.AddChild(_hudEnemy);
    }

    private void BuildOverlay()
    {
        _overlay = Container.Create();
        var g = Graphics.Create();
        g.DrawRect(0, 0, 960, 540, new Color(0, 0, 0, 0.55f));
        _overlay.AddChild(g);
        _overlayTitle = new PixiText("", "bold 56px system-ui, sans-serif", new Color(1f, 0.83f, 0.23f));
        _overlayTitle.X = 480; _overlayTitle.Y = 236;
        _overlay.AddChild(_overlayTitle);
        _overlaySub = new PixiText("", "18px system-ui, sans-serif", new Color(0.55f, 0.62f, 0.75f));
        _overlaySub.X = 480; _overlaySub.Y = 302;
        _overlay.AddChild(_overlaySub);
        _overlay.Visible = false;
        _root.AddChild(_overlay);
    }

    private void UpdateHud()
    {
        _hudStage.Text = $"STAGE {_level}";
        _hudScore.Text = _score.ToString();
        _hudHigh.Text = $"最高 {_high}";
        _hudEnemy.Text = $"敌军 ×{Math.Max(0, _enemiesLeft)}";
        _hudStatus.Text = (_shovelTimer > 0) ? "基地钢化中…" : (_player is { Immortal: > 0 }) ? "无敌时间" : (_power > 1) ? $"火力 Lv{_power}" : "";
    }

    private static int ParseInt(string s, int fallback)
    {
        int.TryParse(s, out int v);
        return v == 0 && s != "0" ? fallback : v;
    }

    private static float ClampF(float v, float min, float max) => v < min ? min : v > max ? max : v;
}
