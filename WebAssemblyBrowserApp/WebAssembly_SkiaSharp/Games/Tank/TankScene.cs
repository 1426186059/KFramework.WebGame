using System;
using System.Collections.Generic;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// FC 风格坦克大战（1~3 关）—— SkiaSharp 版。
/// 玩法与 Canvas2D 版一致：击毁全部敌方坦克并保护底部基地（★）。
/// 网格 26×26 像素：B=砖墙（可炸） S=钢墙（需强化弹） E=空地。
/// </summary>
public sealed class TankScene : GameScene
{
    private const float Tile = 26;
    private const int GridW = 26;
    private const int GridH = 23;

    private const float FieldW = GridW * Tile;
    private const float FieldH = GridH * Tile;
    private const float OffsetX = (GameEngine.Width - FieldW) / 2;
    private const float OffsetY = (GameEngine.Height - FieldH) / 2;

    // 关卡地图：B=砖墙 S=钢墙 E=空地
    private static readonly string[][] LevelMaps =
    {
        // ---------- 第 1 关：少量砖墙，4 个敌人，节奏慢 ----------
        new[]
        {
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEEEEEEEEEEEEBBEEE",
            "EEEBBEEEEEEEEEEEEEEEBBEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEBBBBEEEEEEEEEEEE",
            "EEEEEEEEEBBBBEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEEEEEEEEEEEEBBEEE",
            "EEEBBEEEEEEEEEEEEEEEBBEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
        },
        // ---------- 第 2 关：增加钢墙，6 个敌人，含 1 快速 ----------
        new[]
        {
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEEEEBBBBEEESSSEEEE",
            "EEESSSEEEEEEBBBBEEESSSEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEBBSSBBEEEEEEEEEE",
            "EEEEEEEEEBBSSBBEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEEEEEEEEEEEESSSEEE",
            "EEESSSEEEEEEEEEEEEEESSSEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEBBEEEEEBBBBEEEEEBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEEEEEEEEEEEESSSEEE",
            "EEESSSEEEEEEEEEEEEEESSSEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
        },
        // ---------- 第 3 关：钢墙堡垒，8 个敌人，含 2 快速 + 1 装甲 ----------
        new[]
        {
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEESSSSEEEESSSEEEE",
            "EEESSSEEEESSSSEEEESSSEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEEBBBBBBEEEESSSEEE",
            "EEESSSEEEEBBBBBBEEEESSSEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEESSSSSSEEEEEEEEEEE",
            "EEEEEEEEESSSSSSEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEEEESSSSEEEEEBBEEEE",
            "EEEBBEEEEESSSSEEEEEBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEESSSEEEEBBBBBBEEEESSSEEE",
            "EEESSSEEEEBBBBBBEEEESSSEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEBBEEESSSSSSSSEESSBBEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
            "EEEEEEEEEEEEEEEEEEEEEEEE",
        },
    };

    private enum Cell { Empty, Brick, Steel }
    private enum State { Playing, Won, Lost }

    private Cell[,] _grid = new Cell[GridW, GridH];
    private readonly List<Tank> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private Tank _player = null!;
    private Vector2 _base;              // 基地中心
    private bool _baseAlive = true;

    private State _state = State.Playing;
    private int _level;                 // 0-based
    private int _enemiesRemaining;      // 本关还需消灭的敌人总数
    private int _enemiesOnField;        // 同时在场敌人数上限
    private float _spawnTimer;
    private float _stateTime;
    private int _score;
    private int _lives = 3;
    private float _blink;

    public TankScene() : base("tank") { }

    // ------------------------- 生命周期 -------------------------

    public override void Enter()
    {
        _level = 0;
        _score = 0;
        _lives = 3;
        StartLevel(0);
    }

    private void StartLevel(int level)
    {
        _level = level;
        _state = State.Playing;
        _stateTime = 0;
        _enemies.Clear();
        _bullets.Clear();
        BuildMap(level);

        _enemiesRemaining = 4 + level * 2;          // L1:4 L2:6 L3:8
        _enemiesOnField = 3 + level;                // 同时在场 3/4/5
        _spawnTimer = 0.5f;

        float px = OffsetX + Tile * 4 + Tile / 2;
        float py = OffsetY + FieldH - Tile * 1.5f;
        _player = new Tank
        {
            IsPlayer = true,
            Position = new Vector2(px, py),
            Dir = TankDir.Up,
            Speed = 130,
            MaxBullets = 1,
            FireCooldown = 0.35f,
            SpawnProtect = 2.5f,
        };

        int baseCol = GridW / 2;
        int baseRow = GridH - 2;
        _base = new Vector2(OffsetX + baseCol * Tile + Tile / 2, OffsetY + baseRow * Tile + Tile / 2);
        _baseAlive = true;
    }

