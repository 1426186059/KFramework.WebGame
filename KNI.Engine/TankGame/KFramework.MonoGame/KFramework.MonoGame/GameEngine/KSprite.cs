using KFramework.MonoGame.KTexturePacker;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    public struct KSprite
    {
        public KSpriteInfo mRef;
        public Texture2D Texture { get; set; }
        public Rectangle Rectangle { get; set; } = new Rectangle(0, 0, 1, 1);

        public KSprite()
        {
            
        }

        public KSprite(Texture2D texture)
        {
            this.Texture = texture;
            this.Rectangle = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        public KSprite(KSpriteInfo mSpriteFrame)
        {
            this.mRef = mSpriteFrame;
            this.Texture = mSpriteFrame.Texture;
            this.Rectangle = mSpriteFrame.SourceRectangle;
        }

        public static implicit operator KSprite(KSpriteInfo a)
        {
            return new KSprite(a);
        }

        public static implicit operator KSprite(Texture2D a)
        {
            return new KSprite(a);
        }
    }
}
