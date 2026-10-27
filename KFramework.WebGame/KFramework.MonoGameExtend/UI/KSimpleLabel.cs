using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 简易文本控件：直接走 <see cref="SpriteBatch.DrawString"/>，一次画完一行（或按显式 '\n' 的多行），
    /// <b>不做对齐与折行</b>，靠 <see cref="KWidget.Pivot"/> 决定文本相对 <c>WorldPosition</c> 的锚点。
    /// <para>
    /// 适合"位置明确、单行、不需要排版"的场合（如按钮文字、数值标签）。
    /// 需要在控件矩形内居中 / 折行 / 截断时请改用 <see cref="KLabel"/>（内部走引擎 TextRenderer）。
    /// </para>
    /// <para>本类是原 <c>KLabel</c> 的忠实保留版（改名而来），行为未变。</para>
    /// </summary>
    public class KSimpleLabel : KWidget
    {
        private Vector2 _cacheAlignment;

        public IFont Font { get; set; } = null;
        public string Text { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.Green;

        public KSimpleLabel(string mText = "", Color mColor = default, IFont Font = null)
        {
            if(mColor == default)
            {
                mColor = Color.Green;
            }

            this.Text = mText;
            this.Font = Font;
            this.Color = mColor;
        }

        public Vector2 Alignment
        {
            get { return _cacheAlignment; }
            set
            {
                if (_cacheAlignment != value)
                {
                    _cacheAlignment = value;
                }
            }
        }

        private Vector2 Origin
        {
            get
            {
                return new Vector2(GetWidth(), GetHeight()) * Pivot;
            }
        }

        public override void Draw()
        {
            if (!activeInHierarchy) return;

            var mSpriteBatch = KSceneMgr.SpriteBatch;
            if (Font == null)
            {
                Font = KDefaultRes.DefaultSpriteFont;
            }

            //Pivot = new Vector2(0.5f, 1f);
            //Vector2 mTextSize = new Vector2(GetWidth(), GetHeight());
            //Vector2 mPivot = -(Size - mTextSize);

            var WorldPos = WorldPosition;
            //if (_cacheAlignment != Vector2.Zero)
            //{
            //    Vector2 mTextSize = new Vector2(GetWidth(), GetHeight());
            //    WorldPos = WorldPosition + (Size * WorldScale - mTextSize * WorldScale) * _cacheAlignment;
            //}

            // KFramework.MonoGame 的 DrawString 不提供 SpriteEffects 参数，缩放用 float（取 X 作等比缩放）
            mSpriteBatch.DrawString(
                Font,
                Text,
                WorldPos,
                Color,
                LocalRotation,
                Origin,
                WorldScale.X,
                0);
        }

        public int GetHeight()
        {
            return (int)Font.LineSpacing;
        }

        public int GetWidth()
        {
            return (int)Font.MeasureString(Text).X;
        }

    }
}
