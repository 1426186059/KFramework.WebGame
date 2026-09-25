using System;
using System.Collections.Generic;

namespace MirEngine
{
    // 原 Web_Mir2.Engine/MirEngine/Shims/Color.cs（纯值类型，无 Browser 依赖）
    public struct Color : IEquatable<Color>
    {
        private const int AlphaShift = 24;
        private const int RedShift = 16;
        private const int GreenShift = 8;
        private const int BlueShift = 0;

        public static readonly Color Empty;
        public static readonly Color Transparent = FromArgb(0, 255, 255, 255);
        public static readonly Color AliceBlue = FromArgb(255, 240, 248, 255);
        public static readonly Color AntiqueWhite = FromArgb(255, 250, 235, 215);
        public static readonly Color Aqua = FromArgb(255, 0, 255, 255);
        public static readonly Color Aquamarine = FromArgb(255, 127, 255, 212);
        public static readonly Color Azure = FromArgb(255, 240, 255, 255);
        public static readonly Color Beige = FromArgb(255, 245, 245, 220);
        public static readonly Color Bisque = FromArgb(255, 255, 228, 196);
        public static readonly Color Black = FromArgb(255, 0, 0, 0);
        public static readonly Color BlanchedAlmond = FromArgb(255, 255, 235, 205);
        public static readonly Color Blue = FromArgb(255, 0, 0, 255);
        public static readonly Color BlueViolet = FromArgb(255, 138, 43, 226);
        public static readonly Color Brown = FromArgb(255, 165, 42, 42);
        public static readonly Color BurlyWood = FromArgb(255, 222, 184, 135);
        public static readonly Color CadetBlue = FromArgb(255, 95, 158, 160);
        public static readonly Color Chartreuse = FromArgb(255, 127, 255, 0);
        public static readonly Color Chocolate = FromArgb(255, 210, 105, 30);
        public static readonly Color Coral = FromArgb(255, 255, 127, 80);
        public static readonly Color CornflowerBlue = FromArgb(255, 100, 149, 237);
        public static readonly Color Cornsilk = FromArgb(255, 255, 248, 220);
        public static readonly Color Crimson = FromArgb(255, 220, 20, 60);
        public static readonly Color Cyan = FromArgb(255, 0, 255, 255);
        public static readonly Color DarkBlue = FromArgb(255, 0, 0, 139);
        public static readonly Color DarkCyan = FromArgb(255, 0, 139, 139);
        public static readonly Color DarkGoldenrod = FromArgb(255, 184, 134, 11);
        public static readonly Color DarkGray = FromArgb(255, 169, 169, 169);
        public static readonly Color DarkGreen = FromArgb(255, 0, 100, 0);
        public static readonly Color DarkKhaki = FromArgb(255, 189, 183, 107);
        public static readonly Color DarkMagenta = FromArgb(255, 139, 0, 139);
        public static readonly Color DarkOliveGreen = FromArgb(255, 85, 107, 47);
        public static readonly Color DarkOrange = FromArgb(255, 255, 140, 0);
        public static readonly Color DarkOrchid = FromArgb(255, 153, 50, 204);
        public static readonly Color DarkRed = FromArgb(255, 139, 0, 0);
        public static readonly Color DarkSalmon = FromArgb(255, 233, 150, 122);
        public static readonly Color DarkSeaGreen = FromArgb(255, 143, 188, 143);
        public static readonly Color DarkSlateBlue = FromArgb(255, 72, 61, 139);
        public static readonly Color DarkSlateGray = FromArgb(255, 47, 79, 79);
        public static readonly Color DarkTurquoise = FromArgb(255, 0, 206, 209);
        public static readonly Color DarkViolet = FromArgb(255, 148, 0, 211);
        public static readonly Color DeepPink = FromArgb(255, 255, 20, 147);
        public static readonly Color DeepSkyBlue = FromArgb(255, 0, 191, 255);
        public static readonly Color DimGray = FromArgb(255, 105, 105, 105);
        public static readonly Color DodgerBlue = FromArgb(255, 30, 144, 255);
        public static readonly Color Firebrick = FromArgb(255, 178, 34, 34);
        public static readonly Color FloralWhite = FromArgb(255, 255, 250, 240);
        public static readonly Color ForestGreen = FromArgb(255, 34, 139, 34);
        public static readonly Color Fuchsia = FromArgb(255, 255, 0, 255);
        public static readonly Color Gainsboro = FromArgb(255, 220, 220, 220);
        public static readonly Color GhostWhite = FromArgb(255, 248, 248, 255);
        public static readonly Color Gold = FromArgb(255, 255, 215, 0);
        public static readonly Color Goldenrod = FromArgb(255, 218, 165, 32);
        public static readonly Color Gray = FromArgb(255, 128, 128, 128);
        public static readonly Color Green = FromArgb(255, 0, 128, 0);
        public static readonly Color GreenYellow = FromArgb(255, 173, 255, 47);
        public static readonly Color Honeydew = FromArgb(255, 240, 255, 240);
        public static readonly Color HotPink = FromArgb(255, 255, 105, 180);
        public static readonly Color IndianRed = FromArgb(255, 205, 92, 92);
        public static readonly Color Indigo = FromArgb(255, 75, 0, 130);
        public static readonly Color Ivory = FromArgb(255, 255, 255, 240);
        public static readonly Color Khaki = FromArgb(255, 240, 230, 140);
        public static readonly Color Lavender = FromArgb(255, 230, 230, 250);
        public static readonly Color LavenderBlush = FromArgb(255, 255, 240, 245);
        public static readonly Color LawnGreen = FromArgb(255, 124, 252, 0);
        public static readonly Color LemonChiffon = FromArgb(255, 255, 250, 205);
        public static readonly Color LightBlue = FromArgb(255, 173, 216, 230);
        public static readonly Color LightCoral = FromArgb(255, 240, 128, 128);
        public static readonly Color LightCyan = FromArgb(255, 224, 255, 255);
        public static readonly Color LightGoldenrodYellow = FromArgb(255, 250, 250, 210);
        public static readonly Color LightGray = FromArgb(255, 211, 211, 211);
        public static readonly Color LightGreen = FromArgb(255, 144, 238, 144);
        public static readonly Color LightPink = FromArgb(255, 255, 182, 193);
        public static readonly Color LightSalmon = FromArgb(255, 255, 160, 122);
        public static readonly Color LightSeaGreen = FromArgb(255, 32, 178, 170);
        public static readonly Color LightSkyBlue = FromArgb(255, 135, 206, 250);
        public static readonly Color LightSlateGray = FromArgb(255, 119, 136, 153);
        public static readonly Color LightSteelBlue = FromArgb(255, 176, 196, 222);
        public static readonly Color LightYellow = FromArgb(255, 255, 255, 224);
        public static readonly Color Lime = FromArgb(255, 0, 255, 0);
        public static readonly Color LimeGreen = FromArgb(255, 50, 205, 50);
        public static readonly Color Linen = FromArgb(255, 250, 240, 230);
        public static readonly Color Magenta = FromArgb(255, 255, 0, 255);
        public static readonly Color Maroon = FromArgb(255, 128, 0, 0);
        public static readonly Color MediumAquamarine = FromArgb(255, 102, 205, 170);
        public static readonly Color MediumBlue = FromArgb(255, 0, 0, 205);
        public static readonly Color MediumOrchid = FromArgb(255, 186, 85, 211);
        public static readonly Color MediumPurple = FromArgb(255, 147, 112, 219);
        public static readonly Color MediumSeaGreen = FromArgb(255, 60, 179, 113);
        public static readonly Color MediumSlateBlue = FromArgb(255, 123, 104, 238);
        public static readonly Color MediumSpringGreen = FromArgb(255, 0, 250, 154);
        public static readonly Color MediumTurquoise = FromArgb(255, 72, 209, 204);
        public static readonly Color MediumVioletRed = FromArgb(255, 199, 21, 133);
        public static readonly Color MidnightBlue = FromArgb(255, 25, 25, 112);
        public static readonly Color MintCream = FromArgb(255, 245, 255, 250);
        public static readonly Color MistyRose = FromArgb(255, 255, 228, 225);
        public static readonly Color Moccasin = FromArgb(255, 255, 228, 181);
        public static readonly Color NavajoWhite = FromArgb(255, 255, 222, 173);
        public static readonly Color Navy = FromArgb(255, 0, 0, 128);
        public static readonly Color OldLace = FromArgb(255, 253, 245, 230);
        public static readonly Color Olive = FromArgb(255, 128, 128, 0);
        public static readonly Color OliveDrab = FromArgb(255, 107, 142, 35);
        public static readonly Color Orange = FromArgb(255, 255, 165, 0);
        public static readonly Color OrangeRed = FromArgb(255, 255, 69, 0);
        public static readonly Color Orchid = FromArgb(255, 218, 112, 214);
        public static readonly Color PaleGoldenrod = FromArgb(255, 238, 232, 170);
        public static readonly Color PaleGreen = FromArgb(255, 152, 251, 152);
        public static readonly Color PaleTurquoise = FromArgb(255, 175, 238, 238);
        public static readonly Color PaleVioletRed = FromArgb(255, 219, 112, 147);
        public static readonly Color PapayaWhip = FromArgb(255, 255, 239, 213);
        public static readonly Color PeachPuff = FromArgb(255, 255, 218, 185);
        public static readonly Color Peru = FromArgb(255, 205, 133, 63);
        public static readonly Color Pink = FromArgb(255, 255, 192, 203);
        public static readonly Color Plum = FromArgb(255, 221, 160, 221);
        public static readonly Color PowderBlue = FromArgb(255, 176, 224, 230);
        public static readonly Color Purple = FromArgb(255, 128, 0, 128);
        public static readonly Color Red = FromArgb(255, 255, 0, 0);
        public static readonly Color RosyBrown = FromArgb(255, 188, 143, 143);
        public static readonly Color RoyalBlue = FromArgb(255, 65, 105, 225);
        public static readonly Color SaddleBrown = FromArgb(255, 139, 69, 19);
        public static readonly Color Salmon = FromArgb(255, 250, 128, 114);
        public static readonly Color SandyBrown = FromArgb(255, 244, 164, 96);
        public static readonly Color SeaGreen = FromArgb(255, 46, 139, 87);
        public static readonly Color SeaShell = FromArgb(255, 255, 245, 238);
        public static readonly Color Sienna = FromArgb(255, 160, 82, 45);
        public static readonly Color Silver = FromArgb(255, 192, 192, 192);
        public static readonly Color SkyBlue = FromArgb(255, 135, 206, 235);
        public static readonly Color SlateBlue = FromArgb(255, 106, 90, 205);
        public static readonly Color SlateGray = FromArgb(255, 112, 128, 144);
        public static readonly Color Snow = FromArgb(255, 255, 250, 250);
        public static readonly Color SpringGreen = FromArgb(255, 0, 255, 127);
        public static readonly Color SteelBlue = FromArgb(255, 70, 130, 180);
        public static readonly Color Tan = FromArgb(255, 210, 180, 140);
        public static readonly Color Teal = FromArgb(255, 0, 128, 128);
        public static readonly Color Thistle = FromArgb(255, 216, 191, 216);
        public static readonly Color Tomato = FromArgb(255, 255, 99, 71);
        public static readonly Color Turquoise = FromArgb(255, 64, 224, 208);
        public static readonly Color Violet = FromArgb(255, 238, 130, 238);
        public static readonly Color Wheat = FromArgb(255, 245, 222, 179);
        public static readonly Color White = FromArgb(255, 255, 255, 255);
        public static readonly Color WhiteSmoke = FromArgb(255, 245, 245, 245);
        public static readonly Color Yellow = FromArgb(255, 255, 255, 0);
        public static readonly Color YellowGreen = FromArgb(255, 154, 205, 50);

