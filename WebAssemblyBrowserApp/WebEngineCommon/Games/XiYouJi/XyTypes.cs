namespace WebAssemblyBrowserApp.Games;

public enum XyState { Playing, GameOver, Victory }

public enum XyEntityKind { Zombie, Bat, Boss }

/// <summary>敌人实体（妖兵 / 飞妖 / 牛魔王）。位置为左上角坐标。</summary>
public sealed class XyEntity
{
    public XyEntityKind Kind;
    public float X, Y, W, H;
    public float VX, VY;
    public int Facing = 1;
    public int Hp, MaxHp;
    public float Timer, BaseY, MinX, MaxX;
    public float Flash;   // 受击闪白剩余时间
    public bool Alive = true;
}

/// <summary>可站立的砖台（仅顶部碰撞）。</summary>
public readonly struct XyPlatform
{
    public readonly float X, Y, W, H;
    public XyPlatform(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
}

/// <summary>视差背景装饰矩形。</summary>
public readonly struct XyBg
{
    public readonly float X, Y, W, H, Parallax, Alpha;
    public readonly string Color;
    public XyBg(float x, float y, float w, float h, float parallax, string color, float alpha = 1f)
    {
        X = x; Y = y; W = w; H = h; Parallax = parallax; Color = color; Alpha = alpha;
    }
}
