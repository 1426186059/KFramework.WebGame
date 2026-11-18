using KFramework.MonoGame;
using FontStyle = KFramework.MonoGame.FontStyle;

namespace MirEngine
{
    /// </summary>
    public sealed class Font : IFont, IDisposable
    {
        public string Name;
        /// <summary>em 字号（GraphicsUnit.Point 时按 *4/3 换算像素）。</summary>
        public float Size;
        public FontStyle Style;

        public bool Bold => (Style & FontStyle.Bold) != 0;
        public bool Italic => (Style & FontStyle.Italic) != 0;

        public Font(string familyName, float emSize)
            : this(familyName, emSize, FontStyle.Regular) { }

        public Font(string familyName, float emSize, FontStyle style)
        {
            Name = familyName; Size = emSize; Style = style;
        }

        public float GetPixelSize()
        { 
            return Size * 4f / 3f;
        }

        public float LineSpacing => Resolved?.LineSpacing ?? 0f;

        public float Ascent => Resolved?.Ascent ?? 0f;

        public float Descent => (Resolved as IFont)?.Descent ?? 0f;

        public Vector2 Measure(string text) => Resolved?.Measure(text) ?? Vector2.Zero;

        public Vector2 MeasureString(string text) => Resolved?.MeasureString(text) ?? Vector2.Zero;

        // 注意：MirEngine 自带 Color，此处必须完全限定为引擎侧 Color 才能匹配 IFont 签名
        // （文件级 using 别名在命名空间成员面前不生效）。
        public void Draw(SpriteBatch batch, string text, Vector2 position, KFramework.MonoGame.Color color,
                         float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
            => Resolved?.Draw(batch, text, position, color, rotation, origin, scale, effects, layerDepth);

        /// <summary>本描述符对应的真实字形资源（缓存）；设备未就绪时为 null。</summary>
        private SpriteFont Resolved => FontFactory.Resolve(this);

        public void Dispose() { }

        public override string ToString()
            => $"[Font: Name={Name}, Size={Size}, Style={Style}]";
    }

    /// <summary>
    /// 字体描述符 -> 真实字形资源（<see cref="SpriteFont"/>）的解析与缓存；
    /// 另提供 DOM 输入覆盖层所需的 CSS 字体串（基础库 TextInputOverlay.Show 不认识描述符，由调用方算出后传入）。
    /// </summary>
    public static class FontFactory
    {
        private static readonly Dictionary<string, SpriteFont> _cache = new Dictionary<string, SpriteFont>();

        public static SpriteFont Resolve(Font font)
        {
            if (font == null) return null;

            string key = $"{font.Name}|{font.Size}|{(int)font.Style}";
            if (_cache.TryGetValue(key, out SpriteFont cached)) return cached;

            string family = font.Name;
            if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
                family += ", sans-serif";

            SpriteFont sf = new SpriteFont(font.GetPixelSize(), family, font.Bold);
            _cache[key] = sf;
            return sf;
        }

        /// <summary>把描述符还原成完整 CSS 字体串（如 "bold 14px Tahoma"），供 DOM 输入框保持字形一致。</summary>
        public static string BuildCssFont(Font font, double fontScale = 1.0)
        {
            if (font == null) return "10px sans-serif";

            string style = font.Bold ? "bold " : (font.Italic ? "italic " : string.Empty);
            float px = (float)(font.GetPixelSize() * fontScale);

            string family = font.Name;
            if (string.IsNullOrEmpty(family)) family = "sans-serif";
            else if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                     family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                     family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
                family += ", sans-serif";

            return $"{style}{px.ToString(System.Globalization.CultureInfo.InvariantCulture)}px {family}";
        }

        /// <summary>按缩放系数取 DOM 侧应使用的像素字号。</summary>
        public static double GetPixelSize(Font font, double fontScale = 1.0)
            => font != null ? font.GetPixelSize() * fontScale : 10d;
    }
}
