using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 开始界面（对应 PixiJS 的 screens/main/StartScreen.ts）。
/// 继承 KWidget，铺满画布；内容在构造里拼装，输入自己管：
/// Enter / Space 或点 START → 开始。直接 override Update()（随节点树遍历自驱动），
/// 隐藏（Parent==null，脱离节点树）时 Update 直接 return，天然“自己管自己”。
/// </summary>
public sealed class StartScreen : KWidget
{
    private readonly KCanvas _canvas;
    private readonly Action _onStart;

    public StartScreen(KCanvas canvas, SpriteFont font, Action onStart)
    {
        _canvas = canvas;
        _onStart = onStart;

        Parent = canvas;   // 挂到画布：否则不会进入 ChildList，KCanvas 不会画它
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);

        new KLabel("BATTLE CITY", new Color(140, 210, 255), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.34f, 0.34f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        new KLabel("PRESS SPACE / ENTER OR TAP START", new Color(150, 170, 200), font)
        {
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.46f, 0.46f),
            Pivot = new Vector2(0.5f),
            Parent = this,
        };

        // 固定尺寸控件用"零尺寸锚点 + AnchorOffset 固定像素"摆位，Resize 时不会把 Size 冲成 0。
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
        startButton.PointerClickEvent += (_, _) => _onStart();
    }

    // 与 TS 的 update() 一致：屏幕自己轮询输入；隐藏时不响应。
    public override void Update()
    {
        if (Parent == null) return;
        if (KInputMgr.GetKeyDown(Keys.Enter) || KInputMgr.GetKeyDown(Keys.Space))
            _onStart();
    }

    public void Show() => Parent = _canvas;
    public void Hide() => Parent = null;
}
