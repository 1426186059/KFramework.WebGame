using KTexturePacker.Parser;
using KFramework.MonoGame;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KFramework.MonoGameExtend
{
    public class SpriteSheetLoader
    {
        private readonly ContentManager contentManager;

        public SpriteSheetLoader(ContentManager contentManager)
        {
            this.contentManager = contentManager;
        }
        
        public async Task<SpriteSheet> LoadAsync(string jsonPath, CancellationToken cancellationToken = default)
        {
            string dir = Path.GetDirectoryName(jsonPath);

            // KFramework.MonoGame 没有文件系统、TitleContainer 与 .xnb 管线：
            // 所有资源都由 ContentManager 从已加载的 AssetBundle 中按名字异步读取。
            AtlasData mData = await contentManager.LoadJsonAsync<AtlasData>(jsonPath, cancellationToken);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image));
                Texture2D texture = await contentManager.LoadTextureAsync(texturePath, cancellationToken);
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
