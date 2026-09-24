using KFramework.MonoGame;

public class Font : IDisposable
{
    public string Name;
    public float Size;
    public FontStyle Style;
    public GraphicsUnit Unit;

    public int Height => (int)Math.Ceiling(Size * 4f / 3f);
    public bool Bold => (Style & FontStyle.Bold) != 0;

    public Font(string name, float size, FontStyle style = FontStyle.Regular) 
    { 
        Name = name; Size = size; Style = style; Unit = GraphicsUnit.Point; 
    }

    public Font(string name, float size, GraphicsUnit unit) 
    { 
        Name = name; Size = size; Unit = unit;
    }

    public Font(float size) 
    { 
        Name = "Arial"; Size = size; Unit = GraphicsUnit.Point; 
    }

    public Font(float size, FontStyle style) 
    { 
        Name = "Arial"; Size = size; Style = style; Unit = GraphicsUnit.Point; 
    }

    public static Font FromFont(Font f) => new Font(f.Name, f.Size, f.Style);

    // 每个 Font 实例直接持有一个 SpriteFont 句柄（懒创建、随 Font 复用，避免每帧/每次按 css 重建）。
    // 构造时保证字体族带系统 sans-serif 兜底，避免中文专用/子集字体缺数字、标点时显示空白。
    private SpriteFont _spriteFont;
    public SpriteFont GetFont()
    {
        if (_spriteFont == null)
        {
            float px = Unit == GraphicsUnit.Pixel ? Size : Size * 4f / 3f;
            string family = Name;
            if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
            {
                family += ", sans-serif";
            }
            var device = Client.MirGraphics.DXManager.GDevice;
            _spriteFont = new SpriteFont(device, px, family, Bold);
        }
        return _spriteFont;

    }

    public void Dispose()
    {
        if (_spriteFont != null) { _spriteFont.Dispose(); _spriteFont = null; }
    }

    public string ToCss()
    {
        string s = string.Empty;
        if ((Style & FontStyle.Bold) != 0) s += "bold ";
        float px = Unit == GraphicsUnit.Point ? Size * 4f / 3f : Size;
        return $"{s}{px}px {Name}";
    }

    // ---- 测量相关 API（对齐原版 TextRenderer.MeasureText(g, text, font[, size, flags])）----
    // 全部基于 Font 自身持有的 SpriteFont 句柄，调用处从 TextRenderer 静态方法迁移到 Font 实例方法。

    // 单行宽度（像素，向上取整），对应原版 MeasureText(...).Width
    public int MeasureWidth(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var sf = GetFont();
        if (sf == null) return 0;
        return (int)Math.Ceiling(sf.MeasureString(text).X);
    }

    // 单行宽度（浮点，便于精确排版/光标定位）
    public float MeasureWidthF(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        var sf = GetFont();
        if (sf == null) return 0f;
        return sf.MeasureString(text).X;
    }

    // 单个字符宽度（浮点），用于 NPC 链接前缀宽度、光标定位
    public float MeasureCharWidth(char ch)
    {
        if (ch == '\n' || ch == '\r') return 0f;
        var sf = GetFont();
        if (sf == null) return 0f;
        return sf.MeasureString(ch.ToString()).X;
    }

    // 实际渲染行高（SpriteFont 度量，对应原版 Font.Height 的渲染语义）
    public float LineHeight
    {
        get
        {
            var sf = GetFont();
            return sf == null ? 0f : sf.LineHeight;
        }
    }

    // 对齐原版 TextRenderer.MeasureText(g, text, font) —— 单行、不限宽
    public Size MeasureText(string text)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;
        var sf = GetFont();
        if (sf == null) return Size.Empty;
        var v = sf.MeasureString(text);
        return new Size((int)Math.Ceiling(v.X), (int)Math.Ceiling(v.Y));
    }

    // 对齐原版 TextRenderer.MeasureText(g, text, font, proposedSize) —— proposedSize.Width 限制时按词/字折行
    public Size MeasureText(string text, Size proposedSize)
    {
        return MeasureText(text, proposedSize, TextFormatFlags.Default);
    }

    // 对齐原版 TextRenderer.MeasureText(g, text, font, proposedSize, flags)
    // flags 含 WordBreak（或 TextBoxControl）时在 proposedSize.Width 内折行，否则按单行度量。
    public Size MeasureText(string text, Size proposedSize, TextFormatFlags flags)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;
        var sf = GetFont();
        if (sf == null) return Size.Empty;

        bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0;
        int maxWidth = wordBreak ? proposedSize.Width : 0;
        var lines = WrapLines(text, maxWidth, wordBreak);

        int w = 0;
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line))
                w = Math.Max(w, (int)Math.Ceiling(sf.MeasureString(line).X));
        }
        int h = (int)Math.Ceiling(lines.Count * sf.LineHeight);
        return new Size(w, h);
    }

    // 按显式 '\n' 切分；带 wordBreak 时在 maxWidth 内按词折行，连续中文/长串退化逐字折行。
    // 与 DrawLabel 共用同一套折行逻辑（原版 TextRenderer 的 WordBreak 行为）。
    public List<string> WrapLines(string text, int maxWidth, bool wordBreak)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        var sf = GetFont();
        if (sf == null) return result;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Replace("\r", "");
            if (!wordBreak || maxWidth <= 0)
            {
                result.Add(line);
                continue;
            }

            var words = line.Split(' ');
            var current = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                if (current.Length > 0 && sf.MeasureString(current.ToString() + " " + word).X > maxWidth && sf.MeasureString(word).X <= maxWidth)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
                else if (sf.MeasureString(word).X > maxWidth)
                {
                    // 单个词（连续中文/长串）仍超宽：逐字折行
                    foreach (var ch in word)
                    {
                        if (current.Length > 0 && sf.MeasureString(current.ToString() + ch).X > maxWidth)
                        {
                            result.Add(current.ToString());
                            current.Clear();
                        }
                        current.Append(ch);
                    }
                }
                else
                {
                    current.Append(current.Length == 0 ? word : " " + word);
                }
            }
            if (current.Length > 0) result.Add(current.ToString());
        }
        return result;
    }
}