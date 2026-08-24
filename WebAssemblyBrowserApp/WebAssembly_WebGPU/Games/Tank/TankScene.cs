using System;
using System.Collections.Generic;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Scenes.Tank;

/// <summary>
/// FC 风格坦克大战：3 个关卡，玩家守护老鹰，消灭全部敌军坦克过关。
/// - 方向键移动，空格 / 回车射击
/// - ESC 回到主菜单
/// - 玩家 3 条命，老鹰被击中或命耗尽 GameOver
/// </summary>
public sealed class TankScene : GameScene
{
    // --- 地图参数 ---
    private const int Tile = 32;
    private const int Cols = 30;
    private const int Rows = 15;
    private const int HudH = 52;
    private const int GameTop = HudH;
    private const int GameH   = Rows * Tile;           // 480
    private const int GameW   = Cols * Tile;           // 960

    private enum TileKind : byte { Empty = 0, Brick, Steel, Water, Tree, Eagle }

    // --- 关卡地图（B=砖墙 S=钢墙 W=水 T=树 E=老鹰 P=玩家出生 X=敌出生  .=空） ---
    // 关卡 1 ~ 3，15 行 × 30 列
    private static readonly string[][] Levels = new[]
    {
        // 关卡 1：入门（简单砖墙布局）
        new[]
        {
            "..............................",
            "..B.B.B.B.B.B.B.B.B.B.B.B.B.B.",
            "..B.B.B.B.B.B.B.B.B.B.B.B.B.B.",
            "..............................",
            "..B.B.B.B...S.S.S...B.B.B.B...",
            "..B.B.B.B...S...S...B.B.B.B...",
            "..............................",
            "..S.S.....B.B.B.B.B......S.S..",
            "..S.S.....B.B.B.B.B......S.S..",
            "..............................",
            "..B.B.B..T.T.T.T.T..B.B.B.B...",
            "..B.B.B..T.T.T.T.T..B.B.B.B...",
            "...........B....B.............",
            "...........B.EB.B.............",
            "..............................",
        },
        // 关卡 2：水+钢墙防守
        new[]
        {
            "..............................",
            "..S.S..B.B.B.B.B.B.B..S.S.....",
            "..S.S..B.B.B.B.B.B.B..S.S.....",
            "..............................",
            "..B.B.W.W.W....B....W.W.W.B.B.",
            "..B.B.W.W.W....B....W.W.W.B.B.",
            "..............................",
            "..B.B.B.B..S.S.S.S..B.B.B.B...",
            "..B.B.B.B..S.....S..B.B.B.B...",
            "..............................",
            "..T.T....B.B.B.B.B....T.T.....",
            "..T.T....B.B.B.B.B....T.T.....",
            ".............B.B..............",
            "............SBEB.S............",
            "..............................",
        },
        // 关卡 3：迷宫（复杂布局）
        new[]
        {
            "..............................",
            "..S.B.B.B.S.B.B.B.B.S.B.B.B.S.",
            "..S.B.B.B.S.B.B.B.B.S.B.B.B.S.",
            "..............................",
            "..B.B.W.W.W.W.W.W.W.W.W.B.B...",
            "..B.B.W................W.B.B..",
            "..B.B.W..S.S.S..S.S.S..W.B.B..",
            "..B.B.W................W.B.B..",
            "..B.B.W.W.W.W.W.W.W.W.W.B.B...",
            "..............................",
            "..T.T.B.B.B.T.T.T.T.B.B.B.T.T.",
            "..T.T.B.B.B.T.T.T.T.B.B.B.T.T.",
            "..........B..B.B..B...........",
            "..........S..BEB..S...........",
            "..............................",
        },
    };

    // --- 坦克 ---
    private enum Dir : byte { Up, Down, Left, Right }

    private sealed class TankUnit
    {
        public float X, Y;         // 左上角坐标
        public const float Size = 28;
        public Dir Dir;
        public float Speed;
        public float FireCooldown;
        public string Color = "#ffe066";
        public bool IsPlayer;
        public bool Alive = true;
        public float ThinkTimer;
        public int SpawnedBulletCount;
    }

