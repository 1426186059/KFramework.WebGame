using System.Net.Http;
using System.Net.Http.Headers;

namespace KFramework.MonoGame;

/// <summary>
/// 运行时资源管理器：只负责 AssetBundle 的“异步加载 / 卸载”。
///
/// 设计约定（务必遵守，见仓库根 README “内容系统架构”）：
/// <list type="bullet">
///   <item>本类<b>不提供任何取资源的同步方法</b>，也不提供纹理 / JSON / 文本 / 图集的取出逻辑。</item>
///   <item>具体资源的取出（<c>LoadTexture</c> / <c>LoadJson</c> / <c>LoadText</c> / 图集切片）全部在
///     <see cref="AssetBundle"/> 上以<b>同步</b>方式完成 —— 因为包一旦驻留内存，取资源是即时操作
///     （对齐 Unity 的 <c>AssetBundle.LoadAsset</c> 同步语义）。真正的异步只发生在拉包本身。</item>
///   <item>调用方先 <see cref="LoadAsync"/> / <see cref="LoadBundleAsync"/> 把包载进内存，
///     再用 <see cref="GetBundle"/> 取出 <see cref="AssetBundle"/>，随后在包上同步取资源。</item>
/// </list>
/// </summary>
public sealed class ContentManager : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _root;
    private readonly AssetBundleManager _manager;

    // 总清单（包列表 + 哈希）
    private AssetBundleManifest? _bundleManifest;

    // 逻辑包名 -> 已加载的 Bundle
    private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);

    public ContentManager(
        string root = "hot_update_res",
        Func<string, Task<byte[]?>>? loadLocal = null,
        Func<string, byte[], Task>? saveLocal = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root.TrimEnd('/');

        // HttpClient 不接受相对地址，因此用页面基址拼出绝对 URL
        string baseUri = JSBind_Platform.GetBaseUri();
        Uri? baseAddress = null;
        if (!string.IsNullOrEmpty(baseUri))
        {
            int cut = baseUri.LastIndexOf('/');
            if (cut > 0 && !baseUri.EndsWith('/')) baseUri = baseUri[..(cut + 1)];
            if (!Uri.TryCreate(baseUri, UriKind.Absolute, out baseAddress)) baseAddress = null;
        }

        _http = baseAddress is null ? new HttpClient() : new HttpClient { BaseAddress = baseAddress };
        _manager = new AssetBundleManager(_http, _root, loadLocal, saveLocal);
    }

    /// <summary>已加载的 Bundle 逻辑名。</summary>
    public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

    /// <summary>取一个已加载的 Bundle（同步；包必须先经 <see cref="LoadBundleAsync"/> 加载）。</summary>
    public AssetBundle? GetBundle(string bundleName)
        => _bundles.TryGetValue(bundleName, out var b) ? b : null;

    /// <summary>尝试取一个已加载的 Bundle。</summary>
    public bool TryGetBundle(string bundleName, out AssetBundle? bundle)
    {
        if (_bundles.TryGetValue(bundleName, out var b)) { bundle = b; return true; }
        bundle = null; return false;
    }

    #region 清单与 Bundle 加载（全部异步）

    /// <summary>拉取并解析总清单 version.manifest（仅包列表与哈希，不含资源索引）。</summary>
    public async Task LoadManifestAsync(CancellationToken cancellationToken = default)
        => _bundleManifest = await _manager.FetchManifestAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 一键加载：先拉总清单，再并发加载其中列出的全部 Bundle（等价于“加载所有资源”）。
    /// </summary>
    public async Task LoadAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_bundleManifest is null) await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        await LoadBundlesAsync(_bundleManifest!.GetAllAssetBundles(), cancellationToken, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步加载单个 AssetBundle。已加载过则直接返回（幂等）。
    /// </summary>
    public async Task<AssetBundle> LoadBundleAsync(
        string bundleName,
        CancellationToken cancellationToken = default,
        IProgress<float>? progress = null)
    {
        if (_bundles.TryGetValue(bundleName, out var existing))
        {
            progress?.Report(1f);
            return existing;
        }

        if (_bundleManifest is null) await LoadManifestAsync(cancellationToken).ConfigureAwait(false);

        BundlePackage? pkg = AssetBundleManifest.FindPackage(_bundleManifest!.Packages, bundleName);
        if (pkg is null)
            throw new KeyNotFoundException($"总清单中没有资源包 “{bundleName}”（含别名）。");

        byte[]? bytes = await _manager.LoadBundleBytesAsync(pkg, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
            throw new InvalidOperationException($"资源包 “{bundleName}” 下载失败（{pkg.File}）。");

        var bundle = AssetBundle.LoadFromMemory(bytes);
        // 拉包阶段即把需要解码的纹理（Png 等）解码为 RGBA8 并缓存，使后续 LoadTexture 仅做 GPU 上传。
        await bundle.DecodeTexturesAsync().ConfigureAwait(false);
        // 同时以「逻辑名 + 全部别名」登记，使 GetBundle 用任一名字都能取到
        string key = bundle.Content.Name;
        _bundles[key] = bundle;
        if (pkg.Aliases is not null)
            foreach (var alias in pkg.Aliases)
                if (!_bundles.ContainsKey(alias)) _bundles[alias] = bundle;
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

    #region 松散文件下载（非 Bundle 内的资源，如关卡文本；属异步加载，但不经 AssetBundle）

    /// <summary>按页面基址异步下载任意文本（不走内容包，用于关卡等松散文件）。</summary>
    public async Task<string> LoadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        byte[] data = await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return InnerCommonFunc.DecodeUtf8(data);
    }

    /// <summary>按页面基址异步下载任意字节流（不走内容包）。</summary>
    public async Task<byte[]> LoadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
        => await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

    #endregion

    public void Dispose()
    {
        foreach (var b in _bundles.Values) b.Dispose();
        _bundles.Clear();
        _http.Dispose();
    }
}
