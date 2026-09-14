using KFramework.MonoGame;

namespace KFramework.Example2;

/// <summary>
/// 抬头显示：关卡、剩余命数、剩余敌人、状态提示。
/// KFramework.MonoGame 的 SpriteFont 用 Canvas2D 实时光栅化字形并缓存进图集，
/// 因此和精灵共用同一批次，不需要额外的字体资源文件。
/// </summary>
internal sealed class Hud
{
    private readonly SpriteFont _font;
    private readonly SpriteFont _bigFont;

    public Hud(GraphicsDevice device)
    {
        _font = new SpriteFont(device, 16f);
        _bigFont = new SpriteFont(device, 32f);
    }

    public void Draw(SpriteBatch batch, int level, int lives, int enemiesRemaining, int enemiesOnField,
                     string? status, float fieldRight, float fieldTop)
    {
        float x = fieldRight + 16f;
        float y = fieldTop;

        batch.DrawString(_font, $"STAGE {level + 1}", new Vector2(x, y), Color.White);
        batch.DrawString(_font, $"LIFE  {lives}", new Vector2(x, y + 22f), Color.White);
        batch.DrawString(_font, $"ENEMY {enemiesRemaining + enemiesOnField}", new Vector2(x, y + 44f), Color.White);

        if (status is not null)
        {
            Vector2 size = _bigFont.Measure(status);
            batch.DrawString(_bigFont, status,
                new Vector2(fieldRight * 0.5f - size.X * 0.5f, 120f), Color.Yellow);
        }
    }
}
