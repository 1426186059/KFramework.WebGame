using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 坦克基类：位置 / 朝向 / 移动 / 开炮的共同逻辑。
/// 对应 PixiJS 版的 Tank_Base.ts。
/// </summary>
internal abstract class TankBase
{
    public Vector2 Position;
    public Dir Direction = Dir.Up;
    public bool Active = true;

    protected float FireTimer;

    protected abstract float Speed { get; }
    protected abstract int[] DirBase { get; }
    protected abstract Texture2D?[] GetSprites(ResCenter res);

    /// <summary>推进一帧；返回本帧发射的炮弹，没开炮则为 null。</summary>
    public abstract Shell? Update(float dt, TankLevel level);

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

    public void Draw(SpriteBatch batch, Vector2 origin, ResCenter res, int animFrame)
    {
        if (!Active) return;

        Texture2D? tex = ResCenter.Pick(GetSprites(res), DirBase[(int)Direction] + animFrame);
        if (tex is null) return;

        Vector2 at = origin + Position;
        batch.Draw(tex, new Vector2(at.X - tex.Width / 2f, at.Y - tex.Height / 2f), Color.White);
    }
}
