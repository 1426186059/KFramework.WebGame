using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 开始界面（对应 PixiJS 的 screens/main/StartScreen.ts，非常简洁）：
/// 黑色全屏背景 + 居中的 Title.png + 一个玩家坦克光标(Player1_8) 在两选项间移动，Enter 开始。
/// 输入自己管：隐藏（Parent==null）时 Update 直接 return，天然“自己管自己”。
///
/// 节点树控件初始化顺序必须遵守：Parent 先挂 → 再 Pivot → 再 MinMaxAnchor/Anchor，
/// 否则布局时父节点尺寸/Pivot 还是空值，控件会得到错误的矩形。
/// </summary>
public sealed class StartScreen : KWidget
{
    private readonly KCanvas _canvas;
    private readonly Action _onStart;
    private readonly KImage m_Tank;

    private bool _option;   // false=opt1(上一档), true=opt2(下一档)

    private Vector2 opt1Pos;
    private Vector2 opt2Pos;

    public StartScreen(KCanvas canvas, ResCenter res, Action onStart)
    {
        _canvas = canvas;
        _onStart = onStart;

        Parent = canvas;   // 先挂到画布：否则布局拿不到父尺寸
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
        AnchorOffset = KRectangleFOffset.Zero;
        AnchorPosition = Vector2.Zero;

        // 黑色全屏背景（对应 Pixi 的黑色 Graphics 矩形）
        new KImage
        {
            Parent = this,
            Sprite = KDefaultRes.DefaultTexture2D,
            Color = Color.Black,
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1),
            AnchorOffset = KRectangleFOffset.Zero,
            AnchorPosition = Vector2.Zero
        };

        // 居中标题图（对应 Pixi 的 Texture.from("main/MyRes/Textures/Title.png")，pivot=center, position=center）
        new KImage
        {
            Parent = this,
            Sprite = res.Title,
            UseNativeSize = true,
            Pivot = new Vector2(0.5f, 0.5f),
            Anchor = new Vector2(0.5f, 0.5f),
            AnchorPosition = Vector2.Zero
        };

        this.opt1Pos = new Vector2(- 100, 70);
        this.opt2Pos = new Vector2(- 100, 100);
        
        // 玩家坦克光标（对应 Pixi 的 Texture.from("Player1_8")）
        m_Tank = new KImage
        {
            Parent = this,
            Sprite = res.Player[8],
            UseNativeSize = true,
            Pivot = new Vector2(0.5f, 0.5f),
            Anchor = new Vector2(0.5f, 0.5f),
            AnchorPosition = this.opt1Pos
        };


       
    }

    // 与 TS 的 update() 一致：屏幕自己轮询输入；隐藏时不响应。
    public override void Update()
    {
        if (Parent == null) return;

        if (KInputMgr.GetKeyDown(Keys.W) || KInputMgr.GetKeyDown(Keys.Up))
        {
            this.m_Tank.AnchorPosition = this.opt1Pos;
        }
        else if (KInputMgr.GetKeyDown(Keys.S) || KInputMgr.GetKeyDown(Keys.Down))
        {
            this.m_Tank.AnchorPosition = this.opt2Pos;
        }

        if (KInputMgr.GetKeyDown(Keys.Enter))
        {
            this.Dispose();
            _onStart();
        }
    }

    public void Show()
    {
        this.activeSelf = true;
    }

    public void Hide()
    {
        this.activeSelf = false;
    }
}
