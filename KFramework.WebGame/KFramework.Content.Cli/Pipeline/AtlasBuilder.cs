using KTexturePacker.Core;
using SkiaSharp;
using System.IO;
using System.Text.Json.Nodes;

namespace KFramework.Content.Build;

/// <summary>
/// 图集打包（下游）：把自动装箱的散图（SpriteInput）与导入的 .atlas 预切图集合并成同一份 AtlasData。
/// 复用上游 KTexturePacker 的共享核心（<see cref="AtlasBaker"/>：MaxRects 摆放 + 整页合成 + 导出），
/// 核心只产出 RGBA8 中间格式；每张图集页作为独立整图纹理写入包，并在本层（下游）按
/// <see cref="ContentBuilder.BuildOptions.TextureFormat"/> 把 RGBA 转成目标格式（默认 png，可选 webp）再入库。
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

        // 下游转码：默认用上游 RGBA 编码 PNG 入库；若要求 webp，在本层把 RGBA 直接转成 WebP 再入库。
        bool toWebp = string.Equals(options.TextureFormat, "webp", StringComparison.OrdinalIgnoreCase);

        foreach (AtlasPageOutput page in result.Pages)
        {
            byte[] bytes = toWebp ? EncodeWebp(page.RgbaPixels, page.Width, page.Height) : page.ToPng();

            bundle.Assets.Add(new AssetBundleAsset
            {
                Path = page.Name,
                Type = "texture",
                Bytes = bytes,
                Width = page.Width,
                Height = page.Height,
            });

            // 预览图始终用原始 PNG，便于人工核对。
            if (options.WritePreviewPng)
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, $"atlas_{bundleName.Replace('/', '_')}_{page.Name}.png"),
                    page.ToPng());
        }

        var root = JsonNode.Parse(result.AtlasJson)!.AsObject();
        // 若本层转成了 webp，同步把 AtlasData 里 image 的扩展名改掉，保持元数据一致
        // （运行端按 GetFileNameWithoutExtension(image) 取纹理名，扩展名仅为可读提示）。
        if (toWebp)
            foreach (var p in (JsonArray)root["pages"]!)
                if (p is JsonObject o && o["image"] is JsonNode img)
                    o["image"] = img.GetValue<string>().Replace(".png", ".webp");

        return (JsonArray)root["pages"]!;
    }

    /// <summary>把 RGBA8 源像素直接编码为 WebP（下游转码用，避免经由 PNG 多一道解码）。</summary>
    private static byte[] EncodeWebp(byte[] rgba, int w, int h)
    {
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        rgba.CopyTo(bmp.GetPixelSpan());
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Webp, 90);
        return data.ToArray();
    }

    /// <summary>一个被导入的 .atlas 预切图集的整页：页图 JSON 原文 + 页图 PNG 字节。</summary>
    internal sealed record ImportedAtlas(string PageJson, byte[] PageBytes);
}
