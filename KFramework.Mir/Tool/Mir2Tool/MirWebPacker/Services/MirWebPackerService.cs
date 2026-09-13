using System.Text;
using System.Text.Json;
using MirWebPacker.Models;
using WebLib;

namespace MirWebPacker.Services;

/// <summary>
/// Web 版打包服务：
/// 1) 目录浏览（与 MapExtract 同源），用于在网页上选择资源根目录；
/// 2) 打包：把 root 下每个子目录分别打成 .web.lib（多级目录分别打包），每个 zip 文件名带上内容短哈希，
///    并生成一份总的 version.manifest 供游戏热更（通过 WebLib 的 BuildPipeline，对齐 Unity BuildPipeline.BuildAssetBundles）。
///
/// 核心打包/解包/总清单逻辑全部在共享库 WebLib 中（引擎侧同样使用，对应 Unity AssetBundle 体系），
/// 本服务只负责目录扫描（依赖 Skia 转码）+ 落地文件 + 提供 Web 接口。
/// </summary>
public sealed class MirWebPackerService
{
    private readonly ILogger<MirWebPackerService> _logger;
    private readonly AssetScanner _scanner;
    private readonly object _work = new();
    private readonly string _outputRootFile;
    private string _outputRoot = "";

    public MirWebPackerService(ILogger<MirWebPackerService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _scanner = new AssetScanner(msg => _logger.LogInformation("{Msg}", msg));
        _outputRootFile = Path.Combine(env.ContentRootPath, "mirwebpack.output.json");
        _outputRoot = LoadOutputRoot();
    }

    /// <summary>最近一次打包的产物所在目录（下载接口据此定位文件，并持久化到磁盘以跨重启）。</summary>
    public string OutputRoot
    {
        get { lock (_work) return _outputRoot; }
        private set
        {
            lock (_work)
            {
                _outputRoot = value;
                try { File.WriteAllText(_outputRootFile, value, new UTF8Encoding(false)); } catch { }
            }
        }
    }

    private string LoadOutputRoot()
    {
        try
        {
            if (File.Exists(_outputRootFile))
                return File.ReadAllText(_outputRootFile, Encoding.UTF8).Trim();
        }
        catch { }
        return "";
    }

    // ==================== 目录浏览 ====================

