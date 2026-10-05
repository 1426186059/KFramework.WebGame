using KFramework.MonoGame;
using KTexturePacker.Core;
using System.Text.Json.Nodes;

namespace KFramework.Content.Cli
{
    public static class AtlasBuilder
    {
        internal static void BuildAtlas(
            List<string> remainAssetPathList,
            ContentBuilder.BuildOptions options)
        {
            List<SpriteInput> mList = new List<SpriteInput>();
            for(int i = remainAssetPathList.Count - 1; i >= 0; i--)
            {
                string path = remainAssetPathList[i];
                if (path.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase))
                {
                    // 已切好的 .atlas 图集不参与自动装箱，保持用户打包好的布局。
                    remainAssetPathList.RemoveAt(i);
                    continue;
                }
                if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                   // mList.Add(new SpriteInput(path));
                }
            }
        }

        /// <summary>
        /// 把需要自动装箱的散图 inputs 打包成图集，直接把整图纹理页写入 <paramref name="bundle"/>，并返回 AtlasData pages 节点。
        /// 已切好的 .atlas 图集不参与此流程（由 BundleBaker 原样入库）。
        /// </summary>
        internal static JsonArray BuildAtlas(
            AssetBundleBuild bundle,
            List<SpriteInput> inputs,
            ContentBuilder.BuildOptions options,
            string bundleName,
            string tempDirectory,
            ref int atlasPageCount)
        {
            // 交给上游 KTexturePacker 共享核心自动装箱（已切 .atlas 图集不在此合并，保持用户打包好的布局）。
            var imported = new List<ImportedAtlasPage>();

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
                string pageName = Path.Combine(bundleName, page.Name + ".png").Replace('\\', '/');
                (byte[] bytes, ContentTextureDataFormat fmt) = BundleBaker.EncodeTexture(
                    page.RgbaPixels, page.Width, page.Height, options, pageName);

                bundle.Assets.Add(new AssetBundleAsset
                {
                    // 图集页纹理路径按 raw 根目录计算，保留 .png 后缀（如 myres/atlas/atlas_0.png），
                    // 与 SpriteSheetLoader 由 jsonPath 的目录 + 页文件名（含扩展名）推得的纹理路径一致
                    Path = pageName,
                    Type = "texture",
                    Bytes = bytes,
                    Width = page.Width,
                    Height = page.Height,
                    Format = fmt,
                });

                // 预览图始终用原始 PNG，便于人工核对；写到临时目录（tempDirectory），不随 outDir 发布。
                if (options.WritePreviewPng)
                    File.WriteAllBytes(
                        Path.Combine(tempDirectory, $"atlas_{bundleName.Replace('/', '_')}_{page.Name}.png"),
                        page.ToPng());
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
