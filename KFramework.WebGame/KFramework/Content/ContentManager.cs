using System.Net.Http;
using System.Text.Json;
using KFramework.Content.Pak;
using KFramework.Content.Pipeline;
using KFramework.Graphics;

namespace KFramework.Content;

/// <summary>
/// 运行时资源管理器：读取 <c>content/manifest.json</c> 与 <c>content/content.pak</c>，
/// 把图集页上传为纹理，并对外提供按名字取资源的接口。
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
    private readonly Dictionary<string, PakReader> _paks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Texture2D> _atlasPages = new();
    private readonly Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _textCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _logs = new();

    private ContentManifest? _manifest;

    public ContentManager(GraphicsDevice device, string root = "content")
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _root = root.TrimEnd('/');

        // HttpClient 不接受相对地址，因此用页面基址拼出绝对 URL
        string baseUri = Platform.GetBaseUri();
        Uri? baseAddress = null;

        if (!string.IsNullOrEmpty(baseUri))
        {
            int cut = baseUri.LastIndexOf('/');
            if (cut > 0 && !baseUri.EndsWith('/')) baseUri = baseUri[..(cut + 1)];
            if (!Uri.TryCreate(baseUri, UriKind.Absolute, out baseAddress)) baseAddress = null;
        }

        _http = baseAddress is null ? new HttpClient() : new HttpClient { BaseAddress = baseAddress };
    }

    public bool IsLoaded => _manifest is not null;

    /// <summary>已加载的资源名（按字母序）。</summary>
    public IReadOnlyList<string> AssetNames
        => _manifest is null ? Array.Empty<string>() : _manifest.Assets.Select(static a => a.Name).Order().ToArray();

    public IReadOnlyList<string> Logs => _logs;

    /// <summary>下载并初始化全部内容。</summary>
    public async Task LoadAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        string manifestJson = await GetTextAsync("manifest.json", cancellationToken).ConfigureAwait(false);
        _manifest = ContentManifest.FromJson(manifestJson);

        int totalSteps = _manifest.Paks.Count + _atlasPageCount(_manifest) + 1;
        int completed = 0;

        void Report()
        {
            completed++;
            progress?.Report(Math.Clamp(completed / (float)Math.Max(1, totalSteps), 0f, 1f));
        }

        foreach (ManifestPak pak in _manifest.Paks)
        {
            byte[] data = await GetBytesAsync(pak.File, cancellationToken).ConfigureAwait(false);
            _paks[pak.File] = PakReader.FromBytes(data);
            Report();
        }

        Report();

        foreach (int page in _manifest.Assets.Where(static a => a.Page >= 0).Select(static a => a.Page).Distinct())
        {
            (PakReader reader, PakEntry entry) = Locate($"atlas/{page}");
            byte[] pixels = reader.Read(entry);
            _atlasPages[page] = _device.CreateTexture(entry.Width, entry.Height, pixels);
            Console.WriteLine($"[KFramework] 图集页 {page}: {entry.Width}x{entry.Height}，{pixels.Length} 字节");
            Report();
        }
    }

    private static int _atlasPageCount(ContentManifest manifest)
        => manifest.Assets.Where(static a => a.Page >= 0).Select(static a => a.Page).Distinct().Count();

    private (PakReader Reader, PakEntry Entry) Locate(string name)
    {
        ObjectDisposedException.ThrowIf(_manifest is null, this);

        foreach (KeyValuePair<string, PakReader> pair in _paks)
            if (pair.Value.TryGet(name, out PakEntry entry))
                return (pair.Value, entry);

        throw new KeyNotFoundException($"内容包中找不到资源 “{name}”。");
    }

    #region 公开加载接口

    /// <summary>按名字取一张纹理（图集子图会被缓存复用）。</summary>
    public Texture2D LoadTexture(string name)
    {
        string key = PakFormat.NormalizeName(name);
        if (_textureCache.TryGetValue(key, out Texture2D? cached)) return cached;

        ManifestAsset? asset = Require(key);
        if (asset.Type != "texture")
            throw new InvalidOperationException($"资源 “{key}” 不是纹理（类型 {asset.Type}）。");

        if (!_atlasPages.TryGetValue(asset.Page, out Texture2D? page))
            throw new InvalidOperationException($"图集页 {asset.Page} 尚未加载。");

        Texture2D texture = page.CreateSubtexture(new Rectangle(asset.X, asset.Y, asset.Width, asset.Height));
        _textureCache[key] = texture;
        return texture;
    }

    public bool TryLoadTexture(string name, out Texture2D? texture)
    {
        try
        {
            texture = LoadTexture(name);
            return true;
        }
        catch (Exception ex)
        {
            _logs.Add(ex.Message);
            texture = null;
            return false;
        }
    }

    /// <summary>读取包内的文本 / JSON 原文。</summary>
    public string LoadText(string name)
    {
        string key = PakFormat.NormalizeName(name);
        if (_textCache.TryGetValue(key, out string? cached)) return cached;

        Require(key);
        (PakReader reader, PakEntry entry) = Locate(key);
        string text = DecodeUtf8(reader.Read(entry));
        _textCache[key] = text;
        return text;
    }

    /// <summary>读取并反序列化 JSON 资源。</summary>
    public T LoadJson<T>(string name)
        => JsonSerializer.Deserialize<T>(LoadText(name), JsonOptions)
           ?? throw new InvalidDataException($"资源 “{name}” 反序列化结果为空。");

    public bool Contains(string name) => _manifest?.Find(name) is not null;

    #endregion

    private ManifestAsset Require(string normalizedName)
        => _manifest?.Find(normalizedName)
           ?? throw new KeyNotFoundException($"manifest 中没有资源 “{normalizedName}”。");

    private async Task<byte[]> GetBytesAsync(string relativePath, CancellationToken cancellationToken)
    {
        string url = $"{_root}/{relativePath}";
        byte[] data = await _http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        _logs.Add($"已下载 {url}（{data.Length} 字节）");
        return data;
    }

    private async Task<string> GetTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        byte[] data = await GetBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return DecodeUtf8(data);
    }

    /// <summary>解码 UTF-8 并去掉 BOM，避免 JSON 解析在首字节失败。</summary>
    private static string DecodeUtf8(byte[] data)
    {
        int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
    }

    public void Dispose()
    {
        foreach (Texture2D page in _atlasPages.Values) page.Dispose();
        _atlasPages.Clear();
        _textureCache.Clear();
        _http.Dispose();
    }
}