        private long value;

        public Color(byte alpha, byte red, byte green, byte blue)
        {
            value = (long)(uint)((alpha << AlphaShift) | (red << RedShift) | (green << GreenShift) | blue) & 0xffffffff;
        }

        public Color(byte red, byte green, byte blue) : this(255, red, green, blue) { }
        public Color(float red, float green, float blue) : this(1f, red, green, blue) { }
        public Color(float alpha, float red, float green, float blue)
        {
            int a = (int)Math.Round(Math.Max(0f, Math.Min(1f, alpha)) * 255);
            int r = (int)Math.Round(Math.Max(0f, Math.Min(1f, red)) * 255);
            int g = (int)Math.Round(Math.Max(0f, Math.Min(1f, green)) * 255);
            int b = (int)Math.Round(Math.Max(0f, Math.Min(1f, blue)) * 255);
            value = (uint)((a << AlphaShift) | (r << RedShift) | (g << GreenShift) | b);
        }

        public static Color FromArgb(int argb) => new Color((byte)(argb >> AlphaShift & 0xFF), (byte)(argb >> RedShift & 0xFF), (byte)(argb >> GreenShift & 0xFF), (byte)(argb & 0xFF));
        public static Color FromArgb(int alpha, int red, int green, int blue) => new Color((byte)alpha, (byte)red, (byte)green, (byte)blue);
        public static Color FromArgb(int alpha, Color baseColor) => FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        public static Color FromArgb(int red, int green, int blue) => FromArgb(255, red, green, blue);

