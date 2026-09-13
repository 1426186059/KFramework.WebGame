using System.Net.Http;

namespace WebLib;

/// <summary>
/// 运行时资源管家（对齐 Unity 高层的 AssetBundleManager 思路）：
/// 拉取 version.manifest → 与本地清单比对 → 下载并缓存变化的 .web.lib。
///
/// 本地缓存通过回调注入（浏览器侧通常接 IndexedDB），本类完全不碰文件系统，
/// 因此可在 WASM 引擎中直接运行。
/// </summary>
/// <example>
/// <code>
/// var mgr = new AssetBundleManager(http, "https://cdn.example.com/assets/",
///     loadLocal: name =&gt; IndexedDb.LoadAsync(name),
///     saveLocal: (name, bytes) =&gt; IndexedDb.SaveAsync(name, bytes));
/// var remote = await mgr.UpdateAsync(localManifest);
/// string[] all = remote.GetAllAssetBundles();
/// string h = remote.GetAssetBundleHash("3-1");
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
    /// <param name="loadLocal">按文件名从本地缓存取字节（无则返回 null）</param>
    /// <param name="saveLocal">把字节写入本地缓存</param>
    public AssetBundleManager(
        HttpClient http,
        string baseUrl,
        Func<string, Task<byte[]?>> loadLocal,
        Func<string, byte[], Task> saveLocal)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/') + "/";
        _loadLocal = loadLocal;
        _saveLocal = saveLocal;
    }

    /// <summary>拉取远端 version.manifest。</summary>
    public async Task<AssetBundleManifest> FetchManifestAsync()
    {
        using var r = await _http.GetAsync(_baseUrl + "version.manifest");
        r.EnsureSuccessStatusCode();
        await using var s = await r.Content.ReadAsStreamAsync();
        return AssetBundleManifest.Parse(s);
    }

    /// <summary>下载某个 .web.lib 的字节；失败返回 null。</summary>
    public async Task<byte[]?> DownloadBundleAsync(string file)
    {
        using var r = await _http.GetAsync(_baseUrl + file);
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// 拉取远端清单 → 与本地清单比对 → 下载并缓存变化的包。
    /// 返回本次生效的（远端）清单，调用方应将其作为新的本地清单保存。
    /// </summary>
    /// <param name="local">上一次成功应用后的本地清单；首跑传 null</param>
    /// <param name="progress">每下载完一个包时回调（仅报告变化的包）</param>
    public async Task<AssetBundleManifest> UpdateAsync(AssetBundleManifest? local = null, IProgress<BundleUpdate>? progress = null)
    {
        var remote = await FetchManifestAsync();
        var updates = ComputeUpdates(local, remote);
        foreach (var u in updates)
        {
            progress?.Report(u);
            var bytes = await DownloadBundleAsync(u.File);
            if (bytes != null) await _saveLocal(u.File, bytes);
        }
        return remote;
    }

    /// <summary>
    /// 对比本地与远端清单，返回需要下载/更新的包。
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
                updates.Add(new BundleUpdate(p.Name, p.File, p.Hash, p.Size));
        }
        return updates;
    }
}
