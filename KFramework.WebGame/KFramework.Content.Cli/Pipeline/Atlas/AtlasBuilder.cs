using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Text;

namespace KFramework.Content.Cli
{
    public static class AtlasBuilder
    {
        /// <summary>
        /// 模块一（抽取）：从「剩余资源列表」<paramref name="remainAssetPathList"/> 中抽出全部纹理，
        /// 解码为 <see cref="SpriteInput"/>，并从列表中移除（避免主流程重复导入），再交给 <see cref="BuildAtlas2"/> 装箱。
        /// 返回所有图集页的 AtlasData 节点（供调用方写入 atlas.json）。
        /// 已切好的 .atlas 整页图不在此抽取（由 BundleBaker 原样入库）。
        /// </summary>
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

        /// <summary>
        /// 模块二（装箱）：把已准备好的散图 inputs 交给上游 KTexturePacker 共享核心自动装箱，
        /// 逐页按 <see cref="BuildConfigResult.TextureSwitchTarget"/> 编码为纹理写入 <paramref name="bundle"/>，
        /// 并返回 AtlasData 的 pages 节点。
        /// </summary>
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
                    MaxSize = BuildOptions.AtlasMaxSize,
                    Padding = BuildOptions.AtlasPadding,
                    AllowRotation = true,
                },
                new AtlasBakeOptions { BaseName = "atlas" });

            string AtlasPrefix = "Atlas";
            foreach (AtlasPageOutput page in result.Pages)
            {
                (byte[] bytes, ContentTextureDataFormat fmt) = EncodePage(page);
                string pageName = AtlasPrefix + "_" + page.Name + Global.GetTextureFormat_Default_SuffixName(fmt);
                bundle.Assets.Add(new AssetBundleAsset
                {
                    Path = pageName,
                    Type = ContentAssetType.Texture,
                    Bytes = bytes,
                    Width = page.Width,
                    Height = page.Height,
                    Format = fmt,
                });

                if (BuildOptions.WritePreviewPng)
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

                if (BuildOptions.WritePreviewPng)
                {
                    File.WriteAllText(
                        Path.Combine(BuildConfigResult.TempDirFull, atlasJsonName), 
                        result.AtlasJson);
                }
            }
        }

        /// <summary>
        /// 把图集页（无原图，仅 RGBA 像素）编码为目标格式，遵循统一 <see cref="BuildConfigResult.TextureSwitchTarget"/>：
        /// <list type="bullet">
        ///   <item><see cref="ContentTextureSwitchTarget.None"/>：未指定统一目标时，自动图集默认以 <b>Webp</b> 装箱（图集不保留裸 RGBA）。</item>
        ///   <item><see cref="ContentTextureSwitchTarget.Webp"/>：编码为 Webp。</item>
        ///   <item><see cref="ContentTextureSwitchTarget.Rgba"/>：保留烘焙出的原始 RGBA 裸像素。</item>
        ///   <item><see cref="ContentTextureSwitchTarget.Ktx2"/>：宽高皆 4 倍数 → 编码为 KTX2，否则回退为 Webp。</item>
        /// </list>
        /// </summary>
        private static (byte[] Bytes, ContentTextureDataFormat Format) EncodePage(
            AtlasPageOutput page)
        {
            byte[] rgba = page.RgbaPixels;
            // 自动图集默认以 Webp 装箱；仅当统一目标显式指定 Rgba / Ktx2 / Webp 时才覆盖默认。
            ContentTextureSwitchTarget target = BuildConfigResult.TextureSwitchTarget == ContentTextureSwitchTarget.None
                ? ContentTextureSwitchTarget.Webp
                : BuildConfigResult.TextureSwitchTarget;

            switch (target)
            {
                case ContentTextureSwitchTarget.Webp:
                    return (BundleBaker.EncodeWebpFromRgba(rgba, page.Width, page.Height), ContentTextureDataFormat.Webp);

                case ContentTextureSwitchTarget.Ktx2:
                    if (BundleBaker.IsSuitableForKtx2(page.Width, page.Height))
                        return (BundleBaker.EncodeKtx2FromRgba(rgba, page.Width, page.Height, BuildConfigResult.BasisuPathFull, BuildOptions.Ktx2Quality), ContentTextureDataFormat.Ktx2);
                    return (BundleBaker.EncodeWebpFromRgba(rgba, page.Width, page.Height), ContentTextureDataFormat.Webp);

                case ContentTextureSwitchTarget.Rgba:
                default:
                    return (rgba, ContentTextureDataFormat.Rgba);
            }
        }
    }
}
