using System;
using System.Collections.Generic;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战主场景：基于 KFramework.MonoGameExtend 的节点树（<see cref="KSceneBase"/>）。
/// <list type="bullet">
///   <item>游戏逻辑（玩家/敌人坦克、炮弹、关卡、碰撞、特效）沿用 SpriteBatch 渲染，
///         与 PixiJS 版的 TankLevel / Tank_My / Tank_Enemy / Shell 一一对应；</item>
///   <item>标题界面与结束界面用 MonoGameExtend 的 <see cref="KLabel"/> / <see cref="KButton"/> 节点树搭建，
///         移植自 PixiJS 版的 StartScreen / FailScreen；</item>
///   <item>真实音效通过 <see cref="SoundCenter"/>（SoundEffect 加载 wav）播放，
///         并在首次用户输入时调用 <see cref="AudioMaster.Unlock"/> 解锁浏览器音频上下文。</item>
/// </list>
/// </summary>
public sealed class TankScene : KSceneBase
{
    private enum GameState
    {
        Title,
        Playing,
        Over,
    }

    private GameState _state = GameState.Title;

    // ===== 资源 / 渲染 =====
    private readonly ContentManager _content;
    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _batch;
    private readonly SpriteFont _font;
    private ResCenter _res = null!;
    private SoundCenter _sounds = null!;
    private Hud _hud = null!;

    // ===== 关卡 / 实体 =====
    private readonly TankLevel _level = new();
    private readonly Random _rng = new(20240915);
    private PlayerTank? _player;
    private readonly List<EnemyTank> _enemies = new();
    private readonly List<Shell> _shells = new();
    private readonly List<ExplodeEffect> _explosions = new();
    private readonly List<BornEffect> _borns = new();
    private readonly List<PowerUp> _powerUps = new();

    // ===== 玩法常量 / 状态 =====
    private const int LevelCount = 5;
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
    private bool _inspectSprites;
    private bool _gameOver;
    private bool _allCleared;
    private bool _started;
    private bool _audioUnlocked;

    private int AnimFrame => (_animTick / 8) % 2;
    private bool StageCleared => _stageClearTimer > 0f;

    /// <summary>地图左上角偏移，让 21x20 的战场居中。</summary>
    private Vector2 Origin => new(
        (_gd.Viewport.Width - TankConfig.MapWidth * TankConfig.TileSize) * 0.5f,
        (_gd.Viewport.Height - TankConfig.MapHeight * TankConfig.TileSize) * 0.5f);

    // ===== UI 节点树（移植自 PixiJS 的 StartScreen / FailScreen）=====
    private readonly KCanvas _uiRoot = new();
    private KTransform? _titleGroup;
    private KTransform? _overGroup;
    private KLabel? _titleLabel;
    private KLabel? _titleSub;
    private KButton? _startButton;
    private KLabel? _overLabel;
    private KLabel? _overSub;
    private KButton? _restartButton;

    private readonly Color _bg = new(18, 20, 28);
    private readonly Color _dim = new(0, 0, 0, 170);

    public TankScene(ContentManager content, GraphicsDevice gd, SpriteBatch batch, SpriteFont font)
    {
        _content = content;
        _gd = gd;
        _batch = batch;
        _font = font;
    }

    // =====================================================================
    // 加载
    // =====================================================================

    public override void LoadContent()
    {
        _res = ResCenter.Load(_content);
        _sounds = SoundCenter.Load(_content);
        _hud = new Hud(_gd);

        _uiRoot.Parent = SceneNodeRoot;
        BuildTitleUi();
        BuildOverUi();
        LayoutUi();
        ShowGroup(GameState.Title);

        KSceneMgr.ScreenSizeChanged += (_, _) => LayoutUi();
        _started = true;
    }

    private void BuildTitleUi()
    {
        _titleGroup = new KTransform();
        _titleGroup.Parent = _uiRoot;

        _titleLabel = new KLabel("BATTLE CITY", new Color(140, 210, 255), _font)
        {
            Pivot = new Vector2(0.5f),
            Parent = _titleGroup,
        };

        _titleSub = new KLabel("PRESS SPACE / ENTER OR TAP START", new Color(150, 170, 200), _font)
        {
            Pivot = new Vector2(0.5f),
            Parent = _titleGroup,
        };

        _startButton = new KButton
        {
            Size = new Vector2(320, 84),
            Color = new Color(40, 70, 120),
            Parent = _titleGroup,
        };
        _startButton.Label.Text = "START";
        _startButton.Label.Font = _font;
        _startButton.PointerClickEvent += (_, _) => StartGame();
    }

