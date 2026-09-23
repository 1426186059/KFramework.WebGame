namespace KFramework.MonoGame
{
    internal static class ContentFunc
    {
        public static string DecodeUtf8(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
        }
        
        /// <param name="expectedSize">
        /// 已知的准确字节数（如资源包清单里的 Size）。给了的会做一次校验：
        /// 缓存条目长度对不上 = 脏数据（历史版本写坏的缓存），删掉重下一次，自愈。
        /// </param>
        public static async Task<byte[]> LoadCacheOrDownloadAsync(HttpClient http, string path, bool bUseCache = false, Caching mCacheInstance = null, CancellationToken cancellationToken = default)
        {
            string full = http.BaseAddress + path;
            try
            {
                if (bUseCache)
                {
                    long tRead = Environment.TickCount64;
                    byte[] buf = await mCacheInstance.LoadAsync(path).ConfigureAwait(false);
                    long readMs = Environment.TickCount64 - tRead;

                    if (buf != null)
                    {
                        PrintTool.Log($"[cache] 命中 {full} {FmtSize(buf.Length)} | Cache读取 {readMs}ms");
                        return buf;
                    }

                    var dl = await DownloadAsync(http, path, cancellationToken).ConfigureAwait(false);
                    if (dl.data == null) return null;

                    long tSave = Environment.TickCount64;
                    await mCacheInstance.SaveAsync(path, new ArraySegment<byte>(dl.data)).ConfigureAwait(false);
                    long saveMs = Environment.TickCount64 - tSave;

                    PrintTool.Log($"[cache] 未命中 {full} {FmtSize(dl.data.Length)} | " +
                                  $"Cache读取(未命中) {readMs}ms | HTTP响应头 {dl.headMs}ms | 读body {dl.bodyMs}ms | Cache保存 {saveMs}ms");
                    return dl.data;
                }
                else
                {
                    var dl = await DownloadAsync(http, path, cancellationToken).ConfigureAwait(false);
                    PrintTool.Log($"[http] {full} {FmtSize(dl.data?.Length ?? 0)} | " +
                                  $"HTTP响应头 {dl.headMs}ms | 读body {dl.bodyMs}ms");
                    return dl.data;
                }
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadAsync: BaseURL: {http.BaseAddress}   Path:{path}  Error: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// 下载并分段计时（诊断用，不改变行为）：
        /// headMs = GetAsync 到拿到响应头（含排队/连接/TTFB）；bodyMs = 读取响应体到字节数组的耗时。
        /// 分开记是为了分辨“网络慢”还是“wasm 内把字节读进托管堆慢”。
        /// </summary>
        private static async Task<(byte[] data, long headMs, long bodyMs)> DownloadAsync(HttpClient http, string path, CancellationToken cancellationToken)
        {
            long t0 = Environment.TickCount64;
            using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            long headMs = Environment.TickCount64 - t0;

            long t1 = Environment.TickCount64;
            byte[] data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            long bodyMs = Environment.TickCount64 - t1;

            return (data, headMs, bodyMs);
        }

        private static string FmtSize(int bytes)
        {
            return bytes >= 1048576 ? $"{bytes / 1048576.0:F1}MB" : $"{bytes / 1024}KB";
        }
    }
}
