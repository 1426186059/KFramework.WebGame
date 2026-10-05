using KFramework.MonoGame;
using KTexturePacker.Core;
using System.Text.Json.Nodes;

namespace KFramework.Content.Cli
{
    public static class AtlasBuilder
    {
        internal static void BuildAtlas(
            AssetBundleBuild mBundle,
            List<string> remainAssetPathList,
            BuildOptions options)
        {
            List<SpriteInput> mSpriteList = new List<SpriteInput>();
            for(int i = remainAssetPathList.Count - 1; i >= 0; i--)
            {
                string path = remainAssetPathList[i];
                if(Global.supportTextureFileType.Contains(Path.GetExtension(path)))
                {
                    SkiaSharp.SKBitmap bitmap = SkiaSharp.SKBitmap.Decode(path);
                    mSpriteList.Add(new SpriteInput(path, bitmap));
                    remainAssetPathList.RemoveAt(i);
                }
            }

            BuildAtlas2(mBundle, mSpriteList, options);
        }

        /// <summary>
        /// 把需要自动装箱的散图 inputs 打包成图集，直接把整图纹理页写入 <paramref name="bundle"/>，并返回 AtlasData pages 节点。
        /// 已切好的 .atlas 图集不参与此流程（由 BundleBaker 原样入库）。
        /// </summary>
        private static JsonArray BuildAtlas2(
            AssetBundleBuild bundle,
            List<SpriteInput> inputs,
            BuildOptions options)
        {
            // 交给上游 KTexturePacker 共享核心自动装箱（已切 .atlas 图集不在此合并，保持用户打包好的布局）。
            var imported = new List<ImportedAtlasPage>();
            int atlasPageCount = 0;
            AtlasBaker.AtlasBakeResult result = AtlasBaker.Bake(
                inputs,
                imported,
                new PackerSettings
                {
                    MaxSize = options.AtlasMaxSize,
                    Padding = options.AtlasPadding,
                    AllowRotation = true,
                },
                new AtlasBakeOptions { BaseName = "atlas" });

            atlasPageCount += result.Pages.Count;

            foreach (AtlasPageOutput page in result.Pages)
            {
                string pageName = page.Name + Global.GetTextureFormat_Default_SuffixName(ContentTextureDataFormat.Webp);
                
                var bytes = BundleBaker.EncodeWebpFromRgba(page.RgbaPixels, page.Width, page.Height);
                (bytes, ContentTextureDataFormat fmt) = BundleBaker.EncodeTexture(
                    page.Width, 
                    page.Height,
                    bytes,
                    ContentTextureDataFormat.Webp);

                bundle.Assets.Add(new AssetBundleAsset
                {
                    Path = pageName,
                    Type = ContentAssetType.Texture,
                    Bytes = bytes,
                    Width = page.Width,
                    Height = page.Height,
                    Format = fmt,
                });

                if (options.WritePreviewPng)
                {
                    File.WriteAllBytes(Path.Combine(BuildConfigResult.TempDirFull, pageName), page.ToPng());
                }
            }

            // 运行端按 GetFileName(image) 取纹理名（含 .png 后缀，与包内资源名 Path.Combine(bundleName, page.Name + ".png") 对应），
            // 故 AtlasData 中 image 的扩展名需与包内页纹理名一致（仅作可读提示与查找键，无需因编码格式改写内容）。
            var root = JsonNode.Parse(result.AtlasJson)!.AsObject();
            // DeepClone 返回脱离父节点的副本：否则 root["pages"] 仍挂着 root，
            // 被调用方再挂到自己的 JsonObject 时会抛 "The node already has a parent"。
            return (JsonArray)root["pages"]!.DeepClone();
        }
    }

}