    private sealed class Bullet
    {
        public float X, Y;
        public Dir Dir;
        public float Speed = 360;
        public bool FromPlayer;
        public bool Alive = true;
    }

    // --- 运行状态 ---
    private readonly TileKind[,] _map = new TileKind[Rows, Cols];
    private readonly List<TankUnit> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private TankUnit _player = null!;

    private int _levelIndex = 0;
    private int _lives = 3;
    private int _enemiesRemainingToSpawn;
    private int _enemiesOnFieldMax = 4;
    private float _enemySpawnTimer;

    private enum S { Play, LevelClear, GameOver, AllClear }
    private S _state = S.Play;
    private float _stateTime;

    // 出生点（底部左 1/4，底部右 1/4，顶部左 中 右）
    private readonly (int col, int row) _playerSpawn = (7, 14);
    private readonly (int col, int row)[] _enemySpawns = new[] { (1, 0), (14, 0), (28, 0) };

    public TankScene() : base("tank") { }

    public override void Enter() => LoadLevel(_levelIndex = 0);

    private void LoadLevel(int idx)
    {
        idx = Math.Clamp(idx, 0, Levels.Length - 1);
        _levelIndex = idx;
        var layout = Levels[idx];

        for (int r = 0; r < Rows; r++)
        {
            var row = r < layout.Length ? layout[r].PadRight(Cols, '.') : new string('.', Cols);
            for (int c = 0; c < Cols; c++)
            {
                char ch = row[c];
                _map[r, c] = ch switch
                {
                    'B' => TileKind.Brick,
                    'S' => TileKind.Steel,
                    'W' => TileKind.Water,
                    'T' => TileKind.Tree,
                    'E' => TileKind.Eagle,
                    _ => TileKind.Empty,
                };
                if (ch == 'E')
                {
                    // 老鹰
                }
            }
        }

        // 敌人数量随关卡递增
        _enemiesRemainingToSpawn = 5 + idx * 3;
        _enemiesOnFieldMax = 3 + Math.Min(idx, 2);

        _bullets.Clear();
        _enemies.Clear();
        _enemySpawnTimer = 0;

        // 玩家
        _player = new TankUnit
        {
            IsPlayer = true,
            Color = "#4dabf7",
            Speed = 130,
            Dir = Dir.Up,
        };
        SpawnAt(_player, _playerSpawn.col, _playerSpawn.row, -2); // 稍偏上

        _state = S.Play;
        _stateTime = 0;
    }

    private static void SpawnAt(TankUnit t, int col, int row, int rowOffset = 0)
    {
        t.X = Tile * col + (Tile - TankUnit.Size) / 2;
        t.Y = HudH + Tile * (row + rowOffset) + (Tile - TankUnit.Size) / 2;
    }

    // ------------------------------------------------------------
    //  Update
    // ------------------------------------------------------------
    public override void Update(float dt)
    {
        _stateTime += dt;

        if (Input.IsKeyPressed("Escape"))
        {
            // 回主菜单
            GameEngine.Instance.Start("main-menu");
            return;
        }

        switch (_state)
        {
            case S.Play:
                UpdatePlay(dt);
                break;
            case S.LevelClear:
                if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
                {
                    if (_levelIndex + 1 >= Levels.Length)
                    {
                        // 全部通关 → 全部通关状态
                        _state = S.AllClear;
                        _stateTime = 0;
                    }
                    else
                    {
                        LoadLevel(_levelIndex + 1);
                    }
                }
                break;
            case S.GameOver:
            case S.AllClear:
                if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
                {
                    _lives = 3;
                    LoadLevel(0);
                }
                break;
        }
    }

