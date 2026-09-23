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
        /// 加载优先级（数值越小越先执行，0 最高）。经由 <see cref="ContentLoadScheduler"/> 调度：
        /// 只有拿到并发槽才会真正发起请求，避免几十个资源同时发起造成的队头阻塞。
        /// </param>
        /// 
        public const bool bUseJSHttp = true;
        public static async Task<byte[]> LoadCacheOrDownloadAsync(HttpClient http, string path, bool bUseCache = false, Caching mCacheInstance = null, int priority = 0, CancellationToken cancellationToken = default)
        {
            if (bUseJSHttp)
            {
                string url = http.BaseAddress + path;
                return  await LoadCacheOrDownloadJsAsync(url, bUseCache, mCacheInstance, priority, cancellationToken);
            }
            else
            {
                return await LoadCacheOrDownloadAsync_Default(http, path, bUseCache, mCacheInstance, priority, cancellationToken);
            }
        }

        public static async Task<byte[]> LoadCacheOrDownloadAsync_Default(HttpClient http, string path, bool bUseCache = false, Caching mCacheInstance = null, int priority = 0, CancellationToken cancellationToken = default)
        { 
            string tag = http.BaseAddress + path;
            try
            {
                if (bUseCache && mCacheInstance != null)
                {
                    GameProfiler.TestStart();
                    byte[] buf = await mCacheInstance.LoadAsync(path).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 读取 {tag}");

                    if (buf != null) return buf;

                    // 下载统一走调度器排队，避免同时打满浏览器连接
                    byte[] data = await ContentLoadScheduler.Default
                        .EnqueueAsync(priority, ct => DownloadAsync(http, path, tag, ct), cancellationToken)
                        .ConfigureAwait(false);
                    if (data == null) return null;

                    GameProfiler.TestStart();
                    try
                    {
                        await mCacheInstance.SaveAsync(path, new ArraySegment<byte>(data)).ConfigureAwait(false);
                        GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 保存 {tag} {FmtSize(data.Length)}");
                    }
                    catch (Exception saveEx)
                    {
                        // 缓存写入失败绝不能丢掉已下载的字节：之前该异常会一路抛到外层 catch，
                        // 使函数返回 null —— 明明下载成功的 Lib 被判成"远程空"，地图画不全。
                        GameProfiler.TestFinishAndLog($"[cache] 保存失败(忽略) {tag}");
                        PrintTool.Log($"[cache] 保存失败，仍使用已下载数据 {tag}: {saveEx.Message}");
                    }
                    return data;
                }
                else
                {
                    return await ContentLoadScheduler.Default
                        .EnqueueAsync(priority, ct => DownloadAsync(http, path, tag, ct), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadAsync: BaseURL: {tag}  Error: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// JS 侧链路版加载（大文件首选）。
        ///
        /// 为什么需要它：.NET WASM 自带的 HttpClient（http_wasm_fetch）对大响应体极慢 ——
        /// 实测 41.6MB 要 19 秒，而 6.8MB 只要 0.09 秒（超过约 8MB 后断崖式变慢）。
        /// 改为 JS 侧 fetch 后字节先留在 JS 堆，再由同步的 takePending 一次拷进 C#，绕开该瓶颈。
        ///
        /// 缓存策略刻意保持与 HttpClient 版一致：读写都走传入的 <paramref name="mCacheInstance"/>，
        /// 用同一个 key（完整 URL）。这样不会在 JS 侧另存一份（JS 的 Caching.current 与本缓存名不同），
        /// 磁盘不翻倍、缓存也能完全复用。
        /// </summary>
        public static async Task<byte[]> LoadCacheOrDownloadJsAsync(string url, bool bUseCache,
            Caching mCacheInstance, int priority = 0, CancellationToken cancellationToken = default)
        {
            // 1) 缓存命中（与 HttpClient 版同 key，完全复用）
            if (bUseCache && mCacheInstance != null)
            {
                GameProfiler.TestStart();
                byte[] cached = await mCacheInstance.LoadAsync(url).ConfigureAwait(false);
                GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 读取 {url}");
                if (cached != null) return cached;
            }

            // 2) 下载：走 JS fetch（useCache=false，缓存由本方法统一写，避免 JS 侧另存一份）
            byte[] data = null;
            try
            {
                data = await ContentLoadScheduler.Default.EnqueueAsync(priority, async _ =>
                {
                    GameProfiler.TestStart();
                    // 纯 fetch：不碰 Cache Storage（缓存统一由下方 C# Caching 写），省掉一次缓存写入 IO
                    int len = await JSBind_Http.FetchBytesAsync(url).ConfigureAwait(false);
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
            }
            catch (Exception jsEx)
            {
                PrintTool.Log($"[js] 链路不可用，回退 HttpClient {url}: {jsEx.Message}");
            }

            if (data == null) return data;

            // 3) 写回缓存（失败不影响本次结果：磁盘紧张 / 配额满时退化为每次走网络）
            if (bUseCache && mCacheInstance != null)
            {
                GameProfiler.TestStart();
                try
                {
                    await mCacheInstance.SaveAsync(url, new ArraySegment<byte>(data)).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 保存 {url} {FmtSize(data.Length)}");
                }
                catch (Exception saveEx)
                {
                    GameProfiler.TestFinishAndLog($"[cache] 保存失败(忽略) {url}");
                    PrintTool.Log($"[cache] 保存失败，仍使用已下载数据 {url}: {saveEx.Message}");
                }
            }

            return data;
        }

        /// <summary>
        /// 下载并分段计时（诊断用，不改变行为）。耗时一律用 GameProfiler 输出（单位：秒）。
        /// [http] 响应头 = GetAsync 到拿到响应头（含排队/连接/TTFB）；
        /// [http] 读body = 读取响应体到字节数组的耗时。
        /// 分开记是为了分辨“网络慢”还是“wasm 内把字节读进托管堆慢”。
        /// </summary>
        private static async Task<byte[]> DownloadAsync(HttpClient http, string path, string tag, CancellationToken cancellationToken)
        {
            GameProfiler.TestStart();
            using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            GameProfiler.TestFinishAndLog($"[http] 下载 GetAsync耗时: {tag}");

            GameProfiler.TestStart();
            byte[] data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            GameProfiler.TestFinishAndLog($"[http] 下载 ReadAsByteArrayAsync 耗时 {tag} {FmtSize(data.Length)}");
            return data;
        }

        private static string FmtSize(int bytes)
        {
            return bytes >= 1048576 ? $"{bytes / 1048576.0:F1}MB" : $"{bytes / 1024}KB";
        }
    }
}
