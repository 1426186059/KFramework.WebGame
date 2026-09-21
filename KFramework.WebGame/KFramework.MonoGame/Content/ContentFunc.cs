using System.Net.Http;

namespace KFramework.MonoGame
{
    internal static class ContentFunc
    {
        public static string DecodeUtf8(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return System.Text.Encoding.UTF8.GetString(data, start, data.Length - start);
        }

        /// <summary>
        /// 纯下载：按 HttpClient.BaseAddress 解析 <paramref name="path"/>，发起 GET 并返回原始字节。
        /// <b>不设置任何浏览器 HTTP 缓存头</b>——HTTP 缓存是否生效完全由服务器响应头 Cache-Control 决定，客户端无法强制。
        /// 本地持久化统一走 Cache Storage（<see cref="JSBind_CacheStorage"/>），与 HttpClient 无关：
        /// 松散文件见 <see cref="LoadViaCacheStorageAsync"/>，AssetBundle 见 AssetBundleManager。
        /// <para>三种缓存的区别：① .NET 的 HttpClient 本身不缓存（托管层无缓存机制）；② 浏览器 HTTP 缓存（fetch 按响应 Cache-Control 自动生效，无查询 API）；
        /// ③ Cache Storage（caches.open，显式读写、可查询，是本框架的本地缓存方案）。</para>
        /// </summary>
        public static async Task<byte[]> DownloadBytesAsync(HttpClient http, string path, CancellationToken cancellationToken = default)
        {
            using var resp = await http.GetAsync(path, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// “Cache Storage 优先”的统一加载：先查本地 Cache Storage（键 <paramref name="cacheKey"/>），命中即返回；
        /// 未命中则经 <see cref="DownloadBytesAsync"/> 远程下载并把字节写回 Cache Storage 以备复用，最后返回。
        /// 用于松散文件（包外文本/字节/图片）的持久化；AssetBundle 自身另有“查 + 哈希校验 + 写回”逻辑（见 AssetBundleManager）。
        /// <para>注意：松散文件以「路径」为键，不做事后哈希校验；若服务器更新了同路径文件，需靠带版本/哈希的路径来失效旧缓存。</para>
        /// </summary>
        public static async Task<byte[]> LoadViaCacheStorageAsync(HttpClient http, string cacheKey, CancellationToken cancellationToken = default)
        {
            int len = await JSBind_CacheStorage.GetSizeAsync(cacheKey).ConfigureAwait(false);
            if (len > 0)
            {
                var buf = new byte[len];
                int written = await JSBind_CacheStorage.LoadIntoAsync(cacheKey, buf).ConfigureAwait(false);
                if (written == len) return buf;
            }
            var data = await DownloadBytesAsync(http, cacheKey, cancellationToken).ConfigureAwait(false);
            await JSBind_CacheStorage.SaveAsync(cacheKey, data).ConfigureAwait(false);
            return data;
        }
    }
}
