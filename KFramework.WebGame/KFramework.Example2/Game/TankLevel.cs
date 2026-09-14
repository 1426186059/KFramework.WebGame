using System;
using System.Collections.Generic;
using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>
/// 关卡 + 玩法管理器：对应 PixiJS 版的 Game/TankLevel.ts。
/// <list type="bullet">
///   <item>网格地形数据、碰撞查询（PixiJS 的 tiles 网格 + GetTile/SetTileNull）；</item>
///   <item>持有玩家/敌人坦克、炮弹、特效、道具等玩法实体（PixiJS 的 TankList / ShellList / 各类对象池），
///         并在 Update 中驱动它们（PixiJS 的 TankLevel.update）；</item>
///   <item>渲染用 SpriteBatch 即时模式，对应 PixiJS 把 Tile/坦克/Shell 加入各 Root 容器。</item>
/// </list>
/// 场景（TankScene / MainScene）只负责装配与屏幕切换，玩法全在这里。
/// </summary>
internal sealed class TankLevel
{
    // ===== 地形数据（对应 PixiJS 的 tiles 网格）=====
    private readonly Tile[,] _tiles = new Tile[TankConfig.MapHeight, TankConfig.MapWidth];
    private readonly List<Point> _enemySpawns = new();

    public Vector2 PlayerSpawn { get; private set; }
    public IReadOnlyList<Point> EnemySpawns => _enemySpawns;
    public bool HeartDestroyed { get; private set; }

    // ===== 玩法实体（对应 PixiJS 的 TankList / ShellList / 对象池）=====
    private readonly Random _rng = new(20240915);
    private PlayerTank? _player;
    private readonly List<EnemyTank> _enemies = new();
    private readonly List<Shell> _shells = new();
    private readonly List<ExplodeEffect> _explosions = new();
    private readonly List<BornEffect> _borns = new();
    private readonly List<PowerUp> _powerUps = new();

    // ===== 玩法状态 / 常量 =====
    public const int LevelCount = 5;
    private const int DropsEvery = 3;          // 每击毁 N 台敌人掉一个道具
    private const float StageClearDelay = 2f;

    private int _levelIndex;
    private int _lives = TankConfig.PlayerLives;
    private int _enemiesRemaining = TankConfig.EnemyTotal;
    private int _killed;
    private int _fireLevel;

    private float _respawnTimer;
    private float _spawnTimer;
    private float _shieldTimer;
    private float _freezeTimer;
    private float _stageClearTimer = -1f;

    private int _animTick;
    private bool _gameOver;
    private bool _allCleared;

    // 依赖（由场景在加载时注入，对应 PixiJS 的 engine() 全局单例）
    private ContentManager _content = null!;
    private SoundCenter _sounds = null!;

    // ===== 供场景 / HUD 读取的只读状态 =====
    public int LevelIndex => _levelIndex;
    public int Lives => _lives;
    public int EnemiesRemaining => _enemiesRemaining;
    public int ActiveEnemies => _enemies.Count(static e => e.Active);
    public bool GameOver => _gameOver;
    public bool AllCleared => _allCleared;
    public int AnimFrame => (_animTick / 8) % 2;

    public void Init(ContentManager content, SoundCenter sounds)
    {
        _content = content;
        _sounds = sounds;
    }

    // =====================================================================
    // 地形：加载 / 查询 / 碰撞（对应 PixiJS 的 LoadLevel / GetTile / SetTileNull）
    // =====================================================================

    public void Load(string text)
    {
        string[] lines = text.Trim().Split('\n');
        _enemySpawns.Clear();

        int row = 0;
        for (int i = TankConfig.LevelSkipLines; i < lines.Length && row < TankConfig.MapHeight; i++, row++)
        {
            string line = lines[i].TrimEnd('\r');
            for (int x = 0; x < TankConfig.MapWidth; x++)
            {
                char c = x < line.Length ? line[x] : ' ';
                _tiles[row, x] = Parse(c);

                if (c == 'P') PlayerSpawn = TileCenter(x, row);
                else if (c == 'E') _enemySpawns.Add(new Point(x, row));
            }
        }
    }

    private static Tile Parse(char c) => c switch
    {
        '#' => Tile.Wall,
        '*' => Tile.Barriar,
        '~' => Tile.Water,
        '^' => Tile.Grass,
        '@' => Tile.Heart,
        _ => Tile.Empty,
    };

