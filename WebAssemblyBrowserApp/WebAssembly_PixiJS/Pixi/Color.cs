namespace PixiJS;

/// <summary>RGBA 颜色（分量 0..1）。</summary>
public readonly struct Color
{
    public readonly float R, G, B, A;

    public Color(float r, float g, float b, float a = 1f)
    {
        R = Math.Clamp(r, 0, 1);
        G = Math.Clamp(g, 0, 1);
        B = Math.Clamp(b, 0, 1);
        A = Math.Clamp(a, 0, 1);
    }

    public static Color Hex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#') return White;
        var h = hex.Length == 4
            ? new string(new[] { hex[1], hex[1], hex[2], hex[2], hex[3], hex[3] })
            : hex.Substring(1);
        if (h.Length < 6) return White;
        return new Color(Byte(h, 0), Byte(h, 2), Byte(h, 4));
    }

    private static float Byte(string h, int i) => Convert.ToByte(h.Substring(i, 2), 16) / 255f;

    public string ToCss()
    {
        int r = (int)(R * 255), g = (int)(G * 255), b = (int)(B * 255);
        return A >= 0.999f ? $"rgb({r},{g},{b})" : $"rgba({r},{g},{b},{A:0.###})";
    }

    public static readonly Color White = new(1f, 1f, 1f);
    public static readonly Color Black = new(0f, 0f, 0f);
    public static readonly Color Transparent = new(0f, 0f, 0f, 0f);
}
