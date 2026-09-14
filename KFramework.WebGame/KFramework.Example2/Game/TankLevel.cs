using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 关卡：网格数据、地形绘制与碰撞查询。
/// 对应 PixiJS 版的 TankLevel.ts。
/// </summary>
internal sealed class TankLevel
{
    private readonly Tile[,] _tiles = new Tile[TankConfig.MapHeight, TankConfig.MapWidth];
    private readonly List<Point> _enemySpawns = new();

    public Vector2 PlayerSpawn { get; private set; }
    public IReadOnlyList<Point> EnemySpawns => _enemySpawns;
    public bool HeartDestroyed { get; private set; }

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

    /// <summary>绘制地形。草丛要在坦克之后再画，所以用 grassOnTop 分两趟。</summary>
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
}
