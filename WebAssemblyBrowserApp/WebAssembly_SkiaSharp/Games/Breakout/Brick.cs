namespace WebAssemblyBrowserApp.Games;

/// <summary>砖块。</summary>
public sealed class Brick
{
    public float X { get; }
    public float Y { get; }
    public float W { get; }
    public float H { get; }
    public string Color { get; }
    public string Dark { get; }
    public int Points { get; }
    public bool IsAlive { get; set; } = true;

    public Brick(float x, float y, float w, float h, string color, string dark, int points)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
        Color = color;
        Dark = dark;
        Points = points;
    }
}
