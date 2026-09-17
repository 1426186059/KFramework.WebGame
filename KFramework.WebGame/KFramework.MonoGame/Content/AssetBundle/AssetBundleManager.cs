using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace KFramework.MonoGame;

/// <summary>
/// 运行时资源管家（对齐 Unity 高层的 AssetBundleManager 思路）：
/// 拉取 version.manifest → 与本地缓存比对（按完整内容哈希）→ 下载并缓存变化的 .web.lib，
/// 并负责已加载 Bundle 的驻留与卸载。
///
/// 本地字节缓存与已加载 Bundle 都在本类内部管理，调用方（如 <see cref="ContentManager"/>）无需关心持久化细节，
/// 只需通过 <see cref="BundleCacheMode"/> 选择缓存策略。
/// 每个 AssetBundle 文件都带完整内容哈希（<see cref="BundlePackage.Hash"/>），
/// 只有缓存中的哈希与清单不一致时才会重新下载，从而实现“精确热更”。
/// </summary>
/// <example>
/// <code>
/// var mgr = new AssetBundleManager(http, "https://cdn.example.com/assets/", BundleCacheMode.CacheStorage);
/// await mgr.FetchManifestAsync();
/// await mgr.LoadBundleAsync("myres/atlas/characters");
/// using var ab = mgr.GetBundle("myres/atlas/characters");
/// </code>
/// </example>
public sealed class AssetBundleManager : IDisposable
{
    /// <summary>字节缓存策略。</summary>
    public enum BundleCacheMode
    {
        /// <summary>
        /// 仅依赖传输层 HTTP 缓存（浏览器侧即浏览器 HTTP 缓存）；本类不维护任何本地字节缓存。
        /// 最轻量、跨运行时通用，但缓存可能被传输层随时清掉，且冷启动仍需走网络。
        /// </summary>
        Http,
        /// <summary>
        /// 在内存中维护一份字节缓存（进程/会话生命周期内有效），配合内容哈希校验实现免重复下载。
        /// 自包含、引擎无关、可在 WASM 直接运行；但不落盘，页面刷新即失效，仅适合同一会话内反复加载。
        /// </summary>
        Memory,
        /// <summary>
        /// 持久化到浏览器 Cache Storage，跨页面刷新/重进仍然有效（真正的本地缓存）。默认策略。
        /// 仅浏览器/WASM 环境可用；Cache Storage 以 Response 形式存二进制资源、序列化开销更小，
        /// 配合内容哈希校验，确保本地字节与清单声明完全一致才复用。
        /// </summary>
        CacheStorage,
    }

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly BundleCacheMode _cacheMode;

    // 内容哈希校验的字节缓存（仅 Memory 模式使用）
    private readonly Dictionary<string, byte[]> _memoryCache = new(StringComparer.OrdinalIgnoreCase);

    // 总清单（拉取后驻留）
    private AssetBundleManifest? _manifest;

    // 逻辑包名 -> 已加载的 Bundle
    private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// </summary>
    /// <param name="http">用于拉取清单与包的 HttpClient（浏览器侧即 fetch 封装）</param>
    /// <param name="baseUrl">资源基址，如 https://cdn.example.com/assets/（末尾自动补 /）</param>
    /// <param name="cacheMode">字节缓存策略，默认 <see cref="BundleCacheMode.CacheStorage"/></param>
    public AssetBundleManager(
        HttpClient http,
        string baseUrl,
        BundleCacheMode cacheMode = BundleCacheMode.CacheStorage)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _cacheMode = cacheMode;
    }

    /// <summary>已加载的 Bundle 逻辑名。</summary>
    public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

    #region 清单

    /// <summary>拉取并驻留总清单 version.manifest（单请求带 Cache-Control: no-cache，绕过 HTTP 缓存，确保热更能检测到清单变化）。</summary>
    public async Task<AssetBundleManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "version.manifest")
        {
            Headers = { CacheControl = new CacheControlHeaderValue { NoCache = true } }
        };
        using var r = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        r.EnsureSuccessStatusCode();
        await using var s = await r.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return _manifest = AssetBundleManifest.Parse(s);
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
    /// 取一个 .web.lib 的字节：Memory / CacheStorage 模式下先查本地缓存，命中且哈希一致则直接复用（精确热更），
    /// 否则从远端下载并写回缓存。Http 模式下不查本地缓存，直接走 GetAsync（依赖传输层缓存）。
    /// 返回 null 表示下载失败。
    /// </summary>
    public async Task<byte[]?> LoadBundleBytesAsync(BundlePackage package, CancellationToken cancellationToken = default)
    {
        byte[]? cached = _cacheMode switch
        {
            BundleCacheMode.CacheStorage => await LoadFromCacheStorageAsync(package).ConfigureAwait(false),
            BundleCacheMode.Memory    => LoadFromMemory(package),
            _                         => null,
        };
        if (cached is not null) return cached;

        byte[]? remote = await DownloadBundleAsync(package.File, cancellationToken).ConfigureAwait(false);
        if (remote is not null)
        {
            switch (_cacheMode)
            {
                case BundleCacheMode.Memory:    _memoryCache[package.File] = remote; break;
                case BundleCacheMode.CacheStorage: await JSBind_CacheStorage.SaveAsync(package.File, remote).ConfigureAwait(false); break;
            }
        }
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

    /// <summary>从内存缓存取字节，并校验内容哈希与清单一致；不一致/缺失返回 null。</summary>
    private byte[]? LoadFromMemory(BundlePackage package)
    {
        if (_memoryCache.TryGetValue(package.File, out var local)
            && string.Equals(BundleHash.Hex(local), package.Hash, StringComparison.OrdinalIgnoreCase))
            return local;
        return null;
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
        bool strict = true)
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
        // 拉包阶段即把需要解码的纹理（Png 等）解码为 RGBA8 并缓存，使后续 LoadTexture 仅做 GPU 上传。
        await bundle.DecodeTexturesAsync().ConfigureAwait(false);
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
        IProgress<float>? progress = null)
    {
        var list = bundleNames as IReadOnlyList<string> ?? bundleNames.ToArray();
        var results = new AssetBundle[list.Count];
        int completed = 0;

        var tasks = list.Select(async (name, i) =>
        {
            results[i] = await LoadBundleAsync(name, cancellationToken).ConfigureAwait(false);
            int n = Interlocked.Increment(ref completed);
            progress?.Report(n / (float)Math.Max(1, list.Count));
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    /// <summary>一键加载：先拉总清单，再并发加载其中列出的全部 Bundle（等价于“加载所有资源”）。</summary>
    public async Task LoadAllAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_manifest is null) await FetchManifestAsync(cancellationToken).ConfigureAwait(false);
        await LoadBundlesAsync(_manifest!.GetAllAssetBundles(), cancellationToken, progress).ConfigureAwait(false);
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
