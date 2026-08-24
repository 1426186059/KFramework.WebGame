using PixiGame;
using PixiJS;

namespace PixiDemo;

/// <summary>
/// FC《魂斗罗》风格横版卷轴射击（Pixi 原生对象模型）。
/// 所有世界对象挂在 _worldRoot（Container）下，卷轴 = 只改 _worldRoot.X，
/// 平台 / 相机 / 敌人 / 子弹的坐标全在关卡世界坐标中，由 Pixi 场景图统一合成。
/// </summary>
public sealed class ContraGame : GameScene
{
    private const float W = 960f, H = 540f;
    private const float GroundY = 470f;
    private const float LevelW = 2880f;      // 关卡世界宽度
    private const float Gravity = 2300f;

    // ---- 敌人 / 子弹 ----
    private sealed class Enemy
    {
        public Graphics Gfx = null!;
        public float X, Y, Vx, Vy;
        public int Kind;            // 0 步兵  1 炮台  2 飞行器
        public float Timer;
        public bool Alive = true;
    }

    private sealed class Bullet
    {
        public Graphics Gfx = null!;
        public float X, Y, Vx, Vy;
        public bool Alive = true;
    }

    private readonly List<Enemy> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Bullet> _eBullets = new();

    // ---- 平台（世界坐标） ----
    private readonly List<(float x, float y, float w)> _platforms = new();

    // ---- 玩家 ----
    private Container? _player;
    private Graphics? _playerGfx;
    private float _px, _py, _pvx, _pvy;
    private int _facing = 1;
    private bool _onGround;
    private int _lives = 3;
    private int _score;
    private float _invuln;
    private float _fireCd;
    private float _spawnTimer, _flyTimer;
    private bool _win;
    private bool _initialized;

    private Container? _worldRoot;   // 卷轴容器（世界坐标 → 屏幕）
    private Graphics? _terrainGfx, _bgGfx;
    private PixiText? _hud, _overlay;

    public ContraGame() : base("contra") { }

