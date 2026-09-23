using KFramework.MonoGame;


// 浏览器端资源加载：统一经 KFramework.MonoGame.ContentManager 的异步方法取远程资源，
// 不再自行维护 HttpClient，也不再提供会冻结主线程的同步 GetBytes。
public class BrowserResource
{
    private const string CacheName = "WebGame.Mir2.Cache";
    public static readonly Caching mCacheInstance = Caching.Open(CacheName);
    public static ContentManager Content { get; private set; }
    public static ContentManager LibContent => Content;

    private static readonly HashSet<string> _missing = new HashSet<string>();

    // 加载优先级（数值越大越优先，见 KFramework.MonoGame.ContentLoadScheduler）：
    // 地图资源最高 —— 玩家进图 / 传送后第一时间要看到地面；其余（UI、装备、怪物、音效）次之。
    private const int PriorityMap = 10;
    private const int PriorityOther = 0;

    // 地图 .map 与地图图片 Lib 都以 "Map/" 开头（Map/0.map、Map/0/WemadeMir2/Tiles.Lib）
    private static int PriorityOf(string path)
    {
        return path.StartsWith("Map/", StringComparison.OrdinalIgnoreCase) ? PriorityMap : PriorityOther;
    }

    public static async Task<byte[]> GetBytesAsync(string url)
    {
        string path = NormalizePath(url);
        if (Content == null || _missing.Contains(path)) return null;

        try
        {
            // 直接整文件加载（已无超大 Lib，无需分片下载）；地图资源给最高优先级。
            byte[] bytes = await Content.LoadBytesAsync(path, true, mCacheInstance, priority: PriorityOf(path))
                .ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
            {
                _missing.Add(path);
                 PrintTool.Log("[Mir] 资源缺失，后续不再重试: " + path);
            }
            return bytes;
        }
        catch (Exception ex)
        {
            _missing.Add(path);
            PrintTool.Log("[Mir] 资源缺失，后续不再重试: " + path + " " + ex.Message);
            return null;
        }
    }

    public static string ResolveUrl(string fileName) => NormalizePath(fileName);

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
        get => _libBaseUrl;
        set => _libBaseUrl = value;
    }

    private static string _bundleBaseUrl = "/";
    public static string BundleBaseUrl
    {
        get => _bundleBaseUrl;
        set => _bundleBaseUrl = value;
    }

    /// <summary>
    /// 注入两个 HTTP 资源服务器地址：
    ///   libBaseUrl   —— 默认资源 lib 的自建 Web 服务器（:5080，根=Crystal Build），松加载原始 .Lib；
    ///   bundleBaseUrl—— AssetBundle(hot_update_res) 的服务器（:5081，根=Mir2Res），client 请求 URL 已含 /hot_update_res/ 前缀。
    /// :5081 根已是 Mir2Res，bundleBaseUrl 已含 /hot_update_res/ 前缀，因此 BundleContent 的 ContentManager.root 传空串，
    /// 否则 URL 会变成 /hot_update_res/hot_update_res 双重前缀。
    /// </summary>
    public static void Configure(string libBaseUrl, string bundleBaseUrl)
    {
        _libBaseUrl = libBaseUrl;
        _bundleBaseUrl = bundleBaseUrl;
        Content = new ContentManager("", libBaseUrl);
    }

}
