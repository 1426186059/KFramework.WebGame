using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
///   <item>在 raw 目录下放置一个打包配置文件（<c>bundles.json</c> 或 <c>pack.json</c>）指定打包根目录：
///         字段 <c>bundlesDir</c>（或 <c>bundleDirs</c>）可填字符串，也可填字符串数组，例如
///         <c>{ "bundlesDir": ["Bundles", "UI"] }</c>；未配置或字段缺失时缺省为 <c>Bundles</c>。</item>
///   <item>每个打包根目录下的「每一个含资源的子文件夹」分别打包成一个 AssetBundle（包名 = 子文件夹相对该根目录的路径）。</item>
///   <item>多个根目录下的子文件夹包名必须唯一，出现同名会直接报错（请保证各根目录内子文件夹名不重复）。</item>
///   <item>每个文件夹只打包其「直接」资源，不含子目录资源（子目录自身也是独立的 AssetBundle）。</item>
///   <item>除「指定打包目录」外，其余 raw 文件（如静态资源、配置文件等）原封不动地复制到 <c>content/</c>，不做打包/压缩。</item>
///   <item>若配置的根目录都不存在，则回退为「整包 raw 作为一个 content 包」的兼容模式。</item>
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

    public BuildReport Build(string rawDirectory, string? outputOverride = null, BuildOptions? options = null)
    {
        options ??= new BuildOptions();
        var watch = Stopwatch.StartNew();
        var warnings = new List<string>();

        if (!Directory.Exists(rawDirectory))
            throw new DirectoryNotFoundException($"原始资源目录不存在：{rawDirectory}");

        BuildConfig config = ReadConfig(rawDirectory);
        List<string> bundleDirs = config.BundleDirs;
        if (bundleDirs.Count == 0) bundleDirs = new List<string> { "Bundles" };

        // 输出目录：CLI --out 优先，否则取配置 outDir（默认 content，相对 root）
        string root = Path.GetDirectoryName(Path.GetFullPath(rawDirectory)) ?? rawDirectory;
        string outputDirectory = outputOverride ?? Path.Combine(root, config.OutDir);
        Directory.CreateDirectory(outputDirectory);
        CleanOutput(outputDirectory);

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
            string dirRoot = Path.Combine(rawDirectory, dir);
            if (Directory.Exists(dirRoot)) bundleRoots.Add(dirRoot);
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

                    AssetBundleBuild build = BuildBundle(bundleName, directFiles, bundlesRoot, options, warnings,
                        ref rawBytes, ref textureCount, ref dataCount, ref atlasPageCount, outputDirectory);
                    builds.Add(build);
                    bundleCount++;
                }
            }

            if (builds.Count == 0)
                warnings.Add($"打包目录（{string.Join(", ", bundleDirs)}）下没有发现任何「含资源的子文件夹」，未产出任何 AssetBundle。");

            // 除指定打包目录外，其余 raw 文件原封不动地复制到 content/（不做打包/压缩，保持原样）
            int copied = CopyRawAssetsVerbatim(rawDirectory, outputDirectory, bundleRoots);
            if (copied > 0) Console.WriteLine($"[kfc] 其余 {copied} 个文件已原样复制到 content/（未打包）");
        }

        // 构建所有 AssetBundle（每个包独立 .web.lib，并汇总总清单 version.manifest）
        BuildResult result = BundleBuilder.BuildAssetBundles(builds);
        foreach (var pkg in result.Manifest.Packages)
        {
            File.WriteAllBytes(Path.Combine(outputDirectory, pkg.File), result.Bundles[pkg.Name]);
            Console.WriteLine($"[kfc] 资源包 {pkg.Name} -> {pkg.File}（{pkg.Size} 字节，哈希 {pkg.Hash}）");
        }
        File.WriteAllText(Path.Combine(outputDirectory, "version.manifest"), result.Manifest.Serialize());

        // 发布阶段（部署）：复制到其他目录 / 本地 HTTP 服务 / 不发布
        Deploy(config, root, outputDirectory, warnings);

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

    /// <summary>打包配置（raw/bundles.json 或 raw/pack.json）。</summary>
    private sealed class BuildConfig
    {
        /// <summary>打包根目录（相对 raw），字符串或数组；缺省 Bundles。</summary>
        public List<string> BundleDirs { get; set; } = new();

        /// <summary>打包产物目录（相对 root），缺省 content。</summary>
        public string OutDir { get; set; } = "content";

        /// <summary>发布方式：www(复制到 wwwDir) / serve(本地 HTTP) / none；缺省 www。</summary>
        public string Deploy { get; set; } = "www";

        /// <summary>deploy=www 时的复制目标（相对 root），缺省 www。</summary>
        public string WwwDir { get; set; } = "www";

        /// <summary>deploy=serve 时的端口，缺省 8080。</summary>
        public int Port { get; set; } = 8080;
    }

    /// <summary>
    /// 读取打包配置；支持 <c>bundles.json</c> / <c>pack.json</c>。
    /// 字段：<c>bundlesDir</c> / <c>bundleDirs</c>（字符串或数组，默认 <c>Bundles</c>）、
    /// <c>outDir</c>（默认 content）、<c>deploy</c>（www/serve/none，默认 www）、
    /// <c>wwwDir</c>（默认 www）、<c>port</c>（默认 8080）。
    /// 配置文件均不存在时自动生成一个默认 <c>bundles.json</c>。
    /// </summary>
    private static BuildConfig ReadConfig(string rawDirectory)
    {
        foreach (string cfg in new[] { "bundles.json", "pack.json" })
        {
            string path = Path.Combine(rawDirectory, cfg);
            if (!File.Exists(path)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                var config = new BuildConfig();

                var dirs = new List<string>();
                if (root.TryGetProperty("bundlesDir", out JsonElement a)) dirs.AddRange(ResolveBundleDirs(a));
                if (root.TryGetProperty("bundleDirs", out JsonElement b)) dirs.AddRange(ResolveBundleDirs(b));
                config.BundleDirs = dirs;

                if (root.TryGetProperty("outDir", out JsonElement o) && o.ValueKind == JsonValueKind.String)
                    config.OutDir = o.GetString()!.Replace('\\', '/').Trim('/');
                if (root.TryGetProperty("deploy", out JsonElement d) && d.ValueKind == JsonValueKind.String)
                    config.Deploy = d.GetString()!.ToLowerInvariant();
                if (root.TryGetProperty("wwwDir", out JsonElement w) && w.ValueKind == JsonValueKind.String)
                    config.WwwDir = w.GetString()!.Replace('\\', '/').Trim('/');
                if (root.TryGetProperty("port", out JsonElement p) && p.ValueKind == JsonValueKind.Number)
                    config.Port = p.GetInt32();

                return config;
            }
            catch
            {
                // 配置损坏则忽略，使用默认配置
            }
        }

        // 未找到打包配置：自动生成一个默认 bundles.json（含全部默认项），方便后续按目录分别打包
        string defaultPath = Path.Combine(rawDirectory, "bundles.json");
        try
        {
            File.WriteAllText(defaultPath,
                "{\"bundlesDir\":\"Bundles\",\"outDir\":\"content\",\"deploy\":\"www\",\"wwwDir\":\"www\",\"port\":8080}",
                new UTF8Encoding(false));
            Console.WriteLine($"[kfc] 未发现打包配置，已自动生成 {Path.GetFileName(defaultPath)}（默认：打包目录 Bundles，输出 content，发布方式 www）");
        }
        catch
        {
            // 无法写入也不影响本次打包（回退整包 content）
        }
        return new BuildConfig();
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

    /// <summary>
    /// 在「按目录打包」模式下，把不在任何打包根目录内的 raw 文件原样复制到 content/（不做打包/压缩，保持原样）。
    /// 属于打包根目录的文件已被打成 AssetBundle，跳过；打包配置文件（bundles.json / pack.json）也跳过。
    /// </summary>
    private static int CopyRawAssetsVerbatim(string rawDirectory, string outputDirectory, List<string> bundleRoots)
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
            Console.WriteLine($"[kfc] 已发布到 {wwwDir}（deploy = {mode}）");
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
        Console.WriteLine($"[kfc] 本地 HTTP 服务已启动：http://localhost:{port}/ （Ctrl+C 退出）");

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
