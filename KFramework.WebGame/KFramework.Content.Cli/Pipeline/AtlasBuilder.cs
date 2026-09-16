using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.IO;
using System.Text.Json.Nodes;

namespace KFramework.Content.Build;

/// <summary>
/// 图集打包（下游）：把自动装箱的散图（SpriteInput）与导入的 .atlas 预切图集合并成同一份 AtlasData。
/// 复用上游 KTexturePacker 的共享核心（<see cref="AtlasBaker"/>：MaxRects 摆放 + 整页合成 + 导出），
/// 核心只产出 RGBA8 中间格式；每张图集页作为独立整图纹理写入包，并在本层（下游）按
/// <see cref="ContentBuilder.BuildOptions.TextureFormat"/> 把 RGBA 转成目标格式（默认 Rgba，可选 Png / Ktx2）再入库。
/// 即：上游 KTexturePacker 只产出 RGBA 中间格式，PNG 是其默认交付物，其它格式由下游任取 RGBA 自行转换。
/// </summary>
public static class AtlasBuilder
{
    /// <summary>
    /// 把自动装箱的散图 inputs 与导入的 .atlas 预切图集 importedAtlases 合并打包，
    /// 直接把整图纹理页写入 <paramref name="bundle"/>，并返回合并后的 AtlasData pages 节点。
    /// </summary>
    internal static JsonArray BuildAtlas(
        AssetBundleBuild bundle,
        List<SpriteInput> inputs,
        List<ImportedAtlas> importedAtlases,
        ContentBuilder.BuildOptions options,
        string bundleName,
        string outputDirectory,
        ref int atlasPageCount)
    {
        // 交给上游 KTexturePacker 共享核心：自动散图装箱 + 导入页合并 → 通用 AtlasData JSON + 每页 PNG 图像。
        var imported = importedAtlases
            .Select(a => new ImportedAtlasPage { PageJson = a.PageJson, ImageBytes = a.PageBytes })
            .ToList();

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
        //   Ktx2        ：GPU 压缩纹理，本仓库暂未实现编码，预留枚举位。
        foreach (AtlasPageOutput page in result.Pages)
        {
            (byte[] bytes, AssetTextureFormat fmt) = options.TextureFormat switch
            {
                AssetTextureFormat.Rgba => (page.RgbaPixels, AssetTextureFormat.Rgba),
                AssetTextureFormat.Png  => (page.ToPng(),     AssetTextureFormat.Png),
                AssetTextureFormat.Webp => (BundleBaker.EncodeWebpFromRgba(page.RgbaPixels, page.Width, page.Height), AssetTextureFormat.Webp),
                AssetTextureFormat.Ktx2 => throw new NotSupportedException(
                    "KTX2 编码尚未实现：需引入 GPU 压缩纹理编码器（如 Basis/ASTC）。当前可用 Rgba / Png / Webp。"),
                _ => (page.ToPng(), AssetTextureFormat.Png),
            };

            bundle.Assets.Add(new AssetBundleAsset
            {
                Path = page.Name,
                Type = "texture",
                Bytes = bytes,
                Width = page.Width,
                Height = page.Height,
                Format = fmt,
            });

            // 预览图始终用原始 PNG，便于人工核对。
            if (options.WritePreviewPng)
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, $"atlas_{bundleName.Replace('/', '_')}_{page.Name}.png"),
                    page.ToPng());
        }

        // 运行端按 GetFileNameWithoutExtension(image) 取纹理名（与包内资源名 page.Name 对应），
        // 故 AtlasData 中 image 的扩展名仅为可读提示，无需因编码格式而改写。
        var root = JsonNode.Parse(result.AtlasJson)!.AsObject();
        return (JsonArray)root["pages"]!;
    }

    /// <summary>一个被导入的 .atlas 预切图集的整页：页图 JSON 原文 + 页图 PNG 字节。</summary>
    internal sealed record ImportedAtlas(string PageJson, byte[] PageBytes);
}