    private void BuildOverUi()
    {
        _overGroup = new KTransform();
        _overGroup.Parent = _uiRoot;

        _overLabel = new KLabel("GAME OVER", new Color(255, 120, 120), _font)
        {
            Pivot = new Vector2(0.5f),
            Parent = _overGroup,
        };

        _overSub = new KLabel("PRESS R / ENTER OR TAP RETRY", new Color(150, 170, 200), _font)
        {
            Pivot = new Vector2(0.5f),
            Parent = _overGroup,
        };

        _restartButton = new KButton
        {
            Size = new Vector2(320, 84),
            Color = new Color(40, 70, 120),
            Parent = _overGroup,
        };
        _restartButton.Label.Text = "RETRY";
        _restartButton.Label.Font = _font;
        _restartButton.PointerClickEvent += (_, _) => StartGame();
    }

    /// <summary>只保留当前状态对应的 UI 分组挂载到节点树（其余 detach，避免被绘制）。</summary>
    private void ShowGroup(GameState state)
    {
        _titleGroup!.Parent = state == GameState.Title ? _uiRoot : null;
        _overGroup!.Parent = state == GameState.Over ? _uiRoot : null;
    }

    /// <summary>在 1280x720 设计空间内把 UI 控件居中摆放（KCanvas 负责缩放适配屏幕）。</summary>
    private void LayoutUi()
    {
        float cx = GameConst.DesignWidth * 0.5f;
        float cy = GameConst.DesignHeight * 0.5f;

        if (_titleLabel is not null) _titleLabel.LocalPosition = new Vector2(cx, cy - 130f);
        if (_titleSub is not null) _titleSub.LocalPosition = new Vector2(cx, cy - 50f);
        if (_startButton is not null) _startButton.LocalPosition = new Vector2(cx - _startButton.Size.X * 0.5f, cy + 40f);

        if (_overLabel is not null) _overLabel.LocalPosition = new Vector2(cx, cy - 90f);
        if (_overSub is not null) _overSub.LocalPosition = new Vector2(cx, cy - 10f);
        if (_restartButton is not null) _restartButton.LocalPosition = new Vector2(cx - _restartButton.Size.X * 0.5f, cy + 80f);
    }

    // =====================================================================
    // 更新
    // =====================================================================

    public override void Update()
    {
        if (!_started) return;

        EnsureAudioUnlocked();

        switch (_state)
        {
            case GameState.Title:
                if (KInputMgr.GetKeyDown(Keys.Enter) || KInputMgr.GetKeyDown(Keys.Space)) StartGame();
                break;

            case GameState.Over:
                if (KInputMgr.GetKeyDown(Keys.R) || KInputMgr.GetKeyDown(Keys.Enter) || KInputMgr.GetKeyDown(Keys.Space))
                    StartGame();
                break;

            case GameState.Playing:
                UpdatePlaying();
                break;
        }

        // 更新 UI 控件（按钮颜色过渡等）
        base.Update();
    }

    private void UpdatePlaying()
    {
        float dt = KTime.deltaTime;
        _animTick++;

        // Tab：精灵表检视
        if (Input.GetKeyboardState().IsKeyPressed(Keys.Tab)) { _inspectSprites = !_inspectSprites; return; }
        if (_inspectSprites) return;

        // R：重开当前关；N：跳到下一关（调试用）
        if (Input.GetKeyboardState().IsKeyPressed(Keys.R)) { LoadLevel(_levelIndex); return; }
        if (Input.GetKeyboardState().IsKeyPressed(Keys.N) && _levelIndex + 1 < LevelCount)
        {
            LoadLevel(_levelIndex + 1);
            return;
        }

        UpdateStageClear(dt);
        UpdateRespawn(dt);
        UpdateTanks(dt);
        UpdateShells(dt);
        UpdatePowerUps(dt);
        UpdateEffects(dt);
        UpdateSpawning(dt);
        CheckVictory();

        if (_gameOver) EnterOver(false);
        else if (_allCleared) EnterOver(true);
    }