        // 手动登记命名色（大小写不敏感），避免 WASM 上 typeof(Color).GetFields 反射开销。
        // 与 .NET 原版 Color.FromName 行为一致：仅识别已知命名色（不含 Transparent/Empty）。
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
            { "NavajoWhite", NavajoWhite },
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
            { "Turquoise", Turquoise },
            { "Violet", Violet },
            { "Wheat", Wheat },
            { "White", White },
            { "WhiteSmoke", WhiteSmoke },
            { "Yellow", Yellow },
            { "YellowGreen", YellowGreen },
        };

        // 大小写不敏感地解析命名色（与 .NET 原版 Color.FromName 行为一致）。
        // 旧实现用 typeof(Color).GetField(name) 反射，仅按精确大小写匹配，导致小写色名（如 "yellow"）
        // 解析为 Empty → 前景透明 → 只剩下黑描边（即“黑层”覆盖着色字）。
        public static Color FromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Empty;
            if (NamedColors.TryGetValue(name, out var c)) return c;
            KFramework.MonoGame.PrintTool.LogError($"Color.FromName 没有找到对应的Color: {name}");
            return Empty;
        }

        public byte A => (byte)((value >> AlphaShift) & 0xFF);
        public byte R => (byte)((value >> RedShift) & 0xFF);
        public byte G => (byte)((value >> GreenShift) & 0xFF);
        public byte B => (byte)(value & 0xFF);

        public int ToArgb() => (int)value;

        // 与引擎 KFramework.MonoGame.Color 互通：边界代码（如 MirTextBox 桥接到引擎 TextBox）可直接赋值，
        // 无需把全工程的 MirEngine.Color 重写成另一套类型。
        public static implicit operator KFramework.MonoGame.Color(Color c)
            => KFramework.MonoGame.Color.FromArgb((uint)c.ToArgb());
        public bool IsEmpty => value == 0;

        public static bool operator ==(Color left, Color right) => left.value == right.value;
        public static bool operator !=(Color left, Color right) => !(left == right);

        public bool Equals(Color other) => value == other.value;
        public override bool Equals(object obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => value.GetHashCode();

        public static Color MinValue => Color.Empty;
        public static Color MaxValue => Color.White;

        public float GetBrightness()
        {
            float r = R / 255f, g = G / 255f, b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            return (max + min) / 2f;
        }

        public float GetHue()
        {
            float r = R / 255f, g = G / 255f, b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            if (max == min) return 0f;
            float delta = max - min;
            float h;
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
            return h < 0 ? h + 360 : h;
        }

        public float GetSaturation()
        {
            float r = R / 255f, g = G / 255f, b = B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            if (max == min) return 0f;
            float l = (max + min) / 2f;
            return (max - min) / (1 - Math.Abs(2 * l - 1));
        }

        public override string ToString()
        {
            if (IsEmpty) return "Color [Empty]";
            return $"Color [A={A}, R={R}, G={G}, B={B}]";
        }
    }
}
