using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using KFramework.Example2.Screens;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战主场景（MainScene）：对应 PixiJS 的 GameScene / MainScreen。
/// 只负责场景装配、屏幕（StartScreen / FailScreen）切换与输入；
/// 关卡与玩法逻辑全部托管给 Game/TankLevel（对应 PixiJS 的 Game/TankLevel.ts）。
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

    // ===== 依赖 =====
    private readonly ContentManager _content;
    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _batch;
    private readonly SpriteFont _font;

    // ===== 资源 / HUD =====
    private ResCenter _res = null!;
    private SoundCenter _sounds = null!;
    private Hud _hud = null!;

    // ===== 玩法（全部交给 TankLevel，与 PixiJS 一致）=====
    private readonly TankLevel _level = new();

    // ===== UI 节点树（移植自 PixiJS 的 screens/main/StartScreen / FailScreen）=====
    private readonly KCanvas _uiRoot = new();
    private StartScreen _startScreen = null!;
    private FailScreen _failScreen = null!;

    private readonly Color _bg = new(18, 20, 28);
    private readonly Color _dim = new(0, 0, 0, 170);

    private bool _started;
    private bool _audioUnlocked;
    private bool _inspectSprites;

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

        _level.Init(_content, _sounds);

        _uiRoot.Parent = SceneNodeRoot;

        // 屏幕在构造函数里把自己 addChild 到 _uiRoot（对应 PixiJS 的 root.addChild(this)）
        _startScreen = new StartScreen(_uiRoot);
        _startScreen.Build(_font, StartGame);
        _failScreen = new FailScreen(_uiRoot);
        _failScreen.Build(_font, StartGame);
        ShowGroup(GameState.Title);

        _started = true;
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
        var kb = Input.GetKeyboardState();

        // Tab：精灵表检视
        if (kb.IsKeyPressed(Keys.Tab)) { _inspectSprites = !_inspectSprites; return; }
        if (_inspectSprites) return;

        // R：重开当前关；N：跳到下一关（调试用）
        if (kb.IsKeyPressed(Keys.R)) { _level.LoadLevel(_level.LevelIndex); return; }
        if (kb.IsKeyPressed(Keys.N) && _level.LevelIndex + 1 < TankLevel.LevelCount)
        {
            _level.LoadLevel(_level.LevelIndex + 1);
            return;
        }

        _level.Update(KTime.deltaTime);

        if (_level.GameOver) EnterOver(false);
        else if (_level.AllCleared) EnterOver(true);
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
        {
            if (_inspectSprites) DrawSpriteSheet(batch);
            else
            {
                _level.DrawBattlefield(batch, Origin, _res);
                DrawHud(batch);
            }
        }

        if (_state == GameState.Over)
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height), _dim);

        // 先结束战场批处理，再交给 KCanvas（节点树 UI）自行 Begin/End
        batch.End();
        base.Draw();
    }

    /// <summary>地图左上角偏移，让 21x20 的战场居中。</summary>
    private Vector2 Origin => new(
        (_gd.Viewport.Width - TankConfig.MapWidth * TankConfig.TileSize) * 0.5f,
        (_gd.Viewport.Height - TankConfig.MapHeight * TankConfig.TileSize) * 0.5f);

    private void DrawHud(SpriteBatch batch)
    {
        string? status = _state == GameState.Over
            ? (_level.AllCleared ? "ALL CLEAR" : "GAME OVER")
            : null;

        _hud.Draw(batch, _level.LevelIndex, _level.Lives, _level.EnemiesRemaining,
                  _level.ActiveEnemies, status,
                  Origin.X + TankConfig.MapWidth * TankConfig.TileSize, Origin.Y);
    }

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

        _state = GameState.Playing;
        ShowGroup(GameState.Playing);
        _level.LoadLevel(0);
    }

    private void EnterOver(bool victory)
    {
        _state = GameState.Over;
        _failScreen.SetStatus(victory ? "ALL CLEAR" : "GAME OVER");
        ShowGroup(GameState.Over);
    }

    /// <summary>只保留当前状态对应的 UI 窗口挂载到节点树（其余 detach，避免被绘制）。</summary>
    private void ShowGroup(GameState state)
    {
        if (state == GameState.Title)
        {
            _startScreen.Show();
            _failScreen.Hide();
        }
        else if (state == GameState.Over)
        {
            _failScreen.Show();
            _startScreen.Hide();
        }
        else
        {
            _startScreen.Hide();
            _failScreen.Hide();
        }
    }

}
