using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>爆炸特效：两张爆炸图快速交替。对应 PixiJS 版的 ExplodeEffect.ts。</summary>
internal sealed class ExplodeEffect
{
    public Vector2 Position;
    public float Time;
    public bool Active = true;

    public void Update(float dt)
    {
        Time += dt;
        if (Time >= TankConfig.ExplodeDuration) Active = false;
    }

    public void Draw(SpriteBatch batch, Vector2 origin, ResCenter res)
    {
        Texture2D? tex = Time < TankConfig.ExplodeDuration * 0.5f ? res.Explode1 : res.Explode2;
        if (tex is null) return;

        Vector2 at = origin + Position;
        batch.Draw(tex, new Vector2(at.X - tex.Width / 2f, at.Y - tex.Height / 2f), Color.White);
    }
}
