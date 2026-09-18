using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KFramework.MonoGame
{
    /// <summary>BMFont（.fnt）描述的一个字形。坐标与 BMFont 一致：图集页左上角为原点，Y 向下。</summary>
    public readonly struct BmGlyph
    {
        /// <summary>字符的 Unicode 码点。</summary>
        public readonly int Id;

        /// <summary>字形在图集页中的像素位置与尺寸。</summary>
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        /// <summary>绘制偏移：相对画笔原点的 x 偏移，与相对基线（common base）的 y 偏移。</summary>
        public readonly int XOffset;
        public readonly int YOffset;

        /// <summary>画完本字形后画笔前进的距离（像素）。</summary>
        public readonly int XAdvance;

        /// <summary>所在图集页下标（对应 .fnt 的 page id）。</summary>
        public readonly int Page;

        public BmGlyph(int id, int x, int y, int width, int height, int xOffset, int yOffset, int xAdvance, int page)
        {
            Id = id;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            XOffset = xOffset;
            YOffset = yOffset;
            XAdvance = xAdvance;
            Page = page;
        }
    }

    /// <summary>
    /// BMFont（.fnt）字体描述：来自美术工具（BMFont / Hiero / ShoeBox 等）导出的文本或 XML 描述文件，
    /// 只含排版信息（行高、基线、字形矩形、字距），像素在图集页纹理上，由 <see cref="BitmapFont"/> 持有并绘制。
    /// </summary>
    public sealed class BmFontData
    {
        private static readonly Regex AttrPattern = new(@"(\w+)\s*=\s*(""[^""]*""|\S+)", RegexOptions.Compiled);

        private readonly Dictionary<long, int> _kernings = new();

        /// <summary>字体名（.fnt 的 info face）。</summary>
        public string Face { get; } = string.Empty;

        /// <summary>导出时的字号（像素）。BMFont 用负值表示「按像素渲染」，此处取绝对值。</summary>
        public float Size { get; }

        /// <summary>行高（像素）。</summary>
        public int LineHeight { get; }

        /// <summary>基线到行顶的距离（像素）。</summary>
        public int Base { get; }

        /// <summary>图集页宽 / 高（仅元信息，实际尺寸以纹理为准）。</summary>
        public int ScaleW { get; }
        public int ScaleH { get; }

        /// <summary>图集页文件名（按 page id 顺序），供加载时拼出纹理路径。</summary>
        public IReadOnlyList<string> PageFiles { get; }

        /// <summary>码点 → 字形。</summary>
        public IReadOnlyDictionary<int, BmGlyph> Glyphs { get; }

        private BmFontData(string face, float size, int lineHeight, int baseLine, int scaleW, int scaleH,
                           IReadOnlyList<string> pageFiles, IReadOnlyDictionary<int, BmGlyph> glyphs)
        {
            Face = face;
            Size = size;
            LineHeight = lineHeight;
            Base = baseLine;
            ScaleW = scaleW;
            ScaleH = scaleH;
            PageFiles = pageFiles;
            Glyphs = glyphs;
        }

        /// <summary>取两个字之间的字距调整量（像素）；未定义返回 0。</summary>
        public int GetKerning(int first, int second)
            => _kernings.TryGetValue(KerningKey(first, second), out int amount) ? amount : 0;

        private static long KerningKey(int first, int second) => ((long)first << 32) | (uint)second;

        /// <summary>
        /// 解析 .fnt 内容：自动识别 XML（<c>&lt;font&gt;</c>）与纯文本（BMFont 默认导出）两种格式。
        /// 二进制格式（文件头 "BMF"）不支持，请用 BMFont 导出为文本或 XML。
        /// </summary>
        public static BmFontData Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            ReadOnlySpan<char> head = text.AsSpan(0, Math.Min(3, text.Length));
            if (head.StartsWith("BMF", StringComparison.Ordinal))
                throw new NotSupportedException("不支持二进制 .fnt（BMF），请在 BMFont 中导出为文本或 XML 格式。");

            return text.TrimStart().StartsWith('<') ? ParseXml(text) : ParseText(text);
        }

        private static BmFontData ParseText(string text)
        {
            string face = string.Empty;
            float size = 0f;
            int lineHeight = 0, baseLine = 0, scaleW = 0, scaleH = 0;
            var pages = new SortedDictionary<int, string>();
            var glyphs = new Dictionary<int, BmGlyph>();
            var kernings = new Dictionary<long, int>();

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                int cut = line.IndexOf(' ');
                string tag = cut < 0 ? line : line.Substring(0, cut);
                Dictionary<string, string> attrs = ParseAttrs(line);

                switch (tag)
                {
                    case "info":
                        face = Attr(attrs, "face");
                        size = MathF.Abs(AttrInt(attrs, "size", CultureInfo.InvariantCulture));
                        break;

                    case "common":
                        lineHeight = AttrInt(attrs, "lineHeight");
                        baseLine = AttrInt(attrs, "base");
                        scaleW = AttrInt(attrs, "scaleW");
                        scaleH = AttrInt(attrs, "scaleH");
                        break;

                    case "page":
                    {
                        int id = AttrInt(attrs, "id");
                        pages[id] = Attr(attrs, "file");
                        break;
                    }

                    case "char":
                    {
                        int id = AttrInt(attrs, "id");
                        if (id < 0) break;
                        glyphs[id] = new BmGlyph(
                            id,
                            AttrInt(attrs, "x"), AttrInt(attrs, "y"),
                            AttrInt(attrs, "width"), AttrInt(attrs, "height"),
                            AttrInt(attrs, "xoffset"), AttrInt(attrs, "yoffset"),
                            AttrInt(attrs, "xadvance"), AttrInt(attrs, "page"));
                        break;
                    }

                    case "kerning":
                        kernings[KerningKey(AttrInt(attrs, "first"), AttrInt(attrs, "second"))] = AttrInt(attrs, "amount");
                        break;
                }
            }

            if (glyphs.Count == 0) throw new InvalidDataException(".fnt 中没有字形（chars 段缺失或为空）。");

            var data = new BmFontData(face, size, lineHeight, baseLine, scaleW, scaleH,
                                      new List<string>(pages.Values), glyphs);
            foreach (var kv in kernings) data._kernings[kv.Key] = kv.Value;
            return data;
        }

        private static BmFontData ParseXml(string text)
        {
            XDocument doc = XDocument.Parse(text);
            XElement? root = doc.Root ?? throw new InvalidDataException(".fnt XML 解析失败：缺少根节点。");
            XElement? info = root.Element("info");
            XElement? common = root.Element("common");

            string face = info?.Attribute("face")?.Value ?? string.Empty;
            float size = MathF.Abs(int.TryParse(info?.Attribute("size")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawSize) ? rawSize : 0);

            int lineHeight = int.TryParse(common?.Attribute("lineHeight")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lh) ? lh : 0;
            int baseLine = int.TryParse(common?.Attribute("base")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bl) ? bl : 0;
            int scaleW = int.TryParse(common?.Attribute("scaleW")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sw) ? sw : 0;
            int scaleH = int.TryParse(common?.Attribute("scaleH")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sh) ? sh : 0;

            var pages = new SortedDictionary<int, string>();
            foreach (XElement page in root.Element("pages")?.Elements("page") ?? Enumerable.Empty<XElement>())
            {
                int id = int.TryParse(page.Attribute("id")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) ? pid : 0;
                pages[id] = page.Attribute("file")?.Value ?? string.Empty;
            }

            var glyphs = new Dictionary<int, BmGlyph>();
            foreach (XElement ch in root.Element("chars")?.Elements("char") ?? Enumerable.Empty<XElement>())
            {
                int id = XmlInt(ch, "id");
                if (id < 0) continue;
                glyphs[id] = new BmGlyph(id, XmlInt(ch, "x"), XmlInt(ch, "y"), XmlInt(ch, "width"), XmlInt(ch, "height"),
                                         XmlInt(ch, "xoffset"), XmlInt(ch, "yoffset"), XmlInt(ch, "xadvance"), XmlInt(ch, "page"));
            }

            if (glyphs.Count == 0) throw new InvalidDataException(".fnt 中没有字形（chars 段缺失或为空）。");

            var data = new BmFontData(face, size, lineHeight, baseLine, scaleW, scaleH,
                                      new List<string>(pages.Values), glyphs);
            foreach (XElement k in root.Element("kernings")?.Elements("kerning") ?? Enumerable.Empty<XElement>())
                data._kernings[KerningKey(XmlInt(k, "first"), XmlInt(k, "second"))] = XmlInt(k, "amount");
            return data;
        }

        private static int XmlInt(XElement e, string name)
            => int.TryParse(e.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        private static Dictionary<string, string> ParseAttrs(string line)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in AttrPattern.Matches(line))
                map[m.Groups[1].Value] = m.Groups[2].Value.Trim('"');
            return map;
        }

        private static string Attr(Dictionary<string, string> attrs, string name)
            => attrs.TryGetValue(name, out string? v) ? v : string.Empty;

        private static int AttrInt(Dictionary<string, string> attrs, string name, IFormatProvider? provider = null)
            => int.TryParse(Attr(attrs, name), NumberStyles.Integer, provider ?? CultureInfo.InvariantCulture, out int v) ? v : 0;
    }
}
