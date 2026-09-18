using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2.Screens;

/// <summary>
/// 失败/结束界面（对应 PixiJS 的 screens/main/FailScreen.ts，非常简洁）：
/// 黑色全屏背景 + 居中的 UIGameOver.png，3 秒后自动回到开始界面。
/// 与原版一致：没有任何按钮/文字，时间到即切回 Title。
///
/// 节点树控件初始化顺序必须遵守：Parent 先挂 → 再 Pivot → 再 MinMaxAnchor/Anchor。
/// </summary>
public sealed class FailScreen : KWidget
{
    private readonly KCanvas _canvas;
    private readonly Action _onTimeout;

    private float _timer = 3f;
    private bool _fired;

    public FailScreen(KCanvas canvas, ResCenter res, Action onTimeout)
    {
        _canvas = canvas;
        _onTimeout = onTimeout;

        Parent = canvas;   // 先挂到画布：否则布局拿不到父尺寸
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);

        // 黑色全屏背景（对应 Pixi 的黑色 Graphics 矩形）
        new KImage
        {
            Parent = this,
            Sprite = KDefaultRes.DefaultTexture2D,
            Color = Color.Black,
            MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1),
        };

        // 居中失败图（对应 Pixi 的 Texture.from("main/MyRes/Textures/UIGameOver.png")，pivot=center, position=center）
        new KImage
        {
            Parent = this,
            Sprite = res.UIGameOver,
            UseNativeSize = true,
            Pivot = new Vector2(0.5f),
            MinMaxAnchor = KRectangleF.MinMax(0.5f, 0.5f, 0.5f, 0.5f),
        };
    }

    // 与 TS 的 KTween.delayedCall(3, ...) 一致：显示期间倒计时，到 0 自动回到开始界面。
    public override void Update()
    {
        if (Parent == null) return;   // 隐藏时停止计时
        if (_fired) return;

        _timer -= KTime.deltaTime;
        if (_timer <= 0f)
        {
            _fired = true;
            _onTimeout();
        }
    }

    public void Show()
    {
        Parent = _canvas;
        _timer = 3f;     // 每次显示都重置 3 秒计时
        _fired = false;
    }

    public void Hide()
    {
        Parent = null;
        _timer = 3f;
        _fired = false;
    }
}
