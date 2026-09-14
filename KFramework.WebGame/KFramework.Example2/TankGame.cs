using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战（垂直切片）：关卡加载 → 地形绘制 → 玩家移动/开炮 → 子弹碰撞 → 敌人 AI。
/// 采用 KFramework 的立即模式渲染：每帧遍历逻辑对象，用 SpriteBatch 统一提交。
/// </summary>
public sealed class TankGame : Game
{
    private const int MapWidth = 21;
    private const int MapHeight = 20;
    private const int TileSize = 32;
    private const int LevelSkipLines = 3;

    private const float PlayerSpeed = 90f;
    private const float EnemySpeed = 55f;
    private const float BulletSpeed = 260f;
    private const float FireInterval = 0.45f;

    private const int TankSize = 32;

    private enum Tile : byte
    {
        Empty = 0,
        Wall,      // 砖：子弹可摧毁
        Barriar,   // 铁：挡子弹
        Grass,     // 草：可通行，绘制在坦克之上
        Water,     // 水：挡坦克，子弹可飞过
        Heart,     // 老窝
    }

    private enum Dir : byte
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    private static readonly Vector2[] DirVectors =
    {
        new(0f, -1f), new(1f, 0f), new(0f, 1f), new(-1f, 0f),
    };

    // 坦克精灵在 Player1 / Enemys 图集里的排布（按方向分组，每方向 8 帧）。
    // 若实机方向对不上，只改这两个表即可。
    private static readonly int[] PlayerDirBase = { 0, 8, 16, 24 };
    private static readonly int[] EnemyDirBase = { 0, 16, 32, 48 };

    private SpriteBatch _batch = null!;
    private bool _ready;

    private readonly Tile[,] _tiles = new Tile[MapHeight, MapWidth];
    private readonly List<Point> _enemySpawns = new();

    private Texture2D? _wall;
    private Texture2D? _barriar;
    private Texture2D? _grass;
    private Texture2D? _water;
    private Texture2D? _heart;
    private Texture2D?[] _playerSprites = Array.Empty<Texture2D?>();
    private Texture2D?[] _enemySprites = Array.Empty<Texture2D?>();
    private Texture2D?[] _bulletSprites = Array.Empty<Texture2D?>();

    private Vector2 _playerPos;
    private Dir _playerDir = Dir.Up;
    private float _playerFireTimer;
    private bool _playerAlive = true;
    private int _playerAnim;

    private sealed class Bullet
    {
        public Vector2 Pos;
        public Dir Dir;
        public bool Active;
        public bool FromPlayer;
    }

    private sealed class Enemy
    {
        public Vector2 Pos;
        public Dir Dir;
        public bool Active;
        public float TurnTimer;
        public float FireTimer;
    }

    private readonly List<Bullet> _bullets = new();
    private readonly List<Enemy> _enemies = new();
    private readonly Random _random = new(12345);

    private int _animTick;

    /// <summary>精灵表检视模式：把 Player1 全 32 帧平铺出来，用来确认方向排布。</summary>
    private bool _inspectSprites;

    /// <summary>地图左上角在屏幕上的偏移（让 21x20 的地图居中）。</summary>
    private Vector2 Origin => new(
        (GraphicsDevice.Viewport.Width - MapWidth * TileSize) * 0.5f,
        (GraphicsDevice.Viewport.Height - MapHeight * TileSize) * 0.5f);

    protected override async Task LoadContentAsync()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        await Content.LoadAsync().ConfigureAwait(false);

        _wall = TryTex("Map_0");
        _barriar = TryTex("Map_1");
        _grass = TryTex("Map_2");
        _water = TryTex("Map_3");
        _heart = TryTex("Map_5");

        _playerSprites = LoadRange("Player1_", 32);
        _enemySprites = LoadRange("Enemys_", 64);
        _bulletSprites = LoadRange("bullet_", 4);

        LoadLevel(0);
        SpawnEnemies(4);

