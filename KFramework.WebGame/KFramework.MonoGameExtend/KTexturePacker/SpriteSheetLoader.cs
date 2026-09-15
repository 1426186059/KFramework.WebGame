using KFramework.MonoGame;
using KTexturePacker.Parser;
using System.IO;
using System.Threading;

namespace KFramework.MonoGameExtend
{
    public class SpriteSheetLoader
    {
        private readonly AssetBundle _bundle;
        private readonly GraphicsDevice _device;

        public SpriteSheetLoader(AssetBundle bundle, GraphicsDevice device)
        {
            _bundle = bundle;
            _device = device;
        }

        public SpriteSheet Load(string jsonPath)
        {
            AssetBundle mBundle = _bundle;
            string dir = Path.GetDirectoryName(jsonPath);

            // KFramework.MonoGame 没有文件系统、TitleContainer 与 .xnb 管线：
            // 所有资源都由 AssetBundle 从已加载的包中按名字同步读取（包已在内存中，取资源即时完成）。
            AtlasData mData = mBundle.LoadJson<AtlasData>(jsonPath);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image));
                Texture2D texture = mBundle.LoadTexture(texturePath, _device);
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
