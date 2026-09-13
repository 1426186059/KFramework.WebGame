using Mir.Lib;
using SkiaSharp;
using WebLib;

namespace MirWebPacker.Services;

/// <summary>
/// 资源目录扫描 + 图片转码（工具侧、依赖 Skia / MirLib）。
/// 负责把磁盘上的资源目录变成一组 <see cref="AssetBundleAsset"/>（已是最终字节，png 已转 webp）。
/// 真正的“打包成 .web.lib（ZIP + manifest）”由共享库 WebLib 的 <see cref="BuildPipeline"/> 完成——本类不碰 zip 与哈希。
/// </summary>
public sealed class AssetScanner
{
    private readonly Action<string> _log;

    public AssetScanner(Action<string>? log = null)
    {
        _log = log ?? Console.WriteLine;
    }

    // 扩展名 -> (分类目录, MIME)。png 会被转码为 webp。
    private static readonly Dictionary<string, (string Category, string Mime)> s_extMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"]  = ("data", "text/plain"),
        [".json"] = ("data", "application/json"),
        [".webp"] = ("textures", "image/webp"),
        [".png"]  = ("textures", "image/webp"),
        [".wav"]  = ("audio", "audio/wav"),
        [".mp3"]  = ("audio", "audio/mpeg"),
    };

    // 已知分类目录：若输入文件已位于这些目录下，则不再叠加前缀，避免 audio/audio/... 这类重复。
    private static readonly HashSet<string> s_knownCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "textures", "audio",
    };

    /// <summary>
    /// 扫描目录，返回所有可打包资源（已按相对路径排序）。
    /// png 转码为无损 webp（复用 MirLib 已验证的编码路径）；其余文件原样读取。
    /// </summary>
    public List<AssetBundleAsset> Scan(string inputDir, bool lossless = true, int quality = 90)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"输入目录不存在: {inputDir}");

        var list = new List<AssetBundleAsset>();
        foreach (var file in Directory.EnumerateFiles(inputDir, "*", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(file);
            if (!s_extMap.TryGetValue(ext, out var info))
            {
                _log($"[跳过] 不支持的类型: {Path.GetRelativePath(inputDir, file)}");
                continue;
            }

            var rel = Path.GetRelativePath(inputDir, file).Replace('\\', '/');
            var firstSeg = rel.IndexOf('/') >= 0 ? rel[..rel.IndexOf('/')] : string.Empty;
            bool alreadyCategorized = s_knownCategories.Contains(firstSeg);

            byte[] bytes;
            string entryPath;

            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                // png -> 无损 webp 转码
                using var bmp = SKBitmap.Decode(file)
                    ?? throw new InvalidDataException($"无法解码 PNG: {file}");
                bytes = SkiaBitmaps.EncodeWebPBytes(bmp, quality, lossless);
                var relWebp = Path.ChangeExtension(rel, ".webp");
                entryPath = alreadyCategorized ? relWebp : info.Category + "/" + relWebp;
            }
            else
            {
                bytes = File.ReadAllBytes(file);
                entryPath = alreadyCategorized ? rel : info.Category + "/" + rel;
            }

            list.Add(new AssetBundleAsset(entryPath, info.Mime, bytes));
        }

        return list;
    }
}
