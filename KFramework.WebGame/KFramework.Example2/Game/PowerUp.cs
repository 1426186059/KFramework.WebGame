using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>道具种类，下标与 Bonus 图集的帧序一致。</summary>
internal enum PowerUpKind : byte
{
    /// <summary>手雷：清全屏敌人</summary>
    Grenade = 0,
    /// <summary>头盔：短暂无敌</summary>
    Helmet,
    /// <summary>铲子：老窝周围筑铁</summary>
    Shovel,
    /// <summary>星星：提升火力</summary>
    Star,
    /// <summary>坦克：加一条命</summary>
    Tank,
    /// <summary>时钟：冻结敌人</summary>
    Clock,
}

/// <summary>掉落在战场上的道具。对应 PixiJS 版里散落各处的 Bonus 处理。</summary>
internal sealed class PowerUp
{
    private const float LifeTime = 12f;
    private const float BlinkFrom = 3f;

    public Vector2 Position;
    public PowerUpKind Kind;
    public float Life = LifeTime;
    public bool Active = true;

    /// <summary>显示节点（等价于 PixiJS 的 mSprite）。</summary>
    public readonly TGameSprite View = new();

    public PowerUp()
    {
        View.Pivot = new Vector2(0.5f);
        View.UseNativeSize = true;
    }

    public void Update(float dt)
    {
        Life -= dt;
        if (Life <= 0f) Active = false;
    }

    /// <summary>最后 3 秒开始闪烁提示即将消失。</summary>
    private bool BlinkOff => Life < BlinkFrom && ((int)(Life * 8f) % 2) == 0;

    public Rectangle Bounds
        => new((int)Position.X - TankConfig.TileSize / 2,
               (int)Position.Y - TankConfig.TileSize / 2,
               TankConfig.TileSize, TankConfig.TileSize);

    /// <summary>把数据同步到显示节点；返回 false 表示本帧应隐藏（闪烁灭帧）。</summary>
    public bool SyncView(ResCenter res)
    {
        if (BlinkOff) return false;
        View.LocalPosition = Position;
        View.Sprite = ResCenter.Pick(res.Bonus, (int)Kind);
        return true;
    }
}
