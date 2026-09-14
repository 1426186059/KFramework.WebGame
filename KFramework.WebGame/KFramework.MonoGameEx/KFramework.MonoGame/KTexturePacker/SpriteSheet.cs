using System.Collections.Generic;

namespace KFramework.MonoGame.KTexturePacker
{
    public class SpriteSheet
    {
        private readonly IDictionary<string, KSpriteInfo> spriteList;

        public SpriteSheet()
        {
            spriteList = new Dictionary<string, KSpriteInfo>();
        }

        public void Add(string name, KSpriteInfo sprite)
        {
            spriteList.Add(name, sprite);
        }

        public void Add(SpriteSheet otherSheet)
        {
            foreach (var sprite in otherSheet.spriteList)
            {
                spriteList.Add(sprite);
            }
        }

        public KSpriteInfo Sprite(string sprite)
        {
            return spriteList[sprite];
        }

    }
}