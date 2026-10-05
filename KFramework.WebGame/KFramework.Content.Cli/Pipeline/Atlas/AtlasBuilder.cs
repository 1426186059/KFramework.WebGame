using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Text;

namespace KFramework.Content.Cli
{
    public static class AtlasBuilder
    {
        internal static void BuildAtlas(
            AssetBundleBuild bundle,
            List<string> remainAssetPathList)
        {
            List<SpriteInput> sprites = new List<SpriteInput>();
            for (int i = remainAssetPathList.Count - 1; i >= 0; i--)
            {
                string path = remainAssetPathList[i];
                if (!Global.supportTextureFileType.Contains(Path.GetExtension(path)))
                    continue;

                SKBitmap bitmap = SKBitmap.Decode(path);
                // 解码失败或空串纹理都从列表移除，避免主流程再次尝试
                if (bitmap is null)
                {
                    remainAssetPathList.RemoveAt(i);
                    continue;
                }

                sprites.Add(new SpriteInput(path, bitmap));
                remainAssetPathList.RemoveAt(i);
            }

             BuildAtlas2(bundle, sprites);
        }

        private static void BuildAtlas2(
            AssetBundleBuild bundle,
            List<SpriteInput> inputs)
        {
            if (inputs.Count == 0)
            {
                return;
            }

            var imported = new List<ImportedAtlasPage>();
            AtlasBaker.AtlasBakeResult result = AtlasBaker.Bake(
                inputs,
                imported,
                new PackerSettings
                {
                    MaxSize = BuildOptions.AutoAtlasMaxSize,
                    Padding = BuildOptions.AutoAtlasPadding,
                    AllowRotation = true,
                },
                new AtlasBakeOptions { BaseName = "atlas" });

            string AtlasPrefix = "Atlas";
            foreach (AtlasPageOutput page in result.Pages)
            {
                AssetBundleAsset asset = BundleBaker.EncodeRgbaToTarget(page.Width, page.Height, page.RgbaPixels, ContentTextureDataFormat.Rgba);
                string pageName = AtlasPrefix + "_" + page.Name + Global.GetTextureFormat_Default_SuffixName(asset.Format);
                asset.Path = pageName;
                bundle.Assets.Add(asset);

                if (BuildOptions.AutoAtlasWritePreviewPng)
                {
                    File.WriteAllBytes(Path.Combine(BuildConfigResult.TempDirFull, pageName), page.ToPng());
                }
            }

            if (result.Pages.Count > 0)
            {
                string atlasJsonName = AtlasPrefix + ".atlas.json";
                bundle.Assets.Add(new AssetBundleAsset
                {
                    Path = Path.Combine(bundle.FullDir, atlasJsonName).Replace('\\', '/'),
                    Type = ContentAssetType.Text,
                    Bytes = Encoding.UTF8.GetBytes(result.AtlasJson),
                });

                if (BuildOptions.AutoAtlasWritePreviewPng)
                {
                    File.WriteAllText(
                        Path.Combine(BuildConfigResult.TempDirFull, atlasJsonName), 
                        result.AtlasJson);
                }
            }
        }
        
    }
}