    private void UpdatePlay(float dt)
    {
        UpdatePlayer(dt);
        UpdateEnemySpawn(dt);
        UpdateEnemies(dt);
        UpdateBullets(dt);

        // 胜利条件：敌军全部出生且场上没有敌人
        if (_enemiesRemainingToSpawn == 0 && _enemies.Count == 0)
        {
            _state = S.LevelClear;
            _stateTime = 0;
            Audio.Beep(523, 0.1f, "square", 0.08f);
            Audio.Beep(659, 0.1f, "square", 0.08f);
            Audio.Beep(784, 0.18f, "square", 0.09f);
        }
    }

    private void UpdatePlayer(float dt)
    {
        if (!_player.Alive) return;
        _player.FireCooldown -= dt;
        var p = _player;
        Dir? tryDir = null;
        if (Input.IsKeyDown("ArrowLeft")  || Input.IsKeyDown("a") || Input.IsKeyDown("A")) tryDir = Dir.Left;
        else if (Input.IsKeyDown("ArrowRight") || Input.IsKeyDown("d") || Input.IsKeyDown("D")) tryDir = Dir.Right;
        else if (Input.IsKeyDown("ArrowUp")    || Input.IsKeyDown("w") || Input.IsKeyDown("W")) tryDir = Dir.Up;
        else if (Input.IsKeyDown("ArrowDown")  || Input.IsKeyDown("s") || Input.IsKeyDown("S")) tryDir = Dir.Down;

        if (tryDir.HasValue)
        {
            p.Dir = tryDir.Value;
            MoveTank(p, dt);
        }

        if ((Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed(Input.Enter)) && _player.FireCooldown <= 0)
        {
            Fire(p);
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in _enemies)
        {
            if (!e.Alive) continue;
            e.FireCooldown -= dt;
            e.ThinkTimer -= dt;
            if (e.ThinkTimer <= 0)
            {
                // 简单 AI：随机换方向，10% 几率朝玩家
                e.ThinkTimer = 0.6f + (float)Random.Shared.NextDouble() * 1.2f;
                var dirs = new Dir[] { Dir.Up, Dir.Down, Dir.Left, Dir.Right };
                if (Random.Shared.NextDouble() < 0.15f && _player.Alive)
                {
                    e.Dir = PickDirToPlayer(e);
                }
                else
                {
                    e.Dir = dirs[Random.Shared.Next(4)];
                }
            }
            MoveTank(e, dt);
            if (e.FireCooldown <= 0 && Random.Shared.NextDouble() < dt * 1.1f)
            {
                Fire(e);
            }
        }
        for (int i = _enemies.Count - 1; i >= 0; i--)
            if (!_enemies[i].Alive) _enemies.RemoveAt(i);
    }

    private static Dir PickDirToPlayer(TankUnit e)
    {
        float dx = MathF.Abs((GameEngine.Width / 2) - (e.X + TankUnit.Size / 2));
        float dy = MathF.Abs((GameEngine.Height - HudH / 2) - (e.Y + TankUnit.Size / 2));
        return dx > dy
            ? (e.X < GameEngine.Width / 2 ? Dir.Right : Dir.Left)
            : (e.Y < GameEngine.Height / 2 ? Dir.Down : Dir.Up);
    }

    private void UpdateEnemySpawn(float dt)
    {
        if (_enemiesRemainingToSpawn <= 0) return;
        if (_enemies.Count >= _enemiesOnFieldMax) return;
        _enemySpawnTimer -= dt;
        if (_enemySpawnTimer > 0) return;
        _enemySpawnTimer = 2.2f;

        // 选一个没被挡住的出生点
        foreach (var (c, r) in _enemySpawns)
        {
            float x = Tile * c + (Tile - TankUnit.Size) / 2;
            float y = HudH + Tile * r + (Tile - TankUnit.Size) / 2;
            bool blocked = false;
            foreach (var o in _enemies)
            {
                if (RectsOverlap(x, y, TankUnit.Size, TankUnit.Size, o.X, o.Y, TankUnit.Size, TankUnit.Size))
                { blocked = true; break; }
            }
            if (blocked) continue;
            var enemy = new TankUnit
            {
                IsPlayer = false,
                Color = _levelIndex switch { 0 => "#ff6b6b", 1 => "#f6c445", _ => "#c792ea" },
                Speed = 80 + _levelIndex * 15,
                Dir = Dir.Down,
                ThinkTimer = 0.5f,
            };
            enemy.X = x; enemy.Y = y;
            _enemies.Add(enemy);
            _enemiesRemainingToSpawn--;
            break;
        }
    }