    public override void Enter()
    {
        var stage = PixiApp.Instance.Stage;
        _enemies.Clear();
        _bullets.Clear();
        _eBullets.Clear();
        _platforms.Clear();
        _lives = 3;
        _score = 0;
        _win = false;
        _invuln = 2f;
        _spawnTimer = 1.2f;
        _flyTimer = 4f;

        // ---- 世界根 ----
        _worldRoot = Container.Create();
        stage.AddChild(_worldRoot);

        // ---- 背景（天空色带 + 远山 + 丛林） ----
        _bgGfx = Graphics.Create();
        _bgGfx.BeginBatch();
        _bgGfx.DrawRect(0, 0, LevelW, H, new Color(0.10f, 0.13f, 0.20f));
        _bgGfx.DrawRect(0, 150, LevelW, 60, new Color(0.16f, 0.22f, 0.36f, 0.9f));
        _bgGfx.DrawRect(0, 300, LevelW, 90, new Color(0.08f, 0.28f, 0.22f, 0.9f));
        for (int i = 0; i < LevelW; i += 130)
        {
            _bgGfx.DrawTriangle(i, 360, i + 65, 300, i + 130, 360, new Color(0.05f, 0.16f, 0.12f));
            _bgGfx.DrawCircle(i + 40, 330, 26, new Color(0.04f, 0.22f, 0.14f));
            _bgGfx.DrawCircle(i + 95, 350, 20, new Color(0.06f, 0.26f, 0.16f));
        }
        _bgGfx.EndBatch();
        _worldRoot.AddChild(_bgGfx);

        // ---- 地形：地面 + 平台 ----
        _terrainGfx = Graphics.Create();
        _terrainGfx.BeginBatch();
        // 地面（砖纹）
        _terrainGfx.DrawRect(0, GroundY, LevelW, H - GroundY, new Color(0.30f, 0.24f, 0.18f));
        for (int x = 0; x < LevelW; x += 44)
        {
            _terrainGfx.DrawRect(x, GroundY, 44, 6, new Color(0.22f, 0.17f, 0.12f));
            _terrainGfx.DrawRect(x + 22, GroundY + 34, 44, 6, new Color(0.24f, 0.18f, 0.13f));
        }
        _terrainGfx.DrawRect(0, GroundY - 4, LevelW, 4, new Color(0.42f, 0.34f, 0.24f));
        // 平台
        AddPlatform(260, 360, 190); AddPlatform(560, 290, 150); AddPlatform(760, 200, 170);
        AddPlatform(1040, 380, 170); AddPlatform(1330, 300, 160); AddPlatform(1580, 200, 200);
        AddPlatform(1900, 380, 140); AddPlatform(2120, 290, 170); AddPlatform(2380, 200, 240);
        _terrainGfx.EndBatch();
        _worldRoot.AddChild(_terrainGfx);

        // ---- 玩家（局部坐标绘制，场景图负责位置与朝向） ----
        _player = Container.Create();
        _playerGfx = Graphics.Create();
        _playerGfx.BeginBatch();
        _playerGfx.DrawRect(-6, -16, 12, 9, new Color(0.95f, 0.80f, 0.65f));   // 头
        _playerGfx.DrawRect(-6, -17, 12, 2, new Color(0.85f, 0.20f, 0.20f));    // 头带
        _playerGfx.DrawRect(-7, -7, 14, 12, new Color(0.20f, 0.45f, 0.85f));    // 身体
        _playerGfx.DrawRect(-7, -2, 6, 6, new Color(0.12f, 0.20f, 0.35f));      // 左腿
        _playerGfx.DrawRect(1, -2, 6, 6, new Color(0.12f, 0.20f, 0.35f));       // 右腿
        _playerGfx.DrawRect(5, -14, 17, 4, new Color(0.55f, 0.60f, 0.65f));     // 枪管（朝右）
        _playerGfx.EndBatch();
        _player.AddChild(_playerGfx);
        _worldRoot.AddChild(_player);

        // ---- 固定炮台 ----
        SpawnEnemy(1, 640, GroundY - 74, 0, 0);
        SpawnEnemy(1, 1560, 250, 0, 0);
        SpawnEnemy(1, 2440, GroundY - 74, 0, 0);

        _px = 60; _py = GroundY - 28; _pvx = 0; _pvy = 0;

        // ---- HUD / 覆盖层（屏幕空间，直接挂舞台） ----
        _hud = new PixiText("", "bold 17px system-ui, sans-serif", new Color(0.9f, 0.9f, 0.95f), "left");
        _hud.X = 16; _hud.Y = 14;
        stage.AddChild(_hud);
        _overlay = new PixiText("", "bold 32px system-ui, sans-serif", Color.White);
        _overlay.X = 480;   // 默认 align="center"，锚点在文本中心
        _overlay.Y = 240;
        stage.AddChild(_overlay);

        _initialized = true;
    }

    private void AddPlatform(float x, float y, float w)
    {
        _platforms.Add((x, y, w));
        _terrainGfx!.DrawRect(x, y, w, 16, new Color(0.32f, 0.26f, 0.18f));
        _terrainGfx.DrawRect(x, y, w, 3, new Color(0.45f, 0.36f, 0.26f));
        for (float px = x + 12; px < x + w; px += 26)
            _terrainGfx.DrawRect(px, y + 4, 10, 4, new Color(0.20f, 0.16f, 0.11f));
    }

