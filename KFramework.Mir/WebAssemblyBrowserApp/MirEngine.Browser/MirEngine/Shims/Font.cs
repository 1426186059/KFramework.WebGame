namespace MirEngine
{
    public class Font : System.IDisposable
    {
        public string Name { get; set; }
        public float Size { get; set; }
        public FontStyle Style { get; set; }

        public bool Bold => (Style & FontStyle.Bold) != 0;
        public bool Italic => (Style & FontStyle.Italic) != 0;
        public bool Underline => (Style & FontStyle.Underline) != 0;
        public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

        // 高仿实现无非托管资源，Dispose 仅用于满足原版 using/Dispose 调用点。
        public void Dispose() { }

        /// <summary>转成 canvas 2D 的 CSS font 串（如 "bold 12px 'Tahoma', sans-serif"）。
        /// 浏览器没有 Tahoma 时由 CSS fallback 兜底，故统一附加通用 sans-serif。</summary>
        public string ToCss()
        {
            string weight = Bold ? "bold " : string.Empty;
            string slant = Italic ? "italic " : string.Empty;
            string name = string.IsNullOrEmpty(Name) ? "Tahoma" : Name;
            return $"{weight}{slant}{Size}px '{name}', sans-serif";
        }

        public static Font Default => new Font("Arial", 12, FontStyle.Regular);

        public Font(string name, float size)
        {
            Name = name;
            Size = size;
            Style = FontStyle.Regular;
        }

        public Font(string name, float size, FontStyle style)
        {
            Name = name;
            Size = size;
            Style = style;
        }

        public bool Equals(Font otherFont)
        {
            if (otherFont == null)
                return false;

            return Name == otherFont.Name && Size == otherFont.Size && Style == otherFont.Style;
        }

        public override string ToString()
        {
            return $"{Name}, {Size}, {Style}";
        }

        public int Height
        {
            get
            {
                return 20;
            }
        }

        // 重载 == 操作符
        public static bool operator ==(Font f1, Font f2)
        {
            if (ReferenceEquals(f1, null))
                return ReferenceEquals(f2, null);

            return f1.Equals(f2);
        }

        // 重载 != 操作符
        public static bool operator !=(Font f1, Font f2)
        {
            return !(f1 == f2);
        }

        public override bool Equals(object obj)
        {
            if (obj is Font font)
            {
                return Equals(font);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (Name, Size, Style).GetHashCode();
        }
    }

    public enum FontStyle
    {
        Regular = 0x0,
        Bold = 0x1,
        Italic = 0x2,
        Underline = 0x4,
        Strikeout = 0x8
    }

    // TextFormatFlags 不在此定义：两边 shim 已提供 System.Windows.Forms.TextFormatFlags，
    // 若同名类型同时存在于 System.Drawing / System.Windows.Forms 会导致 CS0104 歧义。
}
