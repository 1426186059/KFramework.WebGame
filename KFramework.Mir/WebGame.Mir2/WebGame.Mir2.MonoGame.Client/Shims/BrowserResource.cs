using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using KFramework.MonoGame;

namespace MirEngine
{
    // 浏览器端资源加载：统一经 KFramework.MonoGame.ContentManager 的异步方法取远程资源，
    // 不再自行维护 HttpClient，也不再提供会冻结主线程的同步 GetBytes。
    public class BrowserResource
    {
        // 指向资源服务器（如 http://127.0.0.1:5080/）的 ContentManager，由 Program.Init 经 Configure 注入。
        public static ContentManager Content { get; private set; }

        public static async Task<byte[]> GetBytesAsync(string url)
        {
            string path = NormalizePath(url);
            try
            {
                if (Content != null)
                    return await Content.LoadBytesAsync(path).ConfigureAwait(false);
            }
            catch (Exception) { }

            // Content 未注入时的兜底（非浏览器/调试场景）：直接走 HttpClient。
            try
            {
                using var client = new HttpClient();
                return await client.GetByteArrayAsync(path).ConfigureAwait(false);
            }
            catch (Exception)
            {
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
    }
}
