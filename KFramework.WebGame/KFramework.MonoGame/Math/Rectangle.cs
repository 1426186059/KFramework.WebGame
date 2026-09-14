namespace KFramework;

/// <summary>轴对齐矩形，屏幕坐标系（左上原点，Y 向下）。</summary>
public struct Rectangle : IEquatable<Rectangle>
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

    public Rectangle(int x, int y, int width, int height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    public static Rectangle Empty => default;

    public int Left => X;
    public int Right => X + Width;
    public int Top => Y;
    public int Bottom => Y + Height;

    public Point Location
    {
        get => new(X, Y);
        set { X = value.X; Y = value.Y; }
    }

    public Point Center => new(X + Width / 2, Y + Height / 2);

    public bool IsEmpty => Width == 0 && Height == 0;

    public bool Contains(int x, int y)
        => x >= X && x < X + Width && y >= Y && y < Y + Height;

    public bool Contains(Point p) => Contains(p.X, p.Y);

    public bool Contains(Vector2 p)
        => p.X >= X && p.X < X + Width && p.Y >= Y && p.Y < Y + Height;

    public bool Contains(Rectangle other)
        => X <= other.X && other.Right <= Right && Y <= other.Y && other.Bottom <= Bottom;

    public bool Intersects(Rectangle other)
        => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public static Rectangle Intersect(Rectangle a, Rectangle b)
    {
        if (!a.Intersects(b)) return Empty;
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        return new Rectangle(x, y, Math.Min(a.Right, b.Right) - x, Math.Min(a.Bottom, b.Bottom) - y);
    }

    public static Rectangle Union(Rectangle a, Rectangle b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        return new Rectangle(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    /// <summary>按内缩量收缩（负值表示外扩）。</summary>
    public void Inflate(int horizontal, int vertical)
    {
        X -= horizontal; Y -= vertical;
        Width += horizontal * 2; Height += vertical * 2;
    }

    public static bool operator ==(Rectangle a, Rectangle b)
        => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    public static bool operator !=(Rectangle a, Rectangle b) => !(a == b);

    public bool Equals(Rectangle other) => this == other;
    public override bool Equals(object? obj) => obj is Rectangle r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
}
