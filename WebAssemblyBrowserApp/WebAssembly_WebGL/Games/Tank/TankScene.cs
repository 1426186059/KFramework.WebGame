using System;
using System.Collections.Generic;
using System.Linq;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 坦克闯关（Battle City 风格）：
/// 13×13 网格战场，玩家坦克守护基地，迎战一波波敌方坦克。
/// 砖墙可打碎、钢墙不可摧毁、水/树限制走位；
/// 击毁敌人有概率掉落道具（火力 / 全屏炸 / 基地钢化 / 无敌 / 加命）。
/// </summary>
public sealed class TankScene : GameScene
{
    private static readonly string[][] Levels =
    {
        new[]
        {
            ".............",
            ".B.BBBBBB.B.B",
            ".B.B....B.B.B",
            ".BBB.BBBB.BBB",
            "....B....B...",
            ".BB.B....B.BB",
            "....B....B...",
            ".BBB.BBBB.BBB",
            ".B.B....B.B..",
            ".B.BBBBBB.B.B",
            ".............",
            ".B...BBB...B.",
            ".B..B.E.B..B."
        },
        new[]
        {
            ".............",
            ".BBBBBBBBBBB.",
            ".B.S.B.B.S.B.",
            ".B.B.BBB.B.B.",
            ".B.BB.B.BB.B.",
            ".B...B.B...B.",
            ".B.B.BBB.B.B.",
            ".B.S.B.B.S.B.",
            ".BBBBBBBBBBB.",
            ".............",
            "....BBB.BB...",
            "BBB......BBBB",
            "BB..B.E.B..BB"
        },
        new[]
        {
            ".....S.S.....",
            ".T.BBBBBB.T..",
            ".T.B.BB.B.T..",
            ".T.BS..SB.T..",
            "...BB..BB....",
            ".S.B.WW.B.S..",
            "...BB..BB....",
            ".T.BS..SB.T..",
            ".T.B.BB.B.T..",
            ".T.BBBBBB.T..",
            ".....S.S.....",
            ".B.B..B..B.B.",
            "B..BB.E.BB..B"
        }
    };

    private enum State { Intro, Playing, Over }

    private State _state = State.Intro;
    private float _stateTime;

    private readonly TileMap _map = new();
    private readonly List<Tank> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<PowerUp> _powerUps = new();
    private readonly List<Particle> _particles = new();
    private readonly List<(Vector2 Pos, float T)> _spawnFlashes = new();

    private Tank _player = null!;
    private int _level = 1;
    private int _enemiesTotal;
    private int _enemiesSpawned;
    private int _enemiesLeft;
    private float _spawnTimer;
    private int _score;
    private int _lives = 3;
    private int _power = 1;        // 火力：同时在场子弹数 1..3
    private float _highScore;
    private float _shovelTimer;
    private float _flashTimer;
    private float _shock;
    private bool _won;

    /// <summary>敌方出生点（列, 行）。</summary>
    private static readonly (int Col, int Row)[] SpawnPoints = { (0, 0), (6, 0), (12, 0) };

    public TankScene() : base("tank") { }

    public override void Enter()
    {
        _score = 0;
        _lives = 3;
        _power = 1;
        _won = false;
        float.TryParse(Storage.Get("tank.high", "0"), out _highScore);
        StartLevel(1);
    }

    private void StartLevel(int level)
    {
        _level = level;
        _state = State.Intro;
        _stateTime = 0;
        _map.Load(Levels[level - 1]);
        _bullets.Clear();
        _powerUps.Clear();
        _enemies.Clear();
        _spawnFlashes.Clear();
        _particles.Clear();
        _enemiesSpawned = 0;
        _enemiesTotal = 10 + level * 2;   // 12 / 14 / 16
        _enemiesLeft = _enemiesTotal;
        _spawnTimer = 1.0f;
        _shovelTimer = 0;
        _flashTimer = 0;
        _player = SpawnPlayer();
    }

    private static Tank SpawnTank(int col, int row, bool player, TankType type = TankType.Basic) =>
        new(
            TileMap.OriginX + col * TileMap.Tile + TileMap.Tile / 2.0f,
            TileMap.OriginY + row * TileMap.Tile + TileMap.Tile / 2.0f,
            player, type);

    /// <summary>在出生点生成玩家坦克（2 秒出生无敌）。</summary>
    private static Tank SpawnPlayer()
    {
        var t = SpawnTank(2, 12, true);
        t.SpawnInvul = 2f;
        return t;
    }

