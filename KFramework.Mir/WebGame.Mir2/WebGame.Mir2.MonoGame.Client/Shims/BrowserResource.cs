using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MirEngine
{
    // 浏览器端资源加载（自包含：基于 HttpClient/fetch，不再依赖旧 main.js 的 mir.* JS）。
    public class BrowserResource
    {
        public static async Task<byte[]> GetBytesAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                return await client.GetByteArrayAsync(url);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static byte[] GetBytes(string url) => throw new NotImplementedException();

        public static string ResolveUrl(string fileName) => fileName;

        private static string _baseUrl = "/";
        public static string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = value;
        }

        public static void Log(string msg)
        {
            try { Console.WriteLine(msg); }
            catch { }
        }
    }
}
