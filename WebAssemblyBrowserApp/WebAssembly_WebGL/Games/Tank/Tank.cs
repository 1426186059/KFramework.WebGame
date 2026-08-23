using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

public enum TankType { Basic = 0, Fast = 1, Heavy = 2, Armor = 3 }

/// <summary>
/// 坦克：玩家与敌人共用。Pos 为中心点（世界像素坐标），Dir 为 0上/1右/2下/3左。
/// </summary>
public sealed class Tank
{
    public const double Half = 13;
    public const int Up = 0, Right = 1, Down = 2, Left = 3;

    public static readonly int[] DirX = { 0, 1, 0, -1 };
    public static readonly int[] DirY = { -1, 0, 1, 0 };

    public Vector2 Pos;
    public int Dir = Up;
    public TankType Type = TankType.Basic;
    public bool IsPlayer;
    public bool IsAlive = true;
    public int Hp = 1;
    public float SpawnInvul;   // 出生无敌
    public float Immortal;     // 道具无敌（头盔）
    public float HitFlash;     // 受击闪白
    public double FireCd;
    public int Score;

    public Tank(double x, double y, bool player, TankType type = TankType.Basic)
    {
        Pos = new Vector2(x, y);
        IsPlayer = player;
        Type = type;
        Hp = type == TankType.Heavy ? 3 : 1;
        Score = type switch
        {
            TankType.Basic => 100,
            TankType.Fast => 200,
            TankType.Heavy => 300,
            _ => 400
        };
        FireCd = player ? 0.3 : MathUtils.Rand(1.2, 2.4);
    }

    public double Speed => IsPlayer ? 130 : Type is TankType.Fast or TankType.Armor ? 92 : 60;

    public string Color => IsPlayer ? "#ffd43b" :
        Type switch
        {
            TankType.Fast => "#d6336c",
            TankType.Heavy => "#868e96",
            TankType.Armor => "#ff8787",
            _ => "#e8590c"
        };
}