    // ------------------------- 更新 -------------------------

    public override void Update(float dt)
    {
        // Esc 返回主菜单
        if (Input.IsKeyPressed(Input.Escape)) { GameEngine.Instance.Pop(); return; }

        _stateTime += dt;
        _shock = MathF.Max(0, _shock - dt);
        _flashTimer = MathF.Max(0, _flashTimer - dt);

        // 基地钢化计时
        if (_shovelTimer > 0)
        {
            _shovelTimer -= dt;
            if (_shovelTimer <= 0) _map.SteelAroundBase(false);
        }

        switch (_state)
        {
            case State.Intro:
                if (_stateTime > 2.2f) _state = State.Playing;
                UpdateParticles(dt);
                break;

            case State.Playing:
                UpdatePlaying(dt);
                break;

            case State.Over:
                if (Input.IsKeyPressed(Input.Enter) || Input.IsKeyPressed(Input.Space) || Input.IsMousePressed())
                    RestartGame();
                UpdateParticles(dt);
                break;
        }
    }

    private void UpdatePlaying(float dt)
    {
        UpdatePlayer(dt);
        UpdateEnemies(dt);
        UpdateBullets(dt);
        UpdateSpawning(dt);
        UpdatePowerUps(dt);
        UpdateParticles(dt);

        // 过关：本关敌人全部消灭
        if (_enemiesLeft <= 0 && !_enemies.Any(e => e.IsAlive))
        {
            if (_level >= Levels.Length) GameOver(true);
            else NextLevel();
        }
    }

    private void NextLevel()
    {
        Audio.Beep(523, 0.1f, "square", 0.07f);
        Audio.Beep(659, 0.1f, "square", 0.07f);
        Audio.Beep(784, 0.22f, "square", 0.08f);
        StartLevel(_level + 1);
    }

