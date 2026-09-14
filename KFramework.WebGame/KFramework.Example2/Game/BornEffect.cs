using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>出生特效：四帧循环，坦克复活 / 敌人出现时播放。对应 PixiJS 版的 BornEffect.ts。</summary>
internal sealed class BornEffect
{
    public Vector2 Position;
    public float Time;
    public bool Active = true;

    /// <summary>显示节点（等价于 PixiJS 的 mSprite）。</summary>
    public readonly TGameSprite View = new();

    public BornEffect()
    {
        View.Pivot = new Vector2(0.5f);
        View.UseNativeSize = true;
    }

    public void Update(float dt)
    {
        Time += dt;
        if (Time >= TankConfig.BornDuration) Active = false;
    }

    /// <summary>把数据同步到显示节点。</summary>
    public void SyncView(ResCenter res)
    {
        View.LocalPosition = Position;
        View.Sprite = ResCenter.Pick(res.Born, (int)(Time / TankConfig.BornDuration * 4f));
    }
}
