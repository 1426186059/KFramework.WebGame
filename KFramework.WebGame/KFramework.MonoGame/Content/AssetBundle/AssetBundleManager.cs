namespace KFramework.MonoGame
{
    public sealed class AssetBundleManager : IDisposable
    {
        /// <summary>资源包文件的扩展名（含点）。GC / 过滤时用它圈定"只动资源包"。</summary>
        public const string BundleFileExtension = ".web.lib";

        private readonly HttpClient _http;
        private readonly string _rootDir;
        private AssetBundleManifest? _manifest;
        private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

        public AssetBundleManager(
            HttpClient http,
            string baseUrl)
        {
            _http = http;
            _rootDir = baseUrl.TrimEnd('/') + "/";
        }

        public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

        public async Task<bool> IsBundleCachedAsync(string file, CancellationToken cancellationToken = default)
            => await JSBind_CacheStorage.GetSizeAsync(file).ConfigureAwait(false) > 0;

        private string ResolveRooted(string path)
        {
            return _rootDir + path;
        }

        public async Task<AssetBundleManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
        {
            byte[] data = await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted("version.manifest"), false, cancellationToken).ConfigureAwait(false);
            return _manifest = AssetBundleManifest.Parse(ContentFunc.DecodeUtf8(data));
        }

        public async Task<byte[]?> LoadBundleBytesAsync(BundlePackage package, CancellationToken cancellationToken = default)
        {
            return await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted(package.File), true, cancellationToken).ConfigureAwait(false);
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

            BundlePackage? pkg = AssetBundleManifest.FindPackage(_manifest!.Packages, bundleName, strict);
            if (pkg is null)
                throw new KeyNotFoundException($"总清单中没有资源包 “{bundleName}”（{(strict ? "精确名" : "关键字")}）。");

            byte[]? bytes = await LoadBundleBytesAsync(pkg, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
                throw new InvalidOperationException($"资源包 “{bundleName}” 下载失败（{pkg.File}）。");

            var bundle = AssetBundle.LoadFromMemory(bytes);
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

        /// <summary>
        /// 拉取远端清单 → 与本地清单比对 → 下载并缓存变化的包。
        /// 返回本次生效的（远端）清单，调用方应将其作为新的本地清单保存。
        /// </summary>
        /// <param name="local">上一次成功应用后的本地清单；首跑传 null</param>
        /// <param name="progress">每下载完一个包时回调（仅报告变化的包）</param>
        public async Task<AssetBundleManifest> UpdateAsync(
            AssetBundleManifest? local = null,
            IProgress<BundleUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var remote = await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
            // 拿到远端清单后先做一次缓存 GC：旧版本 / 已下线的包不再保留。
            // 包文件名带内容哈希，热更一次就多一份旧文件，不清理的话 Cache Storage 会随热更次数单调膨胀直到撑爆配额。
            await PruneCacheAsync(remote, null, cancellationToken).ConfigureAwait(false);
            var updates = ComputeUpdates(local, remote);
            foreach (var u in updates)
            {
                progress?.Report(u);
                // LoadBundleBytesAsync 内部已按缓存策略写回（Memory 模式入 _localCache）
                await LoadBundleBytesAsync(u.Package, cancellationToken).ConfigureAwait(false);
            }
            return remote;
        }

        /// <summary>
        /// 缓存 GC：以 <paramref name="manifest"/> 的包清单为白名单，删除 Cache Storage 里的历史版本 / 已下线资源包。
        /// 包文件名带内容哈希，热更一次就留下一份旧文件，故每次拿到新清单后都应 GC 一次。
        /// </summary>
        /// <param name="manifest">当前生效的清单；为 null 时依次回退到已加载清单 / 远端清单。</param>
        /// <param name="extraKeep">额外保留的键（相对路径，会拼上资源根），用于保护同目录下非资源包的缓存条目。</param>
        /// <returns>实际删除的条目数；失败返回 -1（GC 属兜底操作，不抛异常、不阻断加载与热更）。</returns>
        public async Task<int> PruneCacheAsync(
            AssetBundleManifest? manifest = null,
            IEnumerable<string>? extraKeep = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                manifest ??= _manifest ?? await FetchManifestAsync(cancellationToken).ConfigureAwait(false);

                var keep = new List<string>(manifest.Packages.Count + 8);
                foreach (var p in manifest.Packages)
                    keep.Add(ResolveRooted(p.File));
                if (extraKeep is not null)
                    foreach (var k in extraKeep)
                        if (!string.IsNullOrWhiteSpace(k)) keep.Add(ResolveRooted(k));

                // 前缀(资源根目录) + 后缀(.web.lib) 双重限定：只回收资源包，
                // 同目录下的 version.manifest、零散图片等其它缓存条目不受影响
                int removed = await JSBind_CacheStorage.PruneAsync(keep.ToArray(), _rootDir, BundleFileExtension).ConfigureAwait(false);
                if (removed > 0)
                    PrintTool.Log($"[KFramework.MonoGame] 缓存 GC：清理历史版本资源包 {removed} 项（白名单保留 {keep.Count} 项）");
                return removed;
            }
            catch (Exception ex)
            {
                PrintTool.LogError($"[KFramework.MonoGame] 缓存 GC 失败（不影响加载）：{ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 对比本地与远端清单，返回需要下载/更新的包（含其 <see cref="BundlePackage"/>）。
        /// 判定依据：远端包的文件名（含短哈希）或完整哈希与本地不一致，或本地缺失。
        /// 对应 Unity 高层“比对 AssetBundleManifest 差异”的逻辑。
        /// </summary>
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
