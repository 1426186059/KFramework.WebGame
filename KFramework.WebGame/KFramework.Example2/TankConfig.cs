using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>坦克大战的全局常量与枚举。</summary>
internal static class TankConfig
{
    // 地图
    public const int MapWidth = 21;
    public const int MapHeight = 20;
    public const int TileSize = 32;
    public const int TankSize = 32;

    /// <summary>关卡文本开头的注释行数量（原工程 ignoreLineCount = 3）。</summary>
    public const int LevelSkipLines = 3;

    // 数值
    public const float PlayerSpeed = 90f;
    public const float EnemySpeed = 55f;
    public const float BulletSpeed = 260f;
    public const float PlayerFireInterval = 0.45f;
    public const float BornDuration = 1.2f;
    public const float ExplodeDuration = 0.5f;

    // 关卡
    public const int EnemyTotal = 8;
    public const int EnemyOnField = 4;
    public const int PlayerLives = 3;

    /// <summary>方向单位向量，顺序与 <see cref="Dir"/> 一致。</summary>
    public static readonly Vector2[] DirVectors =
    {
        new(0f, -1f), new(1f, 0f), new(0f, 1f), new(-1f, 0f),
    };

    // 坦克精灵在 Player1 / Enemys 图集里的排布（按方向分组，每方向 8 帧）。
    // 若实机方向对不上，只改这两个表即可。
    public static readonly int[] PlayerDirBase = { 0, 8, 16, 24 };
    public static readonly int[] EnemyDirBase = { 0, 16, 32, 48 };
}

/// <summary>地块类型。</summary>
internal enum Tile : byte
{
    Empty = 0,
    Wall,      // 砖：子弹可摧毁
    Barriar,   // 铁：挡子弹
    Grass,     // 草：可通行，绘制在坦克之上
    Water,     // 水：挡坦克，子弹可飞过
    Heart,     // 老窝
}

internal enum Dir : byte
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,
}
