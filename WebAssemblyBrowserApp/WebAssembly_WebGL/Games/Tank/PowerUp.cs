using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

public enum PowerUpKind { Star, Grenade, Shovel, Helmet, Life }

/// <summary>道具：敌人被击毁时掉落，玩家接触拾取。</summary>
public sealed class PowerUp
{
    public Vector2 Pos;
    public PowerUpKind Kind;
    public float Life = 8f;
    public bool IsAlive = true;

    public string Color => Kind switch
    {
        PowerUpKind.Star => "#ffd43b",
        PowerUpKind.Grenade => "#ff6b6b",
        PowerUpKind.Shovel => "#94d82d",
        PowerUpKind.Helmet => "#4dabf7",
        _ => "#f06595"
    };
}
