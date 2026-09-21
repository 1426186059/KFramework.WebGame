using System.Net.Http.Headers;
using static System.Net.WebRequestMethods;

namespace KFramework.MonoGame
{
    internal static class ContentFunc
    {
        public static string DecodeUtf8(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
        }

        public static async Task<byte[]> DownloadBytesAsync(HttpClient _http, string relativePath, bool useCache = true, CancellationToken cancellationToken = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, relativePath);
            if (!useCache)
            {
                req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            }
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

    }
}
