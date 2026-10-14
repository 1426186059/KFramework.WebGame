using System.Net.Http.Headers;

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
        /// <param name="priority">
        /// 加载优先级（数值<strong>越大越优先</strong>，0 = 普通）。经由 <see cref="ContentLoadScheduler"/> 调度：
        /// 只有拿到并发槽才会真正发起请求，避免几十个资源同时发起造成的队头阻塞。
        /// </param>
        /// 
        public const bool bUseJSHttp = true;
        public const bool bUseCacheInstance = false;
        public static async Task<byte[]> LoadCacheOrDownloadAsync(HttpClient http, string path, Caching mCacheInstance = null, int priority = 0, CancellationToken cancellationToken = default)
        {
            string url = http.BaseAddress + path;
            if (bUseJSHttp)
            {
                return  await LoadCacheOrDownloadJsAsync(url, mCacheInstance, priority, cancellationToken);
            }
            else
            {
                return await LoadCacheOrDownloadAsync_Default(http, url, mCacheInstance, priority, cancellationToken);
            }
        }

        public static async Task<byte[]> LoadCacheOrDownloadAsync_Default(
            HttpClient http, 
            string url, 
            Caching mCacheInstance = null, 
            int priority = 0, 
            CancellationToken cancellationToken = default)
        {
            bool bUseHttpCache = mCacheInstance != null && !bUseCacheInstance;
            try
            {
                if (bUseCacheInstance && mCacheInstance != null)
                {
                    GameProfiler.TestStart();
                    byte[]? buf = await mCacheInstance.LoadAsync(url, cancellationToken).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 读取 {url}");
                    if (buf != null) return buf;
                }

                byte[] data = null;
                const int MaxAttempts = 3;
                int nNextAttemptTime = 1000;
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    data = await ContentLoadScheduler.Default.EnqueueAsync(priority, async _ =>
                    {
                        byte[] buffer = await SendHttpClientAsync(http, url, bUseHttpCache, cancellationToken).ConfigureAwait(false);
                        return buffer;
                    }, cancellationToken).ConfigureAwait(false);

                    if (data != null)
                    {
                        break;
                    }

                    await Task.Delay(nNextAttemptTime * attempt, cancellationToken);
                }

                if (data == null) return data;

                if (bUseCacheInstance && mCacheInstance != null)
                {
                    GameProfiler.TestStart();
                    await mCacheInstance.SaveAsync(url, new ArraySegment<byte>(data)).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 保存 {url} {FmtSize(data.Length)}");
                }
                return data;
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadAsync: BaseURL: {url}  Error: {e.Message}");
            }

            return null;
        }
        
        public static async Task<byte[]> LoadCacheOrDownloadJsAsync(
            string url,
            Caching mCacheInstance, 
            int priority = 0, 
            CancellationToken cancellationToken = default)
        {
            bool bUseHttpCache = mCacheInstance != null && !bUseCacheInstance;
            try
            {
                // 1) 缓存命中（与 HttpClient 版同 key，完全复用）
                if (bUseCacheInstance && mCacheInstance != null)
                {
                    GameProfiler.TestStart();
                    byte[] cached = await mCacheInstance.LoadAsync(url).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 读取 {url}");
                    if (cached != null) return cached;
                }

                // 2) 下载：走 JS fetch（useCache=false，缓存由本方法统一写，避免 JS 侧另存一份）
                byte[] data = null;
                const int MaxAttempts = 3;
                int nNextAttemptTime = 1000;
                for (int attempt = 0; attempt < MaxAttempts; attempt++)
                {
                    data = await ContentLoadScheduler.Default.EnqueueAsync(priority, async _ =>
                    {
                        GameProfiler.TestStart();
                        int len = await JSBind_Http.FetchBytesAsync(url, bUseHttpCache).ConfigureAwait(false);
                        GameProfiler.TestFinishAndLog($"[js] fetch {url} len={len}");

                        if (len < 0) return null;   // -1 = 非 2xx / 网络错误，属真实失败，不回退

                        var buf = new byte[len];
                        try
                        {
                            JSBind_Http.TakePending(url, buf);
                        }
                        catch (Exception takeEx)
                        {
                            PrintTool.Log($"[js] takePending 失败 {url}: {takeEx.Message}");
                            JSBind_Http.ReleasePending(url);
                            return null;
                        }
                        return buf;
                    }, cancellationToken).ConfigureAwait(false);

                    if(data != null)
                    {
                        break;
                    }

                    await Task.Delay(nNextAttemptTime * attempt, cancellationToken);
                }

                if (data == null)
                {
                    return null;
                }

                // 3) 写回缓存（失败不影响本次结果：磁盘紧张 / 配额满时退化为每次走网络）
                if (bUseCacheInstance && mCacheInstance != null)
                {
                    GameProfiler.TestStart();
                    await mCacheInstance.SaveAsync(url, new ArraySegment<byte>(data)).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 保存 {url} {FmtSize(data.Length)}");
                }

                return data;
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadJsAsync: BaseURL: {url}  Error: {e.Message}");
            }

            return null;
        }

        private static string FmtSize(int bytes)
        {
            return bytes >= 1048576 ? $"{bytes / 1048576.0:F1}MB" : $"{bytes / 1024}KB";
        }
        
        public static async Task<byte[]> SendHttpClientAsync(
            HttpClient httpClient,
            string url,
            bool bUseHttpCache = true,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!bUseHttpCache)
            {
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
            }

            GameProfiler.TestStart();
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            GameProfiler.TestFinishAndLog($"[http] 下载 GetAsync耗时: {url}");

            GameProfiler.TestStart();
            byte[] buffer = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            GameProfiler.TestFinishAndLog($"[http] 下载 ReadAsByteArrayAsync 耗时 {url} {FmtSize(buffer.Length)}");
            return buffer;
        }

    }
}
