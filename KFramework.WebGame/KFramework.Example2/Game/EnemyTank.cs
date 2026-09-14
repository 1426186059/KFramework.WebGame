using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>
/// 敌方坦克：随机游走 + 定期开炮，撞墙立即改向。
/// 对应 PixiJS 版的 Tank_Enemy.ts。
/// </summary>
internal sealed class EnemyTank : TankBase
{
    private static readonly Random Rng = new(20240915);

    private float _turnTimer;

    protected override float Speed => TankConfig.EnemySpeed;
    protected override int[] DirBase => TankConfig.EnemyDirBase;
    protected override Texture2D?[] GetSprites(ResCenter res) => res.Enemy;

    public override Shell? Update(float dt, TankLevel level)
    {
        _turnTimer -= dt;
        if (_turnTimer <= 0f)
        {
            Direction = (Dir)Rng.Next(4);
            _turnTimer = 0.8f + (float)Rng.NextDouble() * 1.6f;
        }

        // 撞墙就立刻重新选方向，避免贴着障碍抖动
        if (!Move(Direction, dt, level)) _turnTimer = 0f;

        FireTimer -= dt;
        if (FireTimer <= 0f)
        {
            FireTimer = 1.2f + (float)Rng.NextDouble() * 1.5f;
            return SpawnShell(false);
        }
        return null;
    }
}
