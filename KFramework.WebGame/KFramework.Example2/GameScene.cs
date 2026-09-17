using KFramework.Example2.Screens;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

public sealed class GameScene : KSceneBase
{
    // 依赖（来自框架全局，对应 PixiJS 的 engine() 单例；TankGame.LoadContentAsync 里先 Init 再 new GameScene，故构造时均已就绪）
    private readonly ContentManager _content = KSceneMgr.Game.Content;
    private readonly GraphicsDevice _gd = KSceneMgr.Game.GraphicsDevice;
    private readonly SpriteBatch _batch = KSceneMgr.SpriteBatch;
    private readonly SpriteFont _font = KDefaultRes.DefaultSpriteFont;

    // 保留给外部（对应 PixiJS 的 World_Layer / UIRoot_Layer 层级节点）
    public readonly KTransform World_Layer = new KTransform(Name: "World_Layer");
    public readonly KTransform UIRoot_Layer = new KTransform(Name: "UIRoot_Layer");

    // 资源
    private ResCenter _res = null!;
    private SoundCenter _sounds = null!;
    private Hud _hud = null!;

    // 玩法（对应 PixiJS 的 TankLevel）
    private readonly TankLevel _level = new();

    // UI 节点树（对应 PixiJS 的 screens/main/StartScreen、FailScreen）
    private readonly KCanvas _uiRoot = new();
    private StartScreen _startScreen = null!;
    private FailScreen _failScreen = null!;

    // 状态
    private GameState _state = GameState.Title;
    private bool _started;
    private bool _audioUnlocked;
    private bool _inspectSprites;

    private readonly Color _bg = new(18, 20, 28);
    private readonly Color _dim = new(0, 0, 0, 170);

    private static GameScene m_Instance;
    public static GameScene GetInstance()
    {
        if (GameScene.m_Instance == null)
        {
            GameScene.m_Instance = new GameScene();
        }
        return GameScene.m_Instance;
    }

    public GameScene()
    {
        this.World_Layer.Parent = this.SceneNodeRoot;
        this.UIRoot_Layer.Parent = this.SceneNodeRoot;
        KUpdateMgr.Instance.AddListener(this.update, this);   // 自驱动，与 PixiJS 的 KUpdateMgr.AddListener 一致
        KSceneMgr.ScreenSizeChanged += OnScreenSizeChanged;
    }

    // ===================== 加载 =====================

    public override void LoadContent()
    {
        AssetBundle Bundle = _content.GetBundle("main", false);

        _res = ResCenter.Load(Bundle, _gd);
        _sounds = SoundCenter.Load(Bundle);
        _hud = new Hud(_gd);

        _level.Init(Bundle, _sounds, _res);
        _level.Resize(_gd.Viewport.Width, _gd.Viewport.Height);   // 对应 PixiJS 初始化时算一次 fTileScaleCoef

        _uiRoot.Parent = SceneNodeRoot;
        // 屏幕在构造时自行拼装内容并挂到 _uiRoot（对应 PixiJS 的 root.addChild(this) + 构造内建内容）
        _startScreen = new StartScreen(_uiRoot, _font, StartGame);
        _failScreen = new FailScreen(_uiRoot, _font, StartGame);
        ShowGroup(GameState.Title);

        _started = true;
    }

    // ===================== 更新 =====================

    public override void Update()
    {
        if (!_started) return;

        EnsureAudioUnlocked();

        // 屏幕（Title / Over）的输入已各自在 Screen.Update() 里自管，这里只负责 Playing 玩法。
        if (_state == GameState.Playing) update();

        base.Update();
    }

