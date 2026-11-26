using KFramework.MonoGame;


// 浏览器端资源加载：统一经 KFramework.MonoGame.ContentManager 的异步方法取远程资源，
// 不再自行维护 HttpClient，也不再提供会冻结主线程的同步 GetBytes。
public class BrowserResource
{
    private const string CacheName = "WebGame.Mir2.Cache";
    public static readonly Caching mCacheInstance = Caching.Open(CacheName);
    public static ContentManager Content { get; private set; }
    public static ContentManager LibContent
    {
        get
        {
            return Content;
        }
    }

    private static readonly HashSet<string> _missing = new HashSet<string>();

    /// <summary>某资源是否已被确认「永久缺失」（多次重试仍失败）。绘制路径据此决定是否重试，避免每帧重试风暴。</summary>
    public static bool IsMissing(string url)
    {
        string path = NormalizePath(url);
        lock (_missing) return _missing.Contains(path);
    }

    /// <param name="cancellationToken">用于中断本次加载（如切图时不必再等旧地图下载完）。</param>
    public static async Task<byte[]> GetBytesAsync(string url, EResLoadPriority nPriority = EResLoadPriority.User, CancellationToken cancellationToken = default)
    {
        string path = NormalizePath(url);
        if (Content == null) return null;
        if (_missing.Contains(path)) return null;
        
        const int MaxAttempts = 3;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                // 直接整文件加载（已无超大 Lib，无需分片下载）；地图资源给最高优先级。
                byte[] bytes = await Content.LoadBytesAsync(path, mCacheInstance, (int)nPriority, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0)
                    return bytes;
            }
            catch (OperationCanceledException)
            {
                // 被取消（切图时中断旧地图下载）：不记缺失、不重试，交由上层处理。
                throw;
            }
            catch (Exception ex)
            {
                //PrintTool.Log($"[Mir] 资源加载异常(第{attempt}次) {path}: {ex.Message}");
            }

            if (attempt < MaxAttempts)
                await Task.Delay(80 * attempt, cancellationToken).ConfigureAwait(false);
        }

        // 多次重试仍失败 → 视为真正缺失，永久拉黑避免每帧重试风暴。
        _missing.Add(path);
        //PrintTool.Log("[Mir] 资源缺失，后续不再重试: " + path);
        return null;
    }

    public static string ResolveUrl(string fileName)
    {
        return NormalizePath(fileName);
    }

    // 把 Windows 风格的相对路径（".\Data\ChrSel.Lib"）规范成 HTTP 友好的 "Data/ChrSel.Lib"。
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = path.Replace('\\', '/');
        // 折叠多余的连续斜杠（如 "Sound//x.wav"），避免请求路径里出现双斜杠导致 404。
        while (path.Contains("//")) path = path.Replace("//", "/");
        while (path.StartsWith("./")) path = path.Substring(2);
        return path;
    }

    private static string _libBaseUrl = "/";
    public static string LibBaseUrl
    {
        get
        {
            return _libBaseUrl;
        }
        set { _libBaseUrl = value; }
    }

    public static void Configure(string libBaseUrl)
    {
        _libBaseUrl = libBaseUrl;
        Content = new ContentManager("", libBaseUrl);
    }

}
