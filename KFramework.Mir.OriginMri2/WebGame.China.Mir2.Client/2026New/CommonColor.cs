using Color = MirEngine.Color;

// 公共 UI 颜色：集中放这里，弹窗/控件共用，避免每处各写一份 FromArgb / #RRGGBB。
public static class CommonColor
{
    /// <summary>模态弹窗（MirMessageBox / MirInputBox）的大背景：全屏半透明黑遮罩。</summary>
    public static readonly Color ModalBackdrop = Color.FromArgb(160, 0, 0, 0);

    // ===== 字体颜色 Color 对象（MirEngine.Color，用于 MirLabel.ForeColour 等）=====
    /// <summary>白色：普通正文文字。</summary>
    public static readonly Color White = Color.White;
    /// <summary>黑色：文字描边（OutLineColour）。</summary>
    public static readonly Color Black = Color.Black;
    /// <summary>灰色：低级任务 / 禁用态文字。</summary>
    public static readonly Color Gray = Color.Gray;
    /// <summary>黄色：标题 / 新任务高亮。</summary>
    public static readonly Color Yellow = Color.Yellow;
    /// <summary>金色：金钱 / 重要数值强调。</summary>
    public static readonly Color Gold = Color.Gold;
    /// <summary>青色：可点击链接文字（常态）。</summary>
    public static readonly Color Cyan = Color.Cyan;
    /// <summary>橙色：链接文字鼠标悬停态。</summary>
    public static readonly Color Orange = Color.Orange;
    /// <summary>蓝色：滚动条 / 控件前景。</summary>
    public static readonly Color Blue = Color.Blue;
    /// <summary>深绿（#008000）：可导航的任务目标文字。</summary>
    public static readonly Color Green = Color.Green;
    /// <summary>亮绿（#32CD32）：任务名 / NPC 名等强调文字。</summary>
    public static readonly Color LimeGreen = Color.LimeGreen;

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
