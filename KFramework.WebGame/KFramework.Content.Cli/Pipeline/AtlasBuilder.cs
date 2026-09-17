using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.IO;
using System.Text.Json.Nodes;

namespace KFramework.Content.Build;

/// <summary>
/// 图集打包（下游）：把需要自动装箱的散图（SpriteInput）打包成 AtlasData。已切好的 .atlas 图集由 BundleBaker 原样入库，不参与此流程。
/// 复用上游 KTexturePacker 的共享核心（<see cref="AtlasBaker"/>：MaxRects 摆放 + 整页合成 + 导出），
/// 核心只产出 RGBA8 中间格式；每张图集页作为独立整图纹理写入包，并在本层（下游）按
/// <see cref="ContentBuilder.BuildOptions.TextureFormat"/> 把 RGBA 转成目标格式（默认 Rgba，可选 Png / Ktx2）再入库。
/// 即：上游 KTexturePacker 只产出 RGBA 中间格式，PNG 是其默认交付物，其它格式由下游任取 RGBA 自行转换。
/// </summary>
public static class AtlasBuilder
{
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

        // 下游按 BuildOptions.TextureFormat 把上游 RGBA 中间格式转成目标编码入库：
        //   Rgba（默认）：直接存裸 RGBA8，运行端零解码、直接上传 GPU，体积由 .web.lib 的 zip 承担；
        //   Png         ：用上游 RGBA 编码 PNG，体积更小，运行端在 LoadBundle 阶段解码；
        //   Webp        ：用上游 RGBA 编码 WebP，体积更小，运行端借浏览器原生解码；
        //   Ktx2        ：GPU 压缩纹理（Basis 超压缩），构建端用 basisu 编码，运行端借浏览器 Basis 转码器直传 GPU。
        foreach (AtlasPageOutput page in result.Pages)
        {
            (byte[] bytes, AssetTextureFormat fmt) = options.TextureFormat switch
            {
                AssetTextureFormat.Rgba => (page.RgbaPixels, AssetTextureFormat.Rgba),
                AssetTextureFormat.Png  => (page.ToPng(),     AssetTextureFormat.Png),
                AssetTextureFormat.Webp => (BundleBaker.EncodeWebpFromRgba(page.RgbaPixels, page.Width, page.Height), AssetTextureFormat.Webp),
                AssetTextureFormat.Ktx2 => (BundleBaker.EncodeKtx2FromRgba(page.RgbaPixels, page.Width, page.Height, options.BasisuPath, options.Ktx2Quality), AssetTextureFormat.Ktx2),
                _ => (page.ToPng(), AssetTextureFormat.Png),
            };

            bundle.Assets.Add(new AssetBundleAsset
            {
                // 图集页纹理路径按 raw 根目录计算，保留 .png 后缀（如 myres/atlas/atlas_0.png），
                // 与 SpriteSheetLoader 由 jsonPath 的目录 + 页文件名（含扩展名）推得的纹理路径一致
                Path = Path.Combine(bundleName, page.Name + ".png").Replace('\\', '/'),
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
