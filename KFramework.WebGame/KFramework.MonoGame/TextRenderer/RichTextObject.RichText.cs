using System.Globalization;
using System.Text;

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
            float Descent);    // 该段缩放后的基线→字底距离

        /// <summary>
        /// 轻量富文本解析（Unity UGUI/TextMeshPro 风格标签的子集），供 <see cref="RichTextObject"/> 使用。
        /// 支持：
        ///   &lt;color=#RRGGBB&gt; / &lt;color=#RRGGBBAA&gt; / &lt;color=red&gt; / &lt;color&gt;（恢复默认）
        ///   &lt;b&gt;&lt;/b&gt; 粗体、&lt;i&gt;&lt;/i&gt; 斜体、&lt;u&gt;&lt;/u&gt; 下划线
        ///   &lt;size=20&gt;&lt;/size&gt; 字号（按倍数 scale 近似，见 RichTextStyle.Scale）
        ///   &lt;link="url"&gt;文字&lt;/link&gt; 或 &lt;a href="url"&gt;文字&lt;/a&gt; 超链接（蓝色+下划线）
        /// 未闭合/非法标签一律当普通文本容错处理。Measure/Draw 一律用 <see cref="PlainText"/>（去标签），
        /// 每个纯文本字符在 <see cref="Styles"/> 里有对应样式，折行后按字符区间取样式逐段绘制。
        /// </summary>
        internal sealed class RichTextDocument
        {
            public string PlainText { get; }
            public RichTextStyle[] Styles { get; }   // 与 PlainText 等长，逐字符样式（引用共享，省内存）

            private RichTextDocument(string plain, RichTextStyle[] styles)
            {
                PlainText = plain;
                Styles = styles;
            }

            /// <summary>text 不含富文本标签时返回 null（调用方走原快路径，零开销）。</summary>
            public static RichTextDocument Parse(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }
                if (text.IndexOf('<') < 0)
                {
                    return null;   // 快路径：无标签
                }

                var plain = new StringBuilder(text.Length);
                var styles = new List<RichTextStyle>(text.Length);
                var cur = RichTextStyle.Default;
                // 栈保存进入标签前的样式，用于 </color></b> 等恢复
                var stack = new Stack<RichTextStyle>();
                // link 的 url 随样式一起走，闭合时由栈恢复

                int i = 0;
                while (i < text.Length)
                {
                    if (text[i] != '<')
                    {
                        Append(plain, styles, text[i], cur);
                        i++;
                        continue;
                    }

                    int gt = text.IndexOf('>', i + 1);
                    if (gt < 0)
                    {
                        Append(plain, styles, text[i], cur);
                        i++;
                        continue;   // 无闭合 >，当普通字符
                    }
                    string tag = text.Substring(i + 1, gt - i - 1).Trim();
                    bool closing = tag.StartsWith("/");
                    string name = (closing ? tag.Substring(1) : tag).Trim();
                    int eq = name.IndexOf('=');
                    string key = (eq >= 0 ? name.Substring(0, eq) : name).Trim().ToLowerInvariant();
                    string val = eq >= 0 ? name.Substring(eq + 1).Trim().Trim('"', '\'') : string.Empty;

                    bool handled = false;
                    if (key == "color" || key == "colour")
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
                    else if (key == "b" && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(bold: true);
                    }
                    else if (key == "b" && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (key == "i" && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(italic: true);
                    }
                    else if (key == "i" && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (key == "u" && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        cur = cur.With(underline: true);
                    }
                    else if (key == "u" && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if (key == "size" && !closing)
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
                    else if (key == "size" && closing)
                    {
                        handled = true;
                        cur = stack.Count > 0 ? stack.Pop() : RichTextStyle.Default;
                    }
                    else if ((key == "link" || key == "a" || key == "a href") && !closing)
                    {
                        handled = true;
                        stack.Push(cur);
                        string url = val;
                        if (string.IsNullOrEmpty(url)) // <a href="url">
                        {
                            int href = tag.IndexOf("href", StringComparison.OrdinalIgnoreCase);
                            if (href >= 0)
                            {
                                int hq = tag.IndexOf('"', href);
                                if (hq < 0)
                                {
                                    hq = tag.IndexOf('\'', href);
                                }
                                if (hq >= 0)
                                {
                                    char q = tag[hq];
                                    int end = tag.IndexOf(q, hq + 1);
                                    if (end > hq)
                                    {
                                        url = tag.Substring(hq + 1, end - hq - 1);
                                    }
                                }
                            }
                        }
                        // 超链接默认蓝色+下划线，除非显式 <color> 指定
                        cur = cur.With(link: string.IsNullOrEmpty(url) ? null : url,
                                       color: cur.Color ?? new Color(64, 128, 255), underline: true);
                    }
                    else if ((key == "link" || key == "a") && closing)
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
                        Append(plain, styles, text[k], cur);
                    }
                    i = gt + 1;
                }

                return new RichTextDocument(plain.ToString(), styles.ToArray());
            }

            private static void Append(StringBuilder plain, List<RichTextStyle> styles, char ch, RichTextStyle style)
            {
                plain.Append(ch);
                styles.Add(style);
            }

            private static Color? ParseColor(string val)
            {
                if (string.IsNullOrEmpty(val))
                {
                    return null;
                }
                string s = val.Trim().TrimStart('#');
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(2);
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

            private static Color? NamedColor(string name)
            {
                switch (name.ToLowerInvariant())
                {
                    case "red":
                        return new Color(255, 0, 0);
                    case "green":
                        return new Color(0, 128, 0);
                    case "lime":
                        return new Color(0, 255, 0);
                    case "blue":
                        return new Color(0, 0, 255);
                    case "yellow":
                        return new Color(255, 255, 0);
                    case "orange":
                        return new Color(255, 165, 0);
                    case "white":
                        return new Color(255, 255, 255);
                    case "black":
                        return new Color(0, 0, 0);
                    case "gray":
                    case "grey":
                        return new Color(128, 128, 128);
                    case "cyan":
                    case "aqua":
                        return new Color(0, 255, 255);
                    case "magenta":
                        return new Color(255, 0, 255);
                    case "purple":
                        return new Color(128, 0, 128);
                    case "gold":
                        return new Color(255, 215, 0);
                    default:
                        return null;
                }
            }
        }

        /// <summary>单个字符/连续段的富文本样式。不可变；With 返回新实例，未指定的字段沿用旧值。</summary>
        internal sealed class RichTextStyle
        {
            public readonly Color? Color;         // null = 用宿主传入的 foreColor
            public readonly Color BaseColor;      // 调用方的默认前景色（用于 <color> 恢复）
            public readonly string Link;          // null = 非超链接
            public readonly bool Bold;
            public readonly bool Italic;
            public readonly bool Underline;
            public readonly float Scale;

            public RichTextStyle(Color? color, Color baseColor, string link, bool bold, bool italic, bool underline, float scale)
            {
                Color = color;
                BaseColor = baseColor;
                Link = link;
                Bold = bold;
                Italic = italic;
                Underline = underline;
                Scale = scale;
            }

            public static RichTextStyle Default
            {
                get
                {
                    return new RichTextStyle(null, default, null, false, false, false, 1f);
                }
            }

            public static RichTextStyle ForBase(Color baseColor)
            {
                return new RichTextStyle(null, baseColor, null, false, false, false, 1f);
            }

            public RichTextStyle With(Color? color = null, bool clearColor = false, string link = null, bool clearLink = false,
                                      bool? bold = null, bool? italic = null, bool? underline = null, float? scale = null)
            {
                return new RichTextStyle(
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