    private void BuildMap(int level)
    {
        var map = LevelMaps[level];
        for (int r = 0; r < GridH; r++)
            for (int c = 0; c < GridW; c++)
                _grid[c, r] = Cell.Empty;

        for (int r = 0; r < GridH; r++)
        {
            var row = map[r];
            for (int c = 0; c < GridW; c++)
            {
                char ch = c < row.Length ? row[c] : 'E';
                if (ch == 'B') _grid[c, r] = Cell.Brick;
                else if (ch == 'S') _grid[c, r] = Cell.Steel;
            }
        }

        // 基地三侧用钢墙保护（上方留通道给敌人进攻）
        int bc = GridW / 2;
        int br = GridH - 2;
        (int, int)[] walls = { (bc - 1, br - 1), (bc, br - 1), (bc + 1, br - 1), (bc - 1, br), (bc + 1, br) };
        foreach (var (c, r) in walls)
            if (InBounds(c, r)) _grid[c, r] = Cell.Steel;
    }

    // ------------------------- 更新 -------------------------

    public override void Update(float dt)
    {
        if (Input.IsKeyPressed(Input.Escape))
        {
            GameEngine.Instance.Start("main-menu");
            return;
        }

        _blink += dt;
        if (_state == State.Won || _state == State.Lost)
        {
            _stateTime += dt;
            if (Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
            {
                if (_state == State.Won && _level < LevelMaps.Length - 1)
                    StartLevel(_level + 1);
                else
                    Enter();
            }
            return;
        }

        _stateTime += dt;
        _player!.Update(dt);
        UpdatePlayer(dt);
        SpawnEnemies(dt);
        UpdateEnemies(dt);
        UpdateBullets(dt);

        if (!_baseAlive || _lives <= 0) _state = State.Lost;
        else if (_enemiesRemaining <= 0 && _enemies.Count == 0) _state = State.Won;
    }

    private void UpdatePlayer(float dt)
    {
        var p = _player!;
        if (!p.Alive) return;

        float vx = 0, vy = 0;
        if (Input.IsKeyDown(Input.ArrowUp) || Input.IsKeyDown(Input.KeyW)) { vy = -1; p.Dir = TankDir.Up; }
        else if (Input.IsKeyDown(Input.ArrowDown) || Input.IsKeyDown(Input.KeyS)) { vy = 1; p.Dir = TankDir.Down; }
        else if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) { vx = -1; p.Dir = TankDir.Left; }
        else if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) { vx = 1; p.Dir = TankDir.Right; }

        if (vx != 0 || vy != 0)
            TryMove(p, new Vector2(vx, vy).Normalized() * p.Speed * dt);

