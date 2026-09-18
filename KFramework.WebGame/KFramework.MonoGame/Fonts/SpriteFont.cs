namespace KFramework.MonoGame
{

    /// <summary>
    /// 位图字体：字形在首次使用时通过 Canvas2D 光栅化并写入一张动态字形图集，
    /// 之后所有文字共用同一张纹理，和精灵一起合并进批次，不需要任何字体资源文件。
    ///
    /// 字体来源有两类：
    /// 1) 系统字体——浏览器已可用的 family（默认 "system-ui, sans-serif"），直接构造即可；
    /// 2) 自定义字体——ttf / otf / woff，先经 <see cref="RegisterFontAsync(string, byte[])"/> 注册到
    ///    document.fonts，再按注册时用的 family 名构造（见 <see cref="FromFontAsync(GraphicsDevice, string, float, byte[], FontStyle, int, FontStretch, float)"/>）。
    /// 美术字（预先排好的位图字体）见 <see cref="BitmapFont"/>。
    /// </summary>
    public sealed class SpriteFont : IFont, IDisposable
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

        private const int Padding = 2;
        private const int AtlasSize = 1024;

        private readonly GraphicsDevice _device;
        private readonly string _fontCss;
        private readonly float _letterSpacing;
        private readonly Dictionary<char, Glyph> _glyphs = new();
        private readonly Texture2D _atlas;

        private int _shelfX;
        private int _shelfY;
        private int _shelfHeight;

        /// <summary>字号（像素）。</summary>
        public readonly float Size;

        /// <summary>字体家族：系统字体的 CSS family 串，或自定义字体注册时用的名字。</summary>
        public readonly string Family;

        /// <summary>字形样式（粗 / 斜 / 倾斜），可组合。</summary>
        public readonly FontStyle Style;

        /// <summary>字重（1~1000）；0 表示由 <see cref="Style"/> 是否含 Bold 决定（400 / 700）。</summary>
        public readonly int Weight;

        /// <summary>字形宽度档位（font-stretch）。</summary>
        public readonly FontStretch Stretch;

        /// <summary>字距（像素）；依赖浏览器 Canvas2D 的 letterSpacing，不支持的浏览器自动忽略。</summary>
        public readonly float LetterSpacing;

        /// <summary>基线到行顶的距离。</summary>
        public readonly float Ascent;

        /// <summary>行高（像素）。</summary>
        public readonly float LineHeight;

        /// <summary>行距（MonoGame 命名为 LineSpacing，等价于 LineHeight）。</summary>
        public float LineSpacing => LineHeight;

        /// <summary>用系统字体（浏览器已可用的 family）构造；<paramref name="bold"/> 为 true 时按粗体光栅化。</summary>
        public SpriteFont(GraphicsDevice device, float size = 20f, string family = "system-ui, sans-serif", bool bold = false)
            : this(device, size, family, bold ? FontStyle.Bold : FontStyle.Regular)
        {
        }

        /// <summary>
        /// 用系统字体或已注册的自定义字体构造，可指定斜体 / 字重 / 字形宽度 / 字距。
        /// <paramref name="weight"/> 为 0 时字重由 <paramref name="style"/> 决定；非 0 时按 1~1000 精确指定（如 600 半粗）。
        /// </summary>
        public SpriteFont(GraphicsDevice device, float size, string family, FontStyle style,
                          int weight = 0, FontStretch stretch = FontStretch.Normal, float letterSpacing = 0f)
        {
            _device = device;
            Size = size;
            Family = family;
            Style = style;
            Weight = weight;
            Stretch = stretch;
            LetterSpacing = letterSpacing;
            _letterSpacing = letterSpacing;
            _fontCss = FontCss.Build(size, family, style, weight, stretch);
            _atlas = device.CreateTexture(AtlasSize, AtlasSize);

            Span<int> metrics = stackalloc int[4];
            JSBind_Text.Measure("Hg", _fontCss, _letterSpacing, metrics);
            Ascent = metrics[2];
            LineHeight = metrics[1];

            PrintTool.Log($"[SpriteFont] {Family} 字号 {Size} | 样式 {Style} | 字重 {Weight} | 基线 {Ascent} | 行高 {LineHeight}");
        }

        #region 自定义字体（ttf / otf / woff）

        /// <summary>
        /// 注册自定义字体：从 <paramref name="url"/>（相对页面基址或绝对地址）下载字体文件并加入 document.fonts。
        /// 注册成功后即可用 <paramref name="family"/> 构造 <see cref="SpriteFont"/>。字体须在光栅化之前注册完成，故为异步。
        /// </summary>
        public static Task<bool> RegisterFontAsync(string family, string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(family);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            return JSBind_Text.LoadFontFromUrl(family, url);
        }

        /// <summary>
        /// 注册自定义字体：直接用字体文件字节（ttf / otf / woff，可来自 AssetBundle）构造 FontFace 并加入 document.fonts。
        /// 注册成功后即可用 <paramref name="family"/> 构造 <see cref="SpriteFont"/>。
        /// </summary>
        public static Task<bool> RegisterFontAsync(string family, byte[] bytes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(family);
            ArgumentNullException.ThrowIfNull(bytes);
            return JSBind_Text.LoadFontFromBytes(family, bytes);
        }

        /// <summary>注册自定义字体（字节）并直接产出可绘制的 <see cref="SpriteFont"/>；注册失败抛 <see cref="InvalidOperationException"/>。</summary>
        public static async Task<SpriteFont> FromFontAsync(GraphicsDevice device, string family, float size, byte[] bytes,
                                                           FontStyle style = FontStyle.Regular, int weight = 0,
                                                           FontStretch stretch = FontStretch.Normal, float letterSpacing = 0f)
        {
            bool ok = await RegisterFontAsync(family, bytes).ConfigureAwait(false);
            if (!ok) throw new InvalidOperationException($"自定义字体注册失败（字节不是合法字体或格式不支持）：{family}");
            return new SpriteFont(device, size, family, style, weight, stretch, letterSpacing);
        }

        /// <summary>注册自定义字体（URL）并直接产出可绘制的 <see cref="SpriteFont"/>；注册失败抛 <see cref="InvalidOperationException"/>。</summary>
        public static async Task<SpriteFont> FromFontAsync(GraphicsDevice device, string family, float size, string url,
                                                           FontStyle style = FontStyle.Regular, int weight = 0,
                                                           FontStretch stretch = FontStretch.Normal, float letterSpacing = 0f)
        {
            bool ok = await RegisterFontAsync(family, url).ConfigureAwait(false);
            if (!ok) throw new InvalidOperationException($"自定义字体加载失败（URL 不可达或格式不支持）：{url}");
            return new SpriteFont(device, size, family, style, weight, stretch, letterSpacing);
        }

        #endregion

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
            JSBind_Text.Measure(text, _fontCss, _letterSpacing, metrics);
            int advance = metrics[0];

            // 兜底：即使浏览器返回的度量异常，也保证字形盒子装得下这个字号的字符
            int height = Math.Max(metrics[1], (int)(Size * 1.15f) + 4);
            int ascent = Math.Max(metrics[2], (int)(Size * 0.85f) + 2);

            Rectangle bounds = Rectangle.Empty;
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
                    glyph = new Glyph(Rectangle.Empty, advance, drawOffset);
                    _glyphs[c] = glyph;
                    return glyph;
                }

                byte[] pixels = new byte[cellWidth * cellHeight * 4];
                JSBind_Text.Render(text, _fontCss, _letterSpacing, Padding, Padding + ascent, cellWidth, cellHeight, pixels);
                _atlas.SetData(pixels, _shelfX, _shelfY, cellWidth, cellHeight);

                // 照官方 MonoGame 的图集用法：整张图集是单个 Texture2D，字形用 source rect 绘制，可合批。
                bounds = new Rectangle(_shelfX, _shelfY, cellWidth, cellHeight);
                drawOffset = new Vector2(-Padding, -(Padding + ascent));

                _shelfX += cellWidth;
                _shelfHeight = Math.Max(_shelfHeight, cellHeight);
            }

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
                cursor.X += glyph.Advance * scale;
            }
        }

        public void Dispose() => _atlas.Dispose();
    }
}