    public FsListDto ListDir(string? path, bool includeFiles = false, string? fileFilter = null)
    {
        var dto = new FsListDto();
        dto.Drives = GetDrives();

        if (string.IsNullOrWhiteSpace(path))
        {
            dto.Path = "";
            return dto;
        }

        try
        {
            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
            {
                dto.Path = path;
                return dto;
            }

            dto.Path = full;
            dto.Parent = Directory.GetParent(full)?.FullName ?? "";

            foreach (var dir in Directory.GetDirectories(full).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                dto.Directories.Add(new FsEntry
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir
                });
            }

            if (includeFiles)
            {
                string filter = string.IsNullOrWhiteSpace(fileFilter) ? "*" : fileFilter;
                foreach (var file in Directory.GetFiles(full, filter).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    dto.Files.Add(new FsEntry
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "列举目录失败: {Path}", path);
            dto.Path = path ?? "";
        }

        return dto;
    }

    private static List<FsEntry> GetDrives()
    {
        var list = new List<FsEntry>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    list.Add(new FsEntry { Name = d.Name, FullPath = d.RootDirectory.FullName });
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    /// <summary>把下载请求里的文件名映射到产物目录下的真实文件（仅允许 basename，杜绝路径穿越）。</summary>
    public string? ResolveFile(string name)
    {
        var root = OutputRoot;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(name))
            return null;
        var safe = Path.GetFileName(name); // 去掉任何目录成分
        if (safe != name) return null;
        var full = Path.Combine(root, safe);
        return File.Exists(full) ? full : null;
    }

    // ==================== 打包 ====================

    public PackRootResult PackRoot(PackRootRequest req)
    {
        string root = (req.Root ?? "").Trim().TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return Error($"资源根目录不存在: {root}");

        string outputDir = string.IsNullOrWhiteSpace(req.OutputDir)
            ? Path.Combine(root, "_packages")
            : req.OutputDir.Trim().TrimEnd('\\', '/');
        Directory.CreateDirectory(outputDir);
        OutputRoot = outputDir;

        string kind = req.Kind ?? "map";

        var subDirs = Directory.GetDirectories(root)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Where(d => !string.Equals(
                Path.GetFullPath(d).TrimEnd('\\', '/'),
                Path.GetFullPath(outputDir).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 多级目录：每个子目录分别打包；扁平目录：把 root 本身打成一个包
        bool multiLevel = subDirs.Count > 0;
        var targets = multiLevel ? subDirs : new List<string> { root };

        // 1) 扫描每个目录 + png->webp 转码，构建 AssetBundleBuild 列表
        var builds = new List<AssetBundleBuild>();
        var logLines = new List<string>();
        foreach (var dir in targets)
        {
            string name = multiLevel
                ? Path.GetFileName(dir.TrimEnd('\\', '/'))
                : Path.GetFileName(root);

            try
            {
                var assets = _scanner.Scan(dir, req.Lossless, req.Quality);
                builds.Add(new AssetBundleBuild { AssetBundleName = name, Kind = kind, Assets = assets });
                logLines.Add($"[扫描] {name}: {assets.Count} 个资源");
            }
            catch (Exception ex)
            {
                logLines.Add($"[ERROR] {name}: {ex.Message}");
                _logger.LogWarning(ex, "扫描失败: {Dir}", dir);
            }
        }

        if (builds.Count == 0)
            return Error("没有可打包的目录（根目录下没有子目录，且根目录本身也无资源）。\n" + string.Join("\n", logLines));

        // 2) 交给 WebLib.BuildPipeline 一次性构建所有包（ZIP + 总清单），对齐 Unity BuildPipeline.BuildAssetBundles
        var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.Deterministic;
        var result = BuildPipeline.BuildAssetBundles(builds, options, BuildTarget.WebGL, kind);

        // 3) 落地：每个包写为文件名含短哈希的 .web.lib
        long totalBytes = 0;
        var packages = new List<PackageInfo>();
        foreach (var b in builds)
        {
            var pkg = result.Manifest.Packages.First(p => p.Name == b.AssetBundleName);
            var bytes = result.Bundles[b.AssetBundleName];

            string finalPath = Path.Combine(outputDir, pkg.File);
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.WriteAllBytes(finalPath, bytes);

            long size = new FileInfo(finalPath).Length;
            totalBytes += size;
            packages.Add(new PackageInfo(
                b.AssetBundleName,
                pkg.File,
                "/api/file/" + Uri.EscapeDataString(pkg.File),
                size,
                pkg.Hash,
                pkg.Entries,
                null));
            logLines.Add($"✓ {b.AssetBundleName} → {pkg.File}  ({FmtBytes(size)}, {pkg.Entries} 个资源)");
        }
        packages = packages.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // 4) 写总清单 version.manifest（不含 Root，兼顾分散打包）
        string manifestPath = Path.Combine(outputDir, "version.manifest");
        string manifestJson = result.Manifest.Serialize();
        File.WriteAllText(manifestPath, manifestJson, new UTF8Encoding(false));

        var msg = $"打包完成：{packages.Count} 个资源包，共 {FmtBytes(totalBytes)}\n" +
                  $"哈希算法：{result.Manifest.Hash}\n" +
                  $"输出目录: {outputDir}\nversion.manifest 已生成（{packages.Count} 个包）\n\n" +
                  string.Join("\n", logLines);
        return new PackRootResult(
            true, msg, root, outputDir, packages.Count, totalBytes, packages,
            "version.manifest", manifestJson);
    }

    // ==================== 工具 ====================

    public static string FmtBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return (bytes / 1073741824.0).ToString("F2") + " GB";
        if (bytes >= 1024L * 1024) return (bytes / 1048576.0).ToString("F1") + " MB";
        if (bytes >= 1024) return (bytes / 1024.0).ToString("F0") + " KB";
        return bytes + " B";
    }

    private static PackRootResult Error(string message) => new(
        false, message, "", "", 0, 0, Array.Empty<PackageInfo>(), "", "");
}
