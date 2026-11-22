using System.Collections.Generic;
// 公共 UI 颜色：集中放这里，弹窗/控件共用，避免每处各写一份 FromArgb / #RRGGBB。
public static class CommonColor
{
    private static byte B(int v) => (byte)KFramework.MonoGame.MathHelper.Clamp(v, 0, 255);

    // ===== 颜色构造工具（原 MirEngine.Color.FromArgb，引擎只保留 FromArgb(uint)）=====
    /// <summary>从 0xAARRGGBB 构造（uint 重载，转发引擎）。</summary>
    public static Color FromArgb(uint argb) => Color.FromArgb(argb);
    /// <summary>alpha 在前的 4 参重载（对齐原 FromArgb(a,r,g,b)），注意引擎构造子是 r,g,b,a。</summary>
    public static Color FromArgb(int alpha, int red, int green, int blue)
        => new Color(B(red), B(green), B(blue), B(alpha));
    /// <summary>不透明 3 参重载（对齐原 FromArgb(r,g,b)）。</summary>
    public static Color FromArgb(int red, int green, int blue) => FromArgb(255, red, green, blue);

    /// <summary>模态弹窗（MirMessageBox / MirInputBox）的大背景：全屏半透明黑遮罩。</summary>
    public static readonly Color ModalBackdrop = FromArgb(160, 0, 0, 0);

