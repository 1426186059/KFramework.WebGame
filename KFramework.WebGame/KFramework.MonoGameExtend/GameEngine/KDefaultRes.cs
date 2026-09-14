using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
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
                    // KFramework.MonoGame：纹理由 GraphicsDevice 创建，像素数据是 RGBA8 字节而不是 Color[]
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
