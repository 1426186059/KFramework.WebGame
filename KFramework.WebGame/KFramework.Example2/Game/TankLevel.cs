using System;
using System.Collections.Generic;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>
/// 关卡 + 玩法管理器：对应 PixiJS 版的 Game/TankLevel.ts。
/// <para>
/// 场景图完全按原版搭建（addChild 在本框架里即 Parent）：
///   SceneRoot → BGRoot / Map1Root / TankRoot / Map2Root / EffectRoot
/// 每个玩法实体自带一个 TGameSprite 显示节点（等价于原版的 mSprite），挂到对应 Root；
/// fTileScaleCoef / resize() / GetTilePos() 也与原版一一对应。
/// 坐标系统与 PixiJS 一致：战场以原点居中（见 TankConfig.MapHalfW/MapHalfH）。
/// </para>
/// </summary>
internal sealed class TankLevel
{
    // ===== 场景图（对应 PixiJS 的 SceneRoot / BGRoot / Map1Root / TankRoot / Map2Root / EffectRoot；addChild = Parent）=====
    public readonly TankSceneRoot SceneRoot = new();
    public readonly KTransform BGRoot = new();
    public readonly KTransform Map1Root = new();
    public readonly KTransform TankRoot = new();
    public readonly KTransform Map2Root = new();
    public readonly KTransform EffectRoot = new();

    // ===== 地形数据（对应 PixiJS 的 tiles 网格）+ 对应显示节点 =====
    private readonly Tile[,] _tiles = new Tile[TankConfig.MapHeight, TankConfig.MapWidth];
    private readonly TGameSprite?[,] _tileViews = new TGameSprite?[TankConfig.MapHeight, TankConfig.MapWidth];
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

    // 护盾显示节点
    private readonly TGameSprite _shieldView = new();

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
    private AssetBundle _content = null!;
    private SoundCenter _sounds = null!;
    private ResCenter _res = null!;

    // ===== 自适应：对应 PixiJS 的 fTileScaleCoef / SceneRoot.scale / position（TankLevel.resize）=====
    /// <summary>战场整体缩放系数（原版 fTileScaleCoef）。</summary>
    public float fTileScaleCoef = 1.0f;