    private void SpawnEnemy(int kind, float x, float y, float vx, float vy)
    {
        var e = new Enemy { Kind = kind, X = x, Y = y, Vx = vx, Vy = vy, Timer = Random.Shared.NextSingle() * 1.5f };
        e.Gfx = Graphics.Create();
        e.Gfx.BeginBatch();
        switch (kind)
        {
            case 0: // 步兵（红色小人）
                e.Gfx.DrawRect(-6, -16, 12, 9, new Color(0.90f, 0.75f, 0.60f));
                e.Gfx.DrawRect(-7, -7, 14, 12, new Color(0.85f, 0.25f, 0.25f));
                e.Gfx.DrawRect(-7, -2, 6, 6, new Color(0.30f, 0.12f, 0.12f));
                e.Gfx.DrawRect(1, -2, 6, 6, new Color(0.30f, 0.12f, 0.12f));
                e.Gfx.DrawRect(4, -13, 12, 3, new Color(0.40f, 0.45f, 0.50f));
                break;
            case 1: // 炮台（固定，炮口朝上）
                e.Gfx.DrawRect(-16, -22, 32, 22, new Color(0.45f, 0.50f, 0.55f));
                e.Gfx.DrawRect(-10, -32, 20, 12, new Color(0.25f, 0.30f, 0.35f));
                e.Gfx.DrawCircle(0, 0, 5, new Color(0.95f, 0.85f, 0.40f));
                break;
            case 2: // 飞行器
                e.Gfx.DrawEllipse(0, 0, 18, 9, new Color(0.30f, 0.60f, 0.35f));
                e.Gfx.DrawEllipse(0, -4, 7, 4, new Color(0.55f, 0.85f, 0.55f));
                break;
        }
        e.Gfx.EndBatch();
        e.Gfx.X = x; e.Gfx.Y = y;
        _worldRoot!.AddChild(e.Gfx);
        _enemies.Add(e);
    }

    private void FirePlayer()
    {
        var b = new Bullet { X = _px + _facing * 22, Y = _py - 12, Vx = _facing * 560f, Vy = 0 };
        b.Gfx = Graphics.Create();
        b.Gfx.DrawRect(0, -3, 14, 6, new Color(1.0f, 0.95f, 0.5f));
        b.Gfx.X = b.X; b.Gfx.Y = b.Y;
        _worldRoot!.AddChild(b.Gfx);
        _bullets.Add(b);
        Audio.Beep(760, 0.05, "square", 0.04);
    }

    private void FireEnemy(Enemy e, float tx, float ty)
    {
        float dx = tx - e.X, dy = ty - e.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        dx /= len; dy /= len;
        var b = new Bullet { X = e.X + dx * 20, Y = e.Y + dy * 20, Vx = dx * 240f, Vy = dy * 240f };
        b.Gfx = Graphics.Create();
        b.Gfx.DrawCircle(0, 0, 4, new Color(1.0f, 0.55f, 0.35f));
        b.Gfx.X = b.X; b.Gfx.Y = b.Y;
        _worldRoot!.AddChild(b.Gfx);
        _eBullets.Add(b);
        Audio.Beep(300, 0.06, "square", 0.04);
    }

    private static bool RectHit(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
        => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

    public override void Update(float dt)
    {
        if (!_initialized) return;
        if (Input.IsKeyPressed(Input.Escape)) { GameApp.Instance.Start("main-menu"); return; }
        if (_win || _lives <= 0)
        {
            if (Input.IsKeyPressed(Input.Enter)) { GameApp.Instance.Start("contra"); }
            return;
        }

        // ---- 玩家输入 ----
        float mv = 0;
        if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) { mv = -1; _facing = -1; }
        if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) { mv = 1; _facing = 1; }
        _pvx = mv * 260f;

        if ((Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed(Input.KeyW) || Input.IsKeyPressed(Input.ArrowUp)) && _onGround)
        {
            _pvy = -700f;
            _onGround = false;
            Audio.Beep(400, 0.05, "square", 0.04);
        }
        if (Input.IsKeyDown(Input.KeyJ) || Input.IsKeyDown(Input.KeyZ) || Input.IsKeyDown(Input.KeyX) || Input.IsKeyDown(Input.ControlLeft))
        {
            _fireCd -= dt;
            if (_fireCd <= 0) { FirePlayer(); _fireCd = 0.18f; }
        }

