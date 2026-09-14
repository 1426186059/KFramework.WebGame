using KTexturePacker.Parser;
using KFramework;
using KFramework.Content;
using KFramework.Graphics;
using System.IO;

namespace KFramework.MonoGame.KTexturePacker
{
    public class SpriteSheetLoader
    {
        private readonly ContentManager contentManager;

        public SpriteSheetLoader(ContentManager contentManager)
        {
            this.contentManager = contentManager;
        }
        
        public SpriteSheet Load(string jsonPath)
        {
            string dir = Path.GetDirectoryName(jsonPath);

            // KFramework 没有文件系统、TitleContainer 与 .xnb 管线：
            // 所有资源都由 ContentManager 按名字从内容包里读取。
            AtlasData mData = contentManager.LoadJson<AtlasData>(jsonPath);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image));
                Texture2D texture = contentManager.LoadTexture(texturePath);
                foreach (var v2 in v.Regions)
                {
                    bool isRotated = v2.Rotated;
                    string name = v2.Name;
                    Rectangle sourceRect = new Rectangle(v2.X, v2.Y, v2.W, v2.H);
                    Vector2 size = new Vector2(v2.SourceW, v2.SourceH);
                    Vector2 pivotPoint = new Vector2(0, 0);
                    KSpriteInfo sprite = new KSpriteInfo(name, texture, sourceRect, size, pivotPoint, isRotated);
                    spriteSheet.Add(name, sprite);
                }
            }

            return spriteSheet;
        }
    }

}