    // ------------------------------------------------------------
    //  移动 / 碰撞 / 射击
    // ------------------------------------------------------------
    private void MoveTank(TankUnit t, float dt)
    {
        float dist = t.Speed * dt;
        float dx = 0, dy = 0;
        switch (t.Dir)
        {
            case Dir.Up:    dy = -dist; break;
            case Dir.Down:  dy =  dist; break;
            case Dir.Left:  dx = -dist; break;
            case Dir.Right: dx =  dist; break;
        }

        // 对齐网格（让转弯更平滑）
        if (t.Dir is Dir.Up or Dir.Down)
        {
            float gridX = MathF.Round(t.X / Tile) * Tile;
            t.X += (gridX - t.X) * MathF.Min(1, dt * 15);
        }
        else
        {
            float gridY = MathF.Round((t.Y - HudH) / Tile) * Tile + HudH;
            t.Y += (gridY - t.Y) * MathF.Min(1, dt * 15);
        }

        float nx = t.X + dx;
        float ny = t.Y + dy;
        if (!CheckTankCollision(nx, ny, t)) return;
        t.X = nx; t.Y = ny;
    }

    private bool CheckTankCollision(float nx, float ny, TankUnit self)
    {
        // 边界
        if (nx < 0) return false;
        if (ny < HudH) return false;
        if (nx + TankUnit.Size > GameW) return false;
        if (ny + TankUnit.Size > HudH + GameH) return false;

        // 地图
        int c0 = (int)(nx / Tile);
        int c1 = (int)((nx + TankUnit.Size - 0.001f) / Tile);
        int r0 = (int)((ny - HudH) / Tile);
        int r1 = (int)((ny + TankUnit.Size - 0.001f - HudH) / Tile);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                if (r < 0 || r >= Rows || c < 0 || c >= Cols) return false;
                var k = _map[r, c];
                if (k is TileKind.Brick or TileKind.Steel or TileKind.Water or TileKind.Eagle) return false;
            }
        // 其他坦克
        if (self != _player && _player.Alive &&
            RectsOverlap(nx, ny, TankUnit.Size, TankUnit.Size, _player.X, _player.Y, TankUnit.Size, TankUnit.Size))
            return false;
        foreach (var other in _enemies)
        {
            if (other == self || !other.Alive) continue;
            if (RectsOverlap(nx, ny, TankUnit.Size, TankUnit.Size, other.X, other.Y, TankUnit.Size, TankUnit.Size))
                return false;
        }
        return true;
    }

    private void Fire(TankUnit t)
    {
        t.FireCooldown = t.IsPlayer ? 0.45f : 0.9f;
        const float bs = 10; // bullet size
        float bx = t.X + TankUnit.Size / 2 - bs / 2;
        float by = t.Y + TankUnit.Size / 2 - bs / 2;
        switch (t.Dir)
        {
            case Dir.Up:    by = t.Y - bs; break;
            case Dir.Down:  by = t.Y + TankUnit.Size; break;
            case Dir.Left:  bx = t.X - bs; break;
            case Dir.Right: bx = t.X + TankUnit.Size; break;
        }
        _bullets.Add(new Bullet
        {
            X = bx, Y = by, Dir = t.Dir, Speed = t.IsPlayer ? 420 : 300,
            FromPlayer = t.IsPlayer,
        });
        t.SpawnedBulletCount++;
        Audio.Beep(t.IsPlayer ? 720 : 220, 0.04f, "square", 0.04f);
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            if (!b.Alive) { _bullets.RemoveAt(i); continue; }
            float d = b.Speed * dt;
            switch (b.Dir)
            {
                case Dir.Up: b.Y -= d; break;
                case Dir.Down: b.Y += d; break;
                case Dir.Left: b.X -= d; break;
                case Dir.Right: b.X += d; break;
            }
            const float bs = 10;
            // 出界
            if (b.X < 0 || b.Y < HudH || b.X + bs > GameW || b.Y + bs > HudH + GameH)
            {
                b.Alive = false; continue;
            }
            // 打地图
            if (BulletHitMap(b, bs)) continue;
            // 打坦克
            if (BulletHitTanks(b, bs)) continue;
        }
    }

    private bool BulletHitMap(Bullet b, float bs)
    {
        int c0 = (int)(b.X / Tile);
        int c1 = (int)((b.X + bs - 0.001f) / Tile);
        int r0 = (int)((b.Y - HudH) / Tile);
        int r1 = (int)((b.Y + bs - 0.001f - HudH) / Tile);
        bool hit = false;
        for (int r = r0; r <= r1; r++)
        for (int c = c0; c <= c1; c++)
        {
            if (r < 0 || r >= Rows || c < 0 || c >= Cols) continue;
            ref var k = ref _map[r, c];
            if (k == TileKind.Brick) { k = TileKind.Empty; hit = true; Audio.Beep(520, 0.03f, "square", 0.04f); }
            else if (k == TileKind.Steel) { hit = true; Audio.Beep(160, 0.04f, "square", 0.04f); }
            else if (k == TileKind.Eagle)
            {
                k = TileKind.Empty;
                _state = S.GameOver;
                _stateTime = 0;
                Audio.Beep(120, 0.8f, "sawtooth", 0.15f);
                hit = true;
            }
        }
        if (hit) b.Alive = false;
        return hit;
    }

    private bool BulletHitTanks(Bullet b, float bs)
    {
        if (b.FromPlayer)
        {
            foreach (var e in _enemies)
            {
                if (!e.Alive) continue;
                if (RectsOverlap(b.X, b.Y, bs, bs, e.X, e.Y, TankUnit.Size, TankUnit.Size))
                {
                    e.Alive = false; b.Alive = false;
                    Audio.Beep(480, 0.08f, "square", 0.06f);
                    return true;
                }
            }
        }
        else
        {
            if (_player.Alive && RectsOverlap(b.X, b.Y, bs, bs, _player.X, _player.Y, TankUnit.Size, TankUnit.Size))
            {
                b.Alive = false;
                _lives--;
                Audio.Beep(220, 0.3f, "triangle", 0.1f);
                if (_lives <= 0)
                {
                    _player.Alive = false;
                    _state = S.GameOver;
                    _stateTime = 0;
                }
                else
                {
                    // 玩家重生
                    SpawnAt(_player, _playerSpawn.col, _playerSpawn.row, -2);
                    _player.Dir = Dir.Up;
                }
                return true;
            }
        }
        // 子弹对撞抵消
        for (int i = 0; i < _bullets.Count; i++)
        {
            var o = _bullets[i];
            if (o == b || !o.Alive || o.FromPlayer == b.FromPlayer) continue;
            if (RectsOverlap(b.X, b.Y, bs, bs, o.X, o.Y, bs, bs))
            {
                o.Alive = false; b.Alive = false;
                return true;
            }
        }
        return false;
    }

    private static bool RectsOverlap(float ax, float ay, float aw, float ah,
                                     float bx, float by, float bw, float bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }

    // ------------------------------------------------------------
    //  Render
    // ------------------------------------------------------------
    public override void Render()
    {
        WebGPU.Clear("#0d1117");
        RenderHud();
        RenderMap();

        // 坦克（先画玩家，再画敌人）
        if (_player.Alive) RenderTank(_player);
        foreach (var e in _enemies) RenderTank(e);

        // 子弹
        foreach (var b in _bullets)
        {
            if (!b.Alive) continue;
            string c = b.FromPlayer ? "#ffe066" : "#ff6b6b";
            WebGPU.FillRect(b.X, b.Y, 10, 10, c);
            WebGPU.Shadow(c, 8);
            WebGPU.FillRect(b.X + 1, b.Y + 1, 8, 8, "#ffffffaa");
            WebGPU.NoShadow();
        }

        // 树：画在坦克/子弹最上层（遮挡效果）
        RenderTrees();

        // 状态覆盖层
        switch (_state)
        {
            case S.LevelClear:  RenderOverlay($"第 {_levelIndex + 1} 关通过！", "按空格 / 回车进入下一关", "#48dbfb"); break;
            case S.GameOver:    RenderOverlay("GAME OVER", "按空格 / 回车重新开始", "#ff6b6b"); break;
            case S.AllClear:    RenderOverlay("全部通关！", "按空格 / 回车重新挑战 3 关", "#f6c445"); break;
        }
    }

    private void RenderHud()
    {
        // HUD 背景条
        WebGPU.FillRect(0, 0, GameEngine.Width, HudH, "#161b22");
        WebGPU.FillRect(0, HudH - 1, GameEngine.Width, 1, "#30363d");

        float left = 28;
        WebGPU.FillText("坦克大战", left, 20, "bold 18px system-ui, sans-serif", "#e6edf3", "left");
        WebGPU.FillText($"第 {_levelIndex + 1} / {Levels.Length} 关", left, HudH - 14, "14px system-ui, sans-serif", "#8b949e", "left");

        float cx = GameEngine.Width / 2;
        WebGPU.FillText("剩余敌军", cx, 20, "13px system-ui, sans-serif", "#8b949e", "center");
        WebGPU.FillText($"{_enemiesRemainingToSpawn + _enemies.Count}", cx, HudH - 16, "bold 20px system-ui, sans-serif", "#f6c445", "center");

        float rx = GameEngine.Width - 28;
        WebGPU.FillText("生命", rx, 20, "13px system-ui, sans-serif", "#8b949e", "right");
        for (int i = 0; i < _lives; i++)
            WebGPU.FillRect(rx - 24 - i * 26, HudH - 22, 18, 18, "#4dabf7");

        // ESC 提示
        WebGPU.FillText("ESC 菜单", rx - 120, HudH - 14, "12px system-ui, sans-serif", "#6e7681", "right");
    }

    private void RenderMap()
    {
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            var k = _map[r, c];
            if (k == TileKind.Empty || k == TileKind.Tree) continue;
            float x = c * Tile;
            float y = HudH + r * Tile;
            switch (k)
            {
                case TileKind.Brick:
                    WebGPU.FillRect(x, y, Tile, Tile, "#8b5a2b");
                    WebGPU.FillRect(x + 1, y + 1, Tile / 2 - 2, Tile / 2 - 2, "#a97142");
                    WebGPU.FillRect(x + Tile / 2 + 1, y + Tile / 2 + 1, Tile / 2 - 2, Tile / 2 - 2, "#a97142");
                    break;
                case TileKind.Steel:
                    WebGPU.FillRect(x, y, Tile, Tile, "#6e7681");
                    WebGPU.FillRect(x + 3, y + 3, Tile - 6, Tile - 6, "#c9d1d9");
                    WebGPU.FillRect(x + 3, y + 3, Tile - 6, 2, "#ffffffaa");
                    break;
                case TileKind.Water:
                    float wave = (int)((MathF.Sin(GameEngine.Instance.Time * 4 + r) + 1) * 8);
                    WebGPU.FillRect(x, y, Tile, Tile, "#2f6fed");
                    WebGPU.FillRect(x + 2, y + 4 + wave % 6, Tile - 4, 2, "#ffffff55");
                    WebGPU.FillRect(x + 2, y + 14 + (wave * 2) % 6, Tile - 4, 2, "#ffffff33");
                    break;
                case TileKind.Eagle:
                    RenderEagle(x, y);
                    break;
            }
        }
    }

    private void RenderEagle(float x, float y)
    {
        WebGPU.FillRect(x, y, Tile, Tile, "#30363d");
        WebGPU.FillRect(x + 4, y + 4, Tile - 8, Tile - 8, "#f6c445");
        // 鹰：两条 V 形线
        WebGPU.Shadow("#f6c445", 10);
        WebGPU.DrawLine(x + 6, y + Tile - 8, x + Tile / 2, y + 8, 2, "#8a6d1b");
        WebGPU.DrawLine(x + Tile / 2, y + 8, x + Tile - 6, y + Tile - 8, 2, "#8a6d1b");
        WebGPU.NoShadow();
    }

    private void RenderTrees()
    {
        for (int r = 0; r < Rows; r++)
        for (int c = 0; c < Cols; c++)
        {
            if (_map[r, c] != TileKind.Tree) continue;
            float x = c * Tile;
            float y = HudH + r * Tile;
            WebGPU.Alpha(0.78f);
            WebGPU.FillRect(x + 2, y + 2, Tile - 4, Tile - 4, "#2ea043");
            WebGPU.FillRect(x + 6, y + 6, 6, 6, "#3fb950");
            WebGPU.FillRect(x + 16, y + 10, 7, 7, "#3fb950");
            WebGPU.FillRect(x + 10, y + 18, 8, 8, "#3fb950");
            WebGPU.Alpha(1);
        }
    }

    private static void RenderTank(TankUnit t)
    {
        WebGPU.Shadow(t.Color, 10);
        // 主体圆角矩形
        WebGPU.RoundedRect(t.X, t.Y, TankUnit.Size, TankUnit.Size, 4, t.Color);
        WebGPU.NoShadow();
        // 履带阴影
        WebGPU.FillRect(t.X + 1, t.Y + 2, 4, TankUnit.Size - 4, "#00000066");
        WebGPU.FillRect(t.X + TankUnit.Size - 5, t.Y + 2, 4, TankUnit.Size - 4, "#00000066");
        // 炮塔（圆心）
        float cx = t.X + TankUnit.Size / 2;
        float cy = t.Y + TankUnit.Size / 2;
        WebGPU.FillCircle(cx, cy, 7, t.IsPlayer ? "#e6edf3" : "#ffffffcc");
        // 炮管（朝 Dir）
        float bx, by, bw, bh;
        switch (t.Dir)
        {
            case Dir.Up:    bx = cx - 2; by = t.Y - 4;             bw = 4; bh = TankUnit.Size / 2 + 4; break;
            case Dir.Down:  bx = cx - 2; by = cy;                   bw = 4; bh = TankUnit.Size / 2 + 4; break;
            case Dir.Left:  bx = t.X - 4; by = cy - 2;              bw = TankUnit.Size / 2 + 4; bh = 4; break;
            case Dir.Right: bx = cx;      by = cy - 2;              bw = TankUnit.Size / 2 + 4; bh = 4; break;
            default:        bx = 0; by = 0; bw = 0; bh = 0; break;
        }
        WebGPU.FillRect(bx, by, bw, bh, t.IsPlayer ? "#22272e" : "#3b1b1b");
    }

    private static void RenderOverlay(string title, string sub, string color)
    {
        float cx = GameEngine.Width / 2;
        float cy = GameEngine.Height / 2;
        WebGPU.Alpha(0.6f);
        WebGPU.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#000000");
        WebGPU.Alpha(1);
        WebGPU.Shadow(color, 28);
        WebGPU.FillText(title, cx, cy - 20, "bold 50px system-ui, sans-serif", color, "center");
        WebGPU.NoShadow();
        WebGPU.FillText(sub, cx, cy + 30, "bold 20px system-ui, sans-serif", "#e6edf3", "center");
    }
}
