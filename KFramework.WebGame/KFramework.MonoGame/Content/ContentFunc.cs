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
            try
            {
                if (bUseCache)
                {
                    long tRead = Environment.TickCount64;
                    byte[] buf = await mCacheInstance.LoadAsync(path).ConfigureAwait(false);
                    if (buf != null)
                    {
                        PrintTool.Log($"[cache] 命中 {http.BaseAddress}{path} {buf.Length / 1024}KB 读取耗时 {Environment.TickCount64 - tRead}ms");
                        return buf;
                    }

                    long tDown = Environment.TickCount64;
                    using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    var data = await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    long downMs = Environment.TickCount64 - tDown;

                    // 计时诊断：只记录耗时，不改变任何行为（先确认“加载慢”到底慢在下载还是写缓存）
                    long tSave = Environment.TickCount64;
                    await mCacheInstance.SaveAsync(path, new ArraySegment<byte>(data)).ConfigureAwait(false);
                    PrintTool.Log($"[cache] 写入 {http.BaseAddress}{path} {data.Length / 1024}KB 下载 {downMs}ms 写缓存 {Environment.TickCount64 - tSave}ms");
                    return data;
                }
                else
                {
                    using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch(Exception e)
            {
                PrintTool.LogError($"LoadCacheOrDownloadAsync: BaseURL: {http.BaseAddress}   Path:{path}  Error: {e.Message}");
            }

            return null;
        }
    }
}