    // ===== 命名色（原 KFramework.MonoGame.Color 常量统一迁移至此，KFramework.MonoGame.Color 仅保留同名别名）=====
    /// <summary>空颜色（IsEmpty=true，全 0 值）。</summary>
    public static readonly Color Empty = default;
    /// <summary>全透明（Alpha=0）：不显示任何像素。</summary>
    public static readonly Color Transparent = FromArgb(0, 255, 255, 255);
    /// <summary>#F0F8FF 颜色</summary>
    public static readonly Color AliceBlue = FromArgb(255, 240, 248, 255);
    /// <summary>#FAEBD7 颜色</summary>
    public static readonly Color AntiqueWhite = FromArgb(255, 250, 235, 215);
    /// <summary>#00FFFF 颜色</summary>
    public static readonly Color Aqua = FromArgb(255, 0, 255, 255);
    /// <summary>#7FFFD4 颜色</summary>
    public static readonly Color Aquamarine = FromArgb(255, 127, 255, 212);
    /// <summary>#F0FFFF 颜色</summary>
    public static readonly Color Azure = FromArgb(255, 240, 255, 255);
    /// <summary>#F5F5DC 颜色</summary>
    public static readonly Color Beige = FromArgb(255, 245, 245, 220);
    /// <summary>#FFE4C4 颜色</summary>
    public static readonly Color Bisque = FromArgb(255, 255, 228, 196);
    /// <summary>黑色：文字描边（OutLineColour）</summary>
    public static readonly Color Black = FromArgb(255, 0, 0, 0);
    /// <summary>#FFEBCD 颜色</summary>
    public static readonly Color BlanchedAlmond = FromArgb(255, 255, 235, 205);
    /// <summary>蓝色：滚动条 / 控件前景</summary>
    public static readonly Color Blue = FromArgb(255, 0, 0, 255);
    /// <summary>#8A2BE2 颜色</summary>
    public static readonly Color BlueViolet = FromArgb(255, 138, 43, 226);
    /// <summary>#A52A2A 颜色</summary>
    public static readonly Color Brown = FromArgb(255, 165, 42, 42);
    /// <summary>#DEB887 颜色</summary>
    public static readonly Color BurlyWood = FromArgb(255, 222, 184, 135);
    /// <summary>#5F9EA0 颜色</summary>
    public static readonly Color CadetBlue = FromArgb(255, 95, 158, 160);
    /// <summary>#7FFF00 颜色</summary>
    public static readonly Color Chartreuse = FromArgb(255, 127, 255, 0);
    /// <summary>#D2691E 颜色</summary>
    public static readonly Color Chocolate = FromArgb(255, 210, 105, 30);
    /// <summary>#FF7F50 颜色</summary>
    public static readonly Color Coral = FromArgb(255, 255, 127, 80);
    /// <summary>#6495ED 颜色</summary>
    public static readonly Color CornflowerBlue = FromArgb(255, 100, 149, 237);
    /// <summary>#FFF8DC 颜色</summary>
    public static readonly Color Cornsilk = FromArgb(255, 255, 248, 220);
    /// <summary>#DC143C 颜色</summary>
    public static readonly Color Crimson = FromArgb(255, 220, 20, 60);
    /// <summary>青色：可点击链接文字（常态）</summary>
    public static readonly Color Cyan = FromArgb(255, 0, 255, 255);
    /// <summary>#00008B 颜色</summary>
    public static readonly Color DarkBlue = FromArgb(255, 0, 0, 139);
    /// <summary>#008B8B 颜色</summary>
    public static readonly Color DarkCyan = FromArgb(255, 0, 139, 139);
    /// <summary>#B8860B 颜色</summary>
    public static readonly Color DarkGoldenrod = FromArgb(255, 184, 134, 11);
    /// <summary>#A9A9A9 颜色</summary>
    public static readonly Color DarkGray = FromArgb(255, 169, 169, 169);
    /// <summary>#006400 颜色</summary>
    public static readonly Color DarkGreen = FromArgb(255, 0, 100, 0);
    /// <summary>#BDB76B 颜色</summary>
    public static readonly Color DarkKhaki = FromArgb(255, 189, 183, 107);
    /// <summary>#8B008B 颜色</summary>
    public static readonly Color DarkMagenta = FromArgb(255, 139, 0, 139);
    /// <summary>#556B2F 颜色</summary>
    public static readonly Color DarkOliveGreen = FromArgb(255, 85, 107, 47);
    /// <summary>#FF8C00 颜色</summary>
    public static readonly Color DarkOrange = FromArgb(255, 255, 140, 0);
    /// <summary>#9932CC 颜色</summary>
    public static readonly Color DarkOrchid = FromArgb(255, 153, 50, 204);
    /// <summary>#8B0000 颜色</summary>
    public static readonly Color DarkRed = FromArgb(255, 139, 0, 0);
    /// <summary>#E9967A 颜色</summary>
    public static readonly Color DarkSalmon = FromArgb(255, 233, 150, 122);
    /// <summary>#8FBC8F 颜色</summary>
    public static readonly Color DarkSeaGreen = FromArgb(255, 143, 188, 143);
    /// <summary>#483D8B 颜色</summary>
    public static readonly Color DarkSlateBlue = FromArgb(255, 72, 61, 139);
    /// <summary>#2F4F4F 颜色</summary>
    public static readonly Color DarkSlateGray = FromArgb(255, 47, 79, 79);
    /// <summary>#00CED1 颜色</summary>
    public static readonly Color DarkTurquoise = FromArgb(255, 0, 206, 209);
    /// <summary>#9400D3 颜色</summary>
    public static readonly Color DarkViolet = FromArgb(255, 148, 0, 211);
    /// <summary>#FF1493 颜色</summary>
    public static readonly Color DeepPink = FromArgb(255, 255, 20, 147);
    /// <summary>#00BFFF 颜色</summary>
    public static readonly Color DeepSkyBlue = FromArgb(255, 0, 191, 255);
    /// <summary>#696969 颜色</summary>
    public static readonly Color DimGray = FromArgb(255, 105, 105, 105);
    /// <summary>#1E90FF 颜色</summary>
    public static readonly Color DodgerBlue = FromArgb(255, 30, 144, 255);
    /// <summary>#B22222 颜色</summary>
    public static readonly Color Firebrick = FromArgb(255, 178, 34, 34);
    /// <summary>#FFFAF0 颜色</summary>
    public static readonly Color FloralWhite = FromArgb(255, 255, 250, 240);
    /// <summary>#228B22 颜色</summary>
    public static readonly Color ForestGreen = FromArgb(255, 34, 139, 34);
    /// <summary>#FF00FF 颜色</summary>
    public static readonly Color Fuchsia = FromArgb(255, 255, 0, 255);
    /// <summary>#DCDCDC 颜色</summary>
    public static readonly Color Gainsboro = FromArgb(255, 220, 220, 220);
    /// <summary>#F8F8FF 颜色</summary>
    public static readonly Color GhostWhite = FromArgb(255, 248, 248, 255);
    /// <summary>金色：金钱 / 重要数值强调</summary>
    public static readonly Color Gold = FromArgb(255, 255, 215, 0);
    /// <summary>#DAA520 颜色</summary>
    public static readonly Color Goldenrod = FromArgb(255, 218, 165, 32);
    /// <summary>灰色：低级任务 / 禁用态文字</summary>
    public static readonly Color Gray = FromArgb(255, 128, 128, 128);
    /// <summary>深绿（#008000）：可导航的任务目标文字</summary>
    public static readonly Color Green = FromArgb(255, 0, 128, 0);
    /// <summary>#ADFF2F 颜色</summary>
    public static readonly Color GreenYellow = FromArgb(255, 173, 255, 47);
    /// <summary>#F0FFF0 颜色</summary>
    public static readonly Color Honeydew = FromArgb(255, 240, 255, 240);
    /// <summary>#FF69B4 颜色</summary>
    public static readonly Color HotPink = FromArgb(255, 255, 105, 180);
    /// <summary>#CD5C5C 颜色</summary>
    public static readonly Color IndianRed = FromArgb(255, 205, 92, 92);
    /// <summary>#4B0082 颜色</summary>
    public static readonly Color Indigo = FromArgb(255, 75, 0, 130);
    /// <summary>#FFFFF0 颜色</summary>
    public static readonly Color Ivory = FromArgb(255, 255, 255, 240);
    /// <summary>#F0E68C 颜色</summary>
    public static readonly Color Khaki = FromArgb(255, 240, 230, 140);
    /// <summary>#E6E6FA 颜色</summary>
    public static readonly Color Lavender = FromArgb(255, 230, 230, 250);
    /// <summary>#FFF0F5 颜色</summary>
    public static readonly Color LavenderBlush = FromArgb(255, 255, 240, 245);
    /// <summary>#7CFC00 颜色</summary>
    public static readonly Color LawnGreen = FromArgb(255, 124, 252, 0);
    /// <summary>#FFFACD 颜色</summary>
    public static readonly Color LemonChiffon = FromArgb(255, 255, 250, 205);
    /// <summary>#ADD8E6 颜色</summary>
    public static readonly Color LightBlue = FromArgb(255, 173, 216, 230);
    /// <summary>#F08080 颜色</summary>
    public static readonly Color LightCoral = FromArgb(255, 240, 128, 128);
    /// <summary>#E0FFFF 颜色</summary>
    public static readonly Color LightCyan = FromArgb(255, 224, 255, 255);
    /// <summary>#FAFAD2 颜色</summary>
    public static readonly Color LightGoldenrodYellow = FromArgb(255, 250, 250, 210);
    /// <summary>#D3D3D3 颜色</summary>
    public static readonly Color LightGray = FromArgb(255, 211, 211, 211);
    /// <summary>#90EE90 颜色</summary>
    public static readonly Color LightGreen = FromArgb(255, 144, 238, 144);
    /// <summary>#FFB6C1 颜色</summary>
    public static readonly Color LightPink = FromArgb(255, 255, 182, 193);
    /// <summary>#FFA07A 颜色</summary>
    public static readonly Color LightSalmon = FromArgb(255, 255, 160, 122);
    /// <summary>#20B2AA 颜色</summary>
    public static readonly Color LightSeaGreen = FromArgb(255, 32, 178, 170);
    /// <summary>#87CEFA 颜色</summary>
    public static readonly Color LightSkyBlue = FromArgb(255, 135, 206, 250);
    /// <summary>#778899 颜色</summary>
    public static readonly Color LightSlateGray = FromArgb(255, 119, 136, 153);
    /// <summary>#B0C4DE 颜色</summary>
    public static readonly Color LightSteelBlue = FromArgb(255, 176, 196, 222);
    /// <summary>#FFFFE0 颜色</summary>
    public static readonly Color LightYellow = FromArgb(255, 255, 255, 224);
    /// <summary>#00FF00 颜色</summary>
    public static readonly Color Lime = FromArgb(255, 0, 255, 0);
    /// <summary>亮绿（#32CD32）：任务名 / NPC 名等强调文字</summary>
    public static readonly Color LimeGreen = FromArgb(255, 50, 205, 50);
    /// <summary>#FAF0E6 颜色</summary>
    public static readonly Color Linen = FromArgb(255, 250, 240, 230);
    /// <summary>#FF00FF 颜色</summary>
    public static readonly Color Magenta = FromArgb(255, 255, 0, 255);
    /// <summary>#800000 颜色</summary>
    public static readonly Color Maroon = FromArgb(255, 128, 0, 0);
    /// <summary>#66CDAA 颜色</summary>
    public static readonly Color MediumAquamarine = FromArgb(255, 102, 205, 170);
    /// <summary>#0000CD 颜色</summary>
    public static readonly Color MediumBlue = FromArgb(255, 0, 0, 205);
    /// <summary>#BA55D3 颜色</summary>
    public static readonly Color MediumOrchid = FromArgb(255, 186, 85, 211);
    /// <summary>#9370DB 颜色</summary>
    public static readonly Color MediumPurple = FromArgb(255, 147, 112, 219);
    /// <summary>#3CB371 颜色</summary>
    public static readonly Color MediumSeaGreen = FromArgb(255, 60, 179, 113);
    /// <summary>#7B68EE 颜色</summary>
    public static readonly Color MediumSlateBlue = FromArgb(255, 123, 104, 238);
    /// <summary>#00FA9A 颜色</summary>
    public static readonly Color MediumSpringGreen = FromArgb(255, 0, 250, 154);
    /// <summary>#48D1CC 颜色</summary>
    public static readonly Color MediumTurquoise = FromArgb(255, 72, 209, 204);
    /// <summary>#C71585 颜色</summary>
    public static readonly Color MediumVioletRed = FromArgb(255, 199, 21, 133);
    /// <summary>#191970 颜色</summary>
    public static readonly Color MidnightBlue = FromArgb(255, 25, 25, 112);
    /// <summary>#F5FFFA 颜色</summary>
    public static readonly Color MintCream = FromArgb(255, 245, 255, 250);
    /// <summary>#FFE4E1 颜色</summary>
    public static readonly Color MistyRose = FromArgb(255, 255, 228, 225);
    /// <summary>#FFE4B5 颜色</summary>
    public static readonly Color Moccasin = FromArgb(255, 255, 228, 181);
    /// <summary>#FFDEAD 颜色</summary>
    public static readonly Color NavajoWhite = FromArgb(255, 255, 222, 173);
    /// <summary>#000080 颜色</summary>
    public static readonly Color Navy = FromArgb(255, 0, 0, 128);
    /// <summary>#FDF5E6 颜色</summary>
    public static readonly Color OldLace = FromArgb(255, 253, 245, 230);
    /// <summary>#808000 颜色</summary>
    public static readonly Color Olive = FromArgb(255, 128, 128, 0);
    /// <summary>#6B8E23 颜色</summary>
    public static readonly Color OliveDrab = FromArgb(255, 107, 142, 35);
    /// <summary>橙色：链接文字鼠标悬停态</summary>
    public static readonly Color Orange = FromArgb(255, 255, 165, 0);
    /// <summary>#FF4500 颜色</summary>
    public static readonly Color OrangeRed = FromArgb(255, 255, 69, 0);
    /// <summary>#DA70D6 颜色</summary>
    public static readonly Color Orchid = FromArgb(255, 218, 112, 214);
    /// <summary>#EEE8AA 颜色</summary>
    public static readonly Color PaleGoldenrod = FromArgb(255, 238, 232, 170);
    /// <summary>#98FB98 颜色</summary>
    public static readonly Color PaleGreen = FromArgb(255, 152, 251, 152);
    /// <summary>#AFEEEE 颜色</summary>
    public static readonly Color PaleTurquoise = FromArgb(255, 175, 238, 238);
    /// <summary>#DB7093 颜色</summary>
    public static readonly Color PaleVioletRed = FromArgb(255, 219, 112, 147);
    /// <summary>#FFEFD5 颜色</summary>
    public static readonly Color PapayaWhip = FromArgb(255, 255, 239, 213);
    /// <summary>#FFDAB9 颜色</summary>
    public static readonly Color PeachPuff = FromArgb(255, 255, 218, 185);
    /// <summary>#CD853F 颜色</summary>
    public static readonly Color Peru = FromArgb(255, 205, 133, 63);
    /// <summary>#FFC0CB 颜色</summary>
    public static readonly Color Pink = FromArgb(255, 255, 192, 203);
    /// <summary>#DDA0DD 颜色</summary>
    public static readonly Color Plum = FromArgb(255, 221, 160, 221);
    /// <summary>#B0E0E6 颜色</summary>
    public static readonly Color PowderBlue = FromArgb(255, 176, 224, 230);
    /// <summary>#800080 颜色</summary>
    public static readonly Color Purple = FromArgb(255, 128, 0, 128);
    /// <summary>#FF0000 颜色</summary>
    public static readonly Color Red = FromArgb(255, 255, 0, 0);
    /// <summary>#BC8F8F 颜色</summary>
    public static readonly Color RosyBrown = FromArgb(255, 188, 143, 143);
    /// <summary>#4169E1 颜色</summary>
    public static readonly Color RoyalBlue = FromArgb(255, 65, 105, 225);
    /// <summary>#8B4513 颜色</summary>
    public static readonly Color SaddleBrown = FromArgb(255, 139, 69, 19);
    /// <summary>#FA8072 颜色</summary>
    public static readonly Color Salmon = FromArgb(255, 250, 128, 114);
    /// <summary>#F4A460 颜色</summary>
    public static readonly Color SandyBrown = FromArgb(255, 244, 164, 96);
    /// <summary>#2E8B57 颜色</summary>
    public static readonly Color SeaGreen = FromArgb(255, 46, 139, 87);
    /// <summary>#FFF5EE 颜色</summary>
    public static readonly Color SeaShell = FromArgb(255, 255, 245, 238);
    /// <summary>#A0522D 颜色</summary>
    public static readonly Color Sienna = FromArgb(255, 160, 82, 45);
    /// <summary>#C0C0C0 颜色</summary>
    public static readonly Color Silver = FromArgb(255, 192, 192, 192);
    /// <summary>#87CEEB 颜色</summary>
    public static readonly Color SkyBlue = FromArgb(255, 135, 206, 235);
    /// <summary>#6A5ACD 颜色</summary>
    public static readonly Color SlateBlue = FromArgb(255, 106, 90, 205);
    /// <summary>#708090 颜色</summary>
    public static readonly Color SlateGray = FromArgb(255, 112, 128, 144);
    /// <summary>#FFFAFA 颜色</summary>
    public static readonly Color Snow = FromArgb(255, 255, 250, 250);
    /// <summary>#00FF7F 颜色</summary>
    public static readonly Color SpringGreen = FromArgb(255, 0, 255, 127);
    /// <summary>#4682B4 颜色</summary>
    public static readonly Color SteelBlue = FromArgb(255, 70, 130, 180);
    /// <summary>#D2B48C 颜色</summary>
    public static readonly Color Tan = FromArgb(255, 210, 180, 140);
    /// <summary>#008080 颜色</summary>
    public static readonly Color Teal = FromArgb(255, 0, 128, 128);
    /// <summary>#D8BFD8 颜色</summary>
    public static readonly Color Thistle = FromArgb(255, 216, 191, 216);
    /// <summary>#FF6347 颜色</summary>
    public static readonly Color Tomato = FromArgb(255, 255, 99, 71);
    /// <summary>#40E0D0 颜色</summary>
    public static readonly Color Turquoise = FromArgb(255, 64, 224, 208);
    /// <summary>#EE82EE 颜色</summary>
    public static readonly Color Violet = FromArgb(255, 238, 130, 238);
    /// <summary>#F5DEB3 颜色</summary>
    public static readonly Color Wheat = FromArgb(255, 245, 222, 179);
    /// <summary>白色：普通正文文字</summary>
    public static readonly Color White = FromArgb(255, 255, 255, 255);
    /// <summary>#F5F5F5 颜色</summary>
    public static readonly Color WhiteSmoke = FromArgb(255, 245, 245, 245);
    /// <summary>黄色：标题 / 新任务高亮</summary>
    public static readonly Color Yellow = FromArgb(255, 255, 255, 0);
    /// <summary>#9ACD32 颜色</summary>
    public static readonly Color YellowGreen = FromArgb(255, 154, 205, 50);

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

