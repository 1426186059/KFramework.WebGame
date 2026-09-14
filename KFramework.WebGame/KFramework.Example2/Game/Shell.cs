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

    private float Speed => TankConfig.BulletSpeed + SpeedBonus * 40f;

    public void Update(float dt)
        => Position += TankConfig.DirVectors[(int)Direction] * Speed * dt;

    /// <summary>子弹所在的格子坐标。</summary>
    public Point Tile
        => new((int)(Position.X / TankConfig.TileSize), (int)(Position.Y / TankConfig.TileSize));

    public bool OutOfField
        => Position.X < 0 || Position.Y < 0 ||
           Position.X >= TankConfig.MapWidth * TankConfig.TileSize ||
           Position.Y >= TankConfig.MapHeight * TankConfig.TileSize;

    public Rectangle Bounds
        => new((int)(Position.X - 4), (int)(Position.Y - 4), 8, 8);

    public void Draw(SpriteBatch batch, Vector2 origin, ResCenter res)
    {
        Texture2D? tex = ResCenter.Pick(res.Bullet, (int)Direction);
        if (tex is null) return;

        Vector2 at = origin + Position;
        batch.Draw(tex, new Vector2(at.X - tex.Width / 2f, at.Y - tex.Height / 2f), Color.White);
    }
}
