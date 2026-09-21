namespace KFramework.MonoGame;

/// <summary>
/// 运行时资源管家（对齐 Unity 高层的 AssetBundleManager 思路）：
/// 拉取 version.manifest → 与本地缓存比对（按完整内容哈希）→ 下载并缓存变化的 .web.lib，
/// 并负责已加载 Bundle 的驻留与卸载。
///
/// 本地字节缓存与已加载 Bundle 都在本类内部管理，调用方（如 <see cref="ContentManager"/>）无需关心持久化细节，
/// 全部采用「Cache Storage 优先、HttpClient 兜底」的单一策略（见 <see cref="LoadBundleBytesAsync"/>）。
/// 每个 AssetBundle 文件都带完整内容哈希（<see cref="BundlePackage.Hash"/>），
/// 只有缓存中的哈希与清单不一致时才会重新下载，从而实现“精确热更”。
/// </summary>
/// <example>
/// <code>
/// var mgr = new AssetBundleManager(http, "https://cdn.example.com/assets/");
/// await mgr.FetchManifestAsync();
/// await mgr.LoadBundleAsync("myres/atlas/characters");
/// using var ab = mgr.GetBundle("myres/atlas/characters");
/// </code>
/// </example>
public sealed class AssetBundleManager : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    // 总清单（拉取后驻留）
    private AssetBundleManifest? _manifest;

    // 逻辑包名 -> 已加载的 Bundle
    private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// </summary>
    /// <param name="http">用于拉取清单与包的 HttpClient（浏览器侧即 fetch 封装）</param>
    /// <param name="baseUrl">资源基址，如 https://cdn.example.com/assets/（末尾自动补 /）</param>
    public AssetBundleManager(
        HttpClient http,
        string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/') + "/";
    }

    /// <summary>已加载的 Bundle 逻辑名。</summary>
    public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

    /// <summary>
    /// 事先探测某个 .web.lib 是否已持久化到本地 Cache Storage（无需下载即可知是否有本地缓存）。
    /// 底层用 <see cref="JSBind_CacheStorage.GetSizeAsync"/> 判断（&gt;0 即在）。
    /// </summary>
    /// <param name="file">包文件名（与 <see cref="BundlePackage.File"/> 一致，作为 Cache Storage 的键）。</param>
    public async Task<bool> IsBundleCachedAsync(string file, CancellationToken cancellationToken = default)
        => await JSBind_CacheStorage.GetSizeAsync(file).ConfigureAwait(false) > 0;

    #region 清单

    /// <summary>拉取并驻留总清单 version.manifest（不走 Cache Storage，每次都重新拉取以确保能检测到清单变化）。</summary>
    public async Task<AssetBundleManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        byte[] data = await ContentFunc.DownloadBytesAsync(_http, _baseUrl + "version.manifest", cancellationToken).ConfigureAwait(false);
        return _manifest = AssetBundleManifest.Parse(ContentFunc.DecodeUtf8(data));
    }

    #endregion

    #region 字节下载与缓存

    /// <summary>下载某个 .web.lib 的原始字节；失败返回 null。</summary>
    public async Task<byte[]?> DownloadBundleAsync(string file, CancellationToken cancellationToken = default)
    {
        using var r = await _http.GetAsync(_baseUrl + file, cancellationToken).ConfigureAwait(false);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 采用「Cache Storage 优先」的单一缓存策略：先查本地 Cache Storage，命中且内容哈希与清单一致则直接复用；
    /// 未命中（或哈希不符）则经 HttpClient 远程下载，下载成功后写回 Cache Storage 以备下次复用。
    /// 返回 null 表示远程也取不到字节。




    /// </summary>
    public async Task<byte[]?> LoadBundleBytesAsync(BundlePackage package, CancellationToken cancellationToken = default)
    {
        byte[]? local = await LoadFromCacheStorageAsync(package).ConfigureAwait(false);
        if (local is not null) return local;
        byte[]? remote = await DownloadBundleAsync(package.File, cancellationToken).ConfigureAwait(false);
        if (remote is not null)
            await JSBind_CacheStorage.SaveAsync(package.File, remote).ConfigureAwait(false);
        return remote;
    }

    /// <summary>
    /// 从 Cache Storage 取字节（先探长度、再按长度分配缓冲写回），并校验内容哈希与清单一致；
    /// 缺失/长度不符/哈希不一致均返回 null。
    /// </summary>
    private async Task<byte[]?> LoadFromCacheStorageAsync(BundlePackage package)
    {
        int len = await JSBind_CacheStorage.GetSizeAsync(package.File).ConfigureAwait(false);
        if (len <= 0) return null;
        byte[] local = new byte[len];
        int written = await JSBind_CacheStorage.LoadIntoAsync(package.File, local).ConfigureAwait(false);
        if (written != len) return null;
        return string.Equals(BundleHash.Hex(local), package.Hash, StringComparison.OrdinalIgnoreCase)
            ? local
            : null;
    }

    #endregion

    #region Bundle 加载 / 卸载（已加载驻留由本类管理）

    /// <summary>取一个已加载的 Bundle（同步；包须先经 <see cref="LoadBundleAsync"/> 加载）。
    /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（键包含）匹配首个已加载 Bundle。</summary>
    public AssetBundle? GetBundle(string bundleName, bool strict = true)
        => TryGetBundle(bundleName, out var b, strict) ? b : null;

    /// <summary>尝试取一个已加载的 Bundle。
    /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（键包含）匹配首个已加载 Bundle。</summary>
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

    /// <summary>
    /// 异步加载单个 AssetBundle：解析包条目 → 取字节（含缓存）→ 解包 → 预解码纹理 → 登记驻留。
    /// 已加载过则直接返回（幂等）。
    /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（Name 包含）匹配首个包。
    /// </summary>
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

    #endregion

    #region 热更（字节级：下载并缓存变化的包）

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

    #endregion

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
