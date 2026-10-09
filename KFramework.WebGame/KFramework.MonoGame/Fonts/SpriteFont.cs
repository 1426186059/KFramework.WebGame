namespace KFramework.MonoGame
{
    /// <summary>
    /// Canvas2D 光栅化的精灵字体：逐字形画进字形图集，绘制时按 source rect 出图（与精灵同一条路径，可合批）。
    /// <para>
    /// <b>多图集</b>：一页（<see cref="AtlasSize"/>²）塞满就自动新建下一页，字形用
    /// <see cref="Glyph.AtlasIndex"/> 记住自己在第几页 —— 中文这种几千字的字集不会再出现"后面的字直接消失"。
    /// 代价是绘制跨页时要换纹理：<see cref="SpriteBatch"/> 按纹理分段，连续文本通常只在页边界处多一次 draw。
    /// </para>
    /// </summary>
    public sealed class SpriteFont : IFont, IDisposable
    {
        private readonly struct Glyph
        {
            /// <summary>字形所在图集页号；-1 = 没有贴图（空白字符 / 退化字形，只推进光标）。</summary>
            public readonly int AtlasIndex;

            public readonly Rectangle Bounds;
            public readonly float Advance;
            public readonly Vector2 DrawOffset;

            public Glyph(int atlasIndex, Rectangle bounds, float advance, Vector2 drawOffset)
            {
                AtlasIndex = atlasIndex;
                Bounds = bounds;
                Advance = advance;
                DrawOffset = drawOffset;
            }
        }

        private const int Padding = 2;
        private const int AtlasSize = 1024;

        /// <summary>图集页数上限（每页 <see cref="AtlasSize"/>² RGBA ≈ 4MB，16 页 ≈ 64MB 封顶，防病态字集吃光显存）。</summary>
        private const int MaxAtlasPages = 16;

        private readonly GraphicsDevice _device;
        private readonly string _fontCss;
        private readonly float _letterSpacing;
        private readonly Dictionary<char, Glyph> _glyphs = new();

        /// <summary>字形图集页表：第 0 页在构造时建好，之后一页满就追加一页；字形用 <see cref="Glyph.AtlasIndex"/> 指回来。</summary>
        private readonly List<Texture2D> _atlases = new();

        private int _shelfX;
        private int _shelfY;
        private int _shelfHeight;

        /// <summary>当前已使用的图集页数（诊断用）。</summary>
        public int AtlasCount => _atlases.Count;

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
            _atlases.Add(device.CreateTexture(AtlasSize, AtlasSize));

            Span<short> metrics = stackalloc short[4];
            JSBind_Text.Measure("Hg", _fontCss, _letterSpacing, metrics);
            Ascent = metrics[2];
            LineHeight = metrics[1];

            PrintTool.Log($"[SpriteFont] {Family} 字号 {Size} | 样式 {Style} | 字重 {Weight} | 基线 {Ascent} | 行高 {LineHeight}");
        }
            
        public static Task<bool> RegisterFontAsync(string family, string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(family);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            return JSBind_Text.LoadFontFromUrl(family, url);
        }

        public static Task<bool> RegisterFontAsync(string family, byte[] bytes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(family);
            ArgumentNullException.ThrowIfNull(bytes);
            return JSBind_Text.LoadFontFromBytes(family, bytes);
        }

        /// <summary>注册自定义字体（字节）并直接产出可绘制的 <see cref="SpriteFont"/>；注册失败抛 <see cref="InvalidOperationException"/>。</summary>
        public static async Task<SpriteFont> FromFontAsync(
            GraphicsDevice device, 
            string family, 
            float size, 
            byte[] bytes,
            FontStyle style = FontStyle.Regular, 
            int weight = 0,
            FontStretch stretch = FontStretch.Normal, 
            float letterSpacing = 0f)
        {
            bool ok = await RegisterFontAsync(family, bytes).ConfigureAwait(false);
            if (!ok) throw new InvalidOperationException($"自定义字体注册失败（字节不是合法字体或格式不支持）：{family}");
            return new SpriteFont(device, size, family, style, weight, stretch, letterSpacing);
        }

        /// <summary>注册自定义字体（URL）并直接产出可绘制的 <see cref="SpriteFont"/>；注册失败抛 <see cref="InvalidOperationException"/>。</summary>
        public static async Task<SpriteFont> FromFontAsync(
            GraphicsDevice device, 
            string family, 
            float size, 
            string url,
            FontStyle style = FontStyle.Regular, 
            int weight = 0,
            FontStretch stretch = FontStretch.Normal, 
            float letterSpacing = 0f)
        {
            bool ok = await RegisterFontAsync(family, url).ConfigureAwait(false);
            if (!ok) throw new InvalidOperationException($"自定义字体加载失败（URL 不可达或格式不支持）：{url}");
            return new SpriteFont(device, size, family, style, weight, stretch, letterSpacing);
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
                width += GetGlyph(c).Advance;
            }
            return new Vector2(Math.Max(maxWidth, width), LineHeight * lines);
        }

        /// <summary>测量并按需光栅化一个字符。</summary>
        private Glyph GetGlyph(char c)
        {
            if (_glyphs.TryGetValue(c, out Glyph glyph)) return glyph;

            string text = c.ToString();
            Span<short> metrics = stackalloc short[4];
            JSBind_Text.Measure(text, _fontCss, _letterSpacing, metrics);
            int advance = metrics[0];

            // 兜底：即使浏览器返回的度量异常，也保证字形盒子装得下这个字号的字符
            int height = Math.Max(metrics[1], (int)(Size * 1.15f) + 4);
            int ascent = Math.Max(metrics[2], (int)(Size * 0.85f) + 2);

            Vector2 drawOffset = new(-Padding, 0f);

            // 仅格子留白加 1px 防裁切；布局 advance 不再额外 +1（见 text.ts measure），文字宽度才与 GDI 一致。
            int cellWidth = advance + Padding * 2 + 1;
            int cellHeight = height + Padding * 2;

            // 空白字符 / 度量退化：不占图集，只推进光标
            if (cellWidth <= 0 || cellHeight <= 0 || char.IsWhiteSpace(c))
            {
                glyph = new Glyph(-1, Rectangle.Empty, advance, drawOffset);
                _glyphs[c] = glyph;
                return glyph;
            }

            // 单格比整页还大（字号病态）：退化为空字形，避免越界写入
            if (cellWidth > AtlasSize || cellHeight > AtlasSize)
            {
                PrintTool.Log($"[SpriteFont] {Family} 字号 {Size}：字形格 {cellWidth}×{cellHeight} 超过单页 {AtlasSize}²，字符「{c}」不绘制");
                glyph = new Glyph(-1, Rectangle.Empty, advance, drawOffset);
                _glyphs[c] = glyph;
                return glyph;
            }

            // 当前页这一行放不下 → 翻行；整页放不下 → 开新页（多图集）
            if (_shelfX + cellWidth > AtlasSize)
            {
                _shelfX = 0;
                _shelfY += _shelfHeight;
                _shelfHeight = 0;
            }
            if (_shelfY + cellHeight > AtlasSize)
            {
                if (!OpenNextAtlas())
                {
                    // 页数已达上限：退化为空字形，避免越界写入
                    PrintTool.Log($"[SpriteFont] {Family} 字号 {Size}：图集已达 {MaxAtlasPages} 页上限，字符「{c}」不绘制");
                    glyph = new Glyph(-1, Rectangle.Empty, advance, drawOffset);
                    _glyphs[c] = glyph;
                    return glyph;
                }

                // 新页从左上角重新起排
                _shelfX = 0;
                _shelfY = 0;
                _shelfHeight = 0;
            }

            int atlasIndex = _atlases.Count - 1;
            byte[] pixels = new byte[cellWidth * cellHeight * 4];
            JSBind_Text.Render(text, _fontCss, _letterSpacing, Padding, Padding + ascent, cellWidth, cellHeight, pixels);
            _atlases[atlasIndex].SetData(pixels, _shelfX, _shelfY, cellWidth, cellHeight);

            // 照官方 MonoGame 的图集用法：字形用 source rect 绘制，与精灵同批。
            Rectangle bounds = new(_shelfX, _shelfY, cellWidth, cellHeight);
            drawOffset = new Vector2(-Padding, -(Padding + ascent));

            _shelfX += cellWidth;
            _shelfHeight = Math.Max(_shelfHeight, cellHeight);

            glyph = new Glyph(atlasIndex, bounds, advance, drawOffset);
            _glyphs[c] = glyph;
            return glyph;
        }

        /// <summary>当前页塞满时开新页；已达 <see cref="MaxAtlasPages"/> 上限返回 false。</summary>
        private bool OpenNextAtlas()
        {
            if (_atlases.Count >= MaxAtlasPages) return false;

            _atlases.Add(_device.CreateTexture(AtlasSize, AtlasSize));
            PrintTool.Log($"[SpriteFont] {Family} 字号 {Size}：第 {_atlases.Count - 1} 页已满，新建第 {_atlases.Count} 页（{AtlasSize}²）");
            return true;
        }

        public void Draw(
            SpriteBatch batch, 
            string text, 
            Vector2 position, 
            Color color,
            float rotation, 
            Vector2 origin, 
            float scale, 
            SpriteEffects effects, 
            float layerDepth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

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
                // 越界保护与 BitmapFont 一致：-1（无贴图）与 Dispose 之后的页号都不画
                if (glyph.AtlasIndex >= 0 && glyph.AtlasIndex < _atlases.Count
                    && glyph.Bounds.Width > 0 && glyph.Bounds.Height > 0)
                {
                    Vector2 drawAt = new Vector2(
                        cursor.X + glyph.DrawOffset.X * scale,
                        cursor.Y + (Ascent + glyph.DrawOffset.Y) * scale);
                    batch.DrawGlyph(
                        _atlases[glyph.AtlasIndex], 
                        drawAt, 
                        glyph.Bounds, 
                        color, 
                        0f, 
                        Vector2.Zero,
                        new Vector2(scale, scale), SpriteEffects.None, layerDepth);
                }
                cursor.X += glyph.Advance * scale;
            }

        }

        public void Dispose()
        {
            // 多图集：每一页都要释放
            foreach (Texture2D atlas in _atlases) atlas.Dispose();
            _atlases.Clear();
        }

    }
}