        _ready = true;
    }

    private Texture2D? TryTex(string name)
    {
        Content.TryLoadTexture(name, out Texture2D? tex);
        return tex;
    }

    private Texture2D?[] LoadRange(string prefix, int count)
    {
        var result = new Texture2D?[count];
        for (int i = 0; i < count; i++) result[i] = TryTex(prefix + i);
        return result;
    }

    // ===== 关卡 =====

    private void LoadLevel(int levelIndex)
    {
        string text = Content.LoadText($"levels/{levelIndex:00}");
        string[] lines = text.Trim().Split('\n');

        _enemySpawns.Clear();
        int row = 0;
        for (int i = LevelSkipLines; i < lines.Length && row < MapHeight; i++, row++)
        {
            string line = lines[i].TrimEnd('\r');
            for (int x = 0; x < MapWidth; x++)
            {
                char c = x < line.Length ? line[x] : ' ';
                _tiles[row, x] = ParseTile(c);

                if (c == 'P') _playerPos = TileCenter(x, row);
                else if (c == 'E') _enemySpawns.Add(new Point(x, row));
            }
        }
    }

    private static Tile ParseTile(char c) => c switch
    {
        '#' => Tile.Wall,
        '*' => Tile.Barriar,
        '~' => Tile.Water,
        '^' => Tile.Grass,
        '@' => Tile.Heart,
        _ => Tile.Empty,
    };

    private Vector2 TileCenter(int x, int y) => new(x * TileSize + TileSize / 2f, y * TileSize + TileSize / 2f);

    private void SpawnEnemies(int count)
    {
        _enemies.Clear();
        for (int i = 0; i < count && i < _enemySpawns.Count; i++)
        {
            Point p = _enemySpawns[i];
            _enemies.Add(new Enemy
            {
                Pos = TileCenter(p.X, p.Y),
                Dir = Dir.Down,
                Active = true,
                TurnTimer = 0f,
                FireTimer = 0f,
            });
        }
    }

    // ===== 逻辑 =====

    protected override void Update(GameTime gameTime)
    {
        if (!_ready) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _animTick++;
        _playerAnim = (_animTick / 8) % 2;

        if (Input.GetKeyboardState().IsKeyPressed(Keys.Tab)) _inspectSprites = !_inspectSprites;
        if (_inspectSprites) return;

        UpdatePlayer(dt);
        UpdateEnemies(dt);
        UpdateBullets(dt);
    }

    private void UpdatePlayer(float dt)
    {
        if (!_playerAlive) return;

        var keyboard = Input.GetKeyboardState();

        Dir? wanted = null;
        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W)) wanted = Dir.Up;
        else if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D)) wanted = Dir.Right;
        else if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S)) wanted = Dir.Down;
        else if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A)) wanted = Dir.Left;

        if (wanted.HasValue)
        {
            _playerDir = wanted.Value;
            Vector2 delta = DirVectors[(int)_playerDir] * PlayerSpeed * dt;
            TryMove(ref _playerPos, delta);
        }

        _playerFireTimer -= dt;
        if (keyboard.IsKeyDown(Keys.Space) && _playerFireTimer <= 0f)
        {
            Fire(_playerPos, _playerDir, true);
            _playerFireTimer = FireInterval;
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (var enemy in _enemies)
        {
            if (!enemy.Active) continue;

            enemy.TurnTimer -= dt;
            if (enemy.TurnTimer <= 0f)
            {
                enemy.Dir = (Dir)_random.Next(4);
                enemy.TurnTimer = 0.8f + (float)_random.NextDouble() * 1.6f;
            }

            Vector2 delta = DirVectors[(int)enemy.Dir] * EnemySpeed * dt;
            if (!TryMove(ref enemy.Pos, delta)) enemy.TurnTimer = 0f;   // 撞墙就立刻转向

            enemy.FireTimer -= dt;
            if (enemy.FireTimer <= 0f)
            {
                Fire(enemy.Pos, enemy.Dir, false);
                enemy.FireTimer = 1.2f + (float)_random.NextDouble() * 1.5f;
            }
        }
    }

    private void UpdateBullets(float dt)
    {
        foreach (var bullet in _bullets)
        {
            if (!bullet.Active) continue;

            bullet.Pos += DirVectors[(int)bullet.Dir] * BulletSpeed * dt;

            // 出界
            if (bullet.Pos.X < 0 || bullet.Pos.Y < 0 ||
                bullet.Pos.X > MapWidth * TileSize || bullet.Pos.Y > MapHeight * TileSize)
            {
                bullet.Active = false;
                continue;
            }

            int tx = (int)(bullet.Pos.X / TileSize);
            int ty = (int)(bullet.Pos.Y / TileSize);
            if (tx < 0 || ty < 0 || tx >= MapWidth || ty >= MapHeight) continue;

            Tile tile = _tiles[ty, tx];
            if (tile == Tile.Wall)
            {
                _tiles[ty, tx] = Tile.Empty;
                bullet.Active = false;
            }
            else if (tile == Tile.Barriar || tile == Tile.Heart)
            {
                bullet.Active = false;
                if (tile == Tile.Heart) _playerAlive = false;
            }
        }

        _bullets.RemoveAll(static b => !b.Active);
    }

    private void Fire(Vector2 from, Dir dir, bool byPlayer)
    {
        _bullets.Add(new Bullet
        {
            Pos = from + DirVectors[(int)dir] * (TankSize / 2f),
            Dir = dir,
            Active = true,
            FromPlayer = byPlayer,
        });
    }

    /// <summary>尝试移动；被阻挡时返回 false（位置保持不变）。</summary>
    private bool TryMove(ref Vector2 pos, Vector2 delta)
    {
        Vector2 next = pos + delta;

        // 用坦克外接矩形覆盖到的格子做检测
        float left = next.X - TankSize / 2f;
        float right = next.X + TankSize / 2f - 0.001f;
        float top = next.Y - TankSize / 2f;
        float bottom = next.Y + TankSize / 2f - 0.001f;

        if (left < 0 || top < 0 || right >= MapWidth * TileSize || bottom >= MapHeight * TileSize)
            return false;

        for (int ty = (int)(top / TileSize); ty <= (int)(bottom / TileSize); ty++)
        {
            for (int tx = (int)(left / TileSize); tx <= (int)(right / TileSize); tx++)
            {
                if (!IsPassable(_tiles[ty, tx])) return false;
            }
        }

        pos = next;
        return true;
    }

    private static bool IsPassable(Tile tile)
        => tile is Tile.Empty or Tile.Grass;

    // ===== 绘制 =====

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

        DrawTerrain(grassOnTop: false);
        DrawTanks();
        DrawBullets();
        DrawTerrain(grassOnTop: true);

        _batch.End();
    }

    private void DrawTerrain(bool grassOnTop)
    {
        Vector2 origin = Origin;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                Tile tile = _tiles[y, x];
                if (tile == Tile.Empty) continue;

                bool isGrass = tile == Tile.Grass;
                if (isGrass != grassOnTop) continue;

                Texture2D? tex = tile switch
                {
                    Tile.Wall => _wall,
                    Tile.Barriar => _barriar,
                    Tile.Water => _water,
                    Tile.Heart => _heart,
                    Tile.Grass => _grass,
                    _ => null,
                };
                if (tex is null) continue;

                _batch.Draw(tex, new Vector2(origin.X + x * TileSize, origin.Y + y * TileSize), Color.White);
            }
        }
    }

    private void DrawTanks()
    {
        Vector2 origin = Origin;

        if (_playerAlive)
        {
            int index = PlayerDirBase[(int)_playerDir] + _playerAnim;
            Texture2D? tex = Pick(_playerSprites, index);
            if (tex is not null) DrawCentered(tex, origin + _playerPos);
        }

        foreach (var enemy in _enemies)
        {
            if (!enemy.Active) continue;
            int index = EnemyDirBase[(int)enemy.Dir] + _playerAnim;
            Texture2D? tex = Pick(_enemySprites, index);
            if (tex is not null) DrawCentered(tex, origin + enemy.Pos);
        }
    }

    private void DrawBullets()
    {
        Vector2 origin = Origin;
        foreach (var bullet in _bullets)
        {
            if (!bullet.Active) continue;
            Texture2D? tex = Pick(_bulletSprites, (int)bullet.Dir);
            if (tex is not null) DrawCentered(tex, origin + bullet.Pos);
        }
    }

    /// <summary>把 Player1 全 32 帧按 8 列平铺，用来肉眼确认「方向 / 动画帧 / 等级」的排布顺序。</summary>
    private void DrawSpriteSheet()
    {
        const int columns = 8;
        const int cell = 48;

        for (int i = 0; i < _playerSprites.Length; i++)
        {
            Texture2D? tex = _playerSprites[i];
            if (tex is null) continue;
            _batch.Draw(tex, new Vector2(24f + (i % columns) * cell, 24f + (i / columns) * cell), Color.White);
        }
    }

    private void DrawCentered(Texture2D tex, Vector2 center)
    {
        _batch.Draw(tex, new Vector2(center.X - tex.Width / 2f, center.Y - tex.Height / 2f), Color.White);
    }

    private static Texture2D? Pick(Texture2D?[] sprites, int index)
    {
        if (sprites.Length == 0) return null;
        return sprites[((index % sprites.Length) + sprites.Length) % sprites.Length];
    }
}
