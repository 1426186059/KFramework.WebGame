using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
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
        string tempDirectory)
    {
        // 图集打包统一复用外部 KTexturePacker 工具的核心（MaxRects 摆放 + 整页合成 + AtlasData 导出）。
        // 产物：整页纹理（atlas_{i}，单张 Texture2D）+ 一份 AtlasData JSON（资源名固定为 "atlas"）。
        // 运行时由 SpriteSheetLoader 读取该 JSON 并提供 source rect，从而同一张图集页可合批。
        var inputs = new List<SpriteInput>();
        var bundle = new AssetBundleBuild { AssetBundleName = bundleName };
        var names = new HashSet<string>(StringComparer.Ordinal);

        // 预扫描：识别 .atlas 预切图集，记录其整页图，避免后续被当成散图重打包；
        // 这些子精灵名（如 characters_256、misc-3_68）需原样保留到最终的 AtlasData。
        var skipRelative = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedAtlases = new List<AtlasBuilder.ImportedAtlas>();
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
                        importedAtlases.Add(new AtlasBuilder.ImportedAtlas(page.GetRawText(), pageBytes));
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
                        // 不装箱：整图原样入包（适合 .atlas 预切图集，运行端按整张页图切片）。
                        // 按 BuildOptions.TextureFormat 编码：Rgba 存裸像素；Png 编码 PNG；Webp 编码 WebP；Ktx2 暂未实现。
                        (byte[] encoded, AssetTextureFormat fmt) = options.TextureFormat switch
                        {
                            AssetTextureFormat.Rgba => (GetPixels(skImage), AssetTextureFormat.Rgba),
                            AssetTextureFormat.Png  => (EncodePng(skImage),  AssetTextureFormat.Png),
                            AssetTextureFormat.Webp => (EncodeWebp(skImage), AssetTextureFormat.Webp),
                            AssetTextureFormat.Ktx2 => (EncodeKtx2(skImage, options.BasisuPath, options.Ktx2Quality), AssetTextureFormat.Ktx2),
                            _ => (GetPixels(skImage), AssetTextureFormat.Rgba),
                        };
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = "texture",
                            Bytes = encoded,
                            Width = skImage.Width,
                            Height = skImage.Height,
                            Format = fmt,
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

        // 图集打包：把自动装箱的散图 + 导入的 .atlas 预切图集合并成同一份 AtlasData
        JsonArray allPages = AtlasBuilder.BuildAtlas(bundle, inputs, importedAtlases, options, bundleName, tempDirectory, ref atlasPageCount);

        if (allPages.Count > 0)
        {
            var rootNode = new JsonObject { ["Pages"] = allPages };
            bundle.Assets.Add(new AssetBundleAsset
            {
                // 图集描述 JSON 的路径按 raw 根目录计算，保留 .json 后缀（如 myres/atlas/atlas.json），与包内其它资源名保持一致
                Path = Path.Combine(bundleName, "atlas.json").Replace('\\', '/'),
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

    /// <summary>把 SKBitmap 编码为 PNG 字节（整图非装箱模式下 TextureFormat=Png 时使用）。</summary>
    private static byte[] EncodePng(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>把 SKBitmap 编码为 WebP 字节（q90，整图非装箱模式下 TextureFormat=Webp 时使用）。</summary>
    private static byte[] EncodeWebp(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Webp, 90);
        return data.ToArray();
    }

    /// <summary>把 RGBA8 字节（行优先 W*H*4）编码为 WebP（供图集页复用，因上游 KTexturePacker 不提供 ToWebp）。</summary>
    internal static byte[] EncodeWebpFromRgba(byte[] rgba, int width, int height)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        try
        {
            using var pixmap = bmp.PeekPixels();
            Marshal.Copy(rgba, 0, pixmap.GetPixels(), rgba.Length);
            return EncodeWebp(bmp);
        }
        finally
        {
            bmp.Dispose();
        }
    }



    /// <summary>构建产物、隐藏文件、content 输出目录都不属于原始资源；打包配置文件也不该进包。</summary>
    /// <summary>把 RGBA8 字节（行优先 W*H*4）编码为 KTX2（Basis Universal 超压缩）。</summary>
    /// <remarks>
    /// 经临时 PNG 调用 <c>basisu</c> 命令行编码为 KTX2（UASTC）。需预先安装 Basis Universal 工具
    /// （https://github.com/BinomialLLC/basis_universal），并配置 <paramref name="basisuPath"/>（为空则用 PATH 中的 basisu）。
    /// 编码失败时抛出明确异常，提示安装/配置 basisu。
    /// </remarks>
    internal static byte[] EncodeKtx2FromRgba(byte[] rgba, int width, int height, string? basisuPath, int quality)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        try
        {
            using var pixmap = bmp.PeekPixels();
            Marshal.Copy(rgba, 0, pixmap.GetPixels(), rgba.Length);
            return EncodeKtx2(bmp, basisuPath, quality);
        }
        finally
        {
            bmp.Dispose();
        }
    }

    /// <summary>把 SKBitmap 编码为 KTX2（Basis Universal 超压缩），借外部 <c>basisu</c> 命令行完成。</summary>
    internal static byte[] EncodeKtx2(SKBitmap bmp, string? basisuPath, int quality)
    {
        string tmpPng = Path.Combine(Path.GetTempPath(), $"kf_{Guid.NewGuid():N}.png");
        string outKtx = Path.Combine(Path.GetTempPath(), $"kf_{Guid.NewGuid():N}.ktx2");
        try
        {
            using (var img = SKImage.FromBitmap(bmp))
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
                File.WriteAllBytes(tmpPng, data.ToArray());

            string exe = string.IsNullOrWhiteSpace(basisuPath) ? "basisu" : basisuPath;
            var psi = new ProcessStartInfo(exe,
                $"-file \"{tmpPng}\" -format ktx2 -uastc {quality} -mipmap -o \"{outKtx}\"")
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process proc;
            try
            {
                proc = Process.Start(psi)
                       ?? throw new InvalidOperationException($"无法启动 basisu（{exe}）。");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"无法启动 basisu（{exe}）。请安装 Basis Universal 命令行工具并配置 BuildOptions.BasisuPath。原因：{ex.Message}");
            }

            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"basisu 编码失败（退出码 {proc.ExitCode}）：{err}");
            return File.ReadAllBytes(outKtx);
        }
        finally
        {
            if (File.Exists(tmpPng)) File.Delete(tmpPng);
            if (File.Exists(outKtx)) File.Delete(outKtx);
        }
    }

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

    /// <summary>文件路径 → 资源名：保留原始扩展名（如 .png/.json/.txt/.sprite.json），仅小写化、统一用 / 分隔。
    /// 保留扩展名可让资源名携带更多类型信息，配合模糊/精确查找更易区分同名不同型的资源。</summary>
    private static string AssetNameOf(string relativePath)
        => PakFormat.NormalizeName(relativePath);

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
