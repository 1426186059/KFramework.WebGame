using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 富文本控件：内部持有 <see cref="RichTextObject"/>，标签解析与排版结果都缓存，
    /// 只有 Text/Font 变化才重解析，尺寸/对齐/折行变化才重排。
    /// <para>
    /// 矩形约定与 <see cref="KLabel"/> 一致（<see cref="Bounds"/>）。另提供 <see cref="DrawTo"/> /
    /// <see cref="Measure"/> / <see cref="GetLinkAt"/> 三个立即模式入口，便于在自绘 UI（如测试页）
    /// 里直接指定矩形使用，而不必自己持有 RichTextObject。
    /// </para>
    /// 支持 &lt;color&gt; / &lt;b&gt; / &lt;i&gt; / &lt;u&gt; / &lt;size&gt; / &lt;link&gt; / &lt;a href&gt;。
    /// </summary>
    public class KRichLabel : KWidget
    {
        private readonly RichTextObject _rich = new RichTextObject();
        private TextAnchor? _alignment;
        private IFont _font;
        private string _text = string.Empty;

        public IFont Font
        {
            get { return _font; }
            set
            {
                _font = value;
                _rich.SetFont(value);
            }
        }

        public string Text
        {
            get { return _text; }
            set
            {
                _text = value ?? string.Empty;
                _rich.SetText(_text);
            }
        }

        public Color Color { get; set; } = Color.White;

        /// <summary>同 <see cref="KLabel.Alignment"/>：未显式赋值时按 Pivot 推导。</summary>
        public TextAnchor Alignment
        {
            get { return _alignment ?? AnchorFromPivot(Pivot); }
            set { _alignment = value; }
        }

        public HorizontalWrapMode HorizontalWrap { get; set; } = HorizontalWrapMode.Overflow;
        public VerticalWrapMode VerticalWrap { get; set; } = VerticalWrapMode.Overflow;

        /// <summary>描边模式：只画去标签的纯文本、忽略段颜色（供多次偏移绘制做轮廓）。</summary>
        public bool OutlineMode { get; set; }

        public KRichLabel(string text = "", Color color = default, IFont font = null)
        {
            Text = text;
            Color = color == default ? Color.White : color;
            Font = font;
        }

        /// <summary>控件矩形（世界坐标：左上角 + 缩放后尺寸），与 KLabel / KImage 同一约定。</summary>
        public RectangleF Bounds
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                Vector2 leftTop = WorldPosition - Pivot * worldSize;
                return new RectangleF(leftTop.X, leftTop.Y, worldSize.X, worldSize.Y);
            }
        }

        public override void Draw()
        {
            if (!activeInHierarchy) return;
            DrawTo(KSceneMgr.SpriteBatch, Bounds, Color, Alignment, HorizontalWrap, VerticalWrap, OutlineMode);
        }

        /// <summary>立即模式绘制：直接指定矩形/颜色/排版参数，内部复用同一个 RichTextObject。</summary>
        public void DrawTo(SpriteBatch batch, RectangleF bounds, Color color,
                           TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap,
                           bool outlineMode = false)
        {
            EnsureFont();
            _rich.Draw(batch, bounds, color, anchor, hWrap, vWrap, outlineMode);
        }

        /// <summary>按给定宽度度量富文本（剥离标签后折行）。</summary>
        public Size Measure(Size proposedSize, HorizontalWrapMode hWrap)
        {
            EnsureFont();
            return _rich.Measure(proposedSize, hWrap);
        }

        /// <summary>超链接命中：传与绘制一致的矩形与排版参数。</summary>
        public string GetLinkAt(RectangleF bounds, TextAnchor anchor, HorizontalWrapMode hWrap,
                                VerticalWrapMode vWrap, float x, float y)
        {
            EnsureFont();
            return _rich.GetLinkAt(bounds, anchor, hWrap, vWrap, x, y);
        }

        public int GetWidth() => Measure(new Size(int.MaxValue, int.MaxValue), HorizontalWrap).Width;
        public int GetHeight() => Measure(new Size(int.MaxValue, int.MaxValue), HorizontalWrap).Height;

        private void EnsureFont()
        {
            if (Font == null)
            {
                Font = KDefaultRes.DefaultSpriteFont;
            }
        }

        /// <summary>把 KWidget.Pivot（0..1）折算成对齐锚点：0 / 0.5 / 1 → Left/Center/Right 与 Upper/Middle/Lower。</summary>
        private static TextAnchor AnchorFromPivot(Vector2 pivot)
        {
            int col = pivot.X >= 0.75f ? 2 : pivot.X >= 0.25f ? 1 : 0;
            int row = pivot.Y >= 0.75f ? 2 : pivot.Y >= 0.25f ? 1 : 0;
            return (TextAnchor)(row * 3 + col);
        }
    }
}
