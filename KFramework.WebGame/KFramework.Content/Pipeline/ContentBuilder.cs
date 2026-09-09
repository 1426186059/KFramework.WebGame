using System.Diagnostics;
using System.Text;
using KFramework.Content.Pak;

namespace KFramework.Content.Pipeline;

/// <summary>一次构建的结果统计。</summary>
public sealed class BuildReport
{
    public int AssetCount { get; init; }
    public int TextureCount { get; init; }
    public int DataCount { get; init; }
    public int AtlasPageCount { get; init; }
    public long RawBytes { get; init; }
    public long PackedBytes { get; init; }
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public double CompressionRatio => RawBytes <= 0 ? 0 : PackedBytes / (double)RawBytes;

    public override string ToString()
    {
        var text = new StringBuilder();
        text.AppendLine($"资源 {AssetCount} 个（纹理 {TextureCount} / 数据 {DataCount}），图集 {AtlasPageCount} 页");
        text.AppendLine($"体积 {RawBytes / 1024.0:F1} KB -> {PackedBytes / 1024.0:F1} KB（{CompressionRatio:P0}）");
        text.Append($"耗时 {Elapsed.TotalMilliseconds:F0} ms");
        return text.ToString();
    }
}

/// <summary>
/// 内容管线：<c>raw/</c>（原始资源）→ <c>content/</c>（发布资源）。
/// 与 PixiJS assetpack 的思路一致：开发者只维护 raw，content 全部由工具生成。
/// </summary>
public sealed class ContentBuilder
{
    public sealed class BuildOptions
    {
        /// <summary>单张图集的边长上限。</summary>
        public int AtlasMaxSize { get; set; } = 2048;

        /// <summary>图集内相邻精灵的间隔。</summary>
        public int AtlasPadding { get; set; } = 2;

        /// <summary>是否额外输出 atlas_N.png，方便用看图工具检查发布结果。</summary>
        public bool WritePreviewPng { get; set; } = true;

        public string PakFileName { get; set; } = "content.pak";

        /// <summary>是否裁掉精灵四周的透明边。</summary>
        public bool TrimSprites { get; set; } = true;
    }

    public BuildReport Build(string rawDirectory, string outputDirectory, BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        var watch = Stopwatch.StartNew();
        var warnings = new List<string>();

        if (!Directory.Exists(rawDirectory))
            throw new DirectoryNotFoundException($"原始资源目录不存在：{rawDirectory}");

        Directory.CreateDirectory(outputDirectory);
        CleanOutput(outputDirectory);

        var packer = new AtlasPacker { MaxSize = options.AtlasMaxSize, Padding = options.AtlasPadding };
        var pak = new PakWriter();
        var manifest = new ContentManifest
        {
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            Root = Path.GetFullPath(rawDirectory),
        };

        long rawBytes = 0;
        int textureCount = 0;
        int dataCount = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(rawDirectory, "*", SearchOption.AllDirectories)
                                         .OrderBy(static f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(rawDirectory, file).Replace('\\', '/');
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
                if (relative.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap sprite = ShapeImporter.Import(Encoding.UTF8.GetString(bytes));
                    if (options.TrimSprites) sprite = sprite.Trim();
                    packer.Add(name, sprite);
                    textureCount++;
                }
                else if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap image = PngDecoder.Decode(bytes);
                    if (options.TrimSprites) image = image.Trim();
                    packer.Add(name, image);
                    textureCount++;
                }
                else if (relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    pak.AddText(name, Encoding.UTF8.GetString(bytes), AssetType.Json);
                    manifest.Assets.Add(new ManifestAsset
                    {
                        Name = name, Type = "json", Page = -1, Size = bytes.Length,
                    });
                    dataCount++;
                }
                else if (relative.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                         relative.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    pak.AddText(name, Encoding.UTF8.GetString(bytes), AssetType.Text);
                    manifest.Assets.Add(new ManifestAsset
                    {
                        Name = name, Type = "text", Page = -1, Size = bytes.Length,
                    });
                    dataCount++;
                }
                else
                {
                    pak.Add(name, AssetType.Bytes, bytes);
                    manifest.Assets.Add(new ManifestAsset
                    {
                        Name = name, Type = "bytes", Page = -1, Size = bytes.Length,
                    });
                    dataCount++;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"导入失败 {relative}：{ex.Message}");
            }
        }

        // 纹理统一装箱
        IReadOnlyList<AtlasPage> pages = packer.Pack();
        foreach (AtlasPage page in pages)
        {
            string pageName = $"atlas/{page.Index}";
            pak.AddTexture(pageName, page.Bitmap, AssetType.Atlas);

            foreach (AtlasRegion region in page.Regions)
            {
                manifest.Assets.Add(new ManifestAsset
                {
                    Name = region.Name,
                    Type = "texture",
                    Page = page.Index,
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height,
                    Size = region.Width * region.Height * 4L,
                });
            }

            if (options.WritePreviewPng)
                File.WriteAllBytes(Path.Combine(outputDirectory, $"atlas_{page.Index}.png"), PngEncoder.Encode(page.Bitmap));
        }

        // 写出 pak
        byte[] pakBytes;
        using (var buffer = new MemoryStream())
        {
            pak.SaveTo(buffer);
            pakBytes = buffer.ToArray();
        }
        File.WriteAllBytes(Path.Combine(outputDirectory, options.PakFileName), pakBytes);

        manifest.Paks.Add(new ManifestPak
        {
            File = options.PakFileName,
            Size = pakBytes.Length,
            Checksum = PakFormat.Checksum(pakBytes),
        });

        File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), manifest.ToJson(), Encoding.UTF8);

        watch.Stop();

        return new BuildReport
        {
            AssetCount = textureCount + dataCount,
            TextureCount = textureCount,
            DataCount = dataCount,
            AtlasPageCount = pages.Count,
            RawBytes = rawBytes,
            PackedBytes = pakBytes.Length,
            Elapsed = watch.Elapsed,
            Warnings = warnings,
        };
    }

    private static void CleanOutput(string outputDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>构建产物、隐藏文件、content 输出目录都不属于原始资源。</summary>
    private static bool IsIgnored(string relativePath)
    {
        if (relativePath.Length == 0) return true;

        foreach (string segment in relativePath.Split('/'))
        {
            if (segment.Length == 0) continue;
            if (segment[0] == '.') return true;
            if (string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "content", StringComparison.OrdinalIgnoreCase)) return true;
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
}
