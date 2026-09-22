namespace KFramework.MonoGame
{
    public sealed class AssetBundleManager : IDisposable
    {
        /// <summary>资源包文件的扩展名（含点）。GC / 过滤时用它圈定"只动资源包"。</summary>
        public const string BundleFileExtension = ".web.lib";
        /// <summary>本地清单在 IndexedDB 里用的键（字符串 KV；清单是 JSON 文本，存字符串最省事、可被可靠持久化）。</summary>
        private const string LocalManifestKey = "KFramework.AssetBundleManifest";
        private const string CacheName = "kframework-bundles";

        public readonly Caching mCacheInstance = new Caching(CacheName);

        private readonly HttpClient _http;
        private readonly string _rootDir;
        private AssetBundleManifest? _manifest;
        private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

        public AssetBundleManager(
            HttpClient http,
            string baseDir)
        {
            _http = http;
            _rootDir = baseDir.TrimEnd('/') + "/";
        }

        public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

        public async Task<bool> IsBundleCachedAsync(string file, CancellationToken cancellationToken = default)
            => await Caching.Default.GetSizeAsync(file).ConfigureAwait(false) > 0;

        private string ResolveRooted(string path)
        {
            return _rootDir + path;
        }

        public async Task<AssetBundleManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
        {
            AssetBundleManifest old_version = null;
            string old_json = await JSBind_IndexedDB.GetStringAsync(LocalManifestKey);
            if (!string.IsNullOrWhiteSpace(old_json))
            {
                old_version = AssetBundleManifest.Parse(old_json);
            }

            byte[] data = await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted("version.manifest"), false, cancellationToken).ConfigureAwait(false);
            string json = ContentFunc.DecodeUtf8(data);
            AssetBundleManifest new_version = AssetBundleManifest.Parse(json);
            await this.DeleteOldCachesAsync(old_version, new_version, cancellationToken).ConfigureAwait(false);

            await JSBind_IndexedDB.SetStringAsync(LocalManifestKey, json).ConfigureAwait(false);
            _manifest = new_version;
            return _manifest;
        }

        /// <summary>从 IndexedDB 读取上次落盘的清单；没有/损坏返回 null，用于冷启动差异比对与缓存 GC。</summary>
        public async Task<AssetBundleManifest?> TryLoadLocalManifestAsync(CancellationToken cancellationToken = default)
        {
            string json = await JSBind_IndexedDB.GetStringAsync(LocalManifestKey).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return AssetBundleManifest.Parse(json);
            }
            catch (Exception ex)
            {
                PrintTool.LogError($"[KFramework.MonoGame] 本地清单解析失败（已忽略）：{ex.Message}");
                return null;
            }
        }

        /// <summary>把清单显式落到 IndexedDB（覆盖式）。一般 <see cref="FetchManifestAsync"/> 拉完会自己存一份；此方法是给“本地构造清单”的场景用。</summary>
        public async Task SaveLocalManifestAsync(AssetBundleManifest manifest, CancellationToken cancellationToken = default)
        {
            await JSBind_IndexedDB.SetStringAsync(LocalManifestKey, manifest.Serialize()).ConfigureAwait(false);
        }

        private async Task<byte[]?> LoadBundleBytesAsync(BundlePackage package, CancellationToken cancellationToken = default)
        {
            // 清单里有准确字节数：顺带当缓存校验用（长度不符的脏缓存会被丢弃重下）
            return await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted(package.File), true, Caching.Default, cancellationToken, package.Size).ConfigureAwait(false);
        }

        public AssetBundle? GetBundle(string bundleName, bool strict = true)
        {
            return TryGetBundle(bundleName, out var b, strict) ? b : null;
        }

        public bool TryGetBundle(string bundleName, out AssetBundle? bundle, bool strict = true)
        {
            if (strict)
            {
                if (_bundles.TryGetValue(bundleName, out var b)) { bundle = b; return true; }
                bundle = null; return false;
            }
            // 宽松：键（逻辑名 + 别名）包含关键字（不区分大小写）的第一个匹配
            foreach (var kv in _bundles)
                if (kv.Key.Contains(bundleName, StringComparison.OrdinalIgnoreCase))
                { bundle = kv.Value; return true; }
            bundle = null; return false;
        }

        public async Task<AssetBundle> LoadBundleAsync(
            string bundleName,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            bool strict = true,
            GraphicsDevice? device = null)
        {
            if (TryGetBundle(bundleName, out var existing, strict))
            {
                progress?.Report(1f);
                return existing!;
            }

            if (_manifest is null) await FetchManifestAsync(cancellationToken).ConfigureAwait(false);

            BundlePackage? pkg = _manifest!.FindPackage(bundleName, strict);
            if (pkg is null)
                throw new KeyNotFoundException($"总清单中没有资源包 “{bundleName}”（{(strict ? "精确名" : "关键字")}）。");

            byte[]? bytes = await LoadBundleBytesAsync(pkg, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
                throw new InvalidOperationException($"资源包 “{bundleName}” 下载失败（{pkg.File}）。");

            AssetBundle bundle;
            try
            {
                bundle = AssetBundle.LoadFromMemory(bytes);
            }
            catch (InvalidDataException ex)
            {
                // zip 不合法时把「哪个包 / 多大 / 头部 4 字节」打出来：一眼能看出是下到 HTML、下了半截还是缓存被写坏
                string head = bytes.Length >= 4 ? BitConverter.ToString(bytes, 0, 4) : "(不足 4 字节)";
                throw new InvalidDataException(
                    $"资源包 “{bundleName}” 不是合法 zip：{pkg.File}（{bytes.Length} 字节，头部 {head}，清单 Size {pkg.Size}）。{ex.Message}", ex);
            }
            // 拉包阶段即把需要解码的纹理（Png 等）解码为 RGBA8 并缓存；传了 device 时 KTX2 也在此阶段转码+上传 GPU，
            // 使后续 LoadTexture 仅做取用、不再做解码/上传。
            await bundle.DecodeTexturesAsync(device).ConfigureAwait(false);
            // 以逻辑名登记，使 GetBundle 用该名字能取到
            string key = bundle.Content.Name;
            _bundles[key] = bundle;
            PrintTool.Log($"[KFramework.MonoGame] 已加载资源包 {key}（{bytes.Length} 字节，{bundle.Content.Entries.Count} 项）");
            progress?.Report(1f);
            return bundle;
        }

        /// <summary>异步并发加载多个 AssetBundle（单个失败不影响其余，逐包上报进度 0~1）。</summary>
        public async Task<IReadOnlyList<AssetBundle>> LoadBundlesAsync(
            IEnumerable<string> bundleNames,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            GraphicsDevice? device = null)
        {
            var list = bundleNames as IReadOnlyList<string> ?? bundleNames.ToArray();
            var results = new AssetBundle[list.Count];
            int completed = 0;

            var tasks = list.Select(async (name, i) =>
            {
                results[i] = await LoadBundleAsync(name, cancellationToken, device: device).ConfigureAwait(false);
                int n = Interlocked.Increment(ref completed);
                progress?.Report(n / (float)Math.Max(1, list.Count));
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return results;
        }

        /// <summary>一键加载：先拉总清单，再并发加载其中列出的全部 Bundle（等价于“加载所有资源”）。</summary>
        public async Task LoadAllAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default, GraphicsDevice? device = null)
        {
            if (_manifest is null) await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
            await LoadBundlesAsync(_manifest!.GetAllAssetBundles(), cancellationToken, progress, device).ConfigureAwait(false);
        }

        /// <summary>卸载一个已加载的 Bundle（释放其 zip 流；正在使用的纹理/字节请自行管理）。</summary>
        public void UnloadBundle(string bundleName)
        {
            if (_bundles.TryGetValue(bundleName, out var b))
            {
                b.Dispose();
                // 移除所有指向该 bundle 的键（逻辑名 + 别名）
                foreach (var kv in _bundles.Where(kv => kv.Value == b).ToArray())
                    _bundles.Remove(kv.Key);
            }
        }

        /// <summary>拉远端清单→与本地比对→下载并缓存变化的包，返回本次生效的（远端）清单。</summary>
        public async Task<AssetBundleManifest> UpdateAsync(
            AssetBundleManifest? local = null,
            IProgress<BundleUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            AssetBundleManifest? old = local ?? await TryLoadLocalManifestAsync(cancellationToken).ConfigureAwait(false);
            var remote = await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
            await DeleteOldCachesAsync(old, remote, cancellationToken).ConfigureAwait(false);
            var updates = ComputeUpdates(old, remote);
            foreach (var u in updates)
            {
                progress?.Report(u);
                await LoadBundleBytesAsync(u.Package, cancellationToken).ConfigureAwait(false);
            }
            return remote;
        }

        public async Task<int> DeleteOldCachesAsync(
            AssetBundleManifest? oldManifest,
            AssetBundleManifest? newManifest,
            CancellationToken cancellationToken = default)
        {
            if (oldManifest == null || newManifest == null) return 0;

            int removed = 0;
            try
            {
                // 旧清单里有、但新清单里已下线的包：直接删掉对应缓存条目（走默认缓存，命名与 C# Caching 一致）
                foreach (var p in oldManifest.Packages)
                {
                    if (newManifest.FindPackage(p.Name) == null)
                    {
                        if (await Caching.Default.RemoveAsync(ResolveRooted(p.File)).ConfigureAwait(false))
                            removed++;
                    }
                }

                PrintTool.Log($"[KFramework.MonoGame] 缓存: {Caching.DefaultName} ：删除资源包 {removed} 项, 现有: {await Caching.Default.GetCountAsync().ConfigureAwait(false)} 项");
                return removed;
            }
            catch (Exception ex)
            {
                PrintTool.LogError($"[KFramework.MonoGame] 缓存 GC 失败（不影响加载）：{ex.Message}");
                return -1;
            }
        }

        /// <summary>对比本地/远端清单，返回文件或哈希不一致、或本地缺失的需要更新的包。</summary>
        public static IReadOnlyList<BundleUpdate> ComputeUpdates(AssetBundleManifest? local, AssetBundleManifest remote)
        {
            var localByFile = local?.Packages.ToDictionary(p => p.File, p => p)
                            ?? new Dictionary<string, BundlePackage>();

            var updates = new List<BundleUpdate>();
            foreach (var p in remote.Packages)
            {
                if (!localByFile.TryGetValue(p.File, out var lp) || lp.Hash != p.Hash)
                    updates.Add(new BundleUpdate(p.Name, p.File, p.Hash, p.Size, p));
            }
            return updates;
        }

        public void Dispose()
        {
            foreach (var b in _bundles.Values) b.Dispose();
            _bundles.Clear();
        }
    }

    /// <summary>一次热更中需要下载的包（对应 Unity 的更新单元）。</summary>
    public sealed record BundleUpdate(
        string Name,
        string File,
        string Hash,
        long Size,
        BundlePackage Package);
}
