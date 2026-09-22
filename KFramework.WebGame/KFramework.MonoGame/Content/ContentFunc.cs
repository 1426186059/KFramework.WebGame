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
                byte[] buf = await Caching.Default.LoadAsync(path).ConfigureAwait(false);
                if (buf != null)
                {
                    return buf;
                }

                using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                await Caching.Default.SaveAsync(path, new ArraySegment<byte>(data)).ConfigureAwait(false);
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
