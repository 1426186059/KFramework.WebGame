using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KFramework.Content.Build;

/// <summary>
/// 构建单个 AssetBundle：把给定文件夹的直接资源文件打包——纹理进图集，其余原样入包。
/// 图集页与子图索引都写入同一个包，因此每个包自带其纹理（运行端按 Page 切片）。
/// 复用外部 KTexturePacker 工具核心（MaxRects 摆放 + 整页合成 + AtlasData 导出）。
/// </summary>
public static class BundleBaker
{
    public static AssetBundleBuild BuildBundle(
        string bundleName, string[] files, string assetBaseDir, ContentBuilder.BuildOptions options, bool autoAtlas,
        List<string> warnings, ref long rawBytes, ref int textureCount, ref int dataCount, ref int atlasPageCount,
        string outputDirectory)
    {
        // 图集打包统一复用外部 KTexturePacker 工具的核心（MaxRects 摆放 + 整页合成 + AtlasData 导出）。
        // 产物：整页纹理（atlas_{i}，单张 Texture2D）+ 一份 AtlasData JSON（资源名固定为 "atlas"）。
        // 运行时由 SpriteSheetLoader 读取该 JSON 并提供 source rect，从而同一张图集页可合批。
        const string AtlasBaseName = "atlas";
        var inputs = new List<SpriteInput>();
        var bundle = new AssetBundleBuild { AssetBundleName = bundleName };
        var names = new HashSet<string>(StringComparer.Ordinal);

        // 预扫描：识别 .atlas 预切图集，记录其整页图，避免后续被当成散图重打包；
        // 这些子精灵名（如 characters_256、misc-3_68）需原样保留到最终的 AtlasData。
        var skipRelative = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedAtlases = new List<ImportedAtlas>();
        foreach (string atlasFile in files)
        {
            string atlasRel = Path.GetRelativePath(assetBaseDir, atlasFile).Replace('\\', '/');
            if (!atlasRel.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase)) continue;
            skipRelative.Add(atlasRel);
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(atlasFile));
                if (doc.RootElement.TryGetProperty("pages", out JsonElement pagesEl))
                {
                    foreach (JsonElement page in pagesEl.EnumerateArray())
                    {
                        string image = page.GetProperty("image").GetString()!;
                        string pagePath = Path.Combine(Path.GetDirectoryName(atlasFile)!, image);
                        byte[] pageBytes = File.ReadAllBytes(pagePath);
                        importedAtlases.Add(new ImportedAtlas(page.GetRawText(), pageBytes));
                        skipRelative.Add(Path.GetRelativePath(assetBaseDir, pagePath).Replace('\\', '/'));
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"导入图集失败 {atlasRel}：{ex.Message}");
            }
        }

        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(assetBaseDir, file).Replace('\\', '/');
            if (IsIgnored(relative)) continue;
            if (skipRelative.Contains(relative)) continue;

            string name = AssetNameOf(relative);
            if (!names.Add(name))
            {
                warnings.Add($"资源名重复，已跳过：{relative}");
                continue;
            }

            byte[] bytes = File.ReadAllBytes(file);
            rawBytes += bytes.Length;

            try
            {
                if (relative.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap sprite = ShapeImporter.Import(Encoding.UTF8.GetString(bytes));
                    SKBitmap skSprite = ToSkBitmap(sprite);
                    if (options.TrimSprites) skSprite = Trim(skSprite);
                    inputs.Add(new SpriteInput(name, skSprite));
                    textureCount++;
                }
                else if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap image = PngDecoder.Decode(bytes);
                    SKBitmap skImage = ToSkBitmap(image);
                    if (autoAtlas)
                    {
                        if (options.TrimSprites) skImage = Trim(skImage);
                        inputs.Add(new SpriteInput(name, skImage));
                    }
                    else
                    {
                        // 不装箱：整图原样入包（适合 .atlas 预切图集，运行端按整张页图切片）
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = "texture",
                            Bytes = GetPixels(skImage),
                            Width = skImage.Width,
                            Height = skImage.Height,
                        });
                        skImage.Dispose();
                    }
                    textureCount++;
                }
                else
                {
                    // 文本 / JSON / 音效 / 任意字节 —— 全部原样作为 Bundle 内资源
                    bundle.Assets.Add(new AssetBundleAsset
                    {
                        Path = name,
                        Type = MimeOf(relative),
                        Bytes = bytes,
                    });
                    dataCount++;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"导入失败 {relative}：{ex.Message}");
            }
        }

        // 纹理装箱：把自动装箱的散图 + 导入的 .atlas 预切图集，合并成同一份 AtlasData
        //（资源名固定 "atlas"）。运行时由 SpriteSheetLoader 读取该 JSON 并按页加载整图纹理，所有精灵可合批。
        var allPages = new JsonArray();
        int pageIndex = 0;

        if (inputs.Count > 0)
        {
            var settings = new PackerSettings
            {
                MaxSize = options.AtlasMaxSize,
                Padding = options.AtlasPadding,
                AllowRotation = true,
            };

            IReadOnlyList<PackingResult> pages = AtlasPacker.PackPages(inputs, settings);
            atlasPageCount += pages.Count;

            var imageNames = new List<string>(pages.Count);
            for (int i = 0; i < pages.Count; i++)
                imageNames.Add($"{AtlasBaseName}_{pageIndex + i}.png");

            for (int i = 0; i < pages.Count; i++)
            {
                PackingResult page = pages[i];

                using SKBitmap atlas = AtlasPacker.RenderAtlas(page);
                byte[] png = EncodePng(atlas);

                bundle.Assets.Add(new AssetBundleAsset
                {
                    Path = $"{AtlasBaseName}_{pageIndex + i}",
                    Type = "texture",
                    Bytes = png,
                    Width = atlas.Width,
                    Height = atlas.Height,
                });

                if (options.WritePreviewPng)
                    File.WriteAllBytes(
                        Path.Combine(outputDirectory, $"atlas_{bundleName.Replace('/', '_')}_{pageIndex + i}.png"),
                        png);
            }

            // 复用 KTexturePacker 的 AtlasData 导出，再把其 Pages 并入合并列表
            string autoJson = AtlasExporter.ToGenericJson(pages, imageNames);
            using (var autoDoc = JsonDocument.Parse(autoJson))
            {
                JsonElement pagesEl = default;
                if (!autoDoc.RootElement.TryGetProperty("Pages", out pagesEl) &&
                    !autoDoc.RootElement.TryGetProperty("pages", out pagesEl)) { }
                else if (pagesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement p in pagesEl.EnumerateArray())
                        allPages.Add(JsonNode.Parse(p.GetRawText()));
                }
            }

            pageIndex += pages.Count;

            // 打包结束，释放输入位图（PackPages/RenderAtlas 已拷贝像素）
            foreach (SpriteInput input in inputs)
                input.Bitmap.Dispose();
        }

        // 导入的 .atlas 预切图集：整页图原样存为整图纹理，子精灵名（regions）原样保留到 AtlasData
        foreach (ImportedAtlas imp in importedAtlases)
        {
            using var pd = JsonDocument.Parse(imp.PageJson);
            int pw = pd.RootElement.GetProperty("width").GetInt32();
            int ph = pd.RootElement.GetProperty("height").GetInt32();

            allPages.Add(BuildAtlasPageNode(imp.PageJson, $"{AtlasBaseName}_{pageIndex}.png"));
            bundle.Assets.Add(new AssetBundleAsset
            {
                Path = $"{AtlasBaseName}_{pageIndex}",
                Type = "texture",
                Bytes = imp.PageBytes,
                Width = pw,
                Height = ph,
            });
            pageIndex++;
        }

        if (allPages.Count > 0)
        {
            var rootNode = new JsonObject { ["Pages"] = allPages };
            bundle.Assets.Add(new AssetBundleAsset
            {
                Path = AtlasBaseName,
                Type = "atlas",
                Bytes = Encoding.UTF8.GetBytes(rootNode.ToJsonString()),
            });
        }

        return bundle;
    }

    /// <summary>把框架 RGBA8 位图（直 alpha）转为 Skia 位图（Rgba8888 / Unpremul），供 KTexturePacker 装箱。</summary>
    private static SKBitmap ToSkBitmap(Bitmap bmp)
    {
        var sk = new SKBitmap(bmp.Width, bmp.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKPixmap pixmap = sk.PeekPixels();
        Marshal.Copy(bmp.Pixels, 0, pixmap.GetPixels(), bmp.Pixels.Length);
        return sk;
    }

    /// <summary>裁掉四周完全透明的行列，减小图集占用（替代原 Bitmap.Trim）。</summary>
    private static SKBitmap Trim(SKBitmap bmp)
    {
        using var pixmap = bmp.PeekPixels();
        ReadOnlySpan<byte> span = pixmap.GetPixelSpan();
        int w = bmp.Width, h = bmp.Height;
        int rowBytes = pixmap.RowBytes;
        int left = w, top = h, right = -1, bottom = -1;

        for (int y = 0; y < h; y++)
        {
            int row = y * rowBytes;
            for (int x = 0; x < w; x++)
            {
                if (span[row + x * 4 + 3] != 0)
                {
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
        }

        if (right < left)
        {
            bmp.Dispose();
            return new SKBitmap(1, 1);
        }

        int tw = right - left + 1, th = bottom - top + 1;
        var trimmed = new SKBitmap(tw, th, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bmp.ExtractSubset(trimmed, new SKRectI(left, top, left + tw, top + th));
        bmp.Dispose();
        return trimmed;
    }

    /// <summary>取出 Rgba8888 直 alpha 像素字节（整图纹理上传与装箱产物均依赖此格式）。</summary>
    private static byte[] GetPixels(SKBitmap bmp)
    {
        using var pixmap = bmp.PeekPixels();
        return pixmap.GetPixelSpan().ToArray();
    }

    /// <summary>把 Skia 整页位图编码为 PNG 字节（用于包内整图纹理与预览）。</summary>
    private static byte[] EncodePng(SKBitmap bmp)
    {
        using SKImage image = SKImage.FromBitmap(bmp);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>一个被导入的 .atlas 预切图集的整页：页图 JSON 原文 + 页图 PNG 字节。</summary>
    private sealed record ImportedAtlas(string PageJson, byte[] PageBytes);

    /// <summary>
    /// 把通用的 .atlas 页（小写键：image/width/height/regions[]）转换成框架 AtlasData 的页节点
    /// （PascalCase：Image/Width/Height/Regions[]，Region 含 Name/X/Y/W/H/Rotated/SourceW/SourceH），
    /// 供 <see cref="KTexturePacker.Parser.AtlasData"/> 反序列化、<see cref="SpriteSheetLoader"/> 读取。
    /// </summary>
    private static JsonNode BuildAtlasPageNode(string pageJson, string imageName)
    {
        using var doc = JsonDocument.Parse(pageJson);
        JsonElement page = doc.RootElement;

        var node = new JsonObject
        {
            ["Image"] = imageName,
            ["Width"] = page.GetProperty("width").GetInt32(),
            ["Height"] = page.GetProperty("height").GetInt32(),
        };

        var regions = new JsonArray();
        foreach (JsonElement r in page.GetProperty("regions").EnumerateArray())
        {
            var rn = new JsonObject
            {
                ["Name"] = r.GetProperty("name").GetString(),
                ["X"] = r.GetProperty("x").GetInt32(),
                ["Y"] = r.GetProperty("y").GetInt32(),
                ["W"] = r.GetProperty("w").GetInt32(),
                ["H"] = r.GetProperty("h").GetInt32(),
                ["Rotated"] = r.GetProperty("rotated").GetBoolean(),
                ["SourceW"] = r.TryGetProperty("sourceW", out var sw) ? sw.GetInt32() : r.GetProperty("w").GetInt32(),
                ["SourceH"] = r.TryGetProperty("sourceH", out var sh) ? sh.GetInt32() : r.GetProperty("h").GetInt32(),
            };
            regions.Add(rn);
        }
        node["Regions"] = regions;
        return node;
    }

    /// <summary>构建产物、隐藏文件、content 输出目录都不属于原始资源；打包配置文件也不该进包。</summary>
    internal static bool IsIgnored(string relativePath)
    {
        if (relativePath.Length == 0) return true;

        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.Length == 0) continue;
            if (segment[0] == '.') return true;
            if (string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "content", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "build.config.json", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>文件路径 → 资源名：去掉扩展名，小写化，统一用 / 分隔。</summary>
    private static string AssetNameOf(string relativePath)
    {
        string name = relativePath;
        if (name.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
            name = name[..^".sprite.json".Length];
        else
            name = Path.ChangeExtension(name, null);

        return PakFormat.NormalizeName(name);
    }

    /// <summary>按扩展名推断资源 MIME（仅作为包内元数据，不影响实际字节）。</summary>
    private static string MimeOf(string relative)
    {
        if (relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return "json";
        if (relative.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            relative.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return "text";
        if (relative.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) return "audio/wav";
        if (relative.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return "audio/mpeg";
        if (relative.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) return "audio/ogg";
        return "application/octet-stream";
    }
}
