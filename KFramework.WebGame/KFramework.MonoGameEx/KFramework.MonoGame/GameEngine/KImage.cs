using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    public class KImage : KWidget
    {
        private KSprite _cacheSprite;
        private bool _cacheUseNativeSize;

        public KSprite Sprite
        {
            get
            {
                return _cacheSprite;
            }
            set
            {
                _cacheSprite = value;
                if (_cacheUseNativeSize)
                {
                    SetNativeSize();
                }
            }
        }

        public bool UseNativeSize
        {
            get
            {
                return _cacheUseNativeSize;
            }
            set
            {
                if (_cacheUseNativeSize != value)
                {
                    _cacheUseNativeSize = value;
                    if (_cacheUseNativeSize)
                    {
                        SetNativeSize();
                    }
                }
            }
        }

        public Color Color { get; set; } = Color.White;

        public void SetNativeSize()
        {
            if (_cacheSprite.Texture != null)
            {
                Size = _cacheSprite.Rectangle.Size.ToVector2();
            }
        }
        
        private Vector2 Origin
        {
            get
            {
                return _cacheSprite.Rectangle.Size.ToVector2() * Pivot;
            }
        }

        public override void Draw()
        {
            if (_cacheSprite.Texture == null)
            {
                _cacheSprite = new KSprite(KDefaultRes.DefaultTexture2D);
            }

            var mSpriteBatch = KSceneMgr.SpriteBatch;
            Rectangle targetRegion = new Rectangle(WorldPosition.ToPoint(), (Size * WorldScale).ToPoint());
            mSpriteBatch.Draw(
                _cacheSprite.Texture,
                targetRegion,
                _cacheSprite.Rectangle,
                Color,
                WorldRotation,
                Origin, //这里 origin 设置为0-1就有效果
                SpriteEffects.None,
                0);
        }

    }
}
