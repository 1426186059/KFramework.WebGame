using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    public class KLabel : KWidget
    {
        private Vector2 _cacheAlignment;

        public SpriteFont Font { get; set; } = null;
        public string Text { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.Green;

        public KLabel(string mText = "", Color mColor = default, SpriteFont Font = null)
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

            mSpriteBatch.DrawString(
                Font,
                Text,
                WorldPos,
                Color,
                LocalRotation,
                Origin,
                WorldScale,
                SpriteEffects.None,
                0);
        }

        public int GetHeight()
        {
            return Font.LineSpacing;
        }

        public int GetWidth()
        {
            return (int)Font.MeasureString(Text).X;
        }

    }
}
