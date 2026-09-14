using KTexturePacker.Parser;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
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
            string dataFile = Path.Combine(contentManager.RootDirectory, jsonPath);  
            string source = string.Empty;
            using (Stream fileStream = TitleContainer.OpenStream(dataFile))
            using (var reader = new StreamReader(fileStream))
            {
                source = reader.ReadToEnd();
            }

            AtlasData mData = AtlasParser.Parse(source);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image));
                Texture2D texture = contentManager.Load<Texture2D>(texturePath);
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