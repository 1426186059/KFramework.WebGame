using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 结束界面（对应 PixiJS 的 screens/main/FailScreen.ts）。
/// 输入自己管：R / Enter / Space 或点 RETRY → 重开。隐藏时 Update 直接 return。
/// </summary>
public sealed class FailScreen : KWidget
{
    private readonly KCanvas _canvas;
    private readonly Action _onRetry;
    private readonly KLabel _statusLabel;

    public FailScreen(KCanvas canvas, SpriteFont font, Action onRetry)
    {
        _canvas = canvas;
        _onRetry = onRetry;

        Parent = canvas;   // 挂到画布：否则不会进入 ChildList，KCanvas 不会画它
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);

        _statusLabel = new KLabel("GAME OVER", new Color(255, 120, 120), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.40f, 0.40f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        new KLabel("PRESS R / ENTER OR TAP RETRY", new Color(150, 170, 200), font)
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
        retryButton.PointerClickEvent += (_, _) => _onRetry();
    }

    // 与 TS 的 update() 一致：屏幕自己轮询输入；隐藏时不响应。
    public override void Update()
    {
        if (Parent == null) return;
        if (KInputMgr.GetKeyDown(Keys.R) || KInputMgr.GetKeyDown(Keys.Enter) || KInputMgr.GetKeyDown(Keys.Space))
            _onRetry();
    }

    /// <summary>胜利/失败文案由场景在切入时设置。</summary>
    public void SetStatus(string text) => _statusLabel.Text = text;

    public void Show() => Parent = _canvas;
    public void Hide() => Parent = null;
}
