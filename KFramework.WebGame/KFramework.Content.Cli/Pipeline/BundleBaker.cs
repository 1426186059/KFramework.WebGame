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
        // 自动装箱的散图合成整页纹理（atlas_{i}）+ 一份 AtlasData JSON（资源名固定为 "atlas.json"）；
        // 已切好的 .atlas 图集（以 .atlas 结尾）由前述预扫描原样入库，不参与此处自动打包。
        // 运行时由 SpriteSheetLoader 读取 JSON 并提供 source rect，从而同一张图集页可合批。
        var inputs = new List<SpriteInput>();
        var bundle = new AssetBundleBuild { AssetBundleName = bundleName };
        var names = new HashSet<string>(StringComparer.Ordinal);

        // 预扫描：识别 .atlas 预切图集（通用判定：以 .atlas 结尾）。
        // 已切好的图集（描述文件 + 整页图）直接原样入库，不再走自动装箱，保留用户打包好的布局。
        var atlasPageRelatives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string atlasFile in files)
        {
            string atlasRel = Path.GetRelativePath(assetBaseDir, atlasFile).Replace('\\', '/');
            if (!AtlasFile.IsAtlas(atlasRel)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(atlasFile));
                if (doc.RootElement.TryGetProperty("pages", out JsonElement pagesEl))
                {
                    foreach (JsonElement page in pagesEl.EnumerateArray())
                    {
                        string image = page.GetProperty("image").GetString()!;
                        string pagePath = Path.Combine(Path.GetDirectoryName(atlasFile)!, image);
                        atlasPageRelatives.Add(Path.GetRelativePath(assetBaseDir, pagePath).Replace('\\', '/'));
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"解析图集失败 {atlasRel}：{ex.Message}");
            }
        }

        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(assetBaseDir, file).Replace('\\', '/');
            if (IsIgnored(relative)) continue;

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
                if (AtlasFile.IsAtlas(relative))
                {
                    // 已切好的图集描述文件：原样入库，运行端按 .atlas 直接加载，不再自动打包
                    bundle.Assets.Add(new AssetBundleAsset
                    {
                        Path = name,
                        Type = "atlas",
                        Bytes = bytes,
                    });
                    dataCount++;
                    continue;
                }

                if (atlasPageRelatives.Contains(relative))
                {
                    // 已切图集的整页图：原样入库为整图纹理，跳过自动装箱（不再重排/重切）
                    Bitmap image = PngDecoder.Decode(bytes);
                    SKBitmap skImage = ToSkBitmap(image);
                    (byte[] encoded, AssetTextureFormat fmt) = EncodeTexture(skImage, options);
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
                    textureCount++;
                    continue;
                }

                if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
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
                        // 不装箱：整图原样入包，按 BuildOptions.TextureFormat 编码（与已切图集整页图同一编码路径）。
                        (byte[] encoded, AssetTextureFormat fmt) = EncodeTexture(skImage, options);
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

        // 仅当存在需要自动装箱的散图时才生成 atlas.json；已切好的 .atlas 图集不参与此处打包（前述预扫描已原样入库）。
        if (inputs.Count > 0)
        {
            JsonArray allPages = AtlasBuilder.BuildAtlas(bundle, inputs, options, bundleName, tempDirectory, ref atlasPageCount);
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

    /// <summary>按 BuildOptions.TextureFormat 把 SKBitmap 编码为目标格式字节 + 格式标记（整图纹理与已切图集整页图共用）。</summary>
    private static (byte[] Bytes, AssetTextureFormat Format) EncodeTexture(SKBitmap skImage, ContentBuilder.BuildOptions options)
        => options.TextureFormat switch
        {
            AssetTextureFormat.Rgba => (GetPixels(skImage), AssetTextureFormat.Rgba),
            AssetTextureFormat.Png  => (EncodePng(skImage),  AssetTextureFormat.Png),
            AssetTextureFormat.Webp => (EncodeWebp(skImage), AssetTextureFormat.Webp),
            AssetTextureFormat.Ktx2 => (EncodeKtx2(skImage, options.BasisuPath, options.Ktx2Quality), AssetTextureFormat.Ktx2),
            _ => (GetPixels(skImage), AssetTextureFormat.Rgba),
        };

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

    /// <summary>文件路径 → 资源名：保留原始扩展名（如 .png/.json/.txt），仅小写化、统一用 / 分隔。
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
