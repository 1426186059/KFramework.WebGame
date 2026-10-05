using KFramework.MonoGame;
using System.Diagnostics;
using System.Text;

namespace KFramework.Content.Cli
{
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

    public sealed class ContentBuilder
    {
        public BuildReport Build(string rawDirectory, BuildOptions? options = null)
        {
            options ??= new BuildOptions();
            var watch = Stopwatch.StartNew();
            var warnings = new List<string>();

            if (!Directory.Exists(rawDirectory))
            {
                throw new DirectoryNotFoundException($"原始资源目录不存在：{rawDirectory}");
            }

            List<string> bundleDirs = BuildConfigResult.BundleDirsFull;
            string root = Path.GetDirectoryName(Path.GetFullPath(rawDirectory)) ?? rawDirectory;
            string outputDirectory = BuildConfigResult.OutDirFull;

            // 打包中间产物目录（atlas 预览 PNG 等）：与 raw 同级，由配置 tempDir 指定（默认 temp），不随 outDir 发布
            string tempDirectory = BuildConfigResult.TempDirFull;
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
                    string dirRoot = dir;
                    if (Directory.Exists(dirRoot))
                        bundleRoots.Add(dirRoot);
                    else
                        warnings.Add($"打包目录未找到，已忽略：{dir}");
                }

                BundleSplitMode mode = BuildConfigResult.SplitMode;
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
                            BuildConfigResult.AutoAtlas, warnings,
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

            Deployer.Deploy(BuildConfigResult.copy_to_wwwroot, root, outputDirectory, warnings);

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
