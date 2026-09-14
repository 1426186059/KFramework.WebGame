using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>
/// 坦克基类：位置 / 朝向 / 移动 / 开炮的共同逻辑。
/// 对应 PixiJS 版的 Tank_Base.ts。
/// 显示对象用 <see cref="TGameSprite"/>（等价于 PixiJS 的 mSprite），由 <see cref="SyncView"/> 每帧同步。
/// </summary>
internal abstract class TankBase
{
    public Vector2 Position;
    public Dir Direction = Dir.Up;
    public bool Active = true;

    /// <summary>显示节点（等价于 PixiJS 的 mSprite）。通过 Parent 加入 TankRoot 完成 addChild。</summary>
    public readonly TGameSprite View = new();

    protected float FireTimer;

    protected abstract float Speed { get; }
    protected abstract int[] DirBase { get; }
    protected abstract Texture2D?[] GetSprites(ResCenter res);

    /// <summary>推进一帧；返回本帧发射的炮弹，没开炮则为 null。</summary>
    public abstract Shell? Update(float dt, TankLevel level);

    protected TankBase()
    {
        View.Pivot = new Vector2(0.5f);   // 以中心为原点，等价于 PixiJS 的 anchor.set(0.5)
        View.UseNativeSize = true;        // 用纹理原始尺寸，SceneRoot 的缩放再整体施加
    }

    /// <summary>朝指定方向移动，被地形或边界阻挡时返回 false。</summary>
    protected bool Move(Dir dir, float dt, TankLevel level)
    {
        Direction = dir;
        Vector2 delta = TankConfig.DirVectors[(int)dir] * Speed * dt;
        return level.TryMove(ref Position, delta);
    }

    protected Shell SpawnShell(bool fromPlayer)
        => new()
        {
            Position = Position + TankConfig.DirVectors[(int)Direction] * (TankConfig.TankSize / 2f),
            Direction = Direction,
            FromPlayer = fromPlayer,
            Active = true,
        };

    public Rectangle Bounds
    {
        get
        {
            int half = TankConfig.TankSize / 2;
            return new Rectangle((int)Position.X - half, (int)Position.Y - half,
                                 TankConfig.TankSize, TankConfig.TankSize);
        }
    }

    /// <summary>把数据（位置 / 朝向帧）同步到显示节点，等价于 PixiJS 里显示对象跟随实体。</summary>
    public void SyncView(ResCenter res, int animFrame)
    {
        View.LocalPosition = Position;
        View.Sprite = ResCenter.Pick(GetSprites(res), DirBase[(int)Direction] + animFrame);
    }
}
