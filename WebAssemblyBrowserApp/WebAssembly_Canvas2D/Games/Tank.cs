using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>坦克大战的方向（以网格坐标系统：0=上 1=右 2=下 3=左）。</summary>
public enum TankDir { Up, Right, Down, Left }

/// <summary>
/// 坦克实体（玩家与敌人共用）。
/// 坐标以逻辑像素表示，坦克大小为 TileSize。
/// </summary>
public sealed class Tank
{
    public const double Size = 26;

    public bool IsPlayer;
    public Vector2 Position;          // 坦克中心
    public TankDir Dir = TankDir.Up;
    public double Speed;
    public bool Alive = true;

    // 玩家火力：可同时存在的最大子弹数、冷却
    public int MaxBullets;
    public double FireCooldown;
    public double FireTimer;

    // 敌人属性
    public bool Armored;              // 装甲坦克（需两发）
    public int Hp = 1;
    public int Power = 1;             // 子弹威力（普通=1，可升级）
    public double SpawnProtect;       // 出生保护剩余秒数（闪烁、无敌）

    public double Half => Size / 2;

    public bool CanFire => FireTimer <= 0 && Alive;

    public void Update(float dt)
    {
        if (FireTimer > 0) FireTimer -= dt;
        if (SpawnProtect > 0) SpawnProtect -= dt;
    }

    public void Fire()
    {
        if (!CanFire) return;
        FireTimer = FireCooldown;
    }

    public Vector2 MuzzlePosition()
    {
        double d = Half + 4;
        return Dir switch
        {
            TankDir.Up => Position + new Vector2(0, -d),
            TankDir.Down => Position + new Vector2(0, d),
            TankDir.Left => Position + new Vector2(-d, 0),
            TankDir.Right => Position + new Vector2(d, 0),
            _ => Position,
        };
    }
}

/// <summary>子弹。owner=true 表示玩家发射（否则敌人）。</summary>
public sealed class Bullet
{
    public Vector2 Position;
    public Vector2 Velocity;
    public bool IsPlayer;
    public int Power;
    public bool Alive = true;

    public const double Size = 6;
}
