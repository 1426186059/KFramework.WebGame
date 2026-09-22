using System.Text.Json;

namespace KFramework.MonoGame
{
    public sealed class AssetBundleManifest
    {
        /// <summary>清单格式标识，固定 "web.lib.bundle"</summary>
        public string Format { get; }

        /// <summary>清单结构版本</summary>
        public int Version { get; }

        /// <summary>内容哈希算法名（小写，无前缀），如 md5</summary>
        public string Hash { get; }

        /// <summary>生成时间（ISO8601 UTC）</summary>
        public string CreatedAt { get; }

        /// <summary>全部包条目</summary>
        public IReadOnlyList<BundlePackage> Packages { get; }

        public AssetBundleManifest(
            string format,
            int version,
            string hash,
            string createdAt,
            IReadOnlyList<BundlePackage> packages)
        {
            Format = format;
            Version = version;
            Hash = hash;
            CreatedAt = createdAt;
            Packages = packages;
        }

        public string Serialize() => JsonSerializer.Serialize(this, AppJsonContext.Default.AssetBundleManifest);

        public static AssetBundleManifest Parse(Stream stream)
            => JsonSerializer.Deserialize(stream, AppJsonContext.Default.AssetBundleManifest)
               ?? throw new InvalidDataException("version.manifest 解析失败");

        public static AssetBundleManifest Parse(string json)
            => JsonSerializer.Deserialize(json, AppJsonContext.Default.AssetBundleManifest)
               ?? throw new InvalidDataException("version.manifest 解析失败");

        public BundlePackage? FindPackage(string name, bool strict = true)
        {
            if (strict)
            {
                foreach (var p in this.Packages)
                {
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
                }
            }
            else
            {
                foreach (var p in this.Packages)
                {
                    if (p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) return p;
                }
            }
            return null;
        }


        public string[] GetAllAssetBundles()
            => Packages.Select(p => p.Name).ToArray();

        public string? GetAssetBundleHash(string bundleName)
            => FindPackage(bundleName)?.Hash;

        public string[] GetDirectDependencies(string bundleName)
        {
            var p = FindPackage(bundleName);
            return p?.Dependencies?.ToArray() ?? Array.Empty<string>();
        }
        
        public string[] GetAllDependencies(string bundleName)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(string name)
            {
                var p = FindPackage(name);
                if (p?.Dependencies == null)
                {
                    return;
                }

                foreach (var d in p.Dependencies)
                {
                    if (seen.Add(d)) 
                    { 
                        result.Add(d); 
                        Visit(d); 
                    }
                }
            }

            Visit(bundleName);
            return result.ToArray();
        }
    }
    
    public sealed record BundlePackage(
        string Name,
        string File,
        long Size,
        string Hash,
        int Entries,
        IReadOnlyList<string> Dependencies = null!);
}
