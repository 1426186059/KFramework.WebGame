namespace KFramework.MonoGame
{
    /// <summary>
    /// 自带渲染的矢量字体：字形由 C# 自己解析 ttf / otf / woff 的轮廓并光栅化（不借浏览器 Canvas2D），
    /// 写入动态字形图集后与精灵一样合并进 SpriteBatch 的 WebGL 批次。
    ///
    /// 与 <see cref="SpriteFont"/> 的区别：<see cref="SpriteFont"/> 用 CSS 字体名让 Canvas2D 光栅化（可吃系统字体，
    /// 但字形像素来自浏览器）；<see cref="KFont"/> 全程在引擎内完成（解析 → 光栅化 → 上传纹理 → 绘制），
    /// 代价是必须拿到字体文件字节，因此只支持自定义字体（AssetBundle 或已下载的字节），不支持浏览器系统字体。
    /// 美术字（BMFont 图集）见 <see cref="BitmapFont"/>。
    /// </summary>
    /// <example>
    /// <code>
    /// var font = KFont.FromBundle(ab, GraphicsDevice, "myres/fonts/hud.ttf", 24f);
    /// batch.DrawString(font, "你好 SCORE 0123", pos, Color.White);
    /// </code>
    /// </example>
    public sealed class KFont : IFont, IDisposable
    {
        private readonly struct Glyph
        {
            public readonly Rectangle Bounds;
            public readonly float Advance;
            public readonly Vector2 DrawOffset;

            public Glyph(Rectangle bounds, float advance, Vector2 drawOffset)
            {
                Bounds = bounds;
                Advance = advance;
                DrawOffset = drawOffset;
            }
        }

        /// <summary>字形四周留白（像素），避免相邻字形互相渗色。</summary>
        private const int Padding = 2;

        private readonly GraphicsDevice _device;
        private readonly TrueTypeFace _face;
        private readonly float _scale;
        private readonly float _skewTan;
        private readonly int _boldPixels;
        private readonly float _letterSpacing;
        private readonly Dictionary<char, Glyph> _glyphs = new();
        private readonly Texture2D _atlas;
        private readonly int _atlasSize;

        private int _shelfX;
        private int _shelfY;
        private int _shelfHeight;

        /// <summary>字号（像素）。</summary>
        public readonly float Size;

        /// <summary>字体名（name 表里的 family）。</summary>
        public string Family => _face.FamilyName;

        /// <summary>每 em 的字体单位数。</summary>
        public int UnitsPerEm => _face.UnitsPerEm;

        /// <summary>字形样式：斜切角度（度），0 为正体。</summary>
        public readonly float ObliqueDegrees;

        /// <summary>合成加粗的强度（像素膨胀半径），0 为不加粗。</summary>
        public int BoldPixels => _boldPixels;

        /// <summary>字距（像素），逐字叠加在推进宽度上。</summary>
        public float LetterSpacing => _letterSpacing;

        /// <summary>基线到行顶的距离（像素）。</summary>
        public readonly float Ascent;

        /// <summary>基线到行底的距离（像素）。</summary>
        public readonly float Descent;

        /// <summary>行高（像素）。</summary>
        public readonly float LineHeight;

        /// <summary>行距（MonoGame 命名为 LineSpacing，等价于 LineHeight）。</summary>
        public float LineSpacing => LineHeight;

        /// <summary>
        /// 用字体文件字节（ttf / otf(TrueType 轮廓) / woff）构造。
        /// </summary>
        /// <param name="device">图形设备（用于建字形图集纹理）。</param>
        /// <param name="size">字号（像素）。</param>
        /// <param name="fontBytes">字体文件字节。</param>
        /// <param name="atlasSize">字形图集边长（像素）；字形多（如中文）时可调大到 2048。</param>
        /// <param name="letterSpacing">字距（像素）。</param>
        /// <param name="obliqueDegrees">斜切角度（度，合成斜体）；0 为正体。</param>
        /// <param name="boldPixels">合成加粗强度（像素膨胀半径）；0 为不加粗。</param>
        public KFont(GraphicsDevice device, float size, byte[] fontBytes, int atlasSize = 1024,
                     float letterSpacing = 0f, float obliqueDegrees = 0f, int boldPixels = 0)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(fontBytes);
            ArgumentOutOfRangeException.ThrowIfLessThan(size, 1f);
            ArgumentOutOfRangeException.ThrowIfNegative(boldPixels);
            if (atlasSize < 128) throw new ArgumentOutOfRangeException(nameof(atlasSize), "图集边长至少 128 像素。");

            _device = device;
            Size = size;
            _letterSpacing = letterSpacing;
            _boldPixels = boldPixels;
            ObliqueDegrees = obliqueDegrees;
            _atlasSize = atlasSize;

            _face = TrueTypeFace.Load(fontBytes);
            _scale = size / _face.UnitsPerEm;
            _skewTan = MathF.Tan(obliqueDegrees * MathF.PI / 180f);

            Ascent = _face.Ascender * _scale;
            Descent = -_face.Descender * _scale;
            LineHeight = (_face.Ascender - _face.Descender + _face.LineGap) * _scale;

            _atlas = device.CreateTexture(atlasSize, atlasSize);

            PrintTool.Log($"[KFont] {Family} 字号 {Size} | upem {UnitsPerEm} | 基线 {Ascent:F1} | 行高 {LineHeight:F1} | 图集 {atlasSize}²");
        }

        /// <summary>用字体字节构造（<see cref="KFont(GraphicsDevice, float, byte[], int, float, float, int)"/> 的具名入口）。</summary>
        public static KFont FromBytes(GraphicsDevice device, float size, byte[] fontBytes, int atlasSize = 1024,
                                      float letterSpacing = 0f, float obliqueDegrees = 0f, int boldPixels = 0)
            => new(device, size, fontBytes, atlasSize, letterSpacing, obliqueDegrees, boldPixels);

        /// <summary>从已加载的 AssetBundle 取字体文件字节构造（包里放 ttf / otf / woff 即可）。</summary>
        public static KFont FromBundle(AssetBundle bundle, GraphicsDevice device, string fontPath, float size,
                                       int atlasSize = 1024, float letterSpacing = 0f,
                                       float obliqueDegrees = 0f, int boldPixels = 0)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentException.ThrowIfNullOrWhiteSpace(fontPath);
            return new KFont(device, size, bundle.LoadAsset(fontPath), atlasSize, letterSpacing, obliqueDegrees, boldPixels);
        }

        /// <summary>Measure 的 MonoGame 命名别名。</summary>
        public Vector2 MeasureString(string text) => Measure(text);

        public Vector2 Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.Zero;

            float width = 0f;
            float maxWidth = 0f;
            int lines = 1;
            foreach (char c in text)
            {
                if (c == '\n') { maxWidth = Math.Max(maxWidth, width); width = 0f; lines++; continue; }
                width += GetGlyph(c).Advance + _letterSpacing;
            }
            return new Vector2(Math.Max(maxWidth, width), LineHeight * lines);
        }

        /// <summary>测量并按需光栅化一个字符。</summary>
        private Glyph GetGlyph(char c)
        {
            if (_glyphs.TryGetValue(c, out Glyph glyph)) return glyph;

            int index = _face.LookupGlyph(c);
            float advance = _face.GetAdvance(index) * _scale;
            GlyphShape shape = _face.GetShape(index);

            if (shape.IsEmpty)
            {
                // 空格 / 缺字：只推进光标，不占图集
                glyph = new Glyph(Rectangle.Empty, advance, Vector2.Zero);
                _glyphs[c] = glyph;
                return glyph;
            }

            // 斜切会让 x 随 y 平移，据此把格子的左右边界算全
            float yMinPx = shape.YMin * _scale;
            float yMaxPx = shape.YMax * _scale;
            float skewLo = _skewTan > 0f ? _skewTan * yMinPx : _skewTan * yMaxPx;
            float skewHi = _skewTan > 0f ? _skewTan * yMaxPx : _skewTan * yMinPx;
            float left = MathF.Min(shape.XMin * _scale + skewLo, -_boldPixels);
            float right = MathF.Max(shape.XMax * _scale + skewHi, advance + _boldPixels);

            int cellWidth = (int)MathF.Ceiling(right - left) + Padding * 2;
            int cellHeight = (int)MathF.Ceiling(Ascent + Descent) + Padding * 2;
            if (cellWidth <= 0 || cellHeight <= 0 || cellWidth > _atlasSize || cellHeight > _atlasSize)
            {
                // 格子放不进图集（字号过大等极端情形）：退化为空字形，仅推进光标
                glyph = new Glyph(Rectangle.Empty, advance, Vector2.Zero);
                _glyphs[c] = glyph;
                return glyph;
            }

            if (_shelfX + cellWidth > _atlasSize)
            {
                _shelfX = 0;
                _shelfY += _shelfHeight;
                _shelfHeight = 0;
            }
            if (_shelfY + cellHeight > _atlasSize)
            {
                // 图集已满：后续字符退化为空，避免越界写入
                glyph = new Glyph(Rectangle.Empty, advance, Vector2.Zero);
                _glyphs[c] = glyph;
                return glyph;
            }

            float originX = -left + Padding;
            float baselineY = Padding + Ascent;

            byte[] pixels = new byte[cellWidth * cellHeight * 4];
            KFontRaster.Rasterize(shape, _scale, _skewTan, originX, baselineY,
                                  cellWidth, cellHeight, pixels, _boldPixels);
            _atlas.SetData(pixels, _shelfX, _shelfY, cellWidth, cellHeight);

            // 整张图集是一个 Texture2D，字形用 source rect 绘制，和精灵一样可合批
            var bounds = new Rectangle(_shelfX, _shelfY, cellWidth, cellHeight);
            var drawOffset = new Vector2(left - Padding, -(Padding + Ascent));

            _shelfX += cellWidth;
            _shelfHeight = Math.Max(_shelfHeight, cellHeight);

            glyph = new Glyph(bounds, advance, drawOffset);
            _glyphs[c] = glyph;
            return glyph;
        }

        public void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
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
                if (glyph.Bounds.Width > 0)
                {
                    Vector2 drawAt = new(cursor.X + glyph.DrawOffset.X * scale,
                                         cursor.Y + (Ascent + glyph.DrawOffset.Y) * scale);
                    batch.Draw(_atlas, drawAt, glyph.Bounds, color, 0f, Vector2.Zero,
                               new Vector2(scale, scale), SpriteEffects.None, layerDepth);
                }
                cursor.X += (glyph.Advance + _letterSpacing) * scale;
            }
        }

        public void Dispose() => _atlas.Dispose();
    }
}
