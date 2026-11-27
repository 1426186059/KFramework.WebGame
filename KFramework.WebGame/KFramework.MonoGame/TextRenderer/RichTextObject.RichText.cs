using System.Globalization;

namespace KFramework.MonoGame
{
    /// <summary>
    /// RichTextObject 的数据部分（partial）：富文本文档解析结果、逐字符样式、排版后的行段。
    /// 三者只服务于宿主，作为嵌套类型随 RichTextObject 一起编译，不再污染命名空间。
    /// </summary>
    public sealed partial class RichTextObject
    {
        /// <summary>富文本布局后的一段可见文字（连续同样式），供绘制与超链接命中共用。
        /// 仿 Unity TMP 的 characterInfo：每段自带按自身字号缩放好的基线与升降部，
        /// 渲染/命中只消费这些值，不再回查字体。坐标相对排版原点 (0,0)，使用时加 bounds.X/Y。</summary>
        internal readonly record struct RichTextRun(
            string Text,
            RichTextStyle Style,
            float X,
            float Width,
            float Height,
            float BaselineY,   // 该段字形基线 Y（已按 scale 落到行基线）
            float Ascent,      // 该段缩放后的基线→字顶距离
            float Descent,     // 该段缩放后的基线→字底距离
            int TextIndexBase);// 段首字符在 PlainText 中的索引

        /// <summary>
        /// 轻量富文本解析（Unity UGUI/TextMeshPro 风格标签的子集），供 <see cref="RichTextObject"/> 使用。
        /// 支持：
        ///   &lt;color=#RRGGBB&gt; / &lt;color=#RRGGBBAA&gt; / &lt;color=red&gt; / &lt;color&gt;（恢复默认）
        ///   &lt;b&gt;&lt;/b&gt; 粗体、&lt;i&gt;&lt;/i&gt; 斜体、&lt;u&gt;&lt;/u&gt; 下划线
        ///   &lt;size=20&gt;&lt;/size&gt; 字号（按倍数 scale 近似，见 RichTextStyle.Scale）
        ///   &lt;link="url"&gt;文字&lt;/link&gt; 或 &lt;a href="url"&gt;文字&lt;/a&gt; 超链接（蓝色+下划线）
        /// 未闭合/非法标签一律当普通文本容错处理。
        /// <para>GC 优化：解析全程用 ReadOnlySpan 切片，标签名/值不再 Substring/ToLowerInvariant，
        /// 只在产出 PlainText 时 ToString 一次；Styles 与 PlainText 等长，逐字符存值类型样式，无逐字符堆对象。</para>
        /// </summary>
        /// <summary>解析结果：字符已并入每个 <see cref="RichTextStyle"/>，直接缓存 List，不再 ToArray。</summary>
        internal class RichTextDocument
        {
            public readonly List<RichTextStyle> Styles = new List<RichTextStyle>();
            private readonly Stack<RichTextStyle> _stack = new Stack<RichTextStyle>();

            public bool bHaveRichText { get; internal set; }

            public void Reset()
            {
                Styles.Clear();
                _stack.Clear();
                bHaveRichText = false;
            }

