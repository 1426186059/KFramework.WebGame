using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KFramework.MonoGame;

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
        text.AppendLine($"资源 {AssetCount} 个（纹理 {TextureCount} / 数据 {DataCount}），图集 {AtlasPageCount} 页，{AtlasCount} 个 AssetBundle");
        text.AppendLine($"体积 {RawBytes / 1024.0:F1} KB -> {PackedBytes / 1024.0:F1} KB（{CompressionRatio:P0}）");
        text.Append($"耗时 {Elapsed.TotalMilliseconds:F0} ms");
        return text.ToString();
    }

    public int AtlasCount { get; init; }
}

/// <summary>
/// 内容管线：<c>raw/</c>（原始资源）→ <c>content/</c>（发布资源）。
/// 与 PixiJS assetpack 的思路一致：开发者只维护 raw，content 全部由工具生成。
///
/// <para>打包目录约定：</para>
/// <list type="bullet">
///   <item>在 raw 目录下放置一个打包配置文件（<c>bundles.json</c> 或 <c>pack.json</c>）指定打包根目录：
///         字段 <c>bundlesDir</c>（或 <c>bundleDirs</c>）可填字符串，也可填字符串数组，例如
///         <c>{ "bundlesDir": ["Bundles", "UI"] }</c>；未配置或字段缺失时缺省为 <c>Bundles</c>。</item>
///   <item>每个打包根目录下的「每一个含资源的子文件夹」分别打包成一个 AssetBundle（包名 = 子文件夹相对该根目录的路径）。</item>
///   <item>多个根目录下的子文件夹包名必须唯一，出现同名会直接报错（请保证各根目录内子文件夹名不重复）。</item>
///   <item>每个文件夹只打包其「直接」资源，不含子目录资源（子目录自身也是独立的 AssetBundle）。</item>
///   <item>若配置的根目录都不存在，则回退为「整包 raw 作为一个 content 包」的兼容模式。</item>
/// </list>
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

        List<string> bundleDirs = ReadBundlesDir(rawDirectory);

        var builds = new List<AssetBundleBuild>();
        long rawBytes = 0;
        int textureCount = 0;
        int dataCount = 0;
        int atlasPageCount = 0;
        int bundleCount = 0;

        // 解析配置里实际存在的打包根目录
        var bundleRoots = new List<string>();
        foreach (string dir in bundleDirs)
        {
            string root = Path.Combine(rawDirectory, dir);
            if (Directory.Exists(root)) bundleRoots.Add(root);
        }

        if (bundleRoots.Count == 0)
        {
            // ===== 模式 B（兼容旧用法）：整包 raw 作为一个 content 包 =====
            Console.WriteLine($"[kfc] 打包模式：整包（未发现 {string.Join(", ", bundleDirs)} 目录，回退为单个 content 包）");
            string[] allFiles = Directory.EnumerateFiles(rawDirectory, "*", SearchOption.AllDirectories)
                .Where(f => !IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
                .OrderBy(static f => f, StringComparer.Ordinal)
                .ToArray();
            builds.Add(BuildBundle("content", allFiles, rawDirectory, options, warnings,
                ref rawBytes, ref textureCount, ref dataCount, ref atlasPageCount, outputDirectory));
            bundleCount = 1;
        }
        else
        {
            // ===== 模式 A：按目录分别打包（更通用）=====
            Console.WriteLine($"[kfc] 打包模式：按目录分别打包（打包目录 = {string.Join(", ", bundleDirs)}）");

            // 提示配置中存在但物理缺失的打包目录
            foreach (string dir in bundleDirs)
            {
                string root = Path.Combine(rawDirectory, dir);
                if (!Directory.Exists(root)) warnings.Add($"打包目录未找到，已忽略：{dir}");
            }

            // 各根目录下的子文件夹包名必须唯一（保证运行端 GetBundle(name) 无歧义）
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string bundlesRoot in bundleRoots)
            {
                foreach (string folder in Directory.EnumerateDirectories(bundlesRoot, "*", SearchOption.AllDirectories)
                                               .OrderBy(static f => f, StringComparer.Ordinal))
                {
                    // 只打包「直接」含资源的子文件夹；子目录下的资源由其自身所在的文件夹负责
                    string[] directFiles = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                        .Where(f => !IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
                        .OrderBy(static f => f, StringComparer.Ordinal)
                        .ToArray();
                    if (directFiles.Length == 0) continue;

                    string bundleName = PakFormat.NormalizeName(Path.GetRelativePath(bundlesRoot, folder).Replace('\\', '/'));
                    if (!usedNames.Add(bundleName))
                        throw new InvalidOperationException(
                            $"发现重复的 AssetBundle 名「{bundleName}」：配置的打包目录（{string.Join(", ", bundleDirs)}）下存在同名子文件夹，请保证各打包目录内的子文件夹名唯一。");

                    AssetBundleBuild build = BuildBundle(bundleName, directFiles, bundlesRoot, options, warnings,
                        ref rawBytes, ref textureCount, ref dataCount, ref atlasPageCount, outputDirectory);
                    builds.Add(build);
                    bundleCount++;
                }
            }

            if (builds.Count == 0)
                warnings.Add($"打包目录（{string.Join(", ", bundleDirs)}）下没有发现任何「含资源的子文件夹」，未产出任何 AssetBundle。");
        }

        // 构建所有 AssetBundle（每个包独立 .web.lib，并汇总总清单 version.manifest）
        BuildResult result = BundleBuilder.BuildAssetBundles(builds);
        foreach (var pkg in result.Manifest.Packages)
        {
            File.WriteAllBytes(Path.Combine(outputDirectory, pkg.File), result.Bundles[pkg.Name]);
            Console.WriteLine($"[kfc] 资源包 {pkg.Name} -> {pkg.File}（{pkg.Size} 字节，哈希 {pkg.Hash}）");
        }
        File.WriteAllText(Path.Combine(outputDirectory, "version.manifest"), result.Manifest.Serialize());

        long totalBundleBytes = result.Manifest.Packages.Sum(p => p.Size);
        watch.Stop();

        return new BuildReport
        {
            AssetCount = textureCount + dataCount,
            TextureCount = textureCount,
            DataCount = dataCount,
            AtlasPageCount = atlasPageCount,
            AtlasCount = bundleCount,
            RawBytes = rawBytes,
            PackedBytes = totalBundleBytes,
            Elapsed = watch.Elapsed,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// 构建单个 AssetBundle：把给定的（某文件夹的）直接资源文件打包——纹理进图集，其余原样入包。
    /// 图集页与子图索引都写入同一个包，因此每个包自带其纹理（运行端按 Page 切片）。
    /// </summary>
    private static AssetBundleBuild BuildBundle(
        string bundleName, string[] files, string assetBaseDir, BuildOptions options,
        List<string> warnings, ref long rawBytes, ref int textureCount, ref int dataCount, ref int atlasPageCount,
        string outputDirectory)
    {
        var packer = new AtlasPacker { MaxSize = options.AtlasMaxSize, Padding = options.AtlasPadding };
        var bundle = new AssetBundleBuild { AssetBundleName = bundleName };
        var names = new HashSet<string>(StringComparer.Ordinal);

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

        // 纹理统一装箱（每个包独立图集）
        IReadOnlyList<AtlasPage> pages = packer.Pack();
        atlasPageCount += pages.Count;
        foreach (AtlasPage page in pages)
        {
            bundle.Assets.Add(new AssetBundleAsset
            {
                Path = $"atlas/{page.Index}",
                Type = "atlas",
                Bytes = page.Bitmap.Pixels,
                Width = page.Bitmap.Width,
                Height = page.Bitmap.Height,
            });

            foreach (AtlasRegion region in page.Regions)
            {
                bundle.Assets.Add(new AssetBundleAsset
                {
                    Path = region.Name,
                    Type = "texture",
                    Bytes = Array.Empty<byte>(),
                    Page = page.Index,
                    X = region.X,
                    Y = region.Y,
                    Width = region.Width,
                    Height = region.Height,
                });
            }

            if (options.WritePreviewPng)
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, $"atlas_{bundleName.Replace('/', '_')}_{page.Index}.png"),
                    PngEncoder.Encode(page.Bitmap));
        }

        return bundle;
    }

    /// <summary>
    /// 读取打包配置，返回打包根目录列表（相对 raw）。
    /// 支持 <c>bundles.json</c> / <c>pack.json</c>，字段 <c>bundlesDir</c>（或 <c>bundleDirs</c>）可为字符串或字符串数组；
    /// 缺省默认 <c>["Bundles"]</c>。若配置文件均不存在，则自动生成一个默认 <c>bundles.json</c>。
    /// </summary>
    private static List<string> ReadBundlesDir(string rawDirectory)
    {
        foreach (string cfg in new[] { "bundles.json", "pack.json" })
        {
            string path = Path.Combine(rawDirectory, cfg);
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                var dirs = new List<string>();
                if (root.TryGetProperty("bundlesDir", out JsonElement a)) dirs.AddRange(ResolveBundleDirs(a));
                if (root.TryGetProperty("bundleDirs", out JsonElement b)) dirs.AddRange(ResolveBundleDirs(b));
                if (dirs.Count > 0) return dirs;
            }
            catch
            {
                // 配置损坏则忽略，使用默认目录
            }
        }

        // 未找到打包配置：自动生成一个默认 bundles.json（打包目录默认 Bundles），方便后续按目录分别打包
        string defaultPath = Path.Combine(rawDirectory, "bundles.json");
        try
        {
            File.WriteAllText(defaultPath, "{\"bundlesDir\":\"Bundles\"}", new UTF8Encoding(false));
            Console.WriteLine($"[kfc] 未发现打包配置，已自动生成 {Path.GetFileName(defaultPath)}（默认打包目录 Bundles）");
        }
        catch
        {
            // 无法写入也不影响本次打包（回退整包 content）
        }
        return new List<string> { "Bundles" };
    }

    /// <summary>把 <c>bundlesDir</c> 字段解析为目录列表：字符串或字符串数组都支持。</summary>
    private static IEnumerable<string> ResolveBundleDirs(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string? s = element.GetString();
            if (!string.IsNullOrWhiteSpace(s)) yield return s!.Replace('\\', '/').Trim('/');
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) yield return s!.Replace('\\', '/').Trim('/');
                }
            }
        }
    }

    private static void CleanOutput(string outputDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".web.lib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("version.manifest", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>构建产物、隐藏文件、content 输出目录都不属于原始资源；打包配置文件也不该进包。</summary>
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
            if (string.Equals(segment, "bundles.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(segment, "pack.json", StringComparison.OrdinalIgnoreCase)) return true;
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
