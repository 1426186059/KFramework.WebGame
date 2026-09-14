using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 结束界面（对应 PixiJS 的 screens/main/FailScreen.ts）。
/// 继承 KWidget，铺满画布；内部控件用锚点摆位，Resize 时自动按比例重排。
/// </summary>
public sealed class FailScreen : KWidget
{
    private readonly KCanvas _canvas;
    private KLabel _statusLabel = null!;

    /// <summary>构造函数即把自己 addChild 到画布（对应 PixiJS 的 root.addChild(this)）。</summary>
    public FailScreen(KCanvas canvas)
    {
        _canvas = canvas;
        Parent = canvas;   // 挂到画布：否则不会进入 _uiRoot.ChildList，KCanvas.DrawWidget 不会画它
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
    }

    public void Build(SpriteFont font, Action onRetry)
    {
        _statusLabel = new KLabel("GAME OVER", new Color(255, 120, 120), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.40f, 0.40f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        var subLabel = new KLabel("PRESS R / ENTER OR TAP RETRY", new Color(150, 170, 200), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.52f, 0.52f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        var retryButton = new KButton
        {
            Size = new Vector2(320, 84),
            Color = new Color(40, 70, 120),
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.66f, 0.66f),
            AnchorOffset = new KRectangleFOffset(-160, 160, -42, 42),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };
        retryButton.Label.Text = "RETRY";
        retryButton.Label.Font = font;
        retryButton.PointerClickEvent += (_, _) => onRetry();
    }

    /// <summary>胜利/失败文案由场景在切入时设置。</summary>
    public void SetStatus(string text) => _statusLabel.Text = text;

    public void Show() => Parent = _canvas;
    public void Hide() => Parent = null;
}
