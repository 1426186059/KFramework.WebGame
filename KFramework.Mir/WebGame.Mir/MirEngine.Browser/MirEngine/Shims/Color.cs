
namespace MirEngine
{
    public struct Color
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; }

        public static Color Empty => new Color(0, 0, 0, 0);
        public static Color Transparent => new Color(0, 0, 0, 0);
        public static Color AliceBlue => new Color(240, 248, 255, 255);
        public static Color LightSalmon => new Color(255, 160, 122, 255);
        public static Color AntiqueWhite => new Color(250, 235, 215, 255);
        public static Color LightSeaGreen => new Color(32, 178, 170, 255);
        public static Color Aqua => new Color(0, 255, 255, 255);
        public static Color LightSkyBlue => new Color(135, 206, 250, 255);
        public static Color Aquamarine => new Color(127, 255, 212, 255);
        public static Color LightSlateGray => new Color(119, 136, 153, 255);
        public static Color Azure => new Color(240, 255, 255, 255);
        public static Color LightSteelBlue => new Color(176, 196, 222, 255);
        public static Color Beige => new Color(245, 245, 220, 255);
        public static Color LightYellow => new Color(255, 255, 224, 255);
        public static Color Bisque => new Color(255, 228, 196, 255);
        public static Color Lime => new Color(0, 255, 0, 255);
        public static Color Black => new Color(0, 0, 0, 255);
        public static Color LimeGreen => new Color(50, 205, 50, 255);
        public static Color BlanchedAlmond => new Color(255, 255, 205, 255);
        public static Color Linen => new Color(250, 240, 230, 255);
        public static Color Blue => new Color(0, 0, 255, 255);
        public static Color Magenta => new Color(255, 0, 255, 255);
        public static Color BlueViolet => new Color(138, 43, 226, 255);
        public static Color Maroon => new Color(128, 0, 0, 255);
        public static Color Brown => new Color(165, 42, 42, 255);
        public static Color MediumAquamarine => new Color(102, 205, 170, 255);
        public static Color BurlyWood => new Color(222, 184, 135, 255);
        public static Color MediumBlue => new Color(0, 0, 205, 255);
        public static Color CadetBlue => new Color(95, 158, 160, 255);
        public static Color MediumOrchid => new Color(186, 85, 211, 255);
        public static Color Chartreuse => new Color(127, 255, 0, 255);
        public static Color MediumPurple => new Color(147, 112, 219, 255);
        public static Color Chocolate => new Color(210, 105, 30, 255);
        public static Color MediumSeaGreen => new Color(60, 179, 113, 255);
        public static Color Coral => new Color(255, 127, 80, 255);
        public static Color MediumSlateBlue => new Color(123, 104, 238, 255);
        public static Color CornflowerBlue => new Color(100, 149, 237, 255);
        public static Color MediumSpringGreen => new Color(0, 250, 154, 255);
        public static Color Cornsilk => new Color(255, 248, 220, 255);
        public static Color MediumTurquoise => new Color(72, 209, 204, 255);
        public static Color Crimson => new Color(220, 20, 60, 255);
        public static Color MediumVioletRed => new Color(199, 21, 112, 255);
        public static Color Cyan => new Color(0, 255, 255, 255);
        public static Color MidnightBlue => new Color(25, 25, 112, 255);
        public static Color DarkBlue => new Color(0, 0, 139, 255);
        public static Color MintCream => new Color(245, 255, 250, 255);
        public static Color DarkCyan => new Color(0, 139, 139, 255);
        public static Color MistyRose => new Color(255, 228, 225, 255);
        public static Color DarkGoldenrod => new Color(184, 134, 11, 255);
        public static Color Moccasin => new Color(255, 228, 181, 255);
        public static Color DarkGray => new Color(169, 169, 169, 255);
        public static Color NavajoWhite => new Color(255, 222, 173, 255);
        public static Color DarkGreen => new Color(0, 100, 0, 255);
        public static Color Navy => new Color(0, 0, 128, 255);
        public static Color DarkKhaki => new Color(189, 183, 107, 255);
        public static Color OldLace => new Color(253, 245, 230, 255);
        public static Color DarkMagena => new Color(139, 0, 139, 255);
        public static Color Olive => new Color(128, 128, 0, 255);
        public static Color DarkOliveGreen => new Color(85, 107, 47, 255);
        public static Color OliveDrab => new Color(107, 142, 45, 255);
        public static Color DarkOrange => new Color(255, 140, 0, 255);
        public static Color Orange => new Color(255, 165, 0, 255);
        public static Color DarkOrchid => new Color(153, 50, 204, 255);
        public static Color OrangeRed => new Color(255, 69, 0, 255);
        public static Color DarkRed => new Color(139, 0, 0, 255);
        public static Color Orchid => new Color(218, 112, 214, 255);
        public static Color DarkSalmon => new Color(233, 150, 122, 255);
        public static Color PaleGoldenrod => new Color(238, 232, 170, 255);
        public static Color DarkSeaGreen => new Color(143, 188, 143, 255);
        public static Color PaleGreen => new Color(152, 251, 152, 255);
        public static Color DarkSlateBlue => new Color(72, 61, 139, 255);
        public static Color PaleTurquoise => new Color(175, 238, 238, 255);
        public static Color DarkSlateGray => new Color(40, 79, 79, 255);
        public static Color PaleVioletRed => new Color(219, 112, 147, 255);
        public static Color DarkTurquoise => new Color(0, 206, 209, 255);
        public static Color PapayaWhip => new Color(255, 239, 213, 255);
        public static Color DarkViolet => new Color(148, 0, 211, 255);
        public static Color PeachPuff => new Color(255, 218, 155, 255);
        public static Color DeepPink => new Color(255, 20, 147, 255);
        public static Color Peru => new Color(205, 133, 63, 255);
        public static Color DeepSkyBlue => new Color(0, 191, 255, 255);
        public static Color Pink => new Color(255, 192, 203, 255);
        public static Color DimGray => new Color(105, 105, 105, 255);
        public static Color Plum => new Color(221, 160, 221, 255);
        public static Color DodgerBlue => new Color(30, 144, 255, 255);
        public static Color PowderBlue => new Color(176, 224, 230, 255);
        public static Color Firebrick => new Color(178, 34, 34, 255);
        public static Color Purple => new Color(128, 0, 128, 255);
        public static Color FloralWhite => new Color(255, 250, 240, 255);
        public static Color Red => new Color(255, 0, 0, 255);
        public static Color ForestGreen => new Color(34, 139, 34, 255);
        public static Color RosyBrown => new Color(188, 143, 143, 255);
        public static Color Fuschia => new Color(255, 0, 255, 255);
        public static Color RoyalBlue => new Color(65, 105, 225, 255);
        public static Color Gainsboro => new Color(220, 220, 220, 255);
        public static Color SaddleBrown => new Color(139, 69, 19, 255);
        public static Color GhostWhite => new Color(248, 248, 255, 255);
        public static Color Salmon => new Color(250, 128, 114, 255);
        public static Color Gold => new Color(255, 215, 0, 255);
        public static Color SandyBrown => new Color(244, 164, 96, 255);
        public static Color Goldenrod => new Color(218, 165, 32, 255);
        public static Color SeaGreen => new Color(46, 139, 87, 255);
        public static Color Gray => new Color(128, 128, 128, 255);
        public static Color Seashell => new Color(255, 245, 238, 255);
        public static Color Green => new Color(0, 128, 0, 255);
        public static Color Sienna => new Color(160, 82, 45, 255);
        public static Color GreenYellow => new Color(173, 255, 47, 255);
        public static Color Silver => new Color(192, 192, 192, 255);
        public static Color Honeydew => new Color(240, 255, 240, 255);
        public static Color SkyBlue => new Color(135, 206, 235, 255);
        public static Color HotPink => new Color(255, 105, 180, 255);
        public static Color SlateBlue => new Color(106, 90, 205, 255);
        public static Color IndianRed => new Color(205, 92, 92, 255);
        public static Color SlateGray => new Color(112, 128, 144, 255);
        public static Color Indigo => new Color(75, 0, 130, 255);
        public static Color Snow => new Color(255, 250, 250, 255);
        public static Color Ivory => new Color(255, 240, 240, 255);
        public static Color SpringGreen => new Color(0, 255, 127, 255);
        public static Color Khaki => new Color(240, 230, 140, 255);
        public static Color SteelBlue => new Color(70, 130, 180, 255);
        public static Color Lavender => new Color(230, 230, 250, 255);
        public static Color Tan => new Color(210, 180, 140, 255);
        public static Color LavenderBlush => new Color(255, 240, 245, 255);
        public static Color Teal => new Color(0, 128, 128, 255);
        public static Color LawnGreen => new Color(124, 252, 0, 255);
        public static Color Thistle => new Color(216, 191, 216, 255);
        public static Color LemonChiffon => new Color(255, 250, 205, 255);
        public static Color Tomato => new Color(253, 99, 71, 255);
        public static Color LightBlue => new Color(173, 216, 230, 255);
        public static Color Turquoise => new Color(64, 224, 208, 255);
        public static Color LightCoral => new Color(240, 128, 128, 255);
        public static Color Violet => new Color(238, 130, 238, 255);
        public static Color LightCyan => new Color(224, 255, 255, 255);
        public static Color Wheat => new Color(245, 222, 179, 255);
        public static Color LightGoldenrodYellow => new Color(250, 250, 210, 255);
        public static Color White => new Color(255, 255, 255, 255);
        public static Color LightGreen => new Color(144, 238, 144, 255);
        public static Color WhiteSmoke => new Color(245, 245, 245, 255);
        public static Color LightGray => new Color(211, 211, 211, 255);
        public static Color Yellow => new Color(255, 255, 0, 255);
        public static Color LightPink => new Color(255, 182, 193, 255);
        public static Color YellowGreen => new Color(154, 205, 50, 255);

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
            A = 255; // Default alpha value
        }

        public Color(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public bool IsEmpty => R == 0 && G == 0 && B == 0 && A == 0;

        public bool Equals(Color otherColor)
        {
            return R == otherColor.R && G == otherColor.G && B == otherColor.B && A == otherColor.A;
        }


        public override string ToString()
        {
            return $"{{R={R}, G={G}, B={B}, A={A}}}";
        }

        // 与 System.Drawing.Color 一致：分量按 int 传入（调用方普遍传 int 表达式），内部截断到 byte。
        public static Color FromArgb(int a, int r, int g, int b)
        {
            return new Color(ToByte(r), ToByte(g), ToByte(b), ToByte(a));
        }

        public static Color FromArgb(int r, int g, int b)
        {
            return new Color(ToByte(r), ToByte(g), ToByte(b), 255); // 默认 alpha 为 255
        }

        private static byte ToByte(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

        public static Color FromArgb(int argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);

            return new Color(r, g, b, a);
        }

        // System.Drawing.Color.FromArgb(int alpha, Color baseColor)
        public static Color FromArgb(int alpha, Color baseColor)
        {
            return new Color(baseColor.R, baseColor.G, baseColor.B, ToByte(alpha));
        }

        public int ToArgb()
        {
            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        public static Color FromName(string name)
        {
            switch (name.ToLower())
            {
                case "empty":
                    return Empty;
                case "transparent":
                    return Transparent;
                case "aliceblue":
                    return AliceBlue;
                case "lightsalmon":
                    return LightSalmon;
                case "antiquewhite":
                    return AntiqueWhite;
                case "lightseagreen":
                    return LightSeaGreen;
                case "aqua":
                    return Aqua;
                case "lightskyblue":
                    return LightSkyBlue;
                case "aquamarine":
                    return Aquamarine;
                case "lightslategray":
                    return LightSlateGray;
                case "azure":
                    return Azure;
                case "lightsteelblue":
                    return LightSteelBlue;
                case "beige":
                    return Beige;
                case "lightyellow":
                    return LightYellow;
                case "bisque":
                    return Bisque;
                case "lime":
                    return Lime;
                case "black":
                    return Black;
                case "limegreen":
                    return LimeGreen;
                case "blanchedalmond":
                    return BlanchedAlmond;
                case "linen":
                    return Linen;
                case "blue":
                    return Blue;
                case "magenta":
                    return Magenta;
                case "blueviolet":
                    return BlueViolet;
                case "maroon":
                    return Maroon;
                case "brown":
                    return Brown;
                case "mediumaquamarine":
                    return MediumAquamarine;
                case "burlywood":
                    return BurlyWood;
                case "mediumblue":
                    return MediumBlue;
                case "cadetblue":
                    return CadetBlue;
                case "mediumorchid":
                    return MediumOrchid;
                case "chartreuse":
                    return Chartreuse;
                case "mediumpurple":
                    return MediumPurple;
                case "chocolate":
                    return Chocolate;
                case "mediumseagreen":
                    return MediumSeaGreen;
                case "coral":
                    return Coral;
                case "mediumslateblue":
                    return MediumSlateBlue;
                case "cornflowerblue":
                    return CornflowerBlue;
                case "mediumspringgreen":
                    return MediumSpringGreen;
                case "cornsilk":
                    return Cornsilk;
                case "mediumturquoise":
                    return MediumTurquoise;
                case "crimson":
                    return Crimson;
                case "mediumvioletred":
                    return MediumVioletRed;
                case "cyan":
                    return Cyan;
                case "midnightblue":
                    return MidnightBlue;
                case "darkblue":
                    return DarkBlue;
                case "mintcream":
                    return MintCream;
                case "darkcyan":
                    return DarkCyan;
                case "mistyrose":
                    return MistyRose;
                case "darkgoldenrod":
                    return DarkGoldenrod;
                case "moccasin":
                    return Moccasin;
                case "darkgray":
                    return DarkGray;
                case "navajowhite":
                    return NavajoWhite;
                case "darkgreen":
                    return DarkGreen;
                case "navy":
                    return Navy;
                case "darkkhaki":
                    return DarkKhaki;
                case "oldlace":
                    return OldLace;
                case "darkmagenta":
                    return DarkMagena;
                case "olive":
                    return Olive;
                case "darkolivegreen":
                    return DarkOliveGreen;
                case "olivedrab":
                    return OliveDrab;
                case "darkorange":
                    return DarkOrange;
                case "orange":
                    return Orange;
                case "darkorchid":
                    return DarkOrchid;
                case "orangered":
                    return OrangeRed;
                case "darkred":
                    return DarkRed;
                case "orchid":
                    return Orchid;
                case "darksalmon":
                    return DarkSalmon;
                case "palegoldenrod":
                    return PaleGoldenrod;
                case "darkseagreen":
                    return DarkSeaGreen;
                case "palegreen":
                    return PaleGreen;
                case "darkslateblue":
                    return DarkSlateBlue;
                case "paleturquoise":
                    return PaleTurquoise;
                case "darkslategray":
                    return DarkSlateGray;
                case "palevioletred":
                    return PaleVioletRed;
                case "darkturquoise":
                    return DarkTurquoise;
                case "papayawhip":
                    return PapayaWhip;
                case "darkviolet":
                    return DarkViolet;
                case "peachpuff":
                    return PeachPuff;
                case "deeppink":
                    return DeepPink;
                case "peru":
                    return Peru;
                case "deepskyblue":
                    return DeepSkyBlue;
                case "pink":
                    return Pink;
                case "dimgray":
                    return DimGray;
                case "plum":
                    return Plum;
                case "dodgerblue":
                    return DodgerBlue;
                case "powderblue":
                    return PowderBlue;
                case "firebrick":
                    return Firebrick;
                case "purple":
                    return Purple;
                case "floralwhite":
                    return FloralWhite;
                case "red":
                    return Red;
                case "forestgreen":
                    return ForestGreen;
                case "rosybrown":
                    return RosyBrown;
                case "fuschia":
                    return Fuschia;
                case "royalblue":
                    return RoyalBlue;
                case "gainsboro":
                    return Gainsboro;
                case "saddlebrown":
                    return SaddleBrown;
                case "ghostwhite":
                    return GhostWhite;
                case "salmon":
                    return Salmon;
                case "gold":
                    return Gold;
                case "sandybrown":
                    return SandyBrown;
                case "goldenrod":
                    return Goldenrod;
                case "seagreen":
                    return SeaGreen;
                case "gray":
                    return Gray;
                case "seashell":
                    return Seashell;
                case "green":
                    return Green;
                case "sienna":
                    return Sienna;
                case "greenyellow":
                    return GreenYellow;
                case "silver":
                    return Silver;
                case "honeydew":
                    return Honeydew;
                case "skyblue":
                    return SkyBlue;
                case "hotpink":
                    return HotPink;
                case "slateblue":
                    return SlateBlue;
                case "indianred":
                    return IndianRed;
                case "slategray":
                    return SlateGray;
                case "indigo":
                    return Indigo;
                case "snow":
                    return Snow;
                case "ivory":
                    return Ivory;
                case "springgreen":
                    return SpringGreen;
                case "khaki":
                    return Khaki;
                case "steelblue":
                    return SteelBlue;
                case "lavender":
                    return Lavender;
                case "tan":
                    return Tan;
                case "lavenderblush":
                    return LavenderBlush;
                case "teal":
                    return Teal;
                case "lawngreen":
                    return LawnGreen;
                case "thistle":
                    return Thistle;
                case "lemonchiffon":
                    return LemonChiffon;
                case "tomato":
                    return Tomato;
                case "lightblue":
                    return LightBlue;
                case "turquoise":
                    return Turquoise;
                case "lightcoral":
                    return LightCoral;
                case "violet":
                    return Violet;
                case "lightcyan":
                    return LightCyan;
                case "wheat":
                    return Wheat;
                case "lightgoldenrodyellow":
                    return LightGoldenrodYellow;
                case "white":
                    return White;
                case "lightgreen":
                    return LightGreen;
                case "whitesmoke":
                    return WhiteSmoke;
                case "lightgray":
                    return LightGray;
                case "yellow":
                    return Yellow;
                case "lightpink":
                    return LightPink;
                case "yellowgreen":
                    return YellowGreen;
                default:
                    return Empty;
            }
        }

        public static bool operator ==(Color color1, Color color2)
        {
            return color1.R == color2.R && color1.G == color2.G && color1.B == color2.B && color1.A == color2.A;
        }

        public static bool operator !=(Color color1, Color color2)
        {
            return !(color1 == color2);
        }

        public string ToHex()
        {
            string hexR = R.ToString("X2");
            string hexG = G.ToString("X2");
            string hexB = B.ToString("X2");
            // string hexA = A.ToString("X2");

            // 将转换后的十六进制颜色值组合在一起
            string hexColor = "#" + hexR + hexG + hexB;

            return hexColor;
        }

        public Color Invert()
        {
            return new Color((byte)(255 - R), (byte)(255 - G), (byte)(255 - B), A);
        }
    }
}
