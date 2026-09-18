using System.Globalization;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 字形样式（可组合）：对应 CSS 的 font-style 与 font-weight 的粗体档位。
    /// 与 XNA/MonoGame 内容管线里 FontDescription 的 Style（Regular / Bold / Italic）对齐，
    /// 额外支持 Oblique（倾斜体），并按位组合出「粗斜体」等组合样式。
    /// </summary>
    [Flags]
    public enum FontStyle
    {
        /// <summary>常规（非粗、非斜）。</summary>
        Regular = 0,

        /// <summary>粗体（等价于 font-weight: bold / 700）。</summary>
        Bold = 1,

        /// <summary>斜体（font-style: italic，使用字体自带的斜体字形）。</summary>
        Italic = 2,

        /// <summary>倾斜体（font-style: oblique，由浏览器把正体做倾斜变换）。</summary>
        Oblique = 4,
    }

    /// <summary>
    /// 字形宽度（拉伸）档位：对应 CSS 的 font-stretch。仅在字体家族本身提供多宽度字形时生效。
    /// </summary>
    public enum FontStretch
    {
        Normal = 0,
        UltraCondensed,
        ExtraCondensed,
        Condensed,
        SemiCondensed,
        SemiExpanded,
        Expanded,
        ExtraExpanded,
        UltraExpanded,
    }

    /// <summary>把字体描述拼成 Canvas2D / CSS 能识别的 font 简写串。</summary>
    internal static class FontCss
    {
        /// <summary>CSS font-weight 的合法区间（1~1000，400 为常规、700 为粗体）。</summary>
        public const int MinWeight = 1;
        public const int MaxWeight = 1000;

        /// <summary>
        /// 拼出 CSS font 简写：<c>[font-style] [font-weight] [font-stretch] &lt;size&gt;px &lt;family&gt;</c>。
        /// <paramref name="weight"/> 为 0 时回退到 <see cref="FontStyle.Bold"/> 决定 400/700；非 0 时用它精确指定字重。
        /// </summary>
        public static string Build(float size, string family, FontStyle style, int weight, FontStretch stretch)
        {
            string styleCss = (style & FontStyle.Italic) != 0
                ? "italic "
                : (style & FontStyle.Oblique) != 0 ? "oblique " : "";

            string weightCss = weight != 0
                ? ClampWeight(weight).ToString(CultureInfo.InvariantCulture) + " "
                : (style & FontStyle.Bold) != 0 ? "bold " : "";

            string stretchCss = stretch == FontStretch.Normal ? "" : StretchToCss(stretch) + " ";

            return string.Concat(styleCss, weightCss, stretchCss,
                                 size.ToString(CultureInfo.InvariantCulture), "px ", family);
        }

        public static int ClampWeight(int weight)
            => weight < MinWeight ? MinWeight : weight > MaxWeight ? MaxWeight : weight;

        private static string StretchToCss(FontStretch stretch) => stretch switch
        {
            FontStretch.UltraCondensed => "ultra-condensed",
            FontStretch.ExtraCondensed => "extra-condensed",
            FontStretch.Condensed => "condensed",
            FontStretch.SemiCondensed => "semi-condensed",
            FontStretch.SemiExpanded => "semi-expanded",
            FontStretch.Expanded => "expanded",
            FontStretch.ExtraExpanded => "extra-expanded",
            FontStretch.UltraExpanded => "ultra-expanded",
            _ => "normal",
        };
    }
}
