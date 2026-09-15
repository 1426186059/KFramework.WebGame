using System.IO.Compression;
using System.Text.Json;

namespace KFramework.MonoGame;

/// <summary>
/// 描述一个待构建的资源包（对齐 Unity <c>AssetBundleBuild</c>）。
/// 在 <see cref="BundleBuilder.BuildAssetBundles"/> 中作为构建单元。
/// </summary>
public sealed class AssetBundleBuild
{
    /// <summary>逻辑包名（对应 Unity 的 assetBundleName，如 myres/atlas/characters）。</summary>
    public string AssetBundleName { get; set; } = "";

    /// <summary>包内资源。</summary>
    public List<AssetBundleAsset> Assets { get; set; } = new();
}

/// <summary>包内一条待写入的资源。</summary>
public sealed class AssetBundleAsset
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    /// <summary>原始像素宽（仅 atlas / 纹理类资源有意义，用于运行时直接上传 GPU）。</summary>
    public int Width { get; set; }
    /// <summary>原始像素高。</summary>
    public int Height { get; set; }
    /// <summary>所属图集页索引（≥0 表示子图；-1 表示独立资源）。</summary>
    public int Page { get; set; } = -1;
    /// <summary>子图在图集页中的 X 偏移。</summary>
    public int X { get; set; }
    /// <summary>子图在图集页中的 Y 偏移。</summary>
    public int Y { get; set; }
}

/// <summary>
/// 资源打包管线（对齐 Unity <c>BuildPipeline.BuildAssetBundles</c>）。
/// 把所有 <see cref="AssetBundleBuild"/> 构建成一组 .web.lib，并汇总出总清单 <see cref="AssetBundleManifest"/>。
/// 纯流式、无文件系统依赖——产物是字节，由调用方自行落地（工具写盘 / 引擎存 IndexedDB）。
///
/// 每个 .web.lib 都带上完整内容哈希（MD5），包文件名含短哈希，
/// 因此运行端能按哈希做精确的增量热更。
/// </summary>
public static class BundleBuilder
{
    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };
    private const string BundleFormat = "web.lib";
    private const string SetFormat = "web.lib.bundle";
    private const int Version = 1;

    /// <summary>
    /// 构建资源包（对齐 Unity BuildPipeline.BuildAssetBundles）。
    /// 返回总清单与“逻辑名 → 包字节”的字典，调用方按需持久化。
    /// </summary>
    public static BuildResult BuildAssetBundles(IReadOnlyList<AssetBundleBuild> builds)
    {
        var bundles = new Dictionary<string, byte[]>();
        var packages = new List<BundlePackage>();

        // 排序保证总清单顺序稳定
        foreach (var b in builds.OrderBy(x => x.AssetBundleName, StringComparer.OrdinalIgnoreCase))
        {
            byte[] bytes = WriteBundle(b);
            string full = BundleHash.Hex(bytes);
            string shortH = BundleHash.Shorten(full);
            string file = $"{Sanitize(b.AssetBundleName)}.{shortH}.web.lib";

            bundles[b.AssetBundleName] = bytes;
            packages.Add(new BundlePackage(b.AssetBundleName, file, bytes.LongLength, full, b.Assets.Count, Array.Empty<string>()));
        }

        var manifest = new AssetBundleManifest(SetFormat, Version, BundleHash.Algorithm, DateTime.UtcNow.ToString("O"), packages);
        return new BuildResult(manifest, bundles);
    }

    // 构建单个 .web.lib（ZIP：manifest.json + 各资源），返回字节
    private static byte[] WriteBundle(AssetBundleBuild build)
    {
        // 排序保证相同内容逐字节一致
        var sorted = build.Assets.OrderBy(a => a.Path, StringComparer.OrdinalIgnoreCase).ToList();

        var entries = sorted.Select(a => new AssetBundleEntry(
            a.Path, a.Type, a.Bytes.LongLength, Crc32Hex(a.Bytes), BundleHash.Hex(a.Bytes),
            a.Width, a.Height, a.Page, a.X, a.Y)).ToList();

        var content = new AssetBundleContent(BundleFormat, Version, build.AssetBundleName, entries);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var m = zip.CreateEntry("manifest.json");
            PinTime(m);
            using (var s = m.Open())
                JsonSerializer.Serialize(s, content, s_opts);

            foreach (var a in sorted)
            {
                var e = zip.CreateEntry(a.Path, CompressionLevel.Optimal);
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
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == '/' ? c : '_');
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

/// <summary><see cref="BundleBuilder.BuildAssetBundles"/> 的产物：总清单 + 各包字节。</summary>
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