    // 与 PixiJS GameScene.update() 一致：每帧轮询输入并推进关卡（仅 Playing 时）。
    public void update()
    {
        var kb = Input.GetKeyboardState();

        // Tab：精灵表检视；检视模式下不再推进关卡
        if (kb.IsKeyPressed(Keys.Tab)) { _inspectSprites = !_inspectSprites; return; }
        if (_inspectSprites) return;

        // R：重开本关；N：跳下一关（调试用）
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

    // ===================== 绘制 =====================

    public override void Draw()
    {
        if (!_started) return;

        DrawBackground();

        if (_state == GameState.Playing || _state == GameState.Over)
        {
            DrawWorld();
            DrawHudLayer();
        }

        if (_state == GameState.Over) DrawDimOverlay();

        base.Draw();
    }

    // 立即模式批处理统一用 PointClamp（像素风不糊）。
    private void BeginBatch()
        => _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

    private void DrawBackground()
    {
        BeginBatch();
        _batch.Draw(KDefaultRes.DefaultTexture2D,
                    new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height), _bg);
        _batch.End();
    }

    // 战场：交给 TankLevel 的 SceneRoot（保留模式容器树）自行绘制，
    // 缩放 / 居中已在 resize() 里通过 LocalScale / LocalPosition 设好（对应 PixiJS 的 SceneRoot.scale / position）。
    private void DrawWorld()
    {
        if (_inspectSprites) DrawSpriteSheet(_batch);
        else _level.SceneRoot.Draw();
    }

    // HUD 在屏幕空间绘制（对应 PixiJS 的 Hud/MainScreen，独立于战场缩放）。
    private void DrawHudLayer()
    {
        BeginBatch();
        DrawHud(_batch);
        _batch.End();
    }

    private void DrawDimOverlay()
    {
        BeginBatch();
        _batch.Draw(KDefaultRes.DefaultTexture2D,
                    new Rectangle(0, 0, _gd.Viewport.Width, _gd.Viewport.Height), _dim);
        _batch.End();
    }

    private void OnScreenSizeChanged(object? sender, EventArgs e)
        => _level.Resize(_gd.Viewport.Width, _gd.Viewport.Height);

    private void DrawHud(SpriteBatch batch)
    {
        string? status = _state == GameState.Over
            ? (_level.AllCleared ? "ALL CLEAR" : "GAME OVER")
            : null;

        // 战场居中显示：半宽 = MapWidth*TileSize*coef/2，据此算 HUD 锚点。
        float coef = _level.fTileScaleCoef;
        float fieldRight = _gd.Viewport.Width / 2f + TankConfig.MapWidth * TankConfig.TileSize * coef / 2f;
        float fieldTop = _gd.Viewport.Height / 2f - TankConfig.MapHeight * TankConfig.TileSize * coef / 2f;

        _hud.Draw(batch, _level.LevelIndex, _level.Lives, _level.EnemiesRemaining,
                  _level.ActiveEnemies, status, fieldRight, fieldTop);
    }

    // 把 Player1 全 32 帧平铺，用于肉眼确认朝向排布顺序。
    private void DrawSpriteSheet(SpriteBatch batch)
    {
        const int columns = 8;
        const int cell = 48;

        for (int i = 0; i < _res.Player.Length; i++)
        {
            KSprite sprite = _res.Player[i];
            if (sprite.Texture is null) continue;
            batch.Draw(sprite.Texture, new Vector2(24f + (i % columns) * cell, 24f + (i / columns) * cell), sprite.Rectangle, Color.White);
        }
    }

    // ===================== 状态切换 =====================

    private void StartGame()
    {
        AudioMaster.Unlock();
        _audioUnlocked = true;

        _state = GameState.Playing;   // 进入玩法状态（原来漏了，导致 Playing 分支永不执行）
        ShowGroup(GameState.Playing); // 隐藏所有屏幕
        _level.LoadLevel(0);
    }

    private void EnterOver(bool victory)
    {
        _state = GameState.Over;
        _failScreen.SetStatus(victory ? "ALL CLEAR" : "GAME OVER");
        ShowGroup(GameState.Over);
    }

    // 只保留当前状态对应的 UI 窗口挂载到节点树（其余 detach，避免被绘制）。
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

/// <summary>游戏状态机（对应 PixiJS 版 Game 的状态）。</summary>
public enum GameState
{
    Title,
    Playing,
    Over,
}
