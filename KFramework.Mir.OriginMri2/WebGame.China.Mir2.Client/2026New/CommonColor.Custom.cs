// 项目自定义的 UI 颜色（非标准命名色）：弹窗遮罩等，以及富文本 <color=...> 用的 Hex 字符串。
public static partial class CommonColor
{
    /// <summary>模态弹窗（MirMessageBox / MirInputBox）的大背景：全屏半透明黑遮罩。</summary>
    public static readonly Color ModalBackdrop = FromArgb(160, 0, 0, 0);

    // ===== 富文本 Hex 字符串（用于 <color=...> 标签）=====
    /// <summary>白色 #FFFFFF：普通正文文字。</summary>
    public const string WhiteHex = "#FFFFFF";
    /// <summary>黑色 #000000：文字描边。</summary>
    public const string BlackHex = "#000000";
    /// <summary>灰色 #808080：低级任务 / 禁用态文字。</summary>
    public const string GrayHex = "#808080";
    /// <summary>黄色 #FFFF00：标题 / 新任务高亮。</summary>
    public const string YellowHex = "#FFFF00";
    /// <summary>金色 #FFD700：金钱 / 重要数值强调。</summary>
    public const string GoldHex = "#FFD700";
    /// <summary>青色 #00FFFF：可点击链接文字。</summary>
    public const string CyanHex = "#00FFFF";
    /// <summary>橙色 #FFA500：链接悬停态。</summary>
    public const string OrangeHex = "#FFA500";
    /// <summary>蓝色 #0000FF：滚动条 / 控件前景。</summary>
    public const string BlueHex = "#0000FF";
    /// <summary>深绿 #008000：可导航的任务目标文字。</summary>
    public const string GreenHex = "#008000";
    /// <summary>亮绿 #32CD32：任务名 / NPC 名等强调文字。</summary>
    public const string LimeGreenHex = "#32CD32";
}
