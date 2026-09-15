using System.Net.Http;
using System.Net.Http.Headers;

namespace KFramework.Content;

/// <summary>
/// 运行时资源管家（对齐 Unity 高层的 AssetBundleManager 思路）：
/// 拉取 version.manifest → 与本地缓存比对（按完整内容哈希）→ 下载并缓存变化的 .web.lib。
///
/// 本地缓存通过回调注入（浏览器侧通常接 IndexedDB），本类完全不碰文件系统，
/// 因此可在 WASM 引擎中直接运行。
///
/// 每个 AssetBundle 文件都带有完整内容哈希（<see cref="BundlePackage.Hash"/>）；
/// 只有缓存中的哈希与清单不一致时才会重新下载，从而实现“精确热更”。
/// </summary>
/// <example>
/// <code>
/// var mgr = new AssetBundleManager(http, "https://cdn.example.com/assets/",
///     loadLocal: name => IndexedDb.LoadAsync(name),
///     saveLocal: (name, bytes) => IndexedDb.SaveAsync(name, bytes));
/// var remote = await mgr.FetchManifestAsync();
/// string[] all = remote.GetAllAssetBundles();
/// string h = remote.GetAssetBundleHash("myres/atlas/characters");
/// </code>
/// </example>
public sealed class AssetBundleManager
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly Func<string, Task<byte[]?>> _loadLocal;
    private readonly Func<string, byte[], Task> _saveLocal;

    /// <summary>
    /// </summary>
    /// <param name="http">用于拉取清单与包的 HttpClient（浏览器侧即 fetch 封装）</param>
    /// <param name="baseUrl">资源基址，如 https://cdn.example.com/assets/（末尾自动补 /）</param>
    /// <param name="loadLocal">按文件名从本地缓存取字节（无则返回 null）。不传则每次都走网络</param>
    /// <param name="saveLocal">把字节写入本地缓存</param>
    public AssetBundleManager(
        HttpClient http,
        string baseUrl,
        Func<string, Task<byte[]?>>? loadLocal = null,
        Func<string, byte[], Task>? saveLocal = null)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _loadLocal = loadLocal ?? (_ => Task.FromResult<byte[]?>(null));
        _saveLocal = saveLocal ?? ((_, _) => Task.CompletedTask);
    }

    /// <summary>拉取远端 version.manifest（单请求带 Cache-Control: no-cache，绕过 HTTP 缓存，确保热更能检测到清单变化）。</summary>
    public async Task<AssetBundleManifest> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "version.manifest")
        {
            Headers = { CacheControl = new CacheControlHeaderValue { NoCache = true } }
        };
        using var r = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        r.EnsureSuccessStatusCode();
        await using var s = await r.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return AssetBundleManifest.Parse(s);
    }

    /// <summary>下载某个 .web.lib 的原始字节；失败返回 null。</summary>
    public async Task<byte[]?> DownloadBundleAsync(string file, CancellationToken cancellationToken = default)
    {
        using var r = await _http.GetAsync(_baseUrl + file, cancellationToken).ConfigureAwait(false);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按包清单取一个 .web.lib 的字节：先查本地缓存，命中且哈希一致则直接复用（精确热更），
    /// 否则从远端下载并写回缓存。返回 null 表示下载失败。
    /// </summary>
    public async Task<byte[]?> LoadBundleBytesAsync(BundlePackage package, CancellationToken cancellationToken = default)
    {
        byte[]? local = await _loadLocal(package.File).ConfigureAwait(false);
        if (local is not null && string.Equals(BundleHash.Hex(local), package.Hash, StringComparison.OrdinalIgnoreCase))
            return local;

        byte[]? remote = await DownloadBundleAsync(package.File, cancellationToken).ConfigureAwait(false);
        if (remote is not null)
            await _saveLocal(package.File, remote).ConfigureAwait(false);
        return remote;
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
        var updates = ComputeUpdates(local, remote);
        foreach (var u in updates)
        {
            progress?.Report(u);
            var bytes = await LoadBundleBytesAsync(u.Package, cancellationToken).ConfigureAwait(false);
            if (bytes != null) await _saveLocal(u.Package.File, bytes).ConfigureAwait(false);
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
}

/// <summary>一次热更中需要下载的包（对应 Unity 的更新单元）。</summary>
public sealed record BundleUpdate(
    string Name,
    string File,
    string Hash,
    long Size,
    BundlePackage Package);
