using KFramework.MonoGame;
using KTexturePacker.Parser;
using System;
using System.IO;
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
        /// 注意：KTX2 纹理需异步上传 GPU，本同步方法无法 await，含 KTX2 页时必须改用 <see cref="LoadAsync"/>。
        /// </summary>
        public SpriteSheet Load(string jsonPath, bool strict = true)
        {
            AssetBundle mBundle = _bundle;
            string dir = Path.GetDirectoryName(jsonPath);

            AtlasData mData = mBundle.LoadJson<AtlasData>(jsonPath, strict);

            // KTX2 纹理需异步转码 / 上传 GPU，本同步方法无法 await，必须走 LoadAsync。
            foreach (var v in mData.Pages)
            {
                string texturePath = strict
                    ? Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image))
                    : Path.GetFileNameWithoutExtension(v.Image);
                var texInfo = mBundle.GetAssetInfo(texturePath, strict);
                if (texInfo is { Format: AssetTextureFormat.Ktx2 })
                    throw new InvalidOperationException(
                        $"纹理 “{texturePath}” 为 KTX2：请改用 SpriteSheetLoader.LoadAsync（KTX2 需异步上传 GPU）。");
            }

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = strict
                    ? Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image))
                    : Path.GetFileNameWithoutExtension(v.Image);
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
        /// 异步加载图集（支持 KTX2 等需异步上传 GPU 的纹理格式）。
        /// 与 <see cref="Load"/> 行为一致，但对每张整页纹理改用 <see cref="AssetBundle.LoadTextureAsync"/>，
        /// 从而兼容 KTX2（借浏览器 Basis 转码器转码并上传）。
        /// </summary>
        /// <summary>
        /// 异步加载图集（支持 KTX2 等需异步上传 GPU 的纹理格式）。
        /// 严格（默认）按精确路径查找 JSON 与整页纹理；<paramref name="strict"/> 为 false 时按关键字（Path 包含）匹配。
        /// 与 <see cref="Load"/> 行为一致，但对每张整页纹理改用 <see cref="AssetBundle.LoadTextureAsync"/>，
        /// 从而兼容 KTX2（借浏览器 Basis 转码器转码并上传）。
        /// </summary>
        public async Task<SpriteSheet> LoadAsync(string jsonPath, bool strict = true)
        {
            AssetBundle mBundle = _bundle;
            string dir = Path.GetDirectoryName(jsonPath);

            AtlasData mData = mBundle.LoadJson<AtlasData>(jsonPath, strict);

            SpriteSheet spriteSheet = new SpriteSheet();
            foreach (var v in mData.Pages)
            {
                string texturePath = strict
                    ? Path.Combine(dir, Path.GetFileNameWithoutExtension(v.Image))
                    : Path.GetFileNameWithoutExtension(v.Image);
                Texture2D texture = await mBundle.LoadTextureAsync(texturePath, _device, strict).ConfigureAwait(false);
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
