using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战主类：只负责资源装载、更新调度与绘制顺序。
/// 具体逻辑分散在 TankLevel / TankBase / PlayerTank / EnemyTank / Shell / PowerUp / *Effect / Hud 中。
/// </summary>
public sealed class TankGame : Game
{
    private const int LevelCount = 5;
    private const int DropsEvery = 3;          // 每击毁 N 台敌人掉一个道具
    private const float StageClearDelay = 2f;

    private SpriteBatch _batch = null!;
    private ResCenter _res = null!;
    private Hud _hud = null!;
    private readonly TankLevel _level = new();
    private readonly Random _rng = new(20240915);

    private PlayerTank? _player;
    private readonly List<EnemyTank> _enemies = new();
    private readonly List<Shell> _shells = new();
    private readonly List<ExplodeEffect> _explosions = new();
    private readonly List<BornEffect> _borns = new();
    private readonly List<PowerUp> _powerUps = new();

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
    private bool _inspectSprites;
    private bool _gameOver;
    private bool _allCleared;
    private bool _ready;

    private int AnimFrame => (_animTick / 8) % 2;
    private bool StageCleared => _stageClearTimer > 0f;

    /// <summary>地图左上角偏移，让 21x20 的战场居中。</summary>
    private Vector2 Origin => new(
        (GraphicsDevice.Viewport.Width - TankConfig.MapWidth * TankConfig.TileSize) * 0.5f,
        (GraphicsDevice.Viewport.Height - TankConfig.MapHeight * TankConfig.TileSize) * 0.5f);

    protected override async Task LoadContentAsync()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        await Content.LoadAsync().ConfigureAwait(false);

        _res = ResCenter.Load(Content);
        _hud = new Hud(GraphicsDevice);

