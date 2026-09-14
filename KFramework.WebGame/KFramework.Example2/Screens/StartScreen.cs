using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 开始界面（对应 PixiJS 的 screens/main/StartScreen.ts）。
/// 继承 KWidget，作为铺满画布（KCanvas）的 UI 控件；内部控件用锚点
/// （MinMaxAnchor / AnchorOffset / Pivot）相对本控件 Size 摆位，屏幕尺寸变化时会自动按比例重排。
/// 黑屏之类的问题可直接在这个脚本里排查，不再和战斗逻辑混在一起。
/// </summary>
public sealed class StartScreen : KWidget
{
    private readonly KCanvas _canvas;

    /// <summary>构造函数即把自己 addChild 到画布（对应 PixiJS 的 root.addChild(this)）。</summary>
    public StartScreen(KCanvas canvas)
    {
        _canvas = canvas;
        Parent = canvas;   // 挂到画布：否则不会进入 _uiRoot.ChildList，KCanvas.DrawWidget 不会画它
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
    }

    public void Build(SpriteFont font, Action onStart)
    {
        var titleLabel = new KLabel("BATTLE CITY", new Color(140, 210, 255), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.34f, 0.34f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        var subLabel = new KLabel("PRESS SPACE / ENTER OR TAP START", new Color(150, 170, 200), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.46f, 0.46f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        // 固定尺寸控件用"零尺寸锚点 + AnchorOffset 固定像素"摆位，
        // 这样 Parent 尺寸变化（Resize）时不会把 Size 冲成 0。
        var startButton = new KButton
        {
            Size = new Vector2(320, 84),
            Color = new Color(40, 70, 120),
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.64f, 0.64f),
            AnchorOffset = new KRectangleFOffset(-160, 160, -42, 42),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };
        startButton.Label.Text = "START";
        startButton.Label.Font = font;
        startButton.PointerClickEvent += (_, _) => onStart();
    }

    public void Show() => Parent = _canvas;
    public void Hide() => Parent = null;
}