        // ---- 玩家物理（世界坐标） ----
        _pvy += Gravity * dt;
        _px += _pvx * dt;
        _py += _pvy * dt;
        _px = MathF.Min(MathF.Max(_px, 12), LevelW - 12);

        // 平台碰撞
        _onGround = false;
        if (_pvy >= 0)
        {
            foreach (var (x, y, w) in _platforms)
            {
                if (_px + 7 >= x && _px - 7 <= x + w && _py <= y + 16 && _py >= y)
                {
                    _py = y; _pvy = 0; _onGround = true;
                }
            }
            if (_py >= GroundY - 28) { _py = GroundY - 28; _pvy = 0; _onGround = true; }
        }
        else
        {
            foreach (var (x, y, w) in _platforms)
                if (_px + 7 >= x && _px - 7 <= x + w && _py >= y - 14 && _py <= y) { _py = y + 14; _pvy = 0; }
        }

        // ---- 滚屏（只移动世界根容器） ----
        float camX = MathF.Min(MathF.Max(_px - W / 2, 0), LevelW - W);
        _worldRoot!.X = -camX;

        // ---- 玩家同步到世界 ----
        _player!.X = _px;
        _player.Y = _py;
        _player.SetScale(_facing, 1);
        _player.Alpha = _invuln > 0 ? (MathF.Sin(GameApp.Instance.Time * 40f) > 0 ? 0.3f : 0.9f) : 1f;

