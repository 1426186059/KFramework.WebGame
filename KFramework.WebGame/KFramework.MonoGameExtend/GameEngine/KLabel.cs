using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 文本控件：内部走引擎的 <see cref="TextRenderer"/>，因此在控件矩形内支持
    /// <b>对齐</b>（<see cref="TextAnchor"/>）、<b>水平折行</b>（<see cref="HorizontalWrapMode"/>）与
    /// <b>垂直截断</b>（<see cref="VerticalWrapMode"/>）。
    /// <para>
    /// 控件矩形与 <see cref="KImage"/> / <see cref="KButton.Bounds"/> 同一套约定：
    /// 左上角 = <c>WorldPosition - Pivot * Size * WorldScale</c>，尺寸 = <c>Size * WorldScale</c>。
    /// </para>
    /// <para>
    /// 与 <see cref="KSimpleLabel"/> 的分工：后者是"一次 DrawString"的轻量实现（不折行、不对齐，靠 Pivot 定位），
    /// 适合单行标签；本类适合需要居中 / 折行 / 截断的场合。
    /// </para>
    /// </summary>
    public class KLabel : KWidget
    {
        public IFont Font { get; set; } = null;
        public string Text { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.Green;

        private TextAnchor? _alignment;

        /// <summary>
        /// 文本在控件矩形内的对齐方式（一个值同时决定水平与垂直），取值类型 <see cref="TextAnchor"/>。
        /// 命名照 Unity 的 <c>Text.alignment</c>；刻意<b>不叫 Anchor</b> ——
        /// <see cref="KWidget.Anchor"/> 是布局用的 Vector2，同名会遮蔽基类成员。
        /// <para>
        /// 默认 <c>null</c>：此时按 <see cref="KWidget.Pivot"/> 推导对齐
        /// （Pivot 的 0 / 0.5 / 1 → Left/Center/Right 与 Upper/Middle/Lower），
        /// 以保留旧 <c>KLabel</c>「文本以 Pivot 为锚点」的行为（按钮文字居中正是这一类）。
        /// 显式赋值后，才在控件矩形内按指定锚点对齐。
        /// </para>
        /// </summary>
        public TextAnchor Alignment
        {
            get { return _alignment ?? AnchorFromPivot(Pivot); }
            set { _alignment = value; }
        }

        /// <summary>水平换行模式：<see cref="HorizontalWrapMode.Wrap"/> 时按控件宽度折行。</summary>
        public HorizontalWrapMode HorizontalWrap { get; set; } = HorizontalWrapMode.Overflow;

        /// <summary>垂直溢出模式：<see cref="VerticalWrapMode.Truncate"/> 时超出控件高度就截断。</summary>
        public VerticalWrapMode VerticalWrap { get; set; } = VerticalWrapMode.Overflow;

        public KLabel(string mText = "", Color mColor = default, IFont font = null)
        {
            if (mColor == default)
            {
                mColor = Color.Green;
            }

            Text = mText;
            Font = font;
            Color = mColor;
        }

        /// <summary>控件矩形（世界坐标：左上角 + 缩放后尺寸），与 <see cref="KImage"/> / KButton.Bounds 同一约定。</summary>
        public Rectangle Bounds
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                Vector2 leftTop = WorldPosition - Pivot * worldSize;
                return new Rectangle(
                    (int)Math.Round(leftTop.X),
                    (int)Math.Round(leftTop.Y),
                    (int)Math.Round(worldSize.X),
                    (int)Math.Round(worldSize.Y));
            }
        }

        public override void Draw()
        {
            if (!activeInHierarchy) return;

            if (Font == null)
            {
                Font = KDefaultRes.DefaultSpriteFont;
            }

            // 走引擎文本库：对齐 / 折行 / 截断都在 TextRenderer 内完成（该重载不再自己 Begin / End 批）。
            TextRenderer.DrawText(
                KSceneMgr.SpriteBatch,
                Text,
                Font,
                Bounds,
                Color,
                Alignment,
                HorizontalWrap,
                VerticalWrap);
        }

        /// <summary>按当前字体量出的文本宽度（与控件矩形无关）。</summary>
        public int GetWidth() => (int)TextRenderer.MeasureText(Text, Font ?? KDefaultRes.DefaultSpriteFont).Width;

        /// <summary>按当前字体量出的文本高度（与控件矩形无关）。</summary>
        public int GetHeight() => (int)TextRenderer.MeasureText(Text, Font ?? KDefaultRes.DefaultSpriteFont).Height;

        /// <summary>把 <see cref="KWidget.Pivot"/>（0..1）折算成对齐锚点：0 / 0.5 / 1 → Left/Center/Right 与 Upper/Middle/Lower。</summary>
        private static TextAnchor AnchorFromPivot(Vector2 pivot)
        {
            int col = pivot.X >= 0.75f ? 2 : pivot.X >= 0.25f ? 1 : 0;
            int row = pivot.Y >= 0.75f ? 2 : pivot.Y >= 0.25f ? 1 : 0;
            return (TextAnchor)(row * 3 + col);
        }
    }
}
