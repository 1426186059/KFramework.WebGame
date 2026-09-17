using KFramework.MonoGame;
using System.Diagnostics;
using System.Text;

namespace KFramework.Content.Build
{
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
    ///   <item>打包根目录自身若直接含资源，也会打成一个以根目录（相对 raw 的路径，如 MyRes）命名的 AssetBundle；其下「每一个含资源的子文件夹」再各自打成一个 AssetBundle（包名 = 子文件夹相对 raw 根目录的全路径，如 MyRes/atlas）。根目录包只含自身直接资源，不含子目录资源。</item>
    ///   <item>多个根目录下的子文件夹包名必须唯一，出现同名会直接报错（请保证各根目录内子文件夹名不重复）。</item>
    ///   <item>每个文件夹只打包其「直接」资源，不含子目录资源（子目录自身也是独立的 AssetBundle）。</item>
    ///   <item>是否自动图集打包由 <c>autoAtlas</c> 控制（默认 true）：true 时独立 <c>.png</c> 经图集打包器装箱成图集；
    ///        false 时 <c>.png</c> 原样整图入包（适合已用 <c>.atlas</c> 预切好的图集，运行端按整张页图切片）。</item>
    ///   <item>资源分包方式由 <c>splitMode</c> 控制（默认 folder）：<c>folder</c> = 按文件夹拆分（顶级根目录自身及其每个含资源的子文件夹各自成包）；
    ///        <c>whole</c> = 整包不拆分（根目录含所有子目录整体打成一个包）。</item>
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

            /// <summary>是否额外输出 atlas_N.png 预览图，方便用看图工具检查发布结果；预览图写到配置 tempDir 指定的临时目录（默认 Content/temp），不随 outDir 发布。</summary>
            public bool WritePreviewPng { get; set; } = true;

            public string PakFileName { get; set; } = "content.pak";

            /// <summary>是否裁掉精灵四周的透明边。</summary>
            public bool TrimSprites { get; set; } = true;

            /// <summary>图集页（整图纹理）的最终编码格式（见 <see cref="AssetTextureFormat"/>）：
            /// <c>Rgba</c>（默认，裸 RGBA8，运行端零解码、直接上传 GPU）/ <c>Png</c>（编码 PNG，体积更小，运行端在 LoadBundle 阶段解码）/
            /// <c>Webp</c>（编码 WebP，体积更小，运行端借浏览器原生解码）/ <c>Ktx2</c>（KTX2/Basis 超压缩 GPU 纹理，显存与上传开销最低，构建端需 basisu，运行端需浏览器 Basis 转码器）。
            /// 可在 build.config.json 的 <c>textureFormat</c> 配置，或用 kfc --format 覆盖。</summary>
            public AssetTextureFormat TextureFormat { get; set; } = AssetTextureFormat.Rgba;

            /// <summary>basisu 可执行文件路径（<c>Ktx2</c> 编码用）。为空则用 PATH 中的 "basisu"。</summary>
            public string? BasisuPath { get; set; }

            /// <summary>KTX2（Basis UASTC）质量等级 0~4，越大越好越慢。仅 <c>TextureFormat=Ktx2</c> 时生效。</summary>
            public int Ktx2Quality { get; set; } = 2;
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

            // 打包中间产物目录（atlas 预览 PNG 等）：与 raw 同级，由配置 tempDir 指定（默认 temp），不随 outDir 发布
            string tempDirectory = Global.mBuildConfig.TempDir;
            if (!Path.IsPathFullyQualified(tempDirectory))
            {
                tempDirectory = Path.Combine(root, Global.mBuildConfig.TempDir);
            }
            Directory.CreateDirectory(tempDirectory);

            // 目录可能不存在（首次构建 / 清理后），删之前先判存在，否则 Directory.Delete 会抛 DirectoryNotFoundException
            if (Directory.Exists(outputDirectory))
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
                // 收集真实存在的打包根目录（供后续原样复制时跳过已打包文件）
                foreach (string dir in bundleDirs)
                {
                    string dirRoot = Path.Combine(rawDirectory, dir);
                    if (Directory.Exists(dirRoot))
                        bundleRoots.Add(dirRoot);
                    else
                        warnings.Add($"打包目录未找到，已忽略：{dir}");
                }

                BundleSplitMode mode = Global.mBuildConfig.SplitMode;
                PrintTool.Log($"[kfc] 分包模式：{mode}（打包目录 = {string.Join(", ", bundleDirs)}）");

                // 各根目录产出的包名必须唯一（保证运行端 GetBundle(name) 无歧义）
                var usedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (string bundlesRoot in bundleRoots)
                {
                    foreach (var (bundleName, files) in BundleSplitter.Enumerate(bundlesRoot, rawDirectory, mode))
                    {
                        if (!usedNames.Add(bundleName))
                            throw new InvalidOperationException(
                                $"发现重复的 AssetBundle 名「{bundleName}」：配置的打包目录（{string.Join(", ", bundleDirs)}）下存在同名子文件夹，请保证各打包目录内的子文件夹名唯一。");

                        AssetBundleBuild build = BundleBaker.BuildBundle(
                            bundleName, files, rawDirectory, options,
                            Global.mBuildConfig.AutoAtlas, warnings,
                            ref rawBytes, ref textureCount, ref dataCount, ref atlasPageCount, tempDirectory);
                        builds.Add(build);
                        bundleCount++;
                    }
                }

                if (builds.Count == 0)
                    warnings.Add($"打包目录（{string.Join(", ", bundleDirs)}）下没有发现任何「含资源的文件夹（含根目录自身）」，未产出任何 AssetBundle。");
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
            Deployer.Deploy(Global.mBuildConfig, root, outputDirectory, warnings);

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
                if (BundleBaker.IsIgnored(relative)) continue;

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

    }
}
