using KFramework.MonoGame;
using System.Net;
using System.Net.Http.Headers;

namespace MirEngine
{
    // 浏览器端资源加载：统一经 KFramework.MonoGame.ContentManager 的异步方法取远程资源，
    // 不再自行维护 HttpClient，也不再提供会冻结主线程的同步 GetBytes。
    public class BrowserResource
    {
        // 指向资源服务器（如 http://127.0.0.1:5080/）的 ContentManager，由 CMain.Init 经 Configure 注入。
        public static ContentManager Content { get; private set; }

        // 确认缺失（404 等）的资源拉黑：同一个文件每次取用都会重发一次请求、再走一遍
        // "分片失败 → 回退整文件"，控制台与网络面板被同一条错误反复刷屏（典型：Sound/1014-6.wav）。
        private static readonly HashSet<string> _missing = new HashSet<string>();

        public static async Task<byte[]> GetBytesAsync(string url)
        {
            string path = NormalizePath(url);
            if (Content == null || _missing.Contains(path)) return null;

            try
            {
                // 分片下载：超大地图片库（Tiles.Lib 数百 MB）若一次性 marshal 成单个 byte[] 会在 WASM 边界失败/返回空。
                return await Content.LoadBytesChunkedAsync(path).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log("[Mir] 分片下载失败，回退整文件: " + path + " " + ex.Message);

                byte[] fallback = null;
                try { fallback = await Content.LoadBytesAsync(path).ConfigureAwait(false); }
                catch { }

                if (fallback == null || fallback.Length == 0)
                {
                    _missing.Add(path);
                    Log("[Mir] 资源缺失，后续不再重试: " + path);
                }
                return fallback;
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

        private static string _baseUrl = "/";
        public static string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = value;
        }

        // 注入资源服务器地址，并创建一个指向它的 ContentManager 供 GetBytesAsync 使用。
        public static void Configure(string baseUrl)
        {
            BaseUrl = baseUrl;
            Content = new ContentManager("hot_update_res", baseUrl);
        }

        public static void Log(string msg)
        {
            try { Console.WriteLine(msg); }
            catch { }
        }
        
        public static async Task<byte[]> LoadBytesChunkedAsync(HttpClient _http, string relativePath, int chunkSize = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
        {
            long cs = Math.Max(1, (long)chunkSize);
            try
            {
                using var firstReq = new HttpRequestMessage(HttpMethod.Get, relativePath);
                firstReq.Headers.Range = new RangeHeaderValue(0, cs - 1);
                using var firstResp = await _http.SendAsync(firstReq, cancellationToken).ConfigureAwait(false);

                // 服务器不支持 Range：退化为整文件下载。
                if (firstResp.StatusCode != HttpStatusCode.PartialContent)
                    return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

                long total = firstResp.Content.Headers.ContentRange?.Length ?? 0;
                // 无总大小或超出单个 byte[] 上限（~2GB）：退化为整文件下载。
                if (total <= 0 || total > int.MaxValue)
                    return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

                var result = new byte[(int)total];
                byte[] first = await firstResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (first.Length > 0) Buffer.BlockCopy(first, 0, result, 0, first.Length);
                long pos = first.Length;

                while (pos < total)
                {
                    long end = Math.Min(pos + cs, total) - 1;
                    using var req = new HttpRequestMessage(HttpMethod.Get, relativePath);
                    req.Headers.Range = new RangeHeaderValue(pos, end);
                    using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) break;
                    byte[] chunk = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (chunk.Length == 0) break;
                    Buffer.BlockCopy(chunk, 0, result, (int)pos, chunk.Length);
                    pos += chunk.Length;
                }
                return result;
            }
            catch (Exception)
            {
                // 分片失败（如 Range 异常）：最后尝试整文件下载兜底。
                return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
