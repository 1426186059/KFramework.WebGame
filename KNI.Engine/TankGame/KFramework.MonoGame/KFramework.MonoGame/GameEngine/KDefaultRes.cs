using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    public static class KDefaultRes
    {
        public static SpriteFont DefaultSpriteFont { get; set; }


        private static Texture2D _cacheDefaultTexture2D;
        public static Texture2D DefaultTexture2D
        {
            get
            {
                if (_cacheDefaultTexture2D == null)
                {
                    _cacheDefaultTexture2D = new Texture2D(KSceneMgr.Game.GraphicsDevice, 1, 1);
                    _cacheDefaultTexture2D.SetData(new Color[] { Color.White });
                }
                return _cacheDefaultTexture2D;
            }

            set
            {
                _cacheDefaultTexture2D = value;
            }
        }

        private static KSprite _cacheDefaultSprite;
        public static KSprite DefaultSprite
        {
            get
            {
                if (_cacheDefaultSprite.Texture == null)
                {
                    _cacheDefaultSprite = new KSprite(DefaultTexture2D);
                }
                return _cacheDefaultSprite;
            }
        }

    }
}
