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
            string tag = http.BaseAddress + path;
            try
            {
                if (bUseCache)
                {
                    GameProfiler.TestStart();
                    byte[] buf = await mCacheInstance.LoadAsync(path).ConfigureAwait(false);
                    GameProfiler.TestFinishAndLog($"[cache] mCacheInstance 读取 {tag}");

                    if (buf != null) return buf;

                    byte[] data = await DownloadAsync(http, path, tag, cancellationToken).ConfigureAwait(false);
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
                    return await DownloadAsync(http, path, tag, cancellationToken).ConfigureAwait(false);
                }
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadAsync: BaseURL: {tag}  Error: {e.Message}");
            }

            return null;
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
