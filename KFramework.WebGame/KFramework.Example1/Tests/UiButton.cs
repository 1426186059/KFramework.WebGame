using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests;

/// <summary>
/// 测试页共用的小按钮：纯色矩形 + 一行文字。鼠标悬停变亮，<see cref="Active"/> 表示开关处于「开」。
/// 统一由 <see cref="TestSceneBase"/> 负责排布、命中与绘制（BeginButtons / AddUiButton / ClickButtons / DrawButtons）。
/// </summary>
public sealed class UiButton
{
    /// <summary>按钮高度（所有测试页统一）。</summary>
    public const int DefaultHeight = 34;

    /// <summary>同一行按钮之间的间隔。</summary>
    public const int Gap = 10;

    public UiButton(Rectangle rect, string label, Action click, bool active = false)
    {
        Rect = rect;
        Label = label;
        Click = click;
        Active = active;
    }

    public Rectangle Rect { get; }

    public string Label { get; }

    /// <summary>点击时执行的操作。</summary>
    public Action Click { get; }

    /// <summary>开关类按钮当前是否处于「开」。</summary>
    public bool Active { get; }

    /// <summary>按文字长度算按钮宽度（文字宽 + 左右内边距）。</summary>
    public static int MeasureWidth(IFont font, string label) => (int)MathF.Ceiling(font.Measure(label).X) + 28;

    /// <summary>绘制按钮（必须在 SpriteBatch.Begin / End 之间）。</summary>
    public void Draw(SpriteBatch batch, IFont font, Texture2D pixel)
    {
        bool hover = Rect.Contains(Input_Mouse.Position);

        Color bg = Active ? new Color(28, 84, 152) : new Color(30, 36, 54);
        if (hover)
            bg = new Color(Math.Min(255, bg.R + 26), Math.Min(255, bg.G + 26), Math.Min(255, bg.B + 26));

        batch.Draw(pixel, Rect, bg);
        // 左侧色条：开 → 亮蓝，关 → 灰
        batch.Draw(pixel, new Rectangle(Rect.X, Rect.Y, 4, Rect.Height),
                   Active ? new Color(96, 190, 255) : new Color(70, 82, 110));
        batch.DrawString(font, Label, new Vector2(Rect.X + 12, Rect.Y + 7),
                         Active ? Color.White : new Color(200, 212, 232));
    }
}
