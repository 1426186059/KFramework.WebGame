using System;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 13×13 网格战场地图（Battle City 风格）。
/// 瓦片：0=空地 1=砖墙 2=钢墙 3=水 4=树 5=基地。
/// 地图左上角世界坐标 (OriginX, OriginY)，每格 32px，砖墙每格再分 2×2 子块（16px）。
/// </summary>
public sealed class TileMap
{
    public const int Cols = 13;
    public const int Rows = 13;
    public const int Tile = 32;
    public const double MapSize = Cols * Tile;                // 416
    public const double OriginX = (800 - MapSize) / 2.0;     // 192
    public const double OriginY = (600 - MapSize) / 2.0;     // 92

    public const int Empty = 0, Brick = 1, Steel = 2, Water = 3, Tree = 4, Base = 5;

    private readonly int[,] _tiles = new int[Rows, Cols];
    private readonly bool[,] _brickBits = new bool[Rows * 2, Cols * 2];
    private readonly bool[,] _steelShovel = new bool[Rows, Cols];
    private readonly int[,] _shovelPrev = new int[Rows, Cols];

    public int BaseRow { get; private set; } = 12;
    public int BaseCol { get; private set; } = 6;

    public int[,] Tiles => _tiles;
    public bool IsBase => _tiles[BaseRow, BaseCol] == Base;

    public void Load(string[] map)
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                char ch = r < map.Length && c < map[r].Length ? map[r][c] : '.';
                int t = ch switch
                {
                    'B' => Brick,
                    'S' => Steel,
                    'W' => Water,
                    'T' => Tree,
                    'E' => Base,
                    _ => Empty
                };
                _tiles[r, c] = t;
                if (t == Base) { BaseRow = r; BaseCol = c; }
            }
        }

        for (int r = 0; r < Rows * 2; r++)
            for (int c = 0; c < Cols * 2; c++)
                _brickBits[r, c] = _tiles[r / 2, c / 2] == Brick;

        Array.Clear(_steelShovel);
        Array.Clear(_shovelPrev);
    }

    private static int ColAt(double wx) => (int)Math.Floor((wx - OriginX) / Tile);
    private static int RowAt(double wy) => (int)Math.Floor((wy - OriginY) / Tile);

    public int TileAt(double wx, double wy)
    {
        int c = ColAt(wx), r = RowAt(wy);
        if (c < 0 || c >= Cols || r < 0 || r >= Rows) return Steel;
        return _tiles[r, c];
    }

    private static bool SolidForTank(int tile) =>
        tile == Brick || tile == Steel || tile == Water || tile == Base;

    /// <summary>坦克矩形（中心 + 半宽）是否与不可通行的地形碰撞。</summary>
    public bool TankCollides(double cx, double cy, double half)
    {
        double left = cx - OriginX - half, top = cy - OriginY - half;
        if (left < 0 || top < 0 || left + half * 2 > MapSize || top + half * 2 > MapSize)
            return true;

        return SolidForTank(TileAt(cx - half, cy - half)) ||
               SolidForTank(TileAt(cx + half, cy - half)) ||
               SolidForTank(TileAt(cx - half, cy + half)) ||
               SolidForTank(TileAt(cx + half, cy + half));
    }

    /// <summary>砖墙某子块（0/1 行、0/1 列）是否存活。</summary>
    public bool BrickBitAlive(int row, int col, int sr, int sc) =>
        _brickBits[row * 2 + sr, col * 2 + sc];

    /// <summary>子弹命中砖墙：摧毁子弹中心所在的一个 16×16 子块。返回是否命中。</summary>
    public bool HitBrick(double wx, double wy)
    {
        int c = ColAt(wx), r = RowAt(wy);
        if (c < 0 || c >= Cols || r < 0 || r >= Rows) return false;
        if (_tiles[r, c] != Brick) return false;

        int sc = (int)((wx - (OriginX + c * Tile)) / (Tile / 2.0));
        int sr = (int)((wy - (OriginY + r * Tile)) / (Tile / 2.0));
        sc = Math.Clamp(sc, 0, 1);
        sr = Math.Clamp(sr, 0, 1);
        int bi = r * 2 + sr, bj = c * 2 + sc;
        if (!_brickBits[bi, bj]) return false;

        _brickBits[bi, bj] = false;
        bool any = false;
        for (int i = 0; i < 2 && !any; i++)
            for (int j = 0; j < 2 && !any; j++)
                if (_brickBits[r * 2 + i, c * 2 + j]) any = true;
        if (!any) _tiles[r, c] = Empty;
        return true;
    }

    /// <summary>基地被击毁。</summary>
    public void DestroyBase() => _tiles[BaseRow, BaseCol] = Empty;

    /// <summary>基地周围 3×3 变钢墙（铲子道具；on=false 时还原）。</summary>
    public void SteelAroundBase(bool on)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue; // 基地本身不动
                int r = BaseRow + dr, c = BaseCol + dc;
                if (r < 0 || r >= Rows || c < 0 || c >= Cols) continue;

                if (on)
                {
                    if (_tiles[r, c] == Brick || _tiles[r, c] == Empty)
                    {
                        _shovelPrev[r, c] = _tiles[r, c];
                        _steelShovel[r, c] = true;
                        _tiles[r, c] = Steel;
                    }
                }
                else if (_steelShovel[r, c])
                {
                    _tiles[r, c] = _shovelPrev[r, c];
                    _steelShovel[r, c] = false;
                }
            }
        }
    }
}