    private void UpdatePlayer(float dt)
    {
        var p = _player;
        if (p == null || !p.IsAlive) return;
        p.FireCd -= dt;
        p.SpawnInvul = MathF.Max(0, p.SpawnInvul - dt);
        p.Immortal = MathF.Max(0, p.Immortal - dt);
        p.HitFlash = MathF.Max(0, p.HitFlash - dt);

        int dir = -1;
        if (Input.IsKeyDown(Input.ArrowUp) || Input.IsKeyDown(Input.KeyW)) dir = Tank.Up;
        else if (Input.IsKeyDown(Input.ArrowDown) || Input.IsKeyDown(Input.KeyS)) dir = Tank.Down;
        else if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) dir = Tank.Left;
        else if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) dir = Tank.Right;

        // 只有按住方向键才移动；松开后停止（否则会用上一次的 Dir 继续前进）
        if (dir >= 0)
        {
            p.Dir = dir;

            // 移动（撞墙时尝试单轴滑动）
            float nx = p.Pos.X + Tank.DirX[p.Dir] * p.Speed * dt;
            float ny = p.Pos.Y + Tank.DirY[p.Dir] * p.Speed * dt;
            if (CanMove(p, nx, ny)) p.Pos = new Vector2(nx, ny);
            else if (CanMove(p, nx, p.Pos.Y)) p.Pos = new Vector2(nx, p.Pos.Y);
            else if (CanMove(p, p.Pos.X, ny)) p.Pos = new Vector2(p.Pos.X, ny);

            p.Pos.X = MathUtils.Clamp(p.Pos.X, TileMap.OriginX + Tank.Half, TileMap.OriginX + TileMap.MapSize - Tank.Half);
            p.Pos.Y = MathUtils.Clamp(p.Pos.Y, TileMap.OriginY + Tank.Half, TileMap.OriginY + TileMap.MapSize - Tank.Half);
        }

        if (Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed(Input.Enter) || Input.IsMousePressed())
            FirePlayer();
    }

    private void FirePlayer()
    {
        var p = _player;
        if (!p.IsAlive || p.FireCd > 0) return;
        if (_bullets.Count(b => b.IsAlive && b.IsPlayer) >= _power) return;
        p.FireCd = 0.24f;
        _bullets.Add(new Bullet(
            p.Pos.X + Tank.DirX[p.Dir] * 21,
            p.Pos.Y + Tank.DirY[p.Dir] * 21,
            p.Dir, true));
        Audio.Beep(520, 0.05f, "square", 0.04f);
    }

    private void UpdateEnemies(float dt)
    {
        // 基地中心，作为敌人寻的目标
        float bx = TileMap.OriginX + _map.BaseCol * TileMap.Tile + TileMap.Tile / 2.0f;
        float by = TileMap.OriginY + _map.BaseRow * TileMap.Tile + TileMap.Tile / 2.0f;

        foreach (var e in _enemies)
        {
            if (!e.IsAlive) continue;
            e.FireCd -= dt;
            e.SpawnInvul = MathF.Max(0, e.SpawnInvul - dt);
            e.Immortal = MathF.Max(0, e.Immortal - dt);
            e.HitFlash = MathF.Max(0, e.HitFlash - dt);

            // 寻的：偶尔转向基地，其余随机（保持压迫感但不至于全堵基地）
            if (Random.Shared.NextDouble() < dt * 0.6f)
                e.Dir = Random.Shared.NextDouble() < 0.45f ? DirToward(e, bx, by) : Random.Shared.Next(4);

            // 移动：撞墙先沿墙滑动，仍堵则多次随机换向，避免卡墙抖动
            float nx = e.Pos.X + Tank.DirX[e.Dir] * e.Speed * dt;
            float ny = e.Pos.Y + Tank.DirY[e.Dir] * e.Speed * dt;
            if (CanMove(e, nx, ny)) e.Pos = new Vector2(nx, ny);
            else if (CanMove(e, nx, e.Pos.Y)) e.Pos = new Vector2(nx, e.Pos.Y);
            else if (CanMove(e, e.Pos.X, ny)) e.Pos = new Vector2(e.Pos.X, ny);
            else TryRandomTurn(e);

            // 射击
            if (e.FireCd <= 0 && Random.Shared.NextDouble() < dt * 2.2f)
            {
                e.FireCd = MathUtils.Rand(1.1f, 2.4f);
                if (_bullets.Count(b => b.IsAlive && !b.IsPlayer) < 4)
                {
                    _bullets.Add(new Bullet(
                        e.Pos.X + Tank.DirX[e.Dir] * 21,
                        e.Pos.Y + Tank.DirY[e.Dir] * 21,
                        e.Dir, false));
                    Audio.Beep(300, 0.04f, "square", 0.03f);
                }
            }
        }
    }

    /// <summary>返回朝向目标的水平/垂直方向（优先走轴距更远的一维）。</summary>
    private static int DirToward(Tank e, float tx, float ty)
    {
        float dx = tx - e.Pos.X, dy = ty - e.Pos.Y;
        return MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? Tank.Right : Tank.Left)
                                           : (dy > 0 ? Tank.Down : Tank.Up);
    }

    /// <summary>随机换向并验证可通行，最多尝试 4 次；返回是否成功转向。</summary>
    private bool TryRandomTurn(Tank e)
    {
        for (int k = 0; k < 4; k++)
        {
            int nd = Random.Shared.Next(4);
            if (nd == e.Dir) continue;
            float nx = e.Pos.X + Tank.DirX[nd] * 2;
            float ny = e.Pos.Y + Tank.DirY[nd] * 2;
            if (CanMove(e, nx, ny)) { e.Dir = nd; return true; }
        }
        return false;
    }

    private void UpdateSpawning(float dt)
    {
        if (_enemiesSpawned >= _enemiesTotal) return;
        _spawnTimer -= dt;
        int maxOnField = _level >= 3 ? 5 : 4;
        if (_spawnTimer > 0 || _enemies.Count(e => e.IsAlive) >= maxOnField) return;

        foreach (var (col, row) in SpawnPoints)
        {
            var pos = new Vector2(
                TileMap.OriginX + col * TileMap.Tile + TileMap.Tile / 2.0f,
                TileMap.OriginY + row * TileMap.Tile + TileMap.Tile / 2.0f);
            if (_enemies.Any(e => e.IsAlive && MathF.Abs(e.Pos.X - pos.X) < 30 && MathF.Abs(e.Pos.Y - pos.Y) < 30))
                continue;

            var enemy = SpawnTank(col, row, false, PickEnemyType());
            enemy.SpawnInvul = 1.1f;
            _enemies.Add(enemy);
            _spawnFlashes.Add((pos, 0.9f));
            _enemiesSpawned++;
            _spawnTimer = (float)MathUtils.Rand(2.5f, 4.5f);
            break;
        }
    }

    private TankType PickEnemyType()
    {
        int r = Random.Shared.Next(100);
        return _level switch
        {
            >= 3 => r < 28 ? TankType.Basic : r < 55 ? TankType.Fast : r < 80 ? TankType.Heavy : TankType.Armor,
            2 => r < 45 ? TankType.Basic : r < 72 ? TankType.Fast : TankType.Heavy,
            _ => r < 75 ? TankType.Basic : TankType.Fast
        };
    }

    private void UpdateBullets(float dt)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            if (!b.IsAlive) { _bullets.RemoveAt(i); continue; }

            // 细分步进：每小步 ≤8px，避免掉帧/高速时穿透砖墙、坦克或边界
            float dist = b.Speed * dt;
            int steps = Math.Max(1, (int)MathF.Ceiling(dist / 8f));
            float perStep = dist / steps;
            for (int s = 0; s < steps && b.IsAlive; s++)
            {
                b.Pos += new Vector2(Tank.DirX[b.Dir] * perStep, Tank.DirY[b.Dir] * perStep);
                StepBullet(b);
            }
        }
    }

    private void StepBullet(Bullet b)
    {
        // 出界
        if (b.Pos.X <= TileMap.OriginX || b.Pos.X >= TileMap.OriginX + TileMap.MapSize ||
            b.Pos.Y <= TileMap.OriginY || b.Pos.Y >= TileMap.OriginY + TileMap.MapSize)
        {
            b.IsAlive = false;
            return;
        }

        int tile = _map.TileAt(b.Pos.X, b.Pos.Y);
        if (tile == TileMap.Brick)
        {
            // 破坏一个子块则子弹消失；中心点落在已破空洞则穿透继续飞（连续射击可打穿整格）
            if (_map.HitBrick(b.Pos.X, b.Pos.Y))
            {
                SpawnBurst(b.Pos, "#d4a24c", 8, 150);
                Audio.Beep(240, 0.04f, "square", 0.04f);
                if (b.IsPlayer) _score += 5;
                b.IsAlive = false;
            }
            return;
        }
        if (tile == TileMap.Steel)
        {
            SpawnBurst(b.Pos, "#ced4da", 6, 120);
            Audio.Beep(180, 0.05f, "square", 0.05f);
            b.IsAlive = false;
            return;
        }
        if (tile == TileMap.Base)
        {
            DestroyBase();
            b.IsAlive = false;
            return;
        }
        if (tile == TileMap.Water || tile == TileMap.Tree) return;   // 子弹穿过水与树

        // 命中坦克
        if (b.IsPlayer)
        {
            foreach (var e in _enemies)
            {
                if (!e.IsAlive) continue;
                if (MathF.Abs(e.Pos.X - b.Pos.X) < Tank.Half + Bullet.Half &&
                    MathF.Abs(e.Pos.Y - b.Pos.Y) < Tank.Half + Bullet.Half)
                {
                    HitEnemy(e, b);
                    b.IsAlive = false;
                    break;
                }
            }
        }
        else if (_player.IsAlive &&
                 MathF.Abs(_player.Pos.X - b.Pos.X) < Tank.Half + Bullet.Half &&
                 MathF.Abs(_player.Pos.Y - b.Pos.Y) < Tank.Half + Bullet.Half)
        {
            HitPlayer(b);
            b.IsAlive = false;
        }
    }

    private void HitEnemy(Tank e, Bullet b)
    {
        if (e.SpawnInvul > 0) { SpawnBurst(b.Pos, "#ffffff", 6, 100); return; }
        e.Hp--;
        e.HitFlash = 0.1f;
        if (e.Hp <= 0)
        {
            e.IsAlive = false;
            _score += e.Score;
            _enemiesLeft--;
            SpawnBurst(e.Pos, e.Color, 18, 220);
            SpawnBurst(e.Pos, "#ffd43b", 10, 150);
            Audio.Beep(420, 0.1f, "square", 0.08f);
            Audio.Beep(220, 0.16f, "triangle", 0.08f);
            _shock = 0.08f;
            MaybeDropPowerUp(e.Pos);
        }
        else
        {
            SpawnBurst(b.Pos, "#ced4da", 10, 140);
            Audio.Beep(300, 0.06f, "square", 0.06f);
        }
    }

    private void HitPlayer(Bullet b)
    {
        var p = _player;
        if (p.SpawnInvul > 0 || p.Immortal > 0)
        {
            SpawnBurst(b.Pos, "#4dabf7", 8, 120);
            return;
        }
        p.IsAlive = false;
        _lives--;
        SpawnBurst(p.Pos, p.Color, 24, 260);
        Audio.Beep(160, 0.4f, "sawtooth", 0.12f);
        _shock = 0.25f;
        if (_lives <= 0) GameOver(false);
        else
        {
            _player = SpawnPlayer();
            Audio.Beep(440, 0.15f, "triangle", 0.09f);
        }
    }

    private void DestroyBase()
    {
        var bp = new Vector2(
            TileMap.OriginX + _map.BaseCol * TileMap.Tile + TileMap.Tile / 2.0f,
            TileMap.OriginY + _map.BaseRow * TileMap.Tile + TileMap.Tile / 2.0f);
        _map.DestroyBase();
        SpawnBurst(bp, "#ff8787", 30, 300);
        SpawnBurst(bp, "#ffe066", 20, 200);
        Audio.Beep(120, 0.6f, "sawtooth", 0.15f);
        _shock = 0.3f;
        _flashTimer = 0.2f;
        GameOver(false);
    }

    private void GameOver(bool won)
    {
        _won = won;
        _state = State.Over;
        _stateTime = 0;
        SaveHighScore();
    }

    private void RestartGame()
    {
        _score = 0;
        _lives = 3;
        _power = 1;
        _level = 1;
        _won = false;
        StartLevel(1);
    }

    private void SaveHighScore()
    {
        if (_score > _highScore)
        {
            _highScore = _score;
            Storage.Set("tank.high", _score.ToString());
        }
    }

    private bool CanMove(Tank t, float nx, float ny) =>
        !_map.TankCollides(nx, ny, Tank.Half) && !TankHitsTank(t, nx, ny);

    private bool TankHitsTank(Tank self, float nx, float ny)
    {
        foreach (var o in _enemies)
        {
            if (!o.IsAlive || ReferenceEquals(o, self)) continue;
            if (MathF.Abs(o.Pos.X - nx) < Tank.Half * 2 - 2 && MathF.Abs(o.Pos.Y - ny) < Tank.Half * 2 - 2)
                return true;
        }
        if (!self.IsPlayer && _player.IsAlive &&
            MathF.Abs(_player.Pos.X - nx) < Tank.Half * 2 - 2 &&
            MathF.Abs(_player.Pos.Y - ny) < Tank.Half * 2 - 2)
            return true;
        return false;
    }

    private void MaybeDropPowerUp(Vector2 pos)
    {
        if (Random.Shared.NextDouble() > 0.16f) return;
        _powerUps.Add(new PowerUp { Pos = FindDropSpot(pos), Kind = (PowerUpKind)Random.Shared.Next(5) });
    }

    /// <summary>找离 pos 最近的坦克可站立落点（避免道具掉进墙里/水里吃不到）。</summary>
    private Vector2 FindDropSpot(Vector2 pos)
    {
        if (!_map.TankCollides(pos.X, pos.Y, 12)) return pos;
        for (int ring = 1; ring <= 3; ring++)
        {
            float step = 16 * ring;
            foreach (var (dx, dy) in new[] { (step, 0.0f), (-step, 0.0f), (0.0f, step), (0.0f, -step) })
            {
                var q = new Vector2(pos.X + dx, pos.Y + dy);
                if (!_map.TankCollides(q.X, q.Y, 12)) return q;
            }
        }
        return pos;
    }

    private void UpdatePowerUps(float dt)
    {
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var pu = _powerUps[i];
            pu.Life -= dt;
            if (pu.Life <= 0) { _powerUps.RemoveAt(i); continue; }

            if (_player.IsAlive &&
                MathF.Abs(_player.Pos.X - pu.Pos.X) < 22 &&
                MathF.Abs(_player.Pos.Y - pu.Pos.Y) < 22)
            {
                ApplyPowerUp(pu.Kind);
                _powerUps.RemoveAt(i);
            }
        }
    }

    private void ApplyPowerUp(PowerUpKind k)
    {
        Audio.Beep(660, 0.08f, "square", 0.07f);
        Audio.Beep(880, 0.1f, "square", 0.07f);
        switch (k)
        {
            case PowerUpKind.Star:
                if (_power < 3) _power++;
                else _score += 500;
                break;
            case PowerUpKind.Grenade:
                foreach (var e in _enemies)
                {
                    if (!e.IsAlive) continue;
                    e.IsAlive = false;
                    _score += e.Score;
                    _enemiesLeft--;
                    SpawnBurst(e.Pos, "#ff8787", 14, 200);
                }
                _flashTimer = 0.22f;
                _shock = 0.18f;
                break;
            case PowerUpKind.Shovel:
                _map.SteelAroundBase(true);
                _shovelTimer = 10f;
                break;
            case PowerUpKind.Helmet:
                _player.Immortal = 10f;
                break;
            case PowerUpKind.Life:
                _lives++;
                break;
        }
    }

    // ------------------------- 特效 -------------------------

    private void SpawnBurst(Vector2 pos, string color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = MathUtils.Rand(0, MathF.PI * 2);
            float spd = MathUtils.Rand(speed * 0.3f, speed);
            _particles.Add(new Particle(
                pos,
                new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * spd,
                MathUtils.Rand(2, 5),
                (float)MathUtils.Rand(0.25f, 0.6f),
                color));
        }
        if (_particles.Count > 500)
            _particles.RemoveRange(0, _particles.Count - 500);
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            if (!_particles[i].Update(dt))
                _particles.RemoveAt(i);
        }
        for (int i = _spawnFlashes.Count - 1; i >= 0; i--)
        {
            var f = _spawnFlashes[i];
            if (f.T <= 0) _spawnFlashes.RemoveAt(i);
            else _spawnFlashes[i] = (f.Pos, f.T - dt);
        }
    }

    // ------------------------- 渲染 -------------------------

    public override void Render()
    {
        WebGL.Clear("#0d1117");
        WebGL.Save();

        if (_shock > 0)
        {
            float s = _shock * 10;
            WebGL.Translate(MathUtils.Rand(-s, s), MathUtils.Rand(-s, s));
        }

        RenderHud();
        RenderMap();
        RenderPowerUps();
        RenderEnemies();
        RenderPlayer();
        RenderBullets();
        RenderSpawnFlashes();
        RenderParticles();

        if (_state == State.Intro) RenderIntro();
        else if (_state == State.Over) RenderGameOver();

        if (_flashTimer > 0)
        {
            WebGL.Alpha(0.35f);
            WebGL.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#ffffff");
            WebGL.Alpha(1);
        }

        WebGL.Restore();
    }

    private void RenderHud()
    {
        WebGL.FillText("坦克闯关", 20, 24, "bold 20px system-ui, sans-serif", "#e6edf3", "left");
        WebGL.FillText("STAGE " + _level, 20, 48, "bold 14px system-ui, sans-serif", "#8b949e", "left");

        WebGL.FillText("得分", GameEngine.Width - 20, 24, "bold 13px system-ui, sans-serif", "#8b949e", "right");
        WebGL.FillText(_score.ToString(), GameEngine.Width - 20, 46, "bold 24px system-ui, sans-serif", "#ffe066", "right");
        WebGL.FillText("最高 " + _highScore.ToString("0"), GameEngine.Width - 20, 70, "bold 12px system-ui, sans-serif", "#f6c445", "right");

        WebGL.FillText("生命", 20, 70, "bold 12px system-ui, sans-serif", "#8b949e", "left");
        for (int i = 0; i < _lives; i++)
        {
            float x = 58 + i * 26;
            WebGL.Shadow("#ffd43b", 6);
            WebGL.RoundedRect(x, 60, 20, 20, 3, "#ffd43b");
            WebGL.NoShadow();
            WebGL.FillRect(x + 8, 52, 4, 9, "#ffd43b");   // 炮管朝上
        }

        if (_power > 1)
            WebGL.FillText("火力 " + _power, 20, 90, "bold 12px system-ui, sans-serif", "#ffd43b", "left");
        else if (_shovelTimer > 0)
            WebGL.FillText("基地钢化 " + _shovelTimer.ToString("0"), 20, 90, "bold 12px system-ui, sans-serif", "#94d82d", "left");
        else if (_player != null && _player.Immortal > 0)
            WebGL.FillText("无敌 " + _player.Immortal.ToString("0"), 20, 90, "bold 12px system-ui, sans-serif", "#4dabf7", "left");
    }

    private void RenderMap()
    {
        for (int r = 0; r < TileMap.Rows; r++)
        {
            for (int c = 0; c < TileMap.Cols; c++)
            {
                float x = TileMap.OriginX + c * TileMap.Tile;
                float y = TileMap.OriginY + r * TileMap.Tile;
                switch (_map.Tiles[r, c])
                {
                    case TileMap.Brick: RenderBrick(r, c, x, y); break;
                    case TileMap.Steel: RenderSteel(x, y); break;
                    case TileMap.Water: RenderWater(x, y); break;
                    case TileMap.Tree: RenderTree(x, y); break;
                    case TileMap.Base: RenderBase(x, y); break;
                }
            }
        }
    }

    private void RenderBrick(int r, int c, float x, float y)
    {
        WebGL.Shadow("#6b4314", 4);
        for (int sr = 0; sr < 2; sr++)
        {
            for (int sc = 0; sc < 2; sc++)
            {
                if (!_map.BrickBitAlive(r, c, sr, sc)) continue;
                float bx = x + sc * 16 + 1, by = y + sr * 16 + 1;
                WebGL.FillRect(bx, by, 14, 14, "#d4a24c");
                WebGL.FillRect(bx + 2, by + 2, 10, 4, "#e8c88a");
            }
        }
        WebGL.NoShadow();
    }

    private void RenderSteel(float x, float y)
    {
        WebGL.Shadow("#495057", 4);
        WebGL.FillRect(x + 2, y + 2, 28, 28, "#868e96");
        WebGL.NoShadow();
        WebGL.FillRect(x + 2, y + 13, 28, 6, "#ced4da");
        WebGL.FillRect(x + 13, y + 2, 6, 28, "#ced4da");
        WebGL.FillRect(x + 6, y + 6, 5, 5, "#f1f3f5");
        WebGL.FillRect(x + 21, y + 21, 5, 5, "#f1f3f5");
    }

    private void RenderWater(float x, float y)
    {
        WebGL.FillRect(x, y, 32, 32, "#1864ab");
        WebGL.FillRect(x, y + 8, 32, 5, "#4dabf7");
        WebGL.FillRect(x, y + 20, 32, 5, "#4dabf7");
    }

    private void RenderTree(float x, float y)
    {
        WebGL.FillRect(x, y, 32, 32, "#23732d");
        WebGL.FillCircle(x + 8, y + 8, 7, "#40c057");
        WebGL.FillCircle(x + 24, y + 8, 7, "#69db7c");
        WebGL.FillCircle(x + 8, y + 24, 7, "#69db7c");
        WebGL.FillCircle(x + 24, y + 24, 7, "#40c057");
    }

    private void RenderBase(float x, float y)
    {
        WebGL.Shadow("#ffd43b", 8);
        WebGL.RoundedRect(x + 3, y + 3, 26, 26, 5, "#f1f3f5");
        WebGL.NoShadow();
        WebGL.FillRect(x + 8, y + 8, 16, 16, "#ffd43b");
        WebGL.FillRect(x + 14, y + 4, 4, 24, "#ffd43b");
        WebGL.FillRect(x + 4, y + 14, 24, 4, "#ffd43b");
    }

    private void RenderPowerUps()
    {
        foreach (var pu in _powerUps)
        {
            float pulse = 1 + MathF.Sin(_stateTime * 6) * 0.1f;
            float x = pu.Pos.X, y = pu.Pos.Y;

            WebGL.Alpha(0.85f);
            WebGL.Shadow(pu.Color, 10);
            WebGL.RoundedRect(x - 12, y - 12, 24, 24, 6, "#1c2128");
            WebGL.NoShadow();
            WebGL.RoundedRect(x - 12, y - 12, 24, 24, 6, pu.Color);
            WebGL.Alpha(1);

            switch (pu.Kind)
            {
                case PowerUpKind.Star:
                    WebGL.FillRect(x - 2, y - 8, 4, 16, "#ffd43b");
                    WebGL.FillRect(x - 8, y - 2, 16, 4, "#ffd43b");
                    break;
                case PowerUpKind.Grenade:
                    WebGL.FillCircle(x, y, 6 * pulse, "#ff6b6b");
                    WebGL.FillCircle(x - 2, y - 2, 2, "#ffffff");
                    break;
                case PowerUpKind.Shovel:
                    WebGL.FillRect(x - 7, y - 4, 14, 3, "#94d82d");
                    WebGL.FillRect(x - 7, y + 2, 14, 3, "#94d82d");
                    break;
                case PowerUpKind.Helmet:
                    WebGL.FillCircle(x, y, 7 * pulse, "#4dabf7");
                    WebGL.FillCircle(x - 2, y - 2, 2.5f, "#ffffff");
                    break;
                case PowerUpKind.Life:
                    WebGL.FillRect(x - 2, y - 7, 4, 14, "#f06595");
                    WebGL.FillRect(x - 7, y - 2, 14, 4, "#f06595");
                    break;
            }
        }
    }

    private void RenderEnemies()
    {
        foreach (var e in _enemies)
            if (e.IsAlive) RenderTank(e);   // 死亡的坦克不再绘制，尸体消失
    }

    private void RenderPlayer()
    {
        if (_player.IsAlive) RenderTank(_player);
    }

    private void RenderTank(Tank t)
    {
        bool invul = t.SpawnInvul > 0 || t.Immortal > 0;
        if (invul && (int)(_stateTime * 8) % 2 == 0) return;   // 无敌闪烁

        string body = t.IsPlayer ? "#ffd43b" :
            t.Type == TankType.Heavy ? (t.Hp >= 3 ? "#868e96" : t.Hp == 2 ? "#adb5bd" : "#dee2e6") : t.Color;
        if (t.HitFlash > 0) body = "#ffffff";

        float ang = t.Dir * MathF.PI / 2;
        WebGL.Shadow(body, 10);
        WebGL.Save();
        WebGL.Translate(t.Pos.X, t.Pos.Y);
        WebGL.Rotate(ang);

        // 履带
        WebGL.FillRect(-15, -15, 8, 30, "#00000055");
        WebGL.FillRect(7, -15, 8, 30, "#00000055");
        // 车体
        WebGL.RoundedRect(-14, -14, 28, 28, 4, body);
        WebGL.RoundedRect(-9, -9, 18, 18, 3, "#ffffff2e");
        // 炮塔
        WebGL.FillCircle(0, 0, 7, body);
        WebGL.FillCircle(0, 0, 4, "#ffffff2e");
        // 炮管
        WebGL.FillRect(-2.5f, -20, 5, 14, body);
        WebGL.FillRect(-2.5f, -20, 5, 4, "#00000033");

        WebGL.Restore();
        WebGL.NoShadow();
    }

    private void RenderBullets()
    {
        foreach (var b in _bullets)
        {
            if (!b.IsAlive) continue;
            string color = b.IsPlayer ? "#ffe066" : "#ff6b6b";
            WebGL.Shadow(color, 6);
            WebGL.FillRect(b.Pos.X - 3.5f, b.Pos.Y - 3.5f, 7, 7, color);
            WebGL.NoShadow();
        }
    }

    private void RenderSpawnFlashes()
    {
        foreach (var (pos, t) in _spawnFlashes)
        {
            float a = MathF.Min(1, t * 3);
            WebGL.Alpha(a);
            WebGL.Line(pos.X - 10, pos.Y - 10, pos.X + 10, pos.Y + 10, "#ffffff", 3);
            WebGL.Line(pos.X + 10, pos.Y - 10, pos.X - 10, pos.Y + 10, "#ffffff", 3);
            WebGL.Alpha(1);
        }
    }

    private void RenderParticles()
    {
        foreach (var p in _particles)
        {
            WebGL.Alpha(p.Alpha);
            WebGL.FillCircle(p.Position.X, p.Position.Y, p.Size * p.Alpha + 0.5f, p.Color);
        }
        WebGL.Alpha(1);
    }

    private void RenderIntro()
    {
        float cx = GameEngine.Width / 2, cy = GameEngine.Height / 2;
        WebGL.Alpha(0.5f);
        WebGL.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#000000");
        WebGL.Alpha(1);

        WebGL.Shadow("#ffd43b", 28);
        WebGL.FillText("STAGE " + _level, cx, cy - 24, "bold 56px system-ui, sans-serif", "#ffd43b", "center");
        WebGL.NoShadow();
        WebGL.FillText("消灭所有敌人，守护基地！", cx, cy + 28, "18px system-ui, sans-serif", "#8b949e", "center");
    }

    private void RenderGameOver()
    {
        float cx = GameEngine.Width / 2, cy = GameEngine.Height / 2;
        WebGL.Alpha(0.55f);
        WebGL.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#000000");
        WebGL.Alpha(1);

        string title = _won ? "全部通关！" : "游戏结束";
        string color = _won ? "#48dbfb" : "#ff6b6b";
        WebGL.Shadow(color, 28);
        WebGL.FillText(title, cx, cy - 30, "bold 54px system-ui, sans-serif", color, "center");
        WebGL.NoShadow();

        WebGL.FillText("得分 " + _score, cx, cy + 24, "bold 24px system-ui, sans-serif", "#e6edf3", "center");
        if ((int)(_stateTime * 2) % 2 == 0)
            WebGL.FillText("按 Enter / 点击 重新开始 · Esc 返回菜单", cx, cy + 72, "16px system-ui, sans-serif", "#8b949e", "center");
    }
}
