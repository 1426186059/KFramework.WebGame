using System;
using System.Threading;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
// Color 在 MirEngine / KFramework.MonoGame 都有，这里明确用 KFramework.MonoGame 版本（KImage.Color 同款）。
using Color = KFramework.MonoGame.Color;

/// <summary>
/// 用新 UI 框架（KFramework.MonoGameExtend：KWidget / KImage / KButton / KUIBase）重建的"主对话框"示例。
///
/// 结构严格对齐马里奥例子（StartScreen / GameScreen）：
///   - 本类是 KUIBase，构造时挂到 KUIRoot.Instance.GetCanvas(0) 这个 KCanvas 下；
///   - KCanvas 在 KSceneMgr.DontDestroyOnLoadRoot 下，KSceneMgr.Draw/Update 会自动遍历它（KSceneMgr.cs:85/101）；
///   - KCanvas.Draw 自己 Begin/End 批（带 UI 缩放变换 + PointClamp 采样），所以新 UI 必须画在 DXManager.EndSprite() 之后，
///     由 KCanvas 独立开批，不能塞进 DXManager 已开的批里（否则重复 Begin 报错）。
///
/// 资源取自原始传奇图集（Egret 精灵表），经 Mir2AtlasLibrary 解析成 KSpriteInfo，隐式转成 KSprite 赋给 KImage/KButton.Sprite，
/// 与老 .Lib 体系完全解耦。老 UI 框架（MirImageControl / MirButton）保持不动；新 UI 走这套，两条线并存。
/// </summary>
public sealed class MainDialogUI : KUIBase
{
    public static readonly MainDialogUI Instance = new MainDialogUI();

    // 一张窗口图集（精灵表）。帧名以实际 sheet 为准；缺失时 KImage 自动回退到默认白块，不会崩。
    private const string SheetKey = "resource/assets/gameui4/window-sheet/advance-sheet.json";

    private bool _built;

    public MainDialogUI()
    {
        // 关键：所有 UI 必须挂在 KCanvas 下才能被绘制（参考马里奥 StartScreen / GameScreen）。
        Parent = KUIRoot.Instance.GetCanvas(0);
        Pivot = Vector2.One * 0.5f;
        MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1); // 铺满画布
        AnchorOffset = KRectangleFOffset.Zero;
    }

    /// <summary>异步加载图集并搭建 UI 控件树（幂等）。在 CMain.Init 里 fire-and-forget 调用，不阻塞登录。</summary>
    public async Task LoadAsync(GraphicsDevice device, CancellationToken ct = default)
    {
        if (_built) return;
        try
        {
            var sheet = await Mir2AtlasLibrary.GetSheetAsync(SheetKey, device, ct);
            Build(sheet);
        }
        catch (Exception ex)
        {
            PrintTool.LogError("[MainDialogUI] 加载/构建失败：" + ex);
        }
    }

    /// <summary>构建控件树：背景面板 + 一个图集按钮（自动注册到 KInputMgr 收点击）。</summary>
    private void Build(Mir2AtlasSheet sheet)
    {
        if (_built) return;

        // 背景面板：取图集里的一帧作为窗体背景（帧名请按实际 sheet 调整；这里用 advance-sheet 里的示例帧）。
        var bg = new KImage
        {
            Parent = this,
            UseNativeSize = true,
            Pivot = Vector2.One * 0.5f,
            Anchor = Vector2.One * 0.5f,
            AnchorPosition = Vector2.Zero,
        };
        if (sheet.Frames.TryGetValue("adv_fail", out var bgFrame))
            bg.Sprite = bgFrame;                 // KSpriteInfo → KSprite（隐式转换）

        // 按钮：用图集另一帧。KButton 构造时自动 KInputMgr.Register，开始收指针事件。
        var btn = new KButton
        {
            Parent = this,
            UseNativeSize = true,
            Pivot = Vector2.One * 0.5f,
            Anchor = Vector2.One * 0.5f,
            AnchorPosition = new Vector2(0, 80),  // 在背景中心下方一点（画布原生坐标，随 KCanvas 缩放）
            Label = new KLabel("确定"),
        };
        if (sheet.Frames.TryGetValue("adv_ok", out var btnFrame))
            btn.Sprite = btnFrame;
        btn.PointerClickEvent += (s, e) => PrintTool.Log("[MainDialogUI] 按钮被点击 ✓（新框架事件链路通了）");

        _built = true;
        PrintTool.Log($"[MainDialogUI] UI 已构建：背景 + 按钮（SheetKey={SheetKey}）");
    }
}