    // ===== 名称查色（对齐原 MirEngine.Color.FromName，大小写不敏感）=====
    private static readonly Dictionary<string, Color> NamedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
    {
        { "AliceBlue", AliceBlue },
        { "AntiqueWhite", AntiqueWhite },
        { "Aqua", Aqua },
        { "Aquamarine", Aquamarine },
        { "Azure", Azure },
        { "Beige", Beige },
        { "Bisque", Bisque },
        { "Black", Black },
        { "BlanchedAlmond", BlanchedAlmond },
        { "Blue", Blue },
        { "BlueViolet", BlueViolet },
        { "Brown", Brown },
        { "BurlyWood", BurlyWood },
        { "CadetBlue", CadetBlue },
        { "Chartreuse", Chartreuse },
        { "Chocolate", Chocolate },
        { "Coral", Coral },
        { "CornflowerBlue", CornflowerBlue },
        { "Cornsilk", Cornsilk },
        { "Crimson", Crimson },
        { "Cyan", Cyan },
        { "DarkBlue", DarkBlue },
        { "DarkCyan", DarkCyan },
        { "DarkGoldenrod", DarkGoldenrod },
        { "DarkGray", DarkGray },
        { "DarkGreen", DarkGreen },
        { "DarkKhaki", DarkKhaki },
        { "DarkMagenta", DarkMagenta },
        { "DarkOliveGreen", DarkOliveGreen },
        { "DarkOrange", DarkOrange },
        { "DarkOrchid", DarkOrchid },
        { "DarkRed", DarkRed },
        { "DarkSalmon", DarkSalmon },
        { "DarkSeaGreen", DarkSeaGreen },
        { "DarkSlateBlue", DarkSlateBlue },
        { "DarkSlateGray", DarkSlateGray },
        { "DarkTurquoise", DarkTurquoise },
        { "DarkViolet", DarkViolet },
        { "DeepPink", DeepPink },
        { "DeepSkyBlue", DeepSkyBlue },
        { "DimGray", DimGray },
        { "DodgerBlue", DodgerBlue },
        { "Empty", Empty },
        { "Firebrick", Firebrick },
        { "FloralWhite", FloralWhite },
        { "ForestGreen", ForestGreen },
        { "Fuchsia", Fuchsia },
        { "Gainsboro", Gainsboro },
        { "GhostWhite", GhostWhite },
        { "Gold", Gold },
        { "Goldenrod", Goldenrod },
        { "Gray", Gray },
        { "Green", Green },
        { "GreenYellow", GreenYellow },
        { "Honeydew", Honeydew },
        { "HotPink", HotPink },
        { "IndianRed", IndianRed },
        { "Indigo", Indigo },
        { "Ivory", Ivory },
        { "Khaki", Khaki },
        { "Lavender", Lavender },
        { "LavenderBlush", LavenderBlush },
        { "LawnGreen", LawnGreen },
        { "LemonChiffon", LemonChiffon },
        { "LightBlue", LightBlue },
        { "LightCoral", LightCoral },
        { "LightCyan", LightCyan },
        { "LightGoldenrodYellow", LightGoldenrodYellow },
        { "LightGray", LightGray },
        { "LightGreen", LightGreen },
        { "LightPink", LightPink },
        { "LightSalmon", LightSalmon },
        { "LightSeaGreen", LightSeaGreen },
        { "LightSkyBlue", LightSkyBlue },
        { "LightSlateGray", LightSlateGray },
        { "LightSteelBlue", LightSteelBlue },
        { "LightYellow", LightYellow },
        { "Lime", Lime },
        { "LimeGreen", LimeGreen },
        { "Linen", Linen },
        { "Magenta", Magenta },
        { "Maroon", Maroon },
        { "MediumAquamarine", MediumAquamarine },
        { "MediumBlue", MediumBlue },
        { "MediumOrchid", MediumOrchid },
        { "MediumPurple", MediumPurple },
        { "MediumSeaGreen", MediumSeaGreen },
        { "MediumSlateBlue", MediumSlateBlue },
        { "MediumSpringGreen", MediumSpringGreen },
        { "MediumTurquoise", MediumTurquoise },
        { "MediumVioletRed", MediumVioletRed },
        { "MidnightBlue", MidnightBlue },
        { "MintCream", MintCream },
        { "MistyRose", MistyRose },
        { "Moccasin", Moccasin },
        { "ModalBackdrop", ModalBackdrop },
        { "NavajoWhite", NavajoWhite },
        { "Navy", Navy },
        { "OldLace", OldLace },
        { "Olive", Olive },
        { "OliveDrab", OliveDrab },
        { "Orange", Orange },
        { "OrangeRed", OrangeRed },
        { "Orchid", Orchid },
        { "PaleGoldenrod", PaleGoldenrod },
        { "PaleGreen", PaleGreen },
        { "PaleTurquoise", PaleTurquoise },
        { "PaleVioletRed", PaleVioletRed },
        { "PapayaWhip", PapayaWhip },
        { "PeachPuff", PeachPuff },
        { "Peru", Peru },
        { "Pink", Pink },
        { "Plum", Plum },
        { "PowderBlue", PowderBlue },
        { "Purple", Purple },
        { "Red", Red },
        { "RosyBrown", RosyBrown },
        { "RoyalBlue", RoyalBlue },
        { "SaddleBrown", SaddleBrown },
        { "Salmon", Salmon },
        { "SandyBrown", SandyBrown },
        { "SeaGreen", SeaGreen },
        { "SeaShell", SeaShell },
        { "Sienna", Sienna },
        { "Silver", Silver },
        { "SkyBlue", SkyBlue },
        { "SlateBlue", SlateBlue },
        { "SlateGray", SlateGray },
        { "Snow", Snow },
        { "SpringGreen", SpringGreen },
        { "SteelBlue", SteelBlue },
        { "Tan", Tan },
        { "Teal", Teal },
        { "Thistle", Thistle },
        { "Tomato", Tomato },
        { "Transparent", Transparent },
        { "Turquoise", Turquoise },
        { "Violet", Violet },
        { "Wheat", Wheat },
        { "White", White },
        { "WhiteSmoke", WhiteSmoke },
        { "Yellow", Yellow },
        { "YellowGreen", YellowGreen },
    };
    public static Color FromName(string name)
    {
        if (!string.IsNullOrEmpty(name) && NamedColors.TryGetValue(name, out var c)) return c;
        return Empty;
    }
}