    public Vector2 TileCenter(int x, int y)
        => new(x * TankConfig.TileSize + TankConfig.TileSize / 2f,
               y * TankConfig.TileSize + TankConfig.TileSize / 2f);

    public Tile Get(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= TankConfig.MapWidth || ty >= TankConfig.MapHeight)
            return Tile.Barriar;   // 地图外一律视为不可穿越
        return _tiles[ty, tx];
    }

    public void Clear(int tx, int ty)
    {
        if (tx < 0 || ty < 0 || tx >= TankConfig.MapWidth || ty >= TankConfig.MapHeight) return;
        _tiles[ty, tx] = Tile.Empty;
    }

    private static bool IsPassable(Tile tile) => tile is Tile.Empty or Tile.Grass;

    /// <summary>尝试把坦克从 pos 移动 delta；被地形或边界阻挡时返回 false。</summary>
    public bool TryMove(ref Vector2 pos, Vector2 delta)
    {
        Vector2 next = pos + delta;
        float half = TankConfig.TankSize / 2f;

        float left = next.X - half;
        float right = next.X + half - 0.001f;
        float top = next.Y - half;
        float bottom = next.Y + half - 0.001f;

        if (left < 0 || top < 0 ||
            right >= TankConfig.MapWidth * TankConfig.TileSize ||
            bottom >= TankConfig.MapHeight * TankConfig.TileSize)
            return false;

        for (int ty = (int)(top / TankConfig.TileSize); ty <= (int)(bottom / TankConfig.TileSize); ty++)
        {
            for (int tx = (int)(left / TankConfig.TileSize); tx <= (int)(right / TankConfig.TileSize); tx++)
            {
                if (!IsPassable(Get(tx, ty))) return false;
            }
        }

        pos = next;
        return true;
    }

    /// <summary>子弹命中格子。返回 true 表示子弹应销毁；击中老窝时 heartHit 为 true。</summary>
    public bool BulletHit(int tx, int ty, out bool heartHit)
    {
        heartHit = false;
        Tile tile = Get(tx, ty);

        if (tile == Tile.Wall)
        {
            Clear(tx, ty);
            return true;
        }
        if (tile == Tile.Heart)
        {
            heartHit = true;
            HeartDestroyed = true;
            return true;
        }
        return tile == Tile.Barriar;   // 空 / 草 / 水 不阻挡子弹
    }

    /// <summary>绘制地形（不含实体）。草丛要在坦克之后再画，所以用 grassOnTop 分两趟。</summary>
    public void Draw(SpriteBatch batch, Vector2 origin, ResCenter res, bool grassOnTop)
    {
        for (int y = 0; y < TankConfig.MapHeight; y++)
        {
            for (int x = 0; x < TankConfig.MapWidth; x++)
            {
                Tile tile = _tiles[y, x];
                if (tile == Tile.Empty) continue;
                if ((tile == Tile.Grass) != grassOnTop) continue;

                Texture2D? tex = tile switch
                {
                    Tile.Wall => res.Wall,
                    Tile.Barriar => res.Barriar,
                    Tile.Water => res.Water,
                    Tile.Heart => res.Heart,
                    Tile.Grass => res.Grass,
                    _ => null,
                };
                if (tex is null) continue;

                batch.Draw(tex, new Vector2(origin.X + x * TankConfig.TileSize,
                                            origin.Y + y * TankConfig.TileSize), Color.White);
            }
        }
    }

    // =====================================================================
    // 关卡 / 玩法（对应 PixiJS 的 Init / LoadLevel / update）
    // =====================================================================

    /// <summary>开始一关（index=0 即新游戏）。</summary>
    public void LoadLevel(int index)
    {
        _gameOver = false;
        _allCleared = false;

        _levelIndex = index;
        Load(_content.LoadText($"levels/{index:00}"));

        _enemies.Clear();
        _shells.Clear();
        _explosions.Clear();
        _borns.Clear();
        _powerUps.Clear();

        _enemiesRemaining = TankConfig.EnemyTotal;
        _killed = 0;
        _spawnTimer = 1.5f;
        _stageClearTimer = -1f;
        _freezeTimer = 0f;

        SpawnPlayer(keepStats: index > 0);
    }

    private void SpawnPlayer(bool keepStats)
    {
        if (!keepStats)
        {
            _lives = TankConfig.PlayerLives;
            _fireLevel = 0;
        }

        _player = new PlayerTank { Position = PlayerSpawn, Direction = Dir.Up, Active = true };
        _borns.Add(new BornEffect { Position = PlayerSpawn });
        _shieldTimer = 2f;   // 出生保护
    }

    public void Update(float dt)
    {
        _animTick++;

        UpdateStageClear(dt);
        UpdateRespawn(dt);
        UpdateTanks(dt);
        UpdateShells(dt);
        UpdatePowerUps(dt);
        UpdateEffects(dt);
        UpdateSpawning(dt);
        CheckVictory();
    }

    private void UpdateStageClear(float dt)
    {
        if (_stageClearTimer <= 0f) return;

        _stageClearTimer -= dt;
        if (_stageClearTimer > 0f) return;

        if (_levelIndex + 1 >= LevelCount)
        {
            _allCleared = true;
            _sounds.Play("gameover");
            return;
        }
        LoadLevel(_levelIndex + 1);
    }

    private void UpdateTanks(float dt)
    {
        if (_player is { Active: true })
        {
            Shell? shell = _player.Update(dt, this);
            if (shell is not null)
            {
                shell.SpeedBonus = _fireLevel;
                _shells.Add(shell);
                _sounds.Play("shoot");
            }
        }

        bool frozen = _freezeTimer > 0f;
        foreach (EnemyTank enemy in _enemies)
        {
            if (!enemy.Active) continue;

            // 时钟道具：敌人停止行动，但仍在场上
            if (frozen) continue;

            Shell? shell = enemy.Update(dt, this);
            if (shell is not null) _shells.Add(shell);
        }
    }

    private void UpdateShells(float dt)
    {
        foreach (Shell shell in _shells)
        {
            if (!shell.Active) continue;
            shell.Update(dt);

            if (shell.OutOfField)
            {
                shell.Active = false;
                continue;
            }

            Point tile = shell.Tile;

            // 强化子弹可以打穿铁块
            if (shell.SpeedBonus >= 2 && Get(tile.X, tile.Y) == Tile.Barriar)
                Clear(tile.X, tile.Y);

            if (BulletHit(tile.X, tile.Y, out bool heartHit))
            {
                shell.Active = false;
                _sounds.Play("hit");
                if (heartHit)
                {
                    _gameOver = true;
                    _explosions.Add(new ExplodeEffect { Position = TileCenter(tile.X, tile.Y) });
                    _sounds.Play("gameover");
                }
                continue;
            }

            if (shell.FromPlayer) HitEnemies(shell);
            else HitPlayer(shell);
        }

        _shells.RemoveAll(static s => !s.Active);
        _enemies.RemoveAll(static e => !e.Active);
    }

    private void HitEnemies(Shell shell)
    {
        foreach (EnemyTank enemy in _enemies)
        {
            if (!enemy.Active) continue;
            if (!shell.Bounds.Intersects(enemy.Bounds)) continue;

            shell.Active = false;
            enemy.Active = false;
            _killed++;
            _explosions.Add(new ExplodeEffect { Position = enemy.Position });
            _sounds.Play("explosion");

            if (_killed % DropsEvery == 0) DropPowerUp(enemy.Position);
            return;
        }
    }

    private void HitPlayer(Shell shell)
    {
        if (_player is not { Active: true }) return;
        if (!shell.Bounds.Intersects(_player.Bounds)) return;

        shell.Active = false;

        // 护盾期间免疫，只播个命中音
        if (_shieldTimer > 0f)
        {
            _sounds.Play("hit");
            return;
        }

        _explosions.Add(new ExplodeEffect { Position = _player.Position });
        _sounds.Play("explosion");
        _player = null;

        _lives--;
        if (_lives <= 0)
        {
            _gameOver = true;
            _sounds.Play("gameover");
        }
        else
        {
            _respawnTimer = 1.5f;
        }
    }

    private void DropPowerUp(Vector2 at)
        => _powerUps.Add(new PowerUp { Position = at, Kind = (PowerUpKind)_rng.Next(6) });

    private void UpdatePowerUps(float dt)
    {
        foreach (PowerUp powerUp in _powerUps) powerUp.Update(dt);
        _powerUps.RemoveAll(static p => !p.Active);

        if (_player is not { Active: true }) return;

        foreach (PowerUp powerUp in _powerUps)
        {
            if (!powerUp.Active) continue;
            if (!powerUp.Bounds.Intersects(_player.Bounds)) continue;

            powerUp.Active = false;
            ApplyPowerUp(powerUp.Kind);
        }
    }

    private void ApplyPowerUp(PowerUpKind kind)
    {
        _sounds.Play("pickup");
        switch (kind)
        {
            case PowerUpKind.Tank:
            case PowerUpKind.Shovel:
                _lives++;
                break;
            case PowerUpKind.Helmet:
                _shieldTimer = 6f;
                break;
            case PowerUpKind.Star:
                _fireLevel = Math.Min(_fireLevel + 1, 3);
                _sounds.Play("powerup");
                break;
            case PowerUpKind.Clock:
                _freezeTimer = 6f;
                break;
            case PowerUpKind.Grenade:
                foreach (EnemyTank enemy in _enemies)
                {
                    if (!enemy.Active) continue;
                    enemy.Active = false;
                    _explosions.Add(new ExplodeEffect { Position = enemy.Position });
                    _killed++;
                }
                _sounds.Play("explosion");
                break;
        }
    }

    private void UpdateRespawn(float dt)
    {
        if (_player is not null || _respawnTimer <= 0f) return;

        _respawnTimer -= dt;
        if (_respawnTimer <= 0f) SpawnPlayer(keepStats: true);
    }

    private void UpdateSpawning(float dt)
    {
        if (_stageClearTimer > 0f) return;

        int onField = _enemies.Count(static e => e.Active);
        if (onField >= TankConfig.EnemyOnField || _enemiesRemaining <= 0) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = 2f;

        if (_enemySpawns.Count == 0) return;

        int slot = (_enemies.Count + TankConfig.EnemyTotal - _enemiesRemaining) % _enemySpawns.Count;
        Point spawn = _enemySpawns[slot];
        Vector2 at = TileCenter(spawn.X, spawn.Y);

        _enemies.Add(new EnemyTank { Position = at, Direction = Dir.Down, Active = true });
        _borns.Add(new BornEffect { Position = at });
        _enemiesRemaining--;
    }

    private void UpdateEffects(float dt)
    {
        foreach (ExplodeEffect effect in _explosions) effect.Update(dt);
        foreach (BornEffect effect in _borns) effect.Update(dt);

        _explosions.RemoveAll(static e => !e.Active);
        _borns.RemoveAll(static b => !b.Active);
    }

    private void CheckVictory()
    {
        if (_stageClearTimer > 0f || _gameOver) return;
        if (_enemiesRemaining > 0 || _enemies.Any(static e => e.Active)) return;

        _stageClearTimer = StageClearDelay;
        _powerUps.Clear();
    }

    // =====================================================================
    // 渲染：地形 + 实体（对应 PixiJS 把 Tile/坦克/Shell 加入各 Root 容器）
    // =====================================================================

    public void DrawBattlefield(SpriteBatch batch, Vector2 origin, ResCenter res)
    {
        Draw(batch, origin, res, grassOnTop: false);
        foreach (PowerUp powerUp in _powerUps) powerUp.Draw(batch, origin, res);
        foreach (BornEffect effect in _borns) effect.Draw(batch, origin, res);

        _player?.Draw(batch, origin, res, AnimFrame);
        if (_shieldTimer > 0f && _player is { Active: true } && res.Shield is not null)
            DrawCentered(batch, res.Shield, origin + _player.Position);

        foreach (EnemyTank enemy in _enemies) enemy.Draw(batch, origin, res, AnimFrame);
        foreach (Shell shell in _shells) shell.Draw(batch, origin, res);
        foreach (ExplodeEffect effect in _explosions) effect.Draw(batch, origin, res);

        // 草丛盖在坦克之上
        Draw(batch, origin, res, grassOnTop: true);
    }

    private void DrawCentered(SpriteBatch batch, Texture2D tex, Vector2 center)
        => batch.Draw(tex, new Vector2(center.X - tex.Width / 2f, center.Y - tex.Height / 2f), Color.White);
}
