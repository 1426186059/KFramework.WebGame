using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KTexturePacker.Core;
using SkiaSharp;

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

    /// <summary>实际发布（打包产物）目录，供命令行回显。</summary>
    public string OutputDirectory { get; init; } = "";
}

/// <summary>
/// 内容管线：<c>raw/</c>（原始资源）→ <c>content/</c>（发布资源）。
/// 与 PixiJS assetpack 的思路一致：开发者只维护 raw，content 全部由工具生成。
///
/// <para>打包目录约定：</para>
/// <list type="bullet">
///   <item>在 Content 根目录放置一个打包配置文件 <c>build.config.json</c> 指定打包根目录：
///         字段 <c>bundlesDir</c> / <c>bundleDirs</c> / <c>AssetBundleDir</c> 可填字符串或字符串数组，例如
///         <c>{ "bundlesDir": ["Bundles", "UI"] }</c>；未配置或字段缺失时缺省为 <c>Bundles</c>。
///         字段值为空字符串 <c>""</c> 表示「打包根目录（Content/raw）自身」作为打包目录，其下每个含资源的子文件夹各自成包。</item>
///   <item>每个打包根目录下的「每一个含资源的子文件夹」分别打包成一个 AssetBundle（包名 = 子文件夹相对该根目录的路径）。</item>
///   <item>多个根目录下的子文件夹包名必须唯一，出现同名会直接报错（请保证各根目录内子文件夹名不重复）。</item>
///   <item>每个文件夹只打包其「直接」资源，不含子目录资源（子目录自身也是独立的 AssetBundle）。</item>
///   <item>是否自动图集打包由 <c>autoAtlas</c> 控制（默认 true）：true 时独立 <c>.png</c> 经图集打包器装箱成图集；
///        false 时 <c>.png</c> 原样整图入包（适合已用 <c>.atlas</c> 预切好的图集，运行端按整张页图切片）。<c>.sprite.json</c> 矢量图始终装箱，不受此项影响。</item>
///   <item>除「指定打包目录」外，其余 raw 文件（如静态资源、配置文件等）原封不动地复制到 <c>content/</c>，不做打包/压缩。</item>
///   <item>配置里指定的打包根目录必须真实存在；若不存在则直接报错（不再支持把整个 raw 打成单个 content 整包的“兼容模式”）。</item>
///   <item>输出目录由配置 <c>outDir</c> 指定（相对 root，默认 <c>content</c>）；发布方式由 <c>deploy</c> 决定：
///         <c>www</c>（把产物整体镜像复制到 <c>wwwDir</c>，默认 www）/ <c>serve</c>（在产物目录上启动本地 HTTP 服务，端口 <c>port</c> 默认 8080）/ <c>none</c>。</item>
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

    public BuildReport Build(string rawDirectory, BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        var watch = Stopwatch.StartNew();
        var warnings = new List<string>();

        if (!Directory.Exists(rawDirectory))
        {
            throw new DirectoryNotFoundException($"原始资源目录不存在：{rawDirectory}");
        }
        
        List<string> bundleDirs = Global.mBuildConfig.BundleDirsResolved;
        string root = Path.GetDirectoryName(Path.GetFullPath(rawDirectory)) ?? rawDirectory;
        string outputDirectory = Global.mBuildConfig.OutDir;
        if (!Path.IsPathFullyQualified(outputDirectory))
        {
            outputDirectory = Path.Combine(root, Global.mBuildConfig.OutDir);
        }

        Directory.Delete(outputDirectory, true);
        Directory.CreateDirectory(outputDirectory);

        List<string> bundleRoots = new List<string>();
        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        long rawBytes = 0;
        int textureCount = 0;
        int dataCount = 0;
        int atlasPageCount = 0;
        int bundleCount = 0;

        if (bundleDirs.Count > 0)
        {
            foreach (string dir in bundleDirs)
            {
                string dirRoot = Path.Combine(rawDirectory, dir);
                if (Directory.Exists(dirRoot))
                {
                    bundleRoots.Add(dirRoot);
                }
            }

            // ===== 按目录分别打包（指定 bundlesDir 模式）：每个含资源的子文件夹各自成包 =====
            PrintTool.Log($"[kfc] 打包模式：按目录分别打包（打包目录 = {string.Join(", ", bundleDirs)}）");

            // 提示配置中存在但物理缺失的打包目录
            foreach (string dir in bundleDirs)
            {
                string dirRoot = Path.Combine(rawDirectory, dir);
                if (!Directory.Exists(dirRoot)) warnings.Add($"打包目录未找到，已忽略：{dir}");
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

                    AssetBundleBuild build = BuildBundle(bundleName, directFiles, bundlesRoot, options, config.AutoAtlas, warnings,
                        ref rawBytes, ref textureCount, ref dataCount, ref atlasPageCount, outputDirectory);
                    builds.Add(build);
                    bundleCount++;
                }
            }

            if (builds.Count == 0)
                warnings.Add($"打包目录（{string.Join(", ", bundleDirs)}）下没有发现任何「含资源的子文件夹」，未产出任何 AssetBundle。");
        }


        int copied = CopyRawAssets(rawDirectory, outputDirectory, bundleRoots);
        PrintTool.Log($"[kfc] 其余 {copied} 个文件已原样复制到 content/（未打包）");
        
        BuildResult result = BundleBuilder.BuildAssetBundles(builds);
        foreach (var pkg in result.Manifest.Packages)
        {
            string pkgPath = Path.Combine(outputDirectory, pkg.File);
            Directory.CreateDirectory(Path.GetDirectoryName(pkgPath)!);
            File.WriteAllBytes(pkgPath, result.Bundles[pkg.Name]);
            PrintTool.Log($"[kfc] 资源包 {pkg.Name} -> {pkg.File}（{pkg.Size} 字节，哈希 {pkg.Hash}）");
        }
        File.WriteAllText(Path.Combine(outputDirectory, "version.manifest"), result.Manifest.Serialize());

        // 发布阶段（部署）：复制到其他目录 / 本地 HTTP 服务 / 不发布
        Deploy(Global.mBuildConfig, root, outputDirectory, warnings);

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
            OutputDirectory = outputDirectory,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// 构建单个 AssetBundle：把给定的（某文件夹的）直接资源文件打包——纹理进图集，其余原样入包。
    /// 图集页与子图索引都写入同一个包，因此每个包自带其纹理（运行端按 Page 切片）。
    /// </summary>
    private static AssetBundleBuild BuildBundle(
        string bundleName, string[] files, string assetBaseDir, BuildOptions options, bool autoAtlas,
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
                    if (options.TrimSprites) sprite = sprite.Trim();
                    inputs.Add(new SpriteInput(name, ToSkBitmap(sprite)));
                    textureCount++;
                }
                else if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    Bitmap image = PngDecoder.Decode(bytes);
                    if (autoAtlas)
                    {
                        if (options.TrimSprites) image = image.Trim();
                        inputs.Add(new SpriteInput(name, ToSkBitmap(image)));
                    }
                    else
                    {
                        // 不装箱：整图原样入包（适合 .atlas 预切图集，运行端按整张页图切片）
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = "texture",
                            Bytes = image.Pixels,
                            Width = image.Width,
                            Height = image.Height,
                        });
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

    /// <summary>把框架 RGBA8 位图（直 alpha）转为 Skia 位图，供 KTexturePacker 装箱。</summary>
    private static SKBitmap ToSkBitmap(Bitmap bmp)
    {
        var sk = new SKBitmap(bmp.Width, bmp.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKPixmap pixmap = sk.PeekPixels();
        Marshal.Copy(bmp.Pixels, 0, pixmap.GetPixels(), bmp.Pixels.Length);
        return sk;
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



    private static void CleanOutput(string outputDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            bool isPreviewPng = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                               name.StartsWith("atlas_", StringComparison.OrdinalIgnoreCase);
            if (name.EndsWith(".pak", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".web.lib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("version.manifest", StringComparison.OrdinalIgnoreCase) ||
                isPreviewPng)
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
            if (string.Equals(segment, "build.config.json", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// 在「按目录打包」模式下，把不在任何打包根目录内的 raw 文件原样复制到 content/（不做打包/压缩，保持原样）。
    /// 属于打包根目录的文件已被打成 AssetBundle，跳过；打包配置文件（build.config.json）也跳过。
    /// </summary>
    private static int CopyRawAssets(string rawDirectory, string outputDirectory, List<string> bundleRoots)
    {
        int copied = 0;
        foreach (string file in Directory.EnumerateFiles(rawDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(static f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(rawDirectory, file).Replace('\\', '/');
            if (IsIgnored(relative)) continue;

            // 属于某个打包根目录的文件已被打成 AssetBundle，不再原样复制
            bool underBundle = false;
            foreach (string root in bundleRoots)
            {
                string rootRel = Path.GetRelativePath(rawDirectory, root).Replace('\\', '/').TrimEnd('/');
                if (relative == rootRel || relative.StartsWith(rootRel + "/", StringComparison.Ordinal))
                {
                    underBundle = true;
                    break;
                }
            }
            if (underBundle) continue;

            string dest = Path.Combine(outputDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
            copied++;
        }
        return copied;
    }

    /// <summary>
    /// 发布（部署）阶段：根据配置把构建产物发布出去。
    /// <list type="bullet">
    ///   <item><c>www</c> / <c>copy</c>：把 outputDirectory 整体镜像复制到 wwwDir（相对 root）。</item>
    ///   <item><c>serve</c>：在 outputDirectory 上启动一个本地静态 HTTP 服务（阻塞，直到 Ctrl+C）。</item>
    ///   <item><c>none</c>（或其它值）：不发布。</item>
    /// </list>
    /// </summary>
    private static void Deploy(BuildConfig config, string root, string outputDirectory, List<string> warnings)
    {
        string mode = config.Deploy;
        if (mode is "www" or "copy")
        {
            string wwwDir = Path.Combine(root, config.WwwDir);
            CopyDirectory(outputDirectory, wwwDir);
            PrintTool.Log($"[kfc] 已发布到 {wwwDir}（deploy = {mode}）");
        }
        else if (mode == "serve")
        {
            ServeContent(outputDirectory, config.Port); // 阻塞直到 Ctrl+C
        }
        else if (mode != "none")
        {
            warnings.Add($"未知的 deploy 模式：{mode}（可选 www / serve / none）");
        }
    }

    /// <summary>把 source 目录整体镜像复制到 dest（先清空 dest 再复制，保证不含残留旧文件）。</summary>
    private static void CopyDirectory(string source, string dest)
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
        Directory.CreateDirectory(dest);

        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (string dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    /// <summary>
    /// 在指定目录上启动一个极简的本地静态 HTTP 服务（用于开发调试）。
    /// 使用原始 Tcp 监听以避开 Windows 下 http.sys 的 URL ACL 限制；Ctrl+C 退出。
    /// </summary>
    private static void ServeContent(string directory, int port)
    {
        string root = Path.GetFullPath(directory);
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        PrintTool.Log($"[kfc] 本地 HTTP 服务已启动：http://localhost:{port}/ （Ctrl+C 退出）");

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            try { listener.Stop(); } catch { /* ignore */ }
        };

        while (true)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (SocketException)
            {
                break; // 已被 Stop()
            }

            _ = Task.Run(() => HandleHttpRequest(client, root));
        }
    }

    private static async Task HandleHttpRequest(TcpClient client, string root)
    {
        try
        {
            using var _ = client;
            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024, leaveOpen: true);

            string? requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine)) return;

            string path = requestLine.Split(' ')[1];
            path = Uri.UnescapeDataString(path.Split('?')[0]);
            if (path.EndsWith('/')) path += "index.html";

            string filePath = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
            if (!filePath.StartsWith(root, StringComparison.Ordinal))
            {
                await WriteStatusAsync(stream, 403, "Forbidden");
                return;
            }

            if (File.Exists(filePath))
            {
                byte[] body = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                await WriteResponseAsync(stream, 200, MimeOfExtension(filePath), body).ConfigureAwait(false);
            }
            else
            {
                await WriteStatusAsync(stream, 404, "Not Found");
            }
        }
        catch
        {
            // 单个请求出错不影响服务继续运行
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int code, string mime, byte[] body)
    {
        var header = new StringBuilder();
        header.AppendLine($"HTTP/1.1 {code} {(code == 200 ? "OK" : code == 404 ? "Not Found" : "Forbidden")}");
        header.AppendLine("Content-Type: " + mime);
        header.AppendLine("Content-Length: " + body.Length);
        header.AppendLine("Access-Control-Allow-Origin: *");
        header.AppendLine("Connection: close");
        header.AppendLine();
        byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        await stream.WriteAsync(headerBytes, default).ConfigureAwait(false);
        await stream.WriteAsync(body, default).ConfigureAwait(false);
    }

    private static async Task WriteStatusAsync(NetworkStream stream, int code, string text)
    {
        byte[] body = Encoding.ASCII.GetBytes(text);
        await WriteResponseAsync(stream, code, "text/plain", body).ConfigureAwait(false);
    }

    private static string MimeOfExtension(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".js" => "text/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".web.lib" => "application/octet-stream",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".wasm" => "application/wasm",
            ".txt" => "text/plain",
            ".map" => "application/json",
            _ => "application/octet-stream",
        };
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
