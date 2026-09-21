using KFramework.MonoGame;

namespace MirEngine
{
    // 浏览器端资源加载：统一经 KFramework.MonoGame.ContentManager 的异步方法取远程资源，
    // 不再自行维护 HttpClient，也不再提供会冻结主线程的同步 GetBytes。
    public class BrowserResource
    {
        // 默认资源 lib（原始 .Lib 等）走自建 Web 服务器（如 http://127.0.0.1:5080/，根=Crystal Build），
        // 由 CMain.Init 经 Configure 注入。松加载按 BaseAddress 根取资源，ContentManager.root 仅作占位。
        public static ContentManager Content { get; private set; }

        // 同 Content（默认 lib 通道），保留别名以便旧调用。
        public static ContentManager LibContent => Content;

        // AssetBundle（hot_update_res，由 kfc 打包）走 :5081 服务器（根=Mir2Res），其 URL 已含 /hot_update_res/ 前缀
        // （bundleBaseUrl=...:5081/hot_update_res/），经 KFramework.MonoGame 内容加载器（AssetBundleManager）加载。
        // 地图图片 Lib 蒸馏产物（Mir2Res/Map/<地图名>/...）也由同一台 :5081 服务器按 /Map/ 类别提供。
        public static ContentManager BundleContent { get; private set; }

        // 确认缺失（404 等）的资源拉黑：同一个文件每次取用都会重发一次请求、再走一遍
        // "分片失败 → 回退整文件"，控制台与网络面板被同一条错误反复刷屏（典型：Sound/1014-6.wav）。
        private static readonly HashSet<string> _missing = new HashSet<string>();

        public static async Task<byte[]> GetBytesAsync(string url)
        {
            string path = NormalizePath(url);
            if (Content == null || _missing.Contains(path)) return null;

            try
            {
                // 直接整文件加载（已无超大 Lib，无需分片下载）。
                byte[] bytes = await Content.LoadBytesAsync(path).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0)
                {
                    _missing.Add(path);
                    Log("[Mir] 资源缺失，后续不再重试: " + path);
                }
                return bytes;
            }
            catch (Exception ex)
            {
                _missing.Add(path);
                Log("[Mir] 资源缺失，后续不再重试: " + path + " " + ex.Message);
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
            // 默认 lib 通道：root 仅占位（松加载忽略），实际按 libBaseUrl 根取资源
            Content = new ContentManager("hot_update_res", libBaseUrl);
            // AssetBundle 通道：服务器根已是 hot_update_res，root 必须为空
            BundleContent = new ContentManager("", bundleBaseUrl);
        }

        public static void Log(string msg)
        {
            try { Console.WriteLine(msg); }
            catch { }
        }
    }
}
