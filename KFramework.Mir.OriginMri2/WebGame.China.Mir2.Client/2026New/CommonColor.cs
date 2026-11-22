// 公共 UI 颜色集中入口：按用途拆成 partial 文件——
//   CommonColor.cs         基础 FromArgb 重载
//   CommonColor.Winform.cs  WinForms/System.Drawing 标准命名色 + FromName
//   CommonColor.Custom.cs   项目自定义 UI 色 + 富文本 Hex
//   CommonColor.OriginMir2.cs 原始传奇专用配色
// 弹窗/控件共用，避免每处各写一份 FromArgb / #RRGGBB。
public static partial class CommonColor
{
    private static byte B(int v) => (byte)KFramework.MonoGame.MathHelper.Clamp(v, 0, 255);
    /// <summary>alpha 在前的 4 参重载（对齐原 FromArgb(a,r,g,b)），注意引擎构造子是 r,g,b,a。</summary>
    public static Color FromArgb(int alpha, int red, int green, int blue)
        => new Color(B(red), B(green), B(blue), B(alpha));
    /// <summary>不透明 3 参重载（对齐原 FromArgb(r,g,b)）。</summary>
    public static Color FromArgb(int red, int green, int blue) => FromArgb(255, red, green, blue);
}
