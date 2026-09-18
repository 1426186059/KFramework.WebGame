using System.Threading;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 美术字 / 位图字体：字形由美术工具（BMFont / Hiero 等）预先排进一张图集，
    /// 运行时按 .fnt 描述里的矩形直接切片绘制（对应 Unity 的 Custom Font / Bitmap Font）。
    ///
    /// 与 <see cref="SpriteFont"/> 的区别：不做任何运行时光栅化，字形像素来自美术产出的图集，
    /// 因此可以是带描边、渐变、美术化的字形；绘制时通常传 <see cref="Color.White"/> 以保留原色。
    /// 同一图集页的字形与精灵一样可合批（单个 Texture2D + source rect）。
    /// </summary>
    public sealed class BitmapFont : IFont
    {
        private readonly struct Glyph
        {
            public readonly int Page;
            public readonly Rectangle Rect;
            public readonly Vector2 Offset;
            public readonly float Advance;

            public Glyph(int page, Rectangle rect, Vector2 offset, float advance)
            {
                Page = page;
                Rect = rect;
                Offset = offset;
                Advance = advance;
            }
        }

        private readonly BmFontData _data;
        private readonly Texture2D[] _pages;
        private readonly Dictionary<char, Glyph> _glyphs = new();

        /// <summary>字体名（.fnt 的 info face）。</summary>
        public string Face => _data.Face;

        /// <summary>导出字号（像素）。</summary>
        public float Size => _data.Size;

        /// <summary>基线到行顶的距离（像素）。</summary>
        public float Base => _data.Base;

        /// <summary>行高（像素）。</summary>
        public float LineHeight => _data.LineHeight;

        /// <summary>行距（MonoGame 命名为 LineSpacing，等价于 LineHeight）。</summary>
        public float LineSpacing => LineHeight;

        /// <summary>
        /// 缺字时的替代字符（对齐 Unity Font 的 Default Character）；为 null 时缺字按零宽处理。
        /// </summary>
        public char? DefaultCharacter { get; set; }

        /// <summary>用已解析的 .fnt 数据 + 单张图集页构造（BMFont 只有一个 page 的常见情形）。</summary>
        public BitmapFont(BmFontData data, Texture2D page)
            : this(data, new[] { page })
        {
        }

        /// <summary>用已解析的 .fnt 数据 + 多张图集页构造（按 page id 顺序）。</summary>
        public BitmapFont(BmFontData data, IReadOnlyList<Texture2D> pages)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(pages);
            if (pages.Count == 0) throw new ArgumentException("至少需要提供一张图集页纹理。", nameof(pages));

            _data = data;
            _pages = new Texture2D[pages.Count];
            for (int i = 0; i < pages.Count; i++)
                _pages[i] = pages[i] ?? throw new ArgumentNullException(nameof(pages), $"图集页 {i} 为空。");
        }

        #region 构造入口

        /// <summary>从 .fnt 文本 + 单张图集页构造（图集页纹理由调用方自行加载）。</summary>
        public static BitmapFont FromFnt(string fntText, Texture2D page)
            => new(BmFontData.Parse(fntText), page);

        /// <summary>从 .fnt 文本 + 多张图集页构造。</summary>
        public static BitmapFont FromFnt(string fntText, IReadOnlyList<Texture2D> pages)
            => new(BmFontData.Parse(fntText), pages);

        /// <summary>
        /// 从已加载的 AssetBundle 取 .fnt 描述与图集页纹理构造美术字。
        /// 图集页按 .fnt 里的 <c>page file</c> 解析：先按「.fnt 同目录 + 文件名」精确取，
        /// 取不到再退化为按文件名关键字匹配（部分工具会在 file 里写绝对路径）。
        /// </summary>
        public static BitmapFont FromBundle(AssetBundle bundle, GraphicsDevice device, string fntPath, bool strict = true)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentNullException.ThrowIfNull(device);
            ArgumentException.ThrowIfNullOrWhiteSpace(fntPath);

            BmFontData data = BmFontData.Parse(bundle.LoadText(fntPath, strict));

            var pages = new List<Texture2D>(data.PageFiles.Count);
            foreach (string pageFile in data.PageFiles)
            {
                string pagePath = ResolvePagePath(fntPath, pageFile);
                if (!bundle.TryLoadTexture(pagePath, device, out Texture2D? tex, strict)
                    && !bundle.TryLoadTexture(pageFile, device, out tex, false))
                {
                    throw new KeyNotFoundException($"位图字体“{fntPath}”的图集页未找到：{pagePath}");
                }
                pages.Add(tex!);
            }

            return new BitmapFont(data, pages);
        }

        /// <summary>.fnt 里的 page file 通常只写文件名，据此拼出与 .fnt 同目录的包内路径。</summary>
        private static string ResolvePagePath(string fntPath, string pageFile)
        {
            string file = pageFile.Replace('\\', '/');
            int cut = fntPath.LastIndexOf('/');
            return cut < 0 ? file : string.Concat(fntPath.AsSpan(0, cut + 1), file);
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
            char prev = '\0';
            foreach (char c in text)
            {
                if (c == '\n') { maxWidth = Math.Max(maxWidth, width); width = 0f; lines++; prev = '\0'; continue; }
                width += GetGlyph(c).Advance + _data.GetKerning(prev, c);
                prev = c;
            }
            return new Vector2(Math.Max(maxWidth, width), LineHeight * lines);
        }

        private Glyph GetGlyph(char c)
        {
            if (_glyphs.TryGetValue(c, out Glyph glyph)) return glyph;

            if (_data.Glyphs.TryGetValue(c, out BmGlyph bm))
            {
                glyph = new Glyph(bm.Page, new Rectangle(bm.X, bm.Y, bm.Width, bm.Height),
                                  new Vector2(bm.XOffset, bm.YOffset), bm.XAdvance);
                _glyphs[c] = glyph;
                return glyph;
            }

            // 缺字：优先用 DefaultCharacter 顶上，其次按零宽处理（不打断整串排版）
            glyph = DefaultCharacter is char fallback && _data.Glyphs.TryGetValue(fallback, out BmGlyph fb)
                ? new Glyph(fb.Page, new Rectangle(fb.X, fb.Y, fb.Width, fb.Height),
                            new Vector2(fb.XOffset, fb.YOffset), fb.XAdvance)
                : new Glyph(-1, Rectangle.Empty, Vector2.Zero, 0f);
            _glyphs[c] = glyph;
            return glyph;
        }

        public void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
                         float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (string.IsNullOrEmpty(text)) return;

            // 与 SpriteFont 保持一致：整串按基线推进画笔，逐字形用 source rect 绘制；
            // rotation / effects 由调用方在 SpriteBatch.Begin 的变换矩阵里处理（字形切片本身不旋转）。
            Vector2 cursor = position - origin * scale;
            float startX = cursor.X;
            char prev = '\0';

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    cursor.X = startX;
                    cursor.Y += LineHeight * scale;
                    prev = '\0';
                    continue;
                }

                Glyph glyph = GetGlyph(c);
                if (glyph.Page >= 0 && glyph.Page < _pages.Length && glyph.Rect.Width > 0 && glyph.Rect.Height > 0)
                {
                    Vector2 drawAt = new(cursor.X + glyph.Offset.X * scale,
                                         cursor.Y + (Base + glyph.Offset.Y) * scale);
                    batch.Draw(_pages[glyph.Page], drawAt, glyph.Rect, color, 0f, Vector2.Zero,
                               new Vector2(scale, scale), SpriteEffects.None, layerDepth);
                }

                cursor.X += (glyph.Advance + _data.GetKerning(prev, c)) * scale;
                prev = c;
            }
        }
    }
}
