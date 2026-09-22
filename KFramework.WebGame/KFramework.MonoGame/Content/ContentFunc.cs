namespace KFramework.MonoGame
{
    internal static class ContentFunc
    {

        public static string DecodeUtf8(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
        }

        private const bool bUseJSHttp = false;

        /// <param name="expectedSize">
        /// 已知的准确字节数（如资源包清单里的 Size）。给了的会做一次校验：
        /// 缓存条目长度对不上 = 脏数据（历史版本写坏的缓存），删掉重下一次，自愈。
        /// </param>
        public static async Task<byte[]> LoadCacheOrDownloadAsync(HttpClient http, string path, bool bUseCache = false, CancellationToken cancellationToken = default, long expectedSize = 0)
        {
            for (int attempt = 0; ; attempt++)
            {
                byte[] bytes = await LoadCacheOrDownloadOnceAsync(http, path, bUseCache, cancellationToken).ConfigureAwait(false);
                if (expectedSize <= 0 || bytes.Length == expectedSize) return bytes;
                // 长度不符：缓存被写坏过（早年版本的 bug / 半截数据）。删掉条目后再来一次，第二次必走网络。
                if (attempt > 0)
                    throw new IOException($"[KFramework.MonoGame] 资源长度不符：{path}（期望 {expectedSize}，实际 {bytes.Length}）");
                PrintTool.Log($"[KFramework.MonoGame] 缓存条目长度不符，已清除重下：{path}（期望 {expectedSize}，实际 {bytes.Length}）");
                await JSBind_CacheStorage.RemoveAsync(path).ConfigureAwait(false);
            }
        }

        private static async Task<byte[]> LoadCacheOrDownloadOnceAsync(HttpClient http, string path, bool bUseCache, CancellationToken cancellationToken)
        {
            if (bUseJSHttp)
            {
                int length = await JSBind_Http.LoadCacheOrDownloadAsync(path, bUseCache).ConfigureAwait(false);
                if (length < 0) throw new IOException($"[KFramework.MonoGame] 取资源失败：{path}");
                if (length == 0)
                {
                    JSBind_Http.ReleasePending(path);
                    return [];
                }

                var buffer = new byte[length];
                try
                {
                    JSBind_Http.TakePending(path, buffer);
                }
                catch
                {
                    JSBind_Http.ReleasePending(path); // 取失败就别让暂存一直占着 JS 内存
                    throw;
                }
                return buffer;
            }

            if (bUseCache)
            {
                int len = await JSBind_CacheStorage.GetSizeAsync(path).ConfigureAwait(false);
                if (len > 0)
                {
                    var buf = new byte[len];
                    int written = await JSBind_CacheStorage.LoadIntoAsync(path, new ArraySegment<byte>(buf)).ConfigureAwait(false);
                    if (written == len) return buf;
                }

                using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await JSBind_CacheStorage.SaveAsync(path, new ArraySegment<byte>(data)).ConfigureAwait(false);
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
