namespace KFramework.MonoGame
{
    internal static class ContentFunc
    {
        public static string DecodeUtf8(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
        }

        public static async Task<byte[]> LoadCacheOrDownloadAsync(HttpClient http, string path, bool bUseCache = false, CancellationToken cancellationToken = default)
        {
            if (bUseCache)
            {
                int len = await JSBind_CacheStorage.GetSizeAsync(path).ConfigureAwait(false);
                if (len > 0)
                {
                    var buf = new byte[len];
                    int written = await JSBind_CacheStorage.LoadIntoAsync(path, buf).ConfigureAwait(false);
                    if (written == len) return buf;
                }

                using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await JSBind_CacheStorage.SaveAsync(path, data).ConfigureAwait(false);
                return data;
            }
            else
            {
                using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
