using System.IO.Compression;
using System.Text.Json;

namespace WebLib;

/// <summary>
/// 资源打包管线（对齐 Unity <c>BuildPipeline.BuildAssetBundles</c>）。
/// 把所有 <see cref="AssetBundleBuild"/> 构建成一组 .web.lib，并汇总出总清单 <see cref="AssetBundleManifest"/>。
/// 纯流式、无文件系统依赖——产物是字节，由调用方自行落地（工具写盘 / 引擎存 IndexedDB）。
///
/// 内容哈希统一走 <see cref="Hash"/>（当前 MD5，小写十六进制、无算法前缀，算法名由总清单 Hash 字段声明）；
/// 包内每个资源的 Crc 字段是 ZIP CRC32（用于快速完整性校验），二者相互独立。
/// </summary>
public static class BuildPipeline
{
    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };
    private const string BundleFormat = "web.lib";
    private const string SetFormat = "web.lib.bundle";
    private const int Version = 1;

    /// <summary>
    /// 构建资源包（对齐 Unity BuildPipeline.BuildAssetBundles）。
    /// 返回总清单与“逻辑名 → 包字节”的字典，调用方按需持久化。
    /// </summary>
    public static BuildResult BuildAssetBundles(
        IReadOnlyList<AssetBundleBuild> builds,
        BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.Deterministic,
        BuildTarget target = BuildTarget.WebGL,
        string kind = "map")
    {
        var bundles = new Dictionary<string, byte[]>();
        var packages = new List<BundlePackage>();

        // 排序保证总清单顺序稳定
        foreach (var b in builds.OrderBy(x => x.AssetBundleName, StringComparer.OrdinalIgnoreCase))
        {
            byte[] bytes = WriteBundle(b, options);
            string full = Hash.Hex(bytes);
            string shortH = Hash.Shorten(full);
            string file = $"{Sanitize(b.AssetBundleName)}.{shortH}.web.lib";

            bundles[b.AssetBundleName] = bytes;
            packages.Add(new BundlePackage(b.AssetBundleName, file, bytes.LongLength, full, b.Assets.Count, Array.Empty<string>()));
        }

        var manifest = new AssetBundleManifest(SetFormat, Version, kind, Hash.Algorithm, DateTime.UtcNow.ToString("O"), packages);
        return new BuildResult(manifest, bundles);
    }

    // 构建单个 .web.lib（ZIP：manifest.json + 各资源），返回字节
    private static byte[] WriteBundle(AssetBundleBuild build, BuildAssetBundleOptions options)
    {
        // 排序保证相同内容逐字节一致
        var sorted = build.Assets.OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase).ToList();

        var entries = sorted.Select(a => new AssetBundleEntry(
            a.Path, a.Mime, a.Bytes.LongLength, Crc32Hex(a.Bytes), Hash.Hex(a.Bytes))).ToList();

        var content = new AssetBundleContent(BundleFormat, Version, build.Kind, build.AssetBundleName, entries);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var m = zip.CreateEntry("manifest.json");
            PinTime(m);
            using (var s = m.Open())
                JsonSerializer.Serialize(s, content, s_opts);

            var level = options.HasFlag(BuildAssetBundleOptions.Uncompressed)
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;

            foreach (var a in sorted)
            {
                var e = zip.CreateEntry(a.Path, level);
                PinTime(e);
                using var s = e.Open();
                s.Write(a.Bytes, 0, a.Bytes.Length);
            }
        }
        return ms.ToArray();
    }

    // 固定时间戳，保证相同内容产出逐字节一致的 zip（跨进程/跨次运行哈希稳定）
    private static void PinTime(ZipArchiveEntry e) =>
        e.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
        var s = sb.ToString().Trim('.', '_');
        return string.IsNullOrEmpty(s) ? "pkg" : s;
    }

    // ZIP CRC32（标准多项式 0xEDB88320），8 位十六进制——仅用于包内资源快速完整性校验，与内容哈希无关
    private static string Crc32Hex(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return (crc ^ 0xFFFFFFFF).ToString("x8");
    }

    private static readonly uint[] s_crcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }
}

/// <summary><see cref="BuildPipeline.BuildAssetBundles"/> 的产物：总清单 + 各包字节。</summary>
public sealed class BuildResult
{
    public AssetBundleManifest Manifest { get; }
    public IReadOnlyDictionary<string, byte[]> Bundles { get; }

    public BuildResult(AssetBundleManifest manifest, IReadOnlyDictionary<string, byte[]> bundles)
    {
        Manifest = manifest;
        Bundles = bundles;
    }
}