    private void EnsureAudioUnlocked()
    {
        if (_audioUnlocked) return;
        if (KInputMgr.AnyKeyDown || KInputMgr.GetMouseButtonDown(0))
        {
            AudioMaster.Unlock();
            _audioUnlocked = true;
        }
    }

    // =====================================================================
    // 绘制
    // =====================================================================

    public override void Draw()
    {
        if (!_started) return;

        var batch = _batch;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // 背景（覆盖上一帧）
        batch.Draw(KDefaultRes.DefaultTexture2D,
                   new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height), _bg);

        if (_state == GameState.Playing || _state == GameState.Over)
            DrawBattlefield(batch);

        if (_state == GameState.Over)
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height), _dim);

        // 先结束战场批处理，再交给 KCanvas（节点树 UI）自行 Begin/End
        batch.End();
        base.Draw();
    }

    private void DrawBattlefield(SpriteBatch batch)
    {
        if (_inspectSprites) { DrawSpriteSheet(batch); return; }

        Vector2 origin = Origin;

        _level.Draw(batch, origin, _res, grassOnTop: false);
        foreach (PowerUp powerUp in _powerUps) powerUp.Draw(batch, origin, _res);
        foreach (BornEffect effect in _borns) effect.Draw(batch, origin, _res);

        _player?.Draw(batch, origin, _res, AnimFrame);
        if (_shieldTimer > 0f && _player is { Active: true } && _res.Shield is not null)
            DrawCentered(batch, _res.Shield, origin + _player.Position);

        foreach (EnemyTank enemy in _enemies) enemy.Draw(batch, origin, _res, AnimFrame);
        foreach (Shell shell in _shells) shell.Draw(batch, origin, _res);
        foreach (ExplodeEffect effect in _explosions) effect.Draw(batch, origin, _res);

        // 草丛盖在坦克之上
        _level.Draw(batch, origin, _res, grassOnTop: true);

        DrawHud(batch, origin);
    }

    private void DrawHud(SpriteBatch batch, Vector2 origin)
    {
        string? status = _state == GameState.Over
            ? (_allCleared ? "ALL CLEAR" : "GAME OVER")
            : null;

        _hud.Draw(batch, _levelIndex, _lives, _enemiesRemaining,
                  _enemies.Count(static e => e.Active), status,
                  origin.X + TankConfig.MapWidth * TankConfig.TileSize, origin.Y);
    }

    private void DrawCentered(SpriteBatch batch, Texture2D tex, Vector2 center)
        => batch.Draw(tex, new Vector2(center.X - tex.Width / 2f, center.Y - tex.Height / 2f), Color.White);

    /// <summary>把 Player1 全 32 帧平铺，用于肉眼确认方向的排布顺序。</summary>
    private void DrawSpriteSheet(SpriteBatch batch)
    {
        const int columns = 8;
        const int cell = 48;

        for (int i = 0; i < _res.Player.Length; i++)
        {
            Texture2D? tex = _res.Player[i];
            if (tex is null) continue;
            batch.Draw(tex, new Vector2(24f + (i % columns) * cell, 24f + (i / columns) * cell), Color.White);
        }
    }

    // =====================================================================
    // 状态切换
    // =====================================================================

    private void StartGame()
    {
        AudioMaster.Unlock();
        _audioUnlocked = true;

        _gameOver = false;
        _allCleared = false;
        _state = GameState.Playing;
        ShowGroup(GameState.Playing);
        LoadLevel(0);
    }

    private void EnterOver(bool victory)
    {
        _state = GameState.Over;
        if (_overLabel is not null) _overLabel.Text = victory ? "ALL CLEAR" : "GAME OVER";
        ShowGroup(GameState.Over);
    }

    // =====================================================================
    // 关卡 / 玩法（移植自原 TankGame，沿用 SpriteBatch 实体）
    // =====================================================================

    private void LoadLevel(int index)
    {
        _levelIndex = index;
        _level.Load(_content.LoadText($"levels/{index:00}"));

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

    private void UpdateStageClear(float dt)
    {
        if (!StageCleared) return;

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
            Shell? shell = _player.Update(dt, _level);
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
                _sounds.Play("hit");
                if (heartHit)
                {
                    _gameOver = true;
                    _explosions.Add(new ExplodeEffect { Position = _level.TileCenter(tile.X, tile.Y) });
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
}
