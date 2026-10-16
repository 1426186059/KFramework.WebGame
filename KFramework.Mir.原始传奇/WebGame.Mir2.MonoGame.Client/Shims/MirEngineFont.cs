using System;
using System.Collections.Generic;
using KFramework.MonoGame;
// MirEngine 自带 Color / 可能自带 FontStyle，显式指定引擎侧类型，避免与本工程 shim 冲突。
using Color = KFramework.MonoGame.Color;
using FontStyle = KFramework.MonoGame.FontStyle;

namespace MirEngine
{
    // ==========================================================================
    //  WinForms 兼容层（本工程自提供）
    //  引擎基础库 KFramework.MonoGame/TextRenderer 只使用 IFont，不认识 Font 描述符；
    //  这里提供 System.Drawing.Font 风格的字体描述符，并让它实现 IFont，
    //  从而可以直接传给基础库的 TextRenderer.DrawText / MeasureText。
    // ==========================================================================

    /// <summary>字体度量单位，对齐 System.Drawing.GraphicsUnit。</summary>
    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6
    }

    /// <summary>
    /// 字体描述符：对齐 System.Drawing.Font（族名 / em 字号 / 样式 / 单位），同时实现引擎的 <see cref="IFont"/>。
    /// 度量与绘制委托给 <see cref="FontFactory"/> 按设备解析（缓存）出的 <see cref="SpriteFont"/>；
    /// 设备未就绪时度量返回 0、绘制为空操作。
    /// </summary>
    public sealed class Font : IFont, IDisposable
    {
        public string Name;
        /// <summary>em 字号（GraphicsUnit.Point 时按 *4/3 换算像素）。</summary>
        public float Size;
        public FontStyle Style;
        public GraphicsUnit Unit;

        /// <summary>行高（像素）。</summary>
        public int Height => (int)Math.Ceiling(GetPixelSize() * 4f / 3f);
        public bool Bold => (Style & FontStyle.Bold) != 0;
        public bool Italic => (Style & FontStyle.Italic) != 0;

        public Font(string familyName, float emSize)
            : this(familyName, emSize, FontStyle.Regular) { }

        public Font(string familyName, float emSize, FontStyle style)
        {
            Name = familyName; Size = emSize; Style = style; Unit = GraphicsUnit.Point;
        }

        public Font(string familyName, float emSize, GraphicsUnit unit)
        {
            Name = familyName; Size = emSize; Unit = unit;
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
        {
            Name = familyName; Size = emSize; Style = style; Unit = unit;
        }

        public Font(Font prototype, FontStyle newStyle)
        {
            Name = prototype.Name; Size = prototype.Size; Unit = prototype.Unit; Style = newStyle;
        }

        /// <summary>按 <see cref="Unit"/> 把 em 字号换算成像素字号。</summary>
        public float GetPixelSize()
            => Unit == GraphicsUnit.Pixel ? Size : Size * 4f / 3f;

        // ---------------- IFont ----------------

        public float LineSpacing => Resolved?.LineSpacing ?? 0f;

        public Vector2 Measure(string text) => Resolved?.Measure(text) ?? Vector2.Zero;

        public Vector2 MeasureString(string text) => Resolved?.MeasureString(text) ?? Vector2.Zero;

        // 注意：MirEngine 自带 Color，此处必须完全限定为引擎侧 Color 才能匹配 IFont 签名
        // （文件级 using 别名在命名空间成员面前不生效）。
        public void Draw(SpriteBatch batch, string text, Vector2 position, KFramework.MonoGame.Color color,
                         float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
            => Resolved?.Draw(batch, text, position, color, rotation, origin, scale, effects, layerDepth);

        /// <summary>本描述符对应的真实字形资源（缓存）；设备未就绪时为 null。</summary>
        private SpriteFont Resolved => FontFactory.Resolve(this);

        // ---------------- 其它 ----------------

        public object Clone() => new Font(Name, Size, Style, Unit);

        public void Dispose() { }

        public override string ToString()
            => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";
    }

    /// <summary>
    /// 字体描述符 -> 真实字形资源（<see cref="SpriteFont"/>）的解析与缓存；
    /// 另提供 DOM 输入覆盖层所需的 CSS 字体串（基础库 TextInputOverlay.Show 不认识描述符，由调用方算出后传入）。
    /// </summary>
    public static class FontFactory
    {
        /// <summary>当前设备。DXManager.Initialize 时赋值一次。</summary>
        public static GraphicsDevice Device { get; set; }

        private static readonly Dictionary<string, SpriteFont> _cache = new Dictionary<string, SpriteFont>();
        private static GraphicsDevice _cacheDevice;

        public static SpriteFont Resolve(Font font)
        {
            if (font == null) return null;

            GraphicsDevice device = Device;
            if (device == null) return null;

            if (!ReferenceEquals(_cacheDevice, device))
            {
                _cache.Clear();
                _cacheDevice = device;
            }

            string key = $"{font.Name}|{font.Size}|{(int)font.Unit}|{(int)font.Style}";
            if (_cache.TryGetValue(key, out SpriteFont cached)) return cached;

            string family = font.Name;
            if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
                family += ", sans-serif";

            SpriteFont sf = new SpriteFont(device, font.GetPixelSize(), family, font.Bold);
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
