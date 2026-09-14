using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>爆炸特效：两张爆炸图快速交替。对应 PixiJS 版的 ExplodeEffect.ts。</summary>
internal sealed class ExplodeEffect
{
    public Vector2 Position;
    public float Time;
    public bool Active = true;

    /// <summary>显示节点（等价于 PixiJS 的 mSprite）。</summary>
    public readonly TGameSprite View = new();

    public ExplodeEffect()
    {
        View.Pivot = new Vector2(0.5f);
        View.UseNativeSize = true;
    }

    public void Update(float dt)
    {
        Time += dt;
        if (Time >= TankConfig.ExplodeDuration) Active = false;
    }

    /// <summary>把数据同步到显示节点。</summary>
    public void SyncView(ResCenter res)
    {
        View.LocalPosition = Position;
        View.Sprite = Time < TankConfig.ExplodeDuration * 0.5f ? res.Explode1 : res.Explode2;
    }
}
