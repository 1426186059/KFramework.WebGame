namespace WebAssemblyBrowserApp.Games;

/// <summary>砖块。</summary>
public sealed class Brick
{
    public double X { get; }
    public double Y { get; }
    public double W { get; }
    public double H { get; }
    public string Color { get; }
    public int Points { get; }
    public bool IsAlive { get; set; } = true;

    public Brick(double x, double y, double w, double h, string color, int points)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
        Color = color;
        Points = points;
    }
}