        if ((Input.IsKeyPressed(Input.Space) || Input.IsMousePressed()) && p.CanFire)
        {
            FireBullet(p);
            Audio.Beep(220, 0.06f, "square", 0.07f);
        }
    }

    private void SpawnEnemies(float dt)
    {
        if (_enemiesRemaining <= 0) return;
        if (_enemies.Count >= _enemiesOnField) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0) return;
        _spawnTimer = MathF.Max(0.8f, 2.2f - _level * 0.4f);

        bool left = _enemies.Count % 2 == 0;
        float cx = OffsetX + (left ? Tile * 1.5f : FieldW - Tile * 1.5f);
        float cy = OffsetY + Tile * 1.5f;

        bool fast = _level >= 1 && _enemiesRemaining % 3 == 0;
        bool armored = _level >= 2 && _enemiesRemaining % 4 == 0;

        _enemies.Add(new Tank
        {
            IsPlayer = false,
            Position = new Vector2(cx, cy),
            Dir = TankDir.Down,
            Speed = fast ? 110 : 70,
            MaxBullets = 1,
            FireCooldown = fast ? 0.7f : 1.2f,
            SpawnProtect = 1.0f,
            Armored = armored,
            Hp = armored ? 2 : 1,
            Power = 1,
        });
        _enemiesRemaining--;
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var e in _enemies)
        {
            if (!e.Alive) continue;
            e.Update(dt);

            // 简单 AI：朝基地方向移动，撞墙则随机换向
            var toBase = (_base - e.Position).Normalized();
            if (MathF.Abs(toBase.X) > MathF.Abs(toBase.Y))
                e.Dir = toBase.X > 0 ? TankDir.Right : TankDir.Left;
            else
                e.Dir = toBase.Y > 0 ? TankDir.Down : TankDir.Up;

            var dir = DirVector(e.Dir);
            if (!TryMove(e, dir * e.Speed * dt) || MathUtils.Rand(0, 1) < dt * 1.5f)
                e.Dir = (TankDir)((int)(e.Dir + 1 + (MathUtils.Rand(0, 2) > 1 ? 1 : 0)) % 4);

            if (e.CanFire && MathUtils.Rand(0, 1) < dt * 1.2f)
            {
                FireBullet(e);
                Audio.Beep(180, 0.05f, "square", 0.04f);
            }
        }
        _enemies.RemoveAll(t => !t.Alive);
    }

    private void UpdateBullets(float dt)
    {
        foreach (var b in _bullets)
        {
            if (!b.Alive) continue;
            b.Position += b.Velocity * dt;

            // 出界
            if (b.Position.X < OffsetX || b.Position.X > OffsetX + FieldW ||
                b.Position.Y < OffsetY || b.Position.Y > OffsetY + FieldH)
            { b.Alive = false; continue; }

            // 撞墙
            int c = (int)((b.Position.X - OffsetX) / Tile);
            int r = (int)((b.Position.Y - OffsetY) / Tile);
            if (InBounds(c, r) && _grid[c, r] != Cell.Empty)
            {
                if (_grid[c, r] == Cell.Brick)
                {
                    _grid[c, r] = Cell.Empty;
                    Audio.Beep(260, 0.04f, "square", 0.05f);
                }
                else // 钢墙：强化弹可击穿
                {
                    if (b.Power > 1) { _grid[c, r] = Cell.Empty; Audio.Beep(120, 0.1f, "sawtooth", 0.06f); }
                    else Audio.Beep(400, 0.03f, "square", 0.04f);
                }
                b.Alive = false;
                continue;
            }

            // 撞坦克
            if (b.IsPlayer)
            {
                foreach (var e in _enemies)
                {
                    if (e.Alive && HitTank(b, e))
                    {
                        e.Hp -= b.Power;
                        if (e.Hp <= 0)
                        {
                            e.Alive = false;
                            _score += e.Armored ? 300 : 100;
                            Audio.Beep(140, 0.18f, "sawtooth", 0.08f);
                        }
                        b.Alive = false;
                        break;
                    }
                }
            }
            else
            {
                var p = _player!;
                if (p.Alive && p.SpawnProtect <= 0 && HitTank(b, p))
                {
                    p.Alive = false;
                    b.Alive = false;
                    OnPlayerDestroyed();
                }
                if (_baseAlive && MathF.Abs(b.Position.X - _base.X) < Tank.Size / 2 &&
                    MathF.Abs(b.Position.Y - _base.Y) < Tank.Size / 2)
                {
                    _baseAlive = false;
                    b.Alive = false;
                    Audio.Beep(90, 0.4f, "sawtooth", 0.12f);
                }
            }
        }
        _bullets.RemoveAll(x => !x.Alive);
    }

    private void OnPlayerDestroyed()
    {
        _lives--;
        Audio.Beep(120, 0.4f, "sawtooth", 0.1f);
        if (_lives > 0)
        {
            _player = new Tank
            {
                IsPlayer = true,
                Position = new Vector2(OffsetX + Tile * 4 + Tile / 2, OffsetY + FieldH - Tile * 1.5f),
                Dir = TankDir.Up,
                Speed = 130,
                MaxBullets = 1,
                FireCooldown = 0.35f,
                SpawnProtect = 2.5f,
            };
        }
    }

    // ------------------------- 辅助 -------------------------

    private static Vector2 DirVector(TankDir d) => d switch
    {
        TankDir.Up => new Vector2(0, -1),
        TankDir.Down => new Vector2(0, 1),
        TankDir.Left => new Vector2(-1, 0),
        TankDir.Right => new Vector2(1, 0),
        _ => new Vector2(0, -1),
    };

    private bool InBounds(int c, int r) => c >= 0 && c < GridW && r >= 0 && r < GridH;

    /// <summary>尝试按位移移动坦克，遇到墙体则阻挡。</summary>
    private bool TryMove(Tank t, Vector2 delta)
    {
        var next = t.Position + delta;
        float half = t.Half;

        if (next.X - half < OffsetX || next.X + half > OffsetX + FieldW ||
            next.Y - half < OffsetY || next.Y + half > OffsetY + FieldH)
            return false;

        int minC = (int)((next.X - half - OffsetX) / Tile);
        int maxC = (int)((next.X + half - OffsetX) / Tile);
        int minR = (int)((next.Y - half - OffsetY) / Tile);
        int maxR = (int)((next.Y + half - OffsetY) / Tile);

        for (int c = minC; c <= maxC; c++)
            for (int r = minR; r <= maxR; r++)
            {
                if (!InBounds(c, r)) continue;
                if (_grid[c, r] == Cell.Empty) continue;
                // 基地格视为障碍（避免坦克踩在基地上）
                if (MathF.Abs(t.Position.X - _base.X) < half && MathF.Abs(t.Position.Y - _base.Y) < half)
                    continue;
                return false;
            }
        t.Position = next;
        return true;
    }

    private void FireBullet(Tank t)
    {
        if (!t.CanFire) return;
        int count = 0;
        foreach (var b in _bullets) if (b.IsPlayer == t.IsPlayer && b.Alive) count++;
        if (count >= t.MaxBullets) return;

        t.Fire();
        _bullets.Add(new Bullet
        {
            Position = t.MuzzlePosition(),
            Velocity = DirVector(t.Dir) * (t.IsPlayer ? 320 : 240),
            IsPlayer = t.IsPlayer,
            Power = t.Power,
        });
    }

    private static bool HitTank(Bullet b, Tank t)
    {
        float dx = b.Position.X - t.Position.X;
        float dy = b.Position.Y - t.Position.Y;
        float rr = Tank.Size / 2 + Bullet.Size / 2;
        return dx * dx + dy * dy <= rr * rr;
    }

    // ------------------------- 渲染 -------------------------

    public override void Render()
    {
        SkiaCanvas.Clear("#0d1117");
        RenderField();
        RenderBase();
        RenderBullets();
        RenderEnemies();
        RenderPlayer();
        RenderHud();

        if (_state == State.Won)
            RenderOverlay(_level < LevelMaps.Length - 1 ? "LEVEL CLEAR!" : "ALL CLEAR!",
                _level < LevelMaps.Length - 1 ? "PRESS SPACE FOR LEVEL " + (_level + 2) : "SCORE " + _score + " · PRESS SPACE",
                "#48dbfb");
        else if (_state == State.Lost)
            RenderOverlay(_baseAlive ? "DEFEATED" : "BASE LOST!",
                "SCORE " + _score + " · PRESS SPACE TO RETRY", "#ff6b6b");
    }

    private void RenderField()
    {
        SkiaCanvas.FillRect(OffsetX, OffsetY, FieldW, FieldH, "#151b24");

        for (int r = 0; r < GridH; r++)
            for (int c = 0; c < GridW; c++)
            {
                if (_grid[c, r] == Cell.Empty) continue;
                float x = OffsetX + c * Tile;
                float y = OffsetY + r * Tile;
                if (_grid[c, r] == Cell.Brick)
                {
                    // 砖墙：Skia 渐变砖体 + 两块砖面高光
                    SkiaCanvas.GradientRoundRect(x + 1, y + 1, Tile - 2, Tile - 2, 2, "#d9772f", "#8c421f");
                    SkiaCanvas.FillRect(x + 3, y + 3, (Tile - 6) / 2 - 1, (Tile - 6) / 2 - 1, "#f08c3a");
                    SkiaCanvas.FillRect(x + Tile / 2 + 1, y + Tile / 2 + 1, (Tile - 6) / 2 - 1, (Tile - 6) / 2 - 1, "#f08c3a");
                }
                else // 钢墙
                {
                    SkiaCanvas.GradientRoundRect(x + 1, y + 1, Tile - 2, Tile - 2, 2, "#e3e8ef", "#7d8896");
                    SkiaCanvas.FillRect(x + 4, y + 4, Tile - 8, Tile - 8, "#c7d0db");
                }
            }
    }

    private void RenderBase()
    {
        float x = _base.X - Tank.Size / 2;
        float y = _base.Y - Tank.Size / 2;
        if (_baseAlive)
        {
            SkiaCanvas.FillRect(x, y, Tank.Size, Tank.Size, "#3a2f1a");
            SkiaCanvas.FillText("*", _base.X, _base.Y, "bold 20px system-ui", "#ffd24a", "center");
        }
        else
        {
            SkiaCanvas.FillRect(x, y, Tank.Size, Tank.Size, "#2a1414");
            SkiaCanvas.FillText("x", _base.X, _base.Y, "bold 20px system-ui", "#ff5252", "center");
        }
    }

    private void RenderBullets()
    {
        foreach (var b in _bullets)
        {
            if (!b.Alive) continue;
            string col = b.IsPlayer ? "#ffe066" : "#ff7b7b";
            SkiaCanvas.Glow(b.Position.X, b.Position.Y, Bullet.Size * 2f, col + "80");
            SkiaCanvas.FillCircle(b.Position.X, b.Position.Y, Bullet.Size / 2f, col);
        }
    }

    private void RenderEnemies()
    {
        foreach (var e in _enemies)
        {
            if (!e.Alive) continue;
            if (e.SpawnProtect > 0 && (int)(_blink * 10) % 2 == 0) continue;
            DrawTank(e, e.Armored ? "#f0a35e" : "#e74c3c", e.Armored ? "#8c5a24" : "#8f2219");
        }
    }

    private void RenderPlayer()
    {
        var p = _player!;
        if (!p.Alive) return;
        if (p.SpawnProtect > 0 && (int)(_blink * 10) % 2 == 0) return;
        DrawTank(p, "#74c0fc", "#1864ab");
    }

    private void DrawTank(Tank t, string light, string dark)
    {
        float x = t.Position.X - t.Half;
        float y = t.Position.Y - t.Half;
        float s = Tank.Size;

        // 车体（渐变）+ 履带
        SkiaCanvas.GradientRoundRect(x + 2, y + 2, s - 4, s - 4, 3, light, dark);
        SkiaCanvas.FillRect(x, y + 2, 4, s - 4, "#222");
        SkiaCanvas.FillRect(x + s - 4, y + 2, 4, s - 4, "#222");

        // 炮塔
        SkiaCanvas.FillCircle(t.Position.X, t.Position.Y, s * 0.18f, dark);

        // 炮管
        float cx = t.Position.X, cy = t.Position.Y;
        float len = s / 2 + 4;
        (float ex, float ey) = t.Dir switch
        {
            TankDir.Up => (cx, cy - len),
            TankDir.Down => (cx, cy + len),
            TankDir.Left => (cx - len, cy),
            TankDir.Right => (cx + len, cy),
            _ => (cx, cy - len),
        };
        SkiaCanvas.Line(cx, cy, ex, ey, "#0b0e14", 5);
    }

    private void RenderHud()
    {
        SkiaCanvas.FillText("LEVEL " + (_level + 1) + "/" + LevelMaps.Length, 40, 28, "bold 14px system-ui", "#8b949e", "left");
        SkiaCanvas.FillText("SCORE " + _score, 40, 52, "bold 20px system-ui", "#e6edf3", "left");

        SkiaCanvas.FillText("ENEMIES " + (_enemiesRemaining + _enemies.Count), GameEngine.Width - 40, 28,
            "bold 14px system-ui", "#8b949e", "right");
        SkiaCanvas.FillText("LIVES", GameEngine.Width - 40, 52, "bold 14px system-ui", "#8b949e", "right");
        for (int i = 0; i < _lives; i++)
            SkiaCanvas.FillCircle(GameEngine.Width - 40 - i * 22, 70, 7, "#4dabf7");
    }

    private void RenderOverlay(string title, string sub, string color)
    {
        float cx = GameEngine.Width / 2;
        float cy = GameEngine.Height / 2;
        SkiaCanvas.Alpha(0.55f);
        SkiaCanvas.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#000000");
        SkiaCanvas.Alpha(1);
        SkiaCanvas.FillText(title, cx, cy - 20, "bold 44px system-ui", color, "center");
        SkiaCanvas.FillText(sub, cx, cy + 30, "bold 18px system-ui", "#e6edf3", "center");
    }
}
