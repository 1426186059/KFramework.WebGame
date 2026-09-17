using KFramework.MonoGame;
using KTexturePacker.Parser;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// 同步加载图集。严格（默认）按精确路径查找 JSON 与整页纹理；<paramref name="strict"/> 为 false 时按关键字（Path 包含）匹配。
        /// 含 KTX2 页的图集需在 LoadBundleAsync 阶段传入 GraphicsDevice 把页纹理转码+上传 GPU，本方法随后同步取用。
        /// </summary>
        public SpriteSheet Load(string jsonPath, bool strict = true)
        {
            AssetBundle mBundle = _bundle;
            string dir = Path.GetDirectoryName(jsonPath);

            string jsonContent = mBundle.LoadText(jsonPath, strict);
            AtlasData mData = JsonTool.FromJson(jsonContent, AppJsonContext.Default.AtlasData);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = strict
                    ? Path.Combine(dir, Path.GetFileName(v.Image))
                    : Path.GetFileName(v.Image);
                Texture2D texture = mBundle.LoadTexture(texturePath, _device, strict);
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

        /// <summary>
        /// 异步加载图集（符合异步 API 约定）。行为与 <see cref="Load"/> 一致：整页纹理在 LoadBundleAsync 阶段
        /// 已随包上传 GPU，此处同步取用即可；因而同样兼容 KTX2。
        /// </summary>
        public Task<SpriteSheet> LoadAsync(string jsonPath, bool strict = true)
            => Task.FromResult(Load(jsonPath, strict));
    }

}
