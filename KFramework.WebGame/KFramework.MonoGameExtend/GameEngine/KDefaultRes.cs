using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    public static class KDefaultRes
    {
        private static IFont _cacheDefaultFont;
        public static IFont DefaultSpriteFont
        {
            get
            {
                if (_cacheDefaultFont == null)
                {
                    _cacheDefaultFont = new SpriteFont();
                }
                return _cacheDefaultFont;
            }
            set
            {
                _cacheDefaultFont = value;
            }
        }


        private static Texture2D _cacheDefaultTexture2D;
        public static Texture2D DefaultTexture2D
        {
            get
            {
                if (_cacheDefaultTexture2D == null)
                {
                    _cacheDefaultTexture2D = KSceneMgr.Game.GraphicsDevice.CreateTexture(1, 1);
                    _cacheDefaultTexture2D.SetData(new byte[] { 255, 255, 255, 255 });
                }
                return _cacheDefaultTexture2D;
            }

            set
            {
                _cacheDefaultTexture2D = value;
            }
        }

        public static KSprite DefaultSprite
        {
            get
            {
                return new KSprite(DefaultTexture2D);
            }
        }

    }
}
