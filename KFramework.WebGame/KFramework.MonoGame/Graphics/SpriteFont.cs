using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Graphics;

/// <summary>
/// 位图字体：字形在首次使用时通过 Canvas2D 光栅化并写入一张动态字形图集，
/// 之后所有文字共用同一张纹理，和精灵一起合并进批次，不需要任何字体资源文件。
/// </summary>
public sealed partial class SpriteFont : IDisposable
{
    private readonly struct Glyph
    {
        public readonly Texture2D? Texture;
        public readonly float Advance;
        public readonly Vector2 DrawOffset;

        public Glyph(Texture2D? texture, float advance, Vector2 drawOffset)
        {
            Texture = texture;
            Advance = advance;
            DrawOffset = drawOffset;
        }
    }

    private const int Padding = 2;
    private const int AtlasSize = 1024;

    private readonly GraphicsDevice _device;
    private readonly string _fontCss;
    private readonly Dictionary<char, Glyph> _glyphs = new();
    private readonly Texture2D _atlas;

    private int _shelfX;
    private int _shelfY;
    private int _shelfHeight;

    /// <summary>字号（像素）。</summary>
    public readonly float Size;

    /// <summary>基线到行顶的距离。</summary>
    public readonly float Ascent;

    /// <summary>行高（像素）。</summary>
    public readonly float LineHeight;

    public SpriteFont(GraphicsDevice device, float size = 28f, string family = "system-ui, sans-serif", bool bold = true)
    {
        _device = device;
        Size = size;
        _fontCss = $"{(bold ? "bold " : "")}{size.ToString(System.Globalization.CultureInfo.InvariantCulture)}px {family}";
        _atlas = device.CreateTexture(AtlasSize, AtlasSize);

        Span<int> metrics = stackalloc int[4];
        MeasureCore("Hg", _fontCss, metrics);
        Ascent = metrics[2];
        LineHeight = metrics[1];

        Console.WriteLine($"[SpriteFont] 字号 {Size} | 基线 {Ascent} | 行高 {LineHeight}");
    }

    public Vector2 Measure(string text)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        float width = 0f;
        float maxWidth = 0f;
        int lines = 1;
        foreach (char c in text)
        {
            if (c == '\n') { maxWidth = Math.Max(maxWidth, width); width = 0f; lines++; continue; }
            width += GetGlyph(c).Advance;
        }
        return new Vector2(Math.Max(maxWidth, width), LineHeight * lines);
    }

    /// <summary>测量并按需光栅化一个字符。</summary>
    private Glyph GetGlyph(char c)
    {
        if (_glyphs.TryGetValue(c, out Glyph glyph)) return glyph;

        string text = c.ToString();
        Span<int> metrics = stackalloc int[4];
        MeasureCore(text, _fontCss, metrics);
        int advance = metrics[0];

        // 兜底：即使浏览器返回的度量异常，也保证字形盒子装得下这个字号的字符
        int height = Math.Max(metrics[1], (int)(Size * 1.15f) + 4);
        int ascent = Math.Max(metrics[2], (int)(Size * 0.85f) + 2);

        Texture2D? texture = null;
        Vector2 drawOffset = new(-Padding, 0f);

        int cellWidth = advance + Padding * 2;
        int cellHeight = height + Padding * 2;

        if (cellWidth > 0 && cellHeight > 0 && !char.IsWhiteSpace(c))
        {
            if (_shelfX + cellWidth > AtlasSize)
            {
                _shelfX = 0;
                _shelfY += _shelfHeight;
                _shelfHeight = 0;
            }
            if (_shelfY + cellHeight > AtlasSize)
            {
                // 图集已满：后续字符退化为空，避免越界写入
                glyph = new Glyph(null, advance, drawOffset);
                _glyphs[c] = glyph;
                return glyph;
            }

            byte[] pixels = new byte[cellWidth * cellHeight * 4];
            RenderCore(text, _fontCss, Padding, Padding + ascent, cellWidth, cellHeight, pixels);
            _atlas.SetData(pixels, _shelfX, _shelfY, cellWidth, cellHeight);

            texture = _atlas.CreateSubtexture(new Rectangle(_shelfX, _shelfY, cellWidth, cellHeight));
            drawOffset = new Vector2(-Padding, -(Padding + ascent));

            _shelfX += cellWidth;
            _shelfHeight = Math.Max(_shelfHeight, cellHeight);
        }

        glyph = new Glyph(texture, advance, drawOffset);
        _glyphs[c] = glyph;
        return glyph;
    }

    internal void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
                       float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
    {
        if (string.IsNullOrEmpty(text)) return;

        Vector2 cursor = position - origin * scale;
        float startX = cursor.X;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                cursor.X = startX;
                cursor.Y += LineHeight * scale;
                continue;
            }

            Glyph glyph = GetGlyph(c);
            if (glyph.Texture is not null)
            {
                Vector2 drawAt = new(cursor.X + glyph.DrawOffset.X * scale,
                                     cursor.Y + (Ascent + glyph.DrawOffset.Y) * scale);
                batch.Draw(glyph.Texture, drawAt, null, color, 0f, Vector2.Zero,
                           new Vector2(scale, scale), SpriteEffects.None, layerDepth);
            }
            cursor.X += glyph.Advance * scale;
        }
    }

    [JSImport("measure", "text")]
    private static partial void MeasureCore(string text, string font, [JSMarshalAs<JSType.MemoryView>] Span<int> result);

    [JSImport("render", "text")]
    private static partial void RenderCore(string text, string font, int x, int y, int width, int height,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

    public void Dispose() => _atlas.Dispose();
}
