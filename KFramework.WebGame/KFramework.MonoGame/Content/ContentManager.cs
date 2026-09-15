using System.Net.Http.Headers;
using System.Text.Json;

namespace KFramework.MonoGame;

/// <summary>
/// 运行时资源管理器（对齐 Unity 的 AssetBundle 体系）。
///
/// 设计要点：
/// <list type="bullet">
///   <item>所有资源都装在若干 <see cref="AssetBundle"/>（.web.lib）里；先 <see cref="LoadBundleAsync"/> 加载包，再从中取资源。</item>
///   <item>提供 <see cref="LoadBundleAsync"/>（单个包）与 <see cref="LoadBundlesAsync"/>（多个包并发）两类异步加载入口。</item>
///   <item>纹理 / 文本 / JSON / 字节等资源全部从已加载的 Bundle 中异步取出（<see cref="LoadTextureAsync"/> 等）。</item>
///   <item>每个 .web.lib 都带完整内容哈希，<see cref="AssetBundleManager"/> 据此做精确热更（缓存哈希一致则跳过下载）。</item>
/// </list>
///
/// 资源定位：总清单 <c>version.manifest</c>（<see cref="AssetBundleManifest"/>）描述“有哪些包、各自的哈希”；
/// 每个包内自带 <c>manifest.json</c>（<see cref="ContentManifest"/>）描述“包内资源名 → 图集页/区域”的索引，
/// 因此图集子图能被正确切片并上传 GPU。
/// </summary>
public sealed class ContentManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly GraphicsDevice _device;
    private readonly HttpClient _http;
    private readonly string _root;
    private readonly AssetBundleManager _manager;

    // 总清单（包列表 + 哈希）
    private AssetBundleManifest? _bundleManifest;

    // 逻辑包名 -> 已加载的 Bundle
    private readonly Dictionary<string, AssetBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);
    // 图集页索引 -> 整页纹理
    private readonly Dictionary<int, Texture2D> _atlasPages = new();
    // 纹理缓存（按资源名，已是切片后的子纹理）
    private readonly Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);
    // 文本 / 字节缓存（按资源名）
    private readonly Dictionary<string, string> _textCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _bytesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _logs = new();

    public ContentManager(
        GraphicsDevice device,
        string root = "content",
        Func<string, Task<byte[]?>>? loadLocal = null,
        Func<string, byte[], Task>? saveLocal = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
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

    /// <summary>总清单与资源索引是否就绪（包已加载、图集子图可被定位）。</summary>
    public bool IsLoaded => _bundleManifest is not null;

    /// <summary>已加载的 Bundle 逻辑名。</summary>
    public IReadOnlyList<string> LoadedBundles => _bundles.Keys.ToArray();

    public IReadOnlyList<string> Logs => _logs;

    #region 清单与 Bundle 加载（全部异步）

    /// <summary>拉取并解析总清单 version.manifest（仅包列表与哈希，不含资源索引）。</summary>
    /// <remarks>
    /// 清单文件名不带内容哈希，因此对这次请求单独带 <c>Cache-Control: no-cache</c>，强制绕过 HTTP 缓存，
    /// 保证每次都拿到最新清单；否则浏览器/代理会一直返回旧缓存，热更永远比对不到新包。
    /// 真正的资源包（.web.lib）文件名已含内容哈希，走默认 HTTP 缓存即可。
    /// </remarks>
    public async Task LoadManifestAsync(CancellationToken cancellationToken = default)
    {
        string json = await GetTextAsync("version.manifest", cancellationToken, noCache: true).ConfigureAwait(false);
        _bundleManifest = AssetBundleManifest.Parse(json);
    }

    /// <summary>
    /// 一键加载：先拉总清单，再并发加载其中列出的全部 Bundle（等价于“加载所有资源”）。
    /// 资源拆成多个 Bundle 时也能一次性就绪；只想加载部分资源可用 <see cref="LoadBundleAsync"/>。
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default, IProgress<float>? progress = null)
    {
        if (_bundleManifest is null) await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        await LoadBundlesAsync(_bundleManifest!.GetAllAssetBundles(), cancellationToken, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步加载单个 AssetBundle。已加载过则直接返回（幂等）。
    /// 下载时按 <see cref="BundlePackage.Hash"/> 与本地缓存比对，命中且不变化时跳过网络，实现精确热更。
    /// 加载完成后会解析包内 manifest.json（资源索引）并合并进全局索引。
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

        BundlePackage? pkg = null;
        foreach (var p in _bundleManifest!.Packages)
            if (string.Equals(p.Name, bundleName, StringComparison.OrdinalIgnoreCase)) { pkg = p; break; }

        if (pkg is null)
            throw new KeyNotFoundException($"总清单中没有资源包 “{bundleName}”。");

        byte[]? bytes = await _manager.LoadBundleBytesAsync(pkg, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
            throw new InvalidOperationException($"资源包 “{bundleName}” 下载失败（{pkg.File}）。");

        var bundle = AssetBundle.LoadFromMemory(bytes);
        _bundles[bundle.Content.Name] = bundle;

        _logs.Add($"已加载资源包 {bundle.Content.Name}（{bytes.Length} 字节，{bundle.Content.Entries.Count} 项）");
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
            _bundles.Remove(bundleName);
        }
    }

    #endregion

    #region 资源加载（全部从已加载的 Bundle 中异步取出）

    private (AssetBundle Bundle, AssetBundleEntry Entry)? FindAsset(string normalizedName)
    {
        foreach (var bundle in _bundles.Values)
        {
            var info = bundle.GetAssetInfo(normalizedName);
            if (info is not null) return (bundle, info);
        }
        return null;
    }

    public bool Contains(string name)
        => FindAsset(PakFormat.NormalizeName(name)) is not null;

    /// <summary>按名字取出原始字节（跨所有已加载 Bundle 查找）。</summary>
    public async Task<byte[]> LoadAssetBytesAsync(string name, CancellationToken cancellationToken = default)
    {
        string key = PakFormat.NormalizeName(name);
        if (_bytesCache.TryGetValue(key, out var cached)) return cached;

        var found = FindAsset(key)
                   ?? throw new KeyNotFoundException($"已加载的 Bundle 中找不到资源 “{key}”（是否忘了先 LoadBundleAsync？）");

        byte[] data = found.Bundle.LoadAsset(key);
        _bytesCache[key] = data;
        return data;
    }

    /// <summary>
    /// 同步取出原始字节（仅适用于“Bundle 已加载完成”之后）。
    /// 与 Unity 的 <c>AssetBundle.LoadAsset</c> 一致：Bundle 驻留内存后取资源是即时操作。
    /// 真正异步的是 <see cref="LoadBundleAsync"/> / <see cref="LoadAssetBytesAsync"/>。
    /// </summary>
    public byte[] LoadBytes(string name)
    {
        string key = PakFormat.NormalizeName(name);
        if (_bytesCache.TryGetValue(key, out var cached)) return cached;

        var found = FindAsset(key)
                   ?? throw new KeyNotFoundException($"已加载的 Bundle 中找不到资源 “{key}”（是否忘了先 LoadBundleAsync？）");

        byte[] data = found.Bundle.LoadAsset(key);
        _bytesCache[key] = data;
        return data;
    }

    /// <summary>按名字读取文本 / JSON 原文。</summary>
    public async Task<string> LoadTextAsync(string name, CancellationToken cancellationToken = default)
        => DecodeUtf8(await LoadAssetBytesAsync(name, cancellationToken).ConfigureAwait(false));

    /// <summary>读取并反序列化 JSON 资源。</summary>
    public async Task<T> LoadJsonAsync<T>(string name, CancellationToken cancellationToken = default)
        => JsonSerializer.Deserialize<T>(await LoadTextAsync(name, cancellationToken).ConfigureAwait(false), JsonOptions)
           ?? throw new InvalidDataException($"资源 “{name}” 反序列化结果为空。");

    /// <summary>
    /// 按名字取一张纹理。资源若带图集页信息（<see cref="ContentManifest"/> 索引中的 Page≥0），
    /// 则从对应图集页切片出子图并缓存；否则按整张原始 RGBA8 上传 GPU。
    /// </summary>
    public async Task<Texture2D> LoadTextureAsync(string name, CancellationToken cancellationToken = default)
    {
        string key = PakFormat.NormalizeName(name);
        if (_textureCache.TryGetValue(key, out Texture2D? cached)) return cached;

        var found = FindAsset(key)
                   ?? throw new KeyNotFoundException($"已加载的 Bundle 中找不到纹理 “{key}”。");
        AssetBundleEntry entry = found.Entry;

        // 图集子图：从对应图集页切片
        if (entry.Page >= 0)
        {
            Texture2D page = await GetAtlasPageAsync(entry.Page, cancellationToken).ConfigureAwait(false);
            Texture2D sub = page.CreateSubtexture(new Rectangle(entry.X, entry.Y, entry.Width, entry.Height));
            _textureCache[key] = sub;
            return sub;
        }

        // 整张纹理（atlas 原始页等）：直接按 RGBA8 上传
        byte[] pixels = found.Bundle.LoadAsset(key);
        int w = entry.Width;
        int h = entry.Height;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"纹理 “{key}” 缺少像素尺寸，无法上传 GPU。");
        Texture2D texture = _device.CreateTexture(w, h, pixels);
        _textureCache[key] = texture;
        return texture;
    }

    /// <summary>确保某图集页纹理已上传（懒加载 + 缓存）。</summary>
    private async Task<Texture2D> GetAtlasPageAsync(int page, CancellationToken cancellationToken)
    {
        if (_atlasPages.TryGetValue(page, out Texture2D? cached)) return cached;

        string pageName = $"atlas/{page}";
        var found = FindAsset(PakFormat.NormalizeName(pageName))
                   ?? throw new KeyNotFoundException($"已加载的 Bundle 中找不到图集页 “{pageName}”。");
        byte[] pixels = found.Bundle.LoadAsset(pageName);
        int w = found.Entry.Width;
        int h = found.Entry.Height;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"图集页 “{pageName}” 缺少像素尺寸，无法上传 GPU。");
        Texture2D tex = _device.CreateTexture(w, h, pixels);
        _atlasPages[page] = tex;
        Console.WriteLine($"[KFramework.MonoGame] 图集页 {page}: {w}x{h}，{pixels.Length} 字节");
        return tex;
    }

    /// <summary>
    /// 按页面基址远程下载任意文本资源（不走内容包，用于关卡等松散文件）。
    /// 与 <see cref="LoadBundleAsync"/> 共用同一个 <see cref="_http"/>，因此天然是异步 / 远程的。
    /// </summary>
    public async Task<string> DownloadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        byte[] data = await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
        _logs.Add($"已下载 {relativePath}（{data.Length} 字节）");
        return DecodeUtf8(data);
    }

    /// <summary>
    /// 按页面基址远程异步下载任意原始字节流（不走内容包，不解码）。
    /// 对应 Unity 的 <c>UnityWebRequest</c> + <c>DownloadHandlerBuffer</c>（或 <c>UnityWebRequest.Get</c> 取字节）。
    /// 与 <see cref="LoadBundleAsync"/> 共用同一个 <see cref="_http"/>，因此天然是异步 / 远程的。
    /// </summary>
    public async Task<byte[]> DownloadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        byte[] data = await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
        _logs.Add($"已下载 {relativePath}（{data.Length} 字节）");
        return data;
    }

    #endregion

    #region 底层 HTTP

    private async Task<byte[]> GetBytesAsync(string relativePath, CancellationToken cancellationToken, bool noCache = false)
    {
        string url = $"{_root}/{relativePath}";
        byte[] data;
        if (noCache)
        {
            // 单请求级别绕过 HTTP 缓存（对应 UnityWebRequest 的 cache 控制），不影响其它走默认缓存的请求
            var req = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true }}
            };
            using var r = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            r.EnsureSuccessStatusCode();
            data = await r.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            data = await _http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        }
        _logs.Add($"已下载 {url}（{data.Length} 字节）");
        return data;
    }

    private async Task<string> GetTextAsync(string relativePath, CancellationToken cancellationToken, bool noCache = false)
    {
        byte[] data = await GetBytesAsync(relativePath, cancellationToken, noCache).ConfigureAwait(false);
        return DecodeUtf8(data);
    }

    #endregion

    /// <summary>解码 UTF-8 并去掉 BOM，避免 JSON 解析在首字节失败。</summary>
    private static string DecodeUtf8(byte[] data)
    {
        int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
    }

    public void Dispose()
    {
        foreach (var tex in _textureCache.Values) tex.Dispose();
        _textureCache.Clear();
        foreach (var tex in _atlasPages.Values) tex.Dispose();
        _atlasPages.Clear();
        foreach (var b in _bundles.Values) b.Dispose();
        _bundles.Clear();
        _http.Dispose();
    }
}
