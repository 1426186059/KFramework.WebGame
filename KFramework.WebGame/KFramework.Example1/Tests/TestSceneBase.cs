using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests;

/// <summary>
/// 测试模块场景基类：统一标题栏 + 左上角「返回」（Esc 或点击），
/// 子类只重写 <see cref="Title"/> 与 <see cref="DrawBody"/> 即可。
/// </summary>
public abstract class TestSceneBase : KSceneBase
{
    /// <summary>本模块标题（显示在返回按钮右侧）。</summary>
    public abstract string Title { get; }

    protected GraphicsDevice Device => KSceneMgr.Game.GraphicsDevice;

    protected SpriteBatch Batch => KSceneMgr.SpriteBatch;

    /// <summary>界面字体（系统字体，20px）。</summary>
    protected IFont Font => KDefaultRes.DefaultSpriteFont;

    private readonly Rectangle _backRect = new(20, 16, 96, 34);

    // ---- 按钮（业务页用 BeginButtons + AddUiButton 登记，基类负责命中与绘制） ----

    private readonly List<UiButton> _buttons = [];
    private int _buttonX;
    private int _buttonY;
    private int _contentTop;

    /// <summary>按钮区之后正文的起始 y（由 <see cref="BeginButtons"/> / <see cref="AddUiButton"/> 算出）。</summary>
    protected float ContentTop => _contentTop;

    /// <summary>开始布置本帧的按钮（会清空上一帧的登记）。</summary>
    /// <param name="top">第一行按钮的顶端 y。</param>
    protected void BeginButtons(int top)
    {
        _buttons.Clear();
        _buttonX = 28;
        _buttonY = top;
        _contentTop = top;
    }

    /// <summary>按行排一个按钮；超出画布右边自动换行。<paramref name="active"/> 表示开关处于「开」。</summary>
    protected void AddUiButton(string label, Action click, bool active = false)
    {
        int width = UiButton.MeasureWidth(Font, label);
        if (_buttonX + width > Device.Viewport.Width - 20 && _buttonX > 28)
        {
            _buttonX = 28;
            _buttonY += UiButton.DefaultHeight + UiButton.Gap;
        }

        _buttons.Add(new UiButton(new Rectangle(_buttonX, _buttonY, width, UiButton.DefaultHeight), label, click, active));
        _buttonX += width + UiButton.Gap;
        _contentTop = _buttonY + UiButton.DefaultHeight + 18;
    }

    /// <summary>处理鼠标左键点击；应在登记完按钮之后调用，返回是否命中了某个按钮。</summary>
    protected bool ClickButtons()
    {
        if (!Input_Mouse.GetButtonDown(MouseButton.Left)) return false;

        Vector2 p = Input_Mouse.Position;
        foreach (UiButton button in _buttons)
        {
            if (button.Rect.Contains(p))
            {
                button.Click();
                return true;
            }
        }
        return false;
    }

    /// <summary>绘制已登记的按钮（必须在 SpriteBatch.Begin / End 之间）。</summary>
    protected void DrawButtons(SpriteBatch batch)
    {
        foreach (UiButton button in _buttons)
            button.Draw(batch, Font, KDefaultRes.DefaultTexture2D);
    }

    public override void Update()
    {
        if (Input_KeyBoard.GetKeyDown(Keys.Escape))
        {
            TestMainScene.Open();
            return;
        }

        if (Input_Mouse.GetButtonDown(MouseButton.Left) && _backRect.Contains(Input_Mouse.Position))
            TestMainScene.Open();
    }

    public override void Draw()
    {
        SpriteBatch batch = Batch;
        batch.Begin();

        // 返回按钮
        batch.Draw(KDefaultRes.DefaultTexture2D, _backRect, new Color(38, 52, 92));
        batch.DrawString(Font, "← 返回", new Vector2(_backRect.X + 12, _backRect.Y + 6), new Color(170, 210, 255));
        // 标题
        batch.DrawString(Font, Title, new Vector2(_backRect.Right + 18, _backRect.Y + 4), new Color(126, 200, 255));

        DrawBody(batch, new Vector2(28f, 68f));

        batch.End();
    }

    /// <summary>绘制模块内容（已在 Begin / End 之间）。</summary>
    protected abstract void DrawBody(SpriteBatch batch, Vector2 origin);

    /// <summary>画一行文本；字体尚未就绪时画占位提示。返回本行占用的高度（含行距）。</summary>
    protected float DrawLine(SpriteBatch batch, IFont? font, string text, Vector2 position, Color color)
    {
        IFont used = font ?? Font;
        if (position.Y < Device.Viewport.Height - 8)
        {
            batch.DrawString(used, font is null ? text + "  [字体未就绪]" : text, position, color);
        }
        return used.LineSpacing + 6f;
    }

    /// <summary>画一段标题（黄色），返回占用高度。</summary>
    protected float DrawSection(SpriteBatch batch, string title, Vector2 position)
    {
        if (position.Y < Device.Viewport.Height - 8)
            batch.DrawString(Font, title, position, new Color(255, 206, 110));
        return Font.LineSpacing + 10f;
    }

    /// <summary>在同一行里接着画一段文本，并把光标 x 往后推；返回新的 x。</summary>
    protected float DrawInline(SpriteBatch batch, IFont font, string text, float x, float y, Color color)
    {
        if (y < Device.Viewport.Height - 8)
            batch.DrawString(font, text, new Vector2(x, y), color);
        return x + font.Measure(text).X + 16f;
    }

    /// <summary>画一个纯色矩形（面板 / 按钮底）。</summary>
    protected void DrawRect(SpriteBatch batch, Rectangle rect, Color color)
        => batch.Draw(KDefaultRes.DefaultTexture2D, rect, color);
}
