using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>出生特效：四帧循环，坦克复活 / 敌人出现时播放。对应 PixiJS 版的 BornEffect.ts。</summary>
internal sealed class BornEffect
{
    public Vector2 Position;
    public float Time;
    public bool Active = true;

    public void Update(float dt)
    {
        Time += dt;
        if (Time >= TankConfig.BornDuration) Active = false;
    }

    public void Draw(SpriteBatch batch, Vector2 origin, ResCenter res)
    {
        int frame = (int)(Time / TankConfig.BornDuration * 4f);
        Texture2D? tex = ResCenter.Pick(res.Born, frame);
        if (tex is null) return;

        Vector2 at = origin + Position;
        batch.Draw(tex, new Vector2(at.X - tex.Width / 2f, at.Y - tex.Height / 2f), Color.White);
    }
}
