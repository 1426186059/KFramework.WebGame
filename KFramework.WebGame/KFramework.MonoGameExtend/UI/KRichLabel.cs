using System.Collections.Generic;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
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

        /// <summary>强制排版（对标 TMP_Text.ForceMeshUpdate）；仅未绘制就要命中/取字符信息时调用。</summary>
        public void Layout(RectangleF bounds, TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            EnsureFont();
            _rich.Layout(bounds, anchor, hWrap, vWrap);
        }

        /// <summary>超链接命中：只传世界坐标，布局已记在 TextInfo。命中返回 url，否则 null。</summary>
        public string FindIntersectingLink(Vector2 worldPos)
        {
            return _rich.FindIntersectingLink(worldPos);
        }

        /// <summary>内部富文本对象，供 RichTextUtil 等静态工具查询（对标 TMP 里传 TMP_Text 内核）。</summary>
        public RichTextObject RichObject => _rich;

        /// <summary>排版结果（逐字符/逐行/逐链接），对标 TMP_Text.textInfo。绘制后可读。</summary>
        public RichTextInfo TextInfo => _rich.TextInfo;

        /// <summary>各真实行顶 Y（相对绘制矩形），供画行辅助线；放大字行更高，行距不均匀。</summary>
        public List<float> GetLineTops(RectangleF bounds, TextAnchor anchor,
                                       HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            EnsureFont();
            return _rich.GetLineTops(bounds, anchor, hWrap, vWrap);
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