            /// <summary>在当前实例内解析：复用 Styles / Stack，仅 Clear 后重填。
            /// rich=false / 无标签时复位标记，调用方无需再手动清理。</summary>
            public void Parse(ReadOnlySpan<char> text)
            {
                Styles.Clear();
                _stack.Clear();
                if (text.IsEmpty || text.IndexOf('<') < 0)
                {
                    bHaveRichText = false;
                    return; // 关闭富文本或无标签，直接当普通文本
                }
                bHaveRichText = true;

                var styles = Styles;
                var stack = _stack;
                var cur = RichTextStyle.Default;

                int i = 0;
                while (i < text.Length)
                {
                    if (text[i] != '<')
                    {
                        Append(styles, text[i], cur);
                        i++;
                        continue;
                    }

                    int relGt = text.Slice(i + 1).IndexOf('>');
                    if (relGt < 0)
                    {
                        Append(styles, text[i], cur);
                        i++;
                        continue;   // 无闭合 >，当普通字符
                    }

                    int gt = i + 1 + relGt;
                    ReadOnlySpan<char> tag = text.Slice(i + 1, gt - i - 1).Trim();
                    bool closing = tag.Length > 0 && tag[0] == '/';
                    ReadOnlySpan<char> name = (closing ? tag.Slice(1) : tag).Trim();
                    int eq = name.IndexOf('=');
                    ReadOnlySpan<char> key = (eq >= 0 ? name.Slice(0, eq) : name).Trim();
                    ReadOnlySpan<char> val = eq >= 0
                        ? name.Slice(eq + 1).Trim().Trim("\"'".AsSpan())
                        : ReadOnlySpan<char>.Empty;

                    bool handled = false;
                    if (Eq(key, "color") || Eq(key, "colour"))
                    {
                        handled = true;
                        if (closing)
                        {
                            cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                        }
                        else
                        {
                            stack.Push(cur);
                            Color? c = ParseColor(val);
                            cur = c.HasValue ? cur.With(color: c) : cur.With(clearColor: true); // <color> 无值=恢复默认
                        }
                    }
                    else if (Eq(key, "b") && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(bold: true);
                    }
                    else if (Eq(key, "b") && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (Eq(key, "i") && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(italic: true);
                    }
                    else if (Eq(key, "i") && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (Eq(key, "u") && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(underline: true);
                    }
                    else if (Eq(key, "u") && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (Eq(key, "size") && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        float scale = 1f;
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float sz))
                        {
                            scale = sz > 0 ? Math.Clamp(sz / 16f, 0.3f, 4f) : 1f; // 以16号为基准折算倍数
                        }
                        cur = cur.With(scale: scale);
                    }
                    else if (Eq(key, "size") && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if ((Eq(key, "link") || Eq(key, "a") || Eq(key, "a href")) && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        string url = val.IsEmpty ? null : new string(val);
                        if (url == null) // <a href="url"> 的 key 是 "a href"，值仍走 val；这里兜底从 tag 取 href
                        {
                            int href = IndexOfIgnoreCase(tag, "href");
                            if (href >= 0)
                            {
                                int relHq = tag.Slice(href).IndexOf('"');
                                if (relHq < 0)
                                {
                                    relHq = tag.Slice(href).IndexOf('\'');
                                }
                                if (relHq >= 0)
                                {
                                    int hq = href + relHq;
                                    char q = tag[hq];
                                    int end = tag.Slice(hq + 1).IndexOf(q);
                                    if (end >= 0)
                                    {
                                        url = new string(tag.Slice(hq + 1, end));
                                    }
                                }
                            }
                        }
                        // 超链接默认蓝色+下划线，除非显式 <color> 指定
                        cur = cur.With(link: string.IsNullOrEmpty(url) ? null : url,
                                       color: cur.Color ?? new Color(64, 128, 255), underline: true);
                    }
                    else if ((Eq(key, "link") || Eq(key, "a")) && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }

                    if (handled)
                    {
                        i = gt + 1;
                        continue;
                    }
                    // 未知标签：整段当普通文本输出（含尖括号）
                    for (int k = i; k <= gt; k++)
                    {
                        Append(styles, text[k], cur);
                    }
                    i = gt + 1;
                }
            }

            private static void Append(List<RichTextStyle> styles, char ch, RichTextStyle style)
            {
                styles.Add(style.With(character: ch));
            }

            private static bool Eq(ReadOnlySpan<char> span, string value)
            {
                return span.Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase);
            }

            private static int IndexOfIgnoreCase(ReadOnlySpan<char> span, string value)
            {
                return span.IndexOf(value.AsSpan(), StringComparison.OrdinalIgnoreCase);
            }

            private static Color? ParseColor(ReadOnlySpan<char> val)
            {
                if (val.IsEmpty)
                {
                    return null;
                }
                ReadOnlySpan<char> s = val.Trim().TrimStart("#".AsSpan());
                if (s.StartsWith("0x".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Slice(2);
                }
                if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                {
                    // 6 位 = RRGGBB（补 FF）；8 位 = RRGGBBAA
                    if (s.Length <= 6)
                    {
                        return Color.FromArgb(0xFF000000 | argb);
                    }
                    return Color.FromArgb((argb << 24) | ((argb >> 8) & 0x00FFFFFF));
                }
                return NamedColor(s);
            }

            private static Color? NamedColor(ReadOnlySpan<char> name)
            {
                if (Eq(name, "red")) return new Color(255, 0, 0);
                if (Eq(name, "green")) return new Color(0, 128, 0);
                if (Eq(name, "lime")) return new Color(0, 255, 0);
                if (Eq(name, "blue")) return new Color(0, 0, 255);
                if (Eq(name, "yellow")) return new Color(255, 255, 0);
                if (Eq(name, "orange")) return new Color(255, 165, 0);
                if (Eq(name, "white")) return new Color(255, 255, 255);
                if (Eq(name, "black")) return new Color(0, 0, 0);
                if (Eq(name, "gray") || Eq(name, "grey")) return new Color(128, 128, 128);
                if (Eq(name, "cyan") || Eq(name, "aqua")) return new Color(0, 255, 255);
                if (Eq(name, "magenta")) return new Color(255, 0, 255);
                if (Eq(name, "purple")) return new Color(128, 0, 128);
                if (Eq(name, "gold")) return new Color(255, 215, 0);
                return null;
            }
        }

        /// <summary>单个字符及其样式。不可变值类型，字符与样式同一份数据；With 返回新实例，无堆分配。</summary>
        internal readonly struct RichTextStyle
        {
            public readonly char Character;       // 该位置的可见字符（解析/排版都从这里读，不再依赖并行 string）
            public readonly Color? Color;         // null = 用宿主传入的 foreColor
            public readonly Color BaseColor;      // 调用方的默认前景色（用于 <color> 恢复）
            public readonly string Link;          // null = 非超链接
            public readonly bool Bold;
            public readonly bool Italic;
            public readonly bool Underline;
            public readonly float Scale;

            public RichTextStyle(char character, Color? color, Color baseColor, string link, bool bold, bool italic, bool underline, float scale)
            {
                Character = character;
                Color = color;
                BaseColor = baseColor;
                Link = link;
                Bold = bold;
                Italic = italic;
                Underline = underline;
                Scale = scale;
            }

            public static readonly RichTextStyle Default = new RichTextStyle('\0', null, default, null, false, false, false, 1f);

            public static RichTextStyle ForBase(Color baseColor)
            {
                return new RichTextStyle('\0', null, baseColor, null, false, false, false, 1f);
            }

            public RichTextStyle With(char? character = null, Color? color = null, bool clearColor = false, string link = null, bool clearLink = false,
                                      bool? bold = null, bool? italic = null, bool? underline = null, float? scale = null)
            {
                return new RichTextStyle(
                    character ?? Character,
                    clearColor ? null : (color ?? Color),
                    BaseColor,
                    clearLink ? null : (link ?? Link),
                    bold ?? Bold,
                    italic ?? Italic,
                    underline ?? Underline,
                    scale ?? Scale);
            }

            /// <summary>实际绘制色：显式 &lt;color&gt; 优先，否则用调用方前景色。</summary>
            public Color EffectiveColor(Color foreColor)
            {
                return Color ?? foreColor;
            }
        }
    }
}