        LoadLevel(0);
        _ready = true;
    }

    private void LoadLevel(int index)
    {
        _levelIndex = index;
        _level.Load(Content.LoadText($"levels/{index:00}"));

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

        _player = new PlayerTank { Position = _level.PlayerSpawn, Direction = Dir.Up, Active = true };
        _borns.Add(new BornEffect { Position = _level.PlayerSpawn });
        _shieldTimer = 2f;   // 出生保护
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_ready) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _animTick++;

        // Tab：精灵表检视，用来确认方向排布
        if (Input.GetKeyboardState().IsKeyPressed(Keys.Tab)) _inspectSprites = !_inspectSprites;
        if (_inspectSprites) return;

        // R：重开当前关；N：跳到下一关（调试用）
        if (Input.GetKeyboardState().IsKeyPressed(Keys.R)) { LoadLevel(_levelIndex); return; }
        if (Input.GetKeyboardState().IsKeyPressed(Keys.N) && _levelIndex + 1 < LevelCount)
        {
            LoadLevel(_levelIndex + 1);
            return;
        }

        if (_gameOver || _allCleared) return;

        _shieldTimer -= dt;
        _freezeTimer -= dt;

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
        if (!StageCleared) return;

        _stageClearTimer -= dt;
        if (_stageClearTimer > 0f) return;

        if (_levelIndex + 1 >= LevelCount)
        {
            _allCleared = true;
            Audio.Play(Audio.Sfx.GameOver);
            return;
        }
        LoadLevel(_levelIndex + 1);
    }

    private void UpdateTanks(float dt)
    {
        if (_player is { Active: true })
        {
            Shell? shell = _player.Update(dt, _level);
            if (shell is not null)
            {
                shell.SpeedBonus = _fireLevel;
                _shells.Add(shell);
                Audio.Play(Audio.Sfx.Shoot);
            }
        }

        bool frozen = _freezeTimer > 0f;
        foreach (EnemyTank enemy in _enemies)
        {
            if (!enemy.Active) continue;

            // 时钟道具：敌人停止行动，但仍在场上
            if (frozen) continue;

            Shell? shell = enemy.Update(dt, _level);
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
            if (shell.SpeedBonus >= 2 && _level.Get(tile.X, tile.Y) == Tile.Barriar)
                _level.Clear(tile.X, tile.Y);

            if (_level.BulletHit(tile.X, tile.Y, out bool heartHit))
            {
                shell.Active = false;
                Audio.Play(Audio.Sfx.Hit);
                if (heartHit)
                {
                    _gameOver = true;
                    _explosions.Add(new ExplodeEffect { Position = _level.TileCenter(tile.X, tile.Y) });
                    Audio.Play(Audio.Sfx.GameOver);
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
            Audio.Play(Audio.Sfx.Explosion);

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
            Audio.Play(Audio.Sfx.Hit);
            return;
        }

        _explosions.Add(new ExplodeEffect { Position = _player.Position });
        Audio.Play(Audio.Sfx.Explosion);
        _player = null;

        _lives--;
        if (_lives <= 0)
        {
            _gameOver = true;
            Audio.Play(Audio.Sfx.GameOver);
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
        Audio.Play(Audio.Sfx.Pickup);
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
                Audio.Play(Audio.Sfx.Explosion);
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
        if (StageCleared) return;

        int onField = _enemies.Count(static e => e.Active);
        if (onField >= TankConfig.EnemyOnField || _enemiesRemaining <= 0) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;
        _spawnTimer = 2f;

        if (_level.EnemySpawns.Count == 0) return;

        int slot = (_enemies.Count + TankConfig.EnemyTotal - _enemiesRemaining) % _level.EnemySpawns.Count;
        Point spawn = _level.EnemySpawns[slot];
        Vector2 at = _level.TileCenter(spawn.X, spawn.Y);

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
        if (StageCleared || _gameOver) return;
        if (_enemiesRemaining > 0 || _enemies.Any(static e => e.Active)) return;

        _stageClearTimer = StageClearDelay;
        _powerUps.Clear();
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!_ready) return;

        _batch.Begin(sortMode: SpriteSortMode.Deferred, samplerState: SamplerState.Point);

        if (_inspectSprites)
        {
            DrawSpriteSheet();
            _batch.End();
            return;
        }

        Vector2 origin = Origin;

        _level.Draw(_batch, origin, _res, grassOnTop: false);
        foreach (PowerUp powerUp in _powerUps) powerUp.Draw(_batch, origin, _res);
        foreach (BornEffect effect in _borns) effect.Draw(_batch, origin, _res);

        _player?.Draw(_batch, origin, _res, AnimFrame);
        if (_shieldTimer > 0f && _player is { Active: true } && _res.Shield is not null)
            DrawCentered(_res.Shield, origin + _player.Position);

        foreach (EnemyTank enemy in _enemies) enemy.Draw(_batch, origin, _res, AnimFrame);
        foreach (Shell shell in _shells) shell.Draw(_batch, origin, _res);
        foreach (ExplodeEffect effect in _explosions) effect.Draw(_batch, origin, _res);

        // 草丛要盖在坦克之上
        _level.Draw(_batch, origin, _res, grassOnTop: true);

        DrawHud(origin);

        _batch.End();
    }

    private void DrawHud(Vector2 origin)
    {
        string? status = null;
        if (_gameOver) status = "GAME OVER";
        else if (_allCleared) status = "ALL CLEAR";
        else if (StageCleared) status = "STAGE CLEAR";

        _hud.Draw(_batch, _levelIndex, _lives, _enemiesRemaining,
                  _enemies.Count(static e => e.Active), status,
                  origin.X + TankConfig.MapWidth * TankConfig.TileSize, origin.Y);
    }

    private void DrawCentered(Texture2D tex, Vector2 center)
        => _batch.Draw(tex, new Vector2(center.X - tex.Width / 2f, center.Y - tex.Height / 2f), Color.White);

    /// <summary>把 Player1 全 32 帧按 8 列平铺，用于肉眼确认方向的排布顺序。</summary>
    private void DrawSpriteSheet()
    {
        const int columns = 8;
        const int cell = 48;

        for (int i = 0; i < _res.Player.Length; i++)
        {
            Texture2D? tex = _res.Player[i];
            if (tex is null) continue;
            _batch.Draw(tex, new Vector2(24f + (i % columns) * cell, 24f + (i / columns) * cell), Color.White);
        }
    }
}
