using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>坦克炮弹。</summary>
public sealed class Bullet
{
    public const float Half = 3.5f;

    public Vector2 Pos;
    public int Dir;
    public bool IsPlayer;
    public bool IsAlive = true;

    public float Speed => IsPlayer ? 340 : 240;

    public Bullet(float x, float y, int dir, bool isPlayer)
    {
        Pos = new Vector2(x, y);
        Dir = dir;
        IsPlayer = isPlayer;
    }
}
