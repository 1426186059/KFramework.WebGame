using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>炮弹。对应 PixiJS 版的 Shell.ts。</summary>
internal sealed class Shell
{
    public Vector2 Position;
    public Dir Direction;
    public bool FromPlayer;
    public bool Active = true;

    /// <summary>火力等级（来自星星道具）：提升飞行速度，2 级以上可打穿铁块。</summary>
    public int SpeedBonus;

    /// <summary>显示节点（等价于 PixiJS 的 mSprite）。</summary>
    public readonly TGameSprite View = new();

    private float Speed => TankConfig.BulletSpeed + SpeedBonus * 40f;

    public Shell()
    {
        View.Pivot = new Vector2(0.5f);
        View.UseNativeSize = true;
    }

    public void Update(float dt)
        => Position += TankConfig.DirVectors[(int)Direction] * Speed * dt;

    /// <summary>子弹所在的格子坐标（居中坐标换算）。</summary>
    public Point Tile
        => new((int)((Position.X + TankConfig.MapHalfW) / TankConfig.TileSize),
               (int)((Position.Y + TankConfig.MapHalfH) / TankConfig.TileSize));

    public bool OutOfField
        => Position.X < -TankConfig.MapHalfW || Position.Y < -TankConfig.MapHalfH ||
           Position.X >= TankConfig.MapHalfW || Position.Y >= TankConfig.MapHalfH;

    public Rectangle Bounds
        => new((int)(Position.X - 4), (int)(Position.Y - 4), 8, 8);

    /// <summary>把数据同步到显示节点。</summary>
    public void SyncView(ResCenter res)
    {
        View.LocalPosition = Position;
        View.Sprite = ResCenter.Pick(res.Bullet, (int)Direction);
    }
}
