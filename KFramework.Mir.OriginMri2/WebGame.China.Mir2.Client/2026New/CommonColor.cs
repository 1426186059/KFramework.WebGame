using Color = MirEngine.Color;

// 公共 UI 颜色：集中放这里，弹窗/控件共用，避免每处各写一份 FromArgb。
public static class CommonColor
{
    /// <summary>模态弹窗（MirMessageBox / MirInputBox）的大背景：全屏半透明黑遮罩。</summary>
    public static readonly Color ModalBackdrop = Color.FromArgb(160, 0, 0, 0);
}