    public void Init(AssetBundle content, SoundCenter sounds, ResCenter res)
    {
        _content = content;
        _sounds = sounds;
        _res = res;

        // 搭场景图（对应 PixiJS 的 Init 里 addChild 链）
        BGRoot.Parent = SceneRoot;
        Map1Root.Parent = SceneRoot;
        TankRoot.Parent = SceneRoot;
        Map2Root.Parent = SceneRoot;
        EffectRoot.Parent = SceneRoot;

        _shieldView.Pivot = new Vector2(0.5f);
        _shieldView.UseNativeSize = true;
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
                BuildTileView(x, row, c);

                if (c == 'P') PlayerSpawn = TileCenter(x, row);
                else if (c == 'E') _enemySpawns.Add(new Point(x, row));
            }
        }
    }

    /// <summary>对应 PixiJS 的 LoadCommonTile / LoadTile_Home：为格子建一个显示节点挂到 Map1Root / Map2Root。</summary>
    private void BuildTileView(int x, int y, char c)
    {
        Texture2D? tex = c switch
        {
            '#' => _res.Wall,
            '*' => _res.Barriar,
            '~' => _res.Water,
            '^' => _res.Grass,
            '@' => _res.Heart,
            _ => null,
        };
        if (tex is null) { _tileViews[y, x] = null; return; }

        var view = new TGameSprite();
        view.Parent = c == '^' ? Map2Root : Map1Root;   // 草在坦克之上
        view.Sprite = tex;
        view.UseNativeSize = true;
        view.LocalPosition = GetTilePos(x, y);
        _tileViews[y, x] = view;
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

    /// <summary>对应 PixiJS 的 GetTilePos：返回格子左上角在“居中坐标”下的位置。</summary>
    public Vector2 GetTilePos(int x, int y)
        => new((x - TankConfig.MapWidth / 2f) * TankConfig.TileSize,
               (y - TankConfig.MapHeight / 2f) * TankConfig.TileSize);

    /// <summary>格子中心（实体出生点用）。</summary>
    public Vector2 TileCenter(int x, int y)
        => GetTilePos(x, y) + new Vector2(TankConfig.TileSize / 2f);

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

        var v = _tileViews[ty, tx];
        if (v != null) { v.Parent = null; _tileViews[ty, tx] = null; }   // 脱离容器树即隐藏
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

        if (left < -TankConfig.MapHalfW || top < -TankConfig.MapHalfH ||
            right >= TankConfig.MapHalfW || bottom >= TankConfig.MapHalfH)
            return false;

        int tx0 = (int)((left + TankConfig.MapHalfW) / TankConfig.TileSize);
        int tx1 = (int)((right + TankConfig.MapHalfW) / TankConfig.TileSize);
        int ty0 = (int)((top + TankConfig.MapHalfH) / TankConfig.TileSize);
        int ty1 = (int)((bottom + TankConfig.MapHalfH) / TankConfig.TileSize);
        for (int ty = ty0; ty <= ty1; ty++)
            for (int tx = tx0; tx <= tx1; tx++)
                if (!IsPassable(Get(tx, ty))) return false;

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

    // =====================================================================
    // 关卡 / 玩法（对应 PixiJS 的 Init / LoadLevel / update）
    // =====================================================================

    /// <summary>开始一关（index=0 即新游戏）。</summary>
    public void LoadLevel(int index)
    {
        DetachAllViews();

        _gameOver = false;
        _allCleared = false;

        _levelIndex = index;
        Load(_content.LoadText($"levels/{index:00}"));

        _enemiesRemaining = TankConfig.EnemyTotal;
        _killed = 0;
        _spawnTimer = 1.5f;
        _stageClearTimer = -1f;
        _freezeTimer = 0f;

        SpawnPlayer(keepStats: index > 0);
    }

    private void DetachAllViews()
    {
        if (_player != null) { _player.View.Parent = null; _player = null; }
        foreach (var e in _enemies) e.View.Parent = null;
        foreach (var s in _shells) s.View.Parent = null;
        foreach (var x in _explosions) x.View.Parent = null;
        foreach (var b in _borns) b.View.Parent = null;
        foreach (var p in _powerUps) p.View.Parent = null;
        for (int y = 0; y < TankConfig.MapHeight; y++)
            for (int x = 0; x < TankConfig.MapWidth; x++)
            {
                var v = _tileViews[y, x];
                if (v != null) { v.Parent = null; _tileViews[y, x] = null; }
            }
        _shieldView.Parent = null;

        _enemies.Clear();
        _shells.Clear();
        _explosions.Clear();
        _borns.Clear();
        _powerUps.Clear();
    }

    private void SpawnPlayer(bool keepStats)
    {
        if (!keepStats)
        {
            _lives = TankConfig.PlayerLives;
            _fireLevel = 0;
        }

        _player = new PlayerTank { Position = PlayerSpawn, Direction = Dir.Up, Active = true };
        _player.View.Parent = TankRoot;

        var born = new BornEffect { Position = PlayerSpawn };
        born.View.Parent = EffectRoot;
        _borns.Add(born);
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

        SyncViews();   // 把数据同步到显示节点（对应 PixiJS 里显示对象跟随实体）
    }

    private void SyncViews()
    {
        _player?.SyncView(_res, AnimFrame);
        foreach (EnemyTank enemy in _enemies) if (enemy.Active) enemy.SyncView(_res, AnimFrame);
        foreach (Shell shell in _shells) if (shell.Active) shell.SyncView(_res);
        foreach (PowerUp powerUp in _powerUps)
            if (powerUp.Active) { if (powerUp.SyncView(_res)) powerUp.View.Parent = EffectRoot; else powerUp.View.Parent = null; }
        foreach (BornEffect effect in _borns) if (effect.Active) effect.SyncView(_res);
        foreach (ExplodeEffect effect in _explosions) if (effect.Active) effect.SyncView(_res);

        if (_shieldTimer > 0f && _player is { Active: true } && _res.Shield is not null)
        {
            _shieldView.Parent = TankRoot;
            _shieldView.LocalPosition = _player.Position;
            _shieldView.Sprite = _res.Shield;
        }
        else _shieldView.Parent = null;
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
                shell.View.Parent = EffectRoot;
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
            if (shell is not null) { shell.View.Parent = EffectRoot; _shells.Add(shell); }
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

        _shells.RemoveAll(s => { if (!s.Active) { s.View.Parent = null; return true; } return false; });
        _enemies.RemoveAll(e => { if (!e.Active) { e.View.Parent = null; return true; } return false; });
    }

    private void HitEnemies(Shell shell)
    {
        foreach (EnemyTank enemy in _enemies)
        {
            if (!enemy.Active) continue;
            if (!shell.Bounds.Intersects(enemy.Bounds)) continue;

            shell.Active = false;
            enemy.Active = false;
            enemy.View.Parent = null;   // 脱离容器树即隐藏
            _killed++;
            var ex = new ExplodeEffect { Position = enemy.Position };
            ex.View.Parent = EffectRoot;
            _explosions.Add(ex);
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
        _player.View.Parent = null;   // 脱离容器树即隐藏
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
    {
        var p = new PowerUp { Position = at, Kind = (PowerUpKind)_rng.Next(6) };
        p.View.Parent = EffectRoot;
        _powerUps.Add(p);
    }

    private void UpdatePowerUps(float dt)
    {
        foreach (PowerUp powerUp in _powerUps) powerUp.Update(dt);
        _powerUps.RemoveAll(p => { if (!p.Active) { p.View.Parent = null; return true; } return false; });

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
                    enemy.View.Parent = null;
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

        var enemy = new EnemyTank { Position = at, Direction = Dir.Down, Active = true };
        enemy.View.Parent = TankRoot;
        _enemies.Add(enemy);

        var born = new BornEffect { Position = at };
        born.View.Parent = EffectRoot;
        _borns.Add(born);
        _enemiesRemaining--;
    }

    private void UpdateEffects(float dt)
    {
        foreach (ExplodeEffect effect in _explosions) effect.Update(dt);
        foreach (BornEffect effect in _borns) effect.Update(dt);

        _explosions.RemoveAll(e => { if (!e.Active) { e.View.Parent = null; return true; } return false; });
        _borns.RemoveAll(b => { if (!b.Active) { b.View.Parent = null; return true; } return false; });
    }

    private void CheckVictory()
    {
        if (_stageClearTimer > 0f || _gameOver) return;
        if (_enemiesRemaining > 0 || _enemies.Any(static e => e.Active)) return;

        _stageClearTimer = StageClearDelay;
        _powerUps.Clear();
    }

    // =====================================================================
    // 自适应（对应 PixiJS 的 TankLevel.resize）：按视口高度把整张战场等比缩放并居中
    // =====================================================================

    /// <summary>
    /// 对应 PixiJS 的 resize()：
    /// <code>
    ///   fTileScaleCoef = (renderer.height - 100) / MapHeight / TileHeight;
    ///   SceneRoot.scale.set(fTileScaleCoef);
    ///   SceneRoot.position = (renderer.width / 2, renderer.height / 2);
    /// </code>
    /// 这里直接改 SceneRoot 的 LocalScale / LocalPosition（原版给整棵 SceneRoot 挂变换）。
    /// </summary>
    public void Resize(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0) return;

        fTileScaleCoef = (screenHeight - 100f) / TankConfig.MapHeight / TankConfig.TileSize;
        SceneRoot.LocalScale = new Vector2(fTileScaleCoef);
        SceneRoot.LocalPosition = new Vector2(screenWidth / 2f, screenHeight / 2f);
    }

    // =====================================================================
    // 供 HUD / 场景读取的只读状态
    // =====================================================================

    public int LevelIndex => _levelIndex;
    public int Lives => _lives;
    public int EnemiesRemaining => _enemiesRemaining;
    public int ActiveEnemies => _enemies.Count(static e => e.Active);
    public bool GameOver => _gameOver;
    public bool AllCleared => _allCleared;
    public int AnimFrame => (_animTick / 8) % 2;
}

/// <summary>
/// 战场根节点：等价于 PixiJS 的 SceneRoot（一个 Container）。
/// 实现 KDrawable，使其能被场景绘制管道调用；Draw() 内用 KCanvas 同款 DrawWidget
/// 遍历并绘制所有 KImage 叶子（即各玩法实体的 TGameSprite 显示节点）。
/// 注意：不能用 KCanvas 当根，因为 KCanvas 会在窗口 resize 时把 LocalScale 自动覆盖成
/// uiScaleMode 的缩放系数，会和我们想要的 fTileScaleCoef 冲突；这里改为手动控制缩放/居中。
/// </summary>
internal sealed class TankSceneRoot : KTransform, KDrawable
{
    public new void Draw()
    {
        var batch = KSceneMgr.SpriteBatch;
        batch.Begin(transformMatrix: Matrix4x4.Identity,
                    sortMode: SpriteSortMode.Deferred,
                    samplerState: SamplerState.PointClamp,
                    blendState: BlendState.NonPremultiplied);
        DrawWidget(this);
        batch.End();
    }

    private static void DrawWidget(KTransform t)
    {
        foreach (var v in t.ChildList)
        {
            if (v is KWidget w) w.Draw();
            DrawWidget(v);
        }
    }
}