        // ---- 敌人 ----
        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            float sx = MathF.Min(_px + 500 + Random.Shared.NextSingle() * 400, LevelW - 60);
            SpawnEnemy(0, sx, GroundY - 26, -110f, 0);
            _spawnTimer = 2.2f + Random.Shared.NextSingle() * 1.6f;
        }
        _flyTimer -= dt;
        if (_flyTimer <= 0)
        {
            float sx = MathF.Max(_px + 300, 300);
            SpawnEnemy(2, MathF.Min(sx, LevelW - 60), 120 + Random.Shared.NextSingle() * 160, -140f, 0);
            _flyTimer = 4.5f + Random.Shared.NextSingle() * 2f;
        }

        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            e.X += e.Vx * dt;
            e.Gfx.X = e.X;
            switch (e.Kind)
            {
                case 0: // 步兵：朝玩家走 + 近距开火
                    if (_px > e.X) e.Vx = 90f; else e.Vx = -90f;
                    if (e.Vx > 0) e.Gfx.SetScale(1, 1); else e.Gfx.SetScale(-1, 1);
                    e.Timer -= dt;
                    if (e.Timer <= 0 && MathF.Abs(_px - e.X) < 420f) { FireEnemy(e, _px, _py - 12); e.Timer = 1.8f; }
                    break;
                case 1: // 炮台：周期朝玩家发射
                    e.Timer -= dt;
                    if (e.Timer <= 0) { FireEnemy(e, _px, _py - 12); e.Timer = 2.4f; }
                    break;
                case 2: // 飞行器：正弦飘移
                    e.Y += MathF.Sin(GameApp.Instance.Time * 3f + e.X) * 40f * dt;
                    if (MathF.Abs(_px - e.X) < 380f) { e.Timer -= dt; if (e.Timer <= 0) { FireEnemy(e, _px, _py - 12); e.Timer = 1.6f; } }
                    break;
            }
            // 出界清理
            if (e.X < -80 || e.X > LevelW + 80 || e.Y > H + 80)
            {
                _worldRoot.RemoveChild(e.Gfx); e.Gfx.Destroy();
                _enemies.RemoveAt(i);
            }
        }

        // ---- 玩家子弹 vs 敌人 ----
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.X += b.Vx * dt;
            b.Gfx.X = b.X;
            if (b.X < -20 || b.X > LevelW + 20) { KillBullet(b); _bullets.RemoveAt(i); continue; }
            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                var e = _enemies[j];
                if (e.Kind == 1) continue;   // 炮台不可被普通子弹击毁（需跳上射击）→ 简化：可击毁
                if (RectHit(b.X - 7, b.Y - 3, 14, 6, e.X - 16, e.Y - 20, 32, 40))
                {
                    KillBullet(b); _bullets.RemoveAt(i);
                    _score += e.Kind == 0 ? 100 : 200;
                    _worldRoot.RemoveChild(e.Gfx); e.Gfx.Destroy();
                    _enemies.RemoveAt(j);
                    Audio.Beep(200, 0.08, "square", 0.05);
                    goto nextBullet;
                }
            }
            // 炮台：跳上顶部踩毁
            foreach (var e in _enemies)
            {
                if (e.Kind != 1) continue;
                if (RectHit(b.X - 7, b.Y - 3, 14, 6, e.X - 16, e.Y - 32, 32, 32))
                {
                    KillBullet(b); _bullets.RemoveAt(i);
                    _score += 300;
                    _worldRoot.RemoveChild(e.Gfx); e.Gfx.Destroy();
                    _enemies.Remove(e);
                    break;
                }
            }
        nextBullet: ;
        }

        // ---- 敌方子弹 vs 玩家 ----
        for (int i = _eBullets.Count - 1; i >= 0; i--)
        {
            var b = _eBullets[i];
            b.X += b.Vx * dt; b.Y += b.Vy * dt;
            b.Gfx.X = b.X; b.Gfx.Y = b.Y;
            if (b.X < -30 || b.X > LevelW + 30 || b.Y < -30 || b.Y > H + 30)
            { KillBullet(b); _eBullets.RemoveAt(i); continue; }
            if (_invuln <= 0 && RectHit(b.X - 4, b.Y - 4, 8, 8, _px - 7, _py - 17, 14, 23))
            {
                KillBullet(b); _eBullets.RemoveAt(i);
                Hurt();
            }
        }

        // ---- 敌人 vs 玩家（身体碰撞） ----
        if (_invuln <= 0)
        {
            foreach (var e in _enemies)
            {
                if (RectHit(_px - 7, _py - 17, 14, 23, e.X - 16, e.Y - 20, 32, 40))
                { Hurt(); break; }
            }
        }

        _invuln -= dt;
        _hud!.Text = $"魂斗罗   得分 {_score:000000}   生命 {_lives}   [←→]移动  [空格]跳  [J]射击  [Esc]菜单";

        // ---- 过关 ----
        if (_px >= LevelW - 60)
        {
            _win = true;
            _overlay!.Text = "过关！得分 " + _score + "  按 Enter 再来";
            _overlay.Visible = true;
        }
    }

    private void Hurt()
    {
        _lives--;
        _invuln = 2f;
        Audio.Beep(140, 0.3, "sawtooth", 0.06);
        if (_lives <= 0)
        {
            _overlay!.Text = "GAME OVER  得分 " + _score + "  按 Enter 重来";
            _overlay.Visible = true;
        }
        else
        {
            // 回到检查点（按关卡进度取整到 500px 检查点）
            _px = 60 + MathF.Floor((_px - 60) / 500f) * 500f;
            _py = GroundY - 28;
            _pvy = 0;
            _player!.Alpha = 1f;
        }
    }

    private void KillBullet(Bullet b)
    {
        _worldRoot!.RemoveChild(b.Gfx);
        b.Gfx.Destroy();
    }

    public override void Exit()
    {
        if (_worldRoot is not null) { _worldRoot.Destroy(); _worldRoot = null; }
        if (_hud is not null) { _hud.Destroy(); _hud = null; }
        if (_overlay is not null) { _overlay.Destroy(); _overlay = null; }
    }

    public override void Render() { }
}
