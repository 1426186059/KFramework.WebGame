using KFramework.MonoGame;
using System.IO.Compression;
using System.Text.Json;

namespace KFramework.Content.Cli
{
    public sealed class AssetBundleBuild
    {
        public string AssetBundleName { get; set; } = "";
        public string FullDir { get; set; } = "";
        public List<AssetBundleAsset> Assets { get; set; } = new();
    }

    public sealed class AssetBundleAsset
    {
        public string Path { get; set; } = "";

        /// <summary>资源类型；默认 <see cref="ContentAssetType.Binary"/>（未识别的一律按二进制资源处理）。</summary>
        public ContentAssetType Type { get; set; } = ContentAssetType.Binary;
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public int Page { get; set; } = -1;
        public int X { get; set; }
        public int Y { get; set; }

        public ContentTextureDataFormat Format { get; set; } = ContentTextureDataFormat.Rgba;
    }

    public static class BundleBuilder
    {
        private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };
        private const string BundleFormat = "web.lib";
        private const string SetFormat = "web.lib.bundle";
        private const int Version = 1;

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
                // 文件名扁平化：把逻辑名的路径分隔符 '/' 换成 '_'，避免产生嵌套子目录（仍含短哈希）
                string flatName = b.AssetBundleName.Replace('/', '_');
                string file = $"{Sanitize(flatName)}.{shortH}.web.lib";

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
                a.Width, a.Height, a.Page, a.X, a.Y, a.Format)).ToList();

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
        private static void PinTime(ZipArchiveEntry e)
        {
            e.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

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

}
