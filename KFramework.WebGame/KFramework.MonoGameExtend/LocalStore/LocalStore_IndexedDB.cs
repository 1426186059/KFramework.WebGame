using System;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 用户本地数据持久化（账号、密码、设置、存档等），底层走 IndexedDB（见 <see cref="JSBind_IndexedDB"/>）。
    /// <para>
    /// 与资源包缓存分离：资源包/纹理等静态二进制走 Cache Storage（<see cref="AssetBundleManager"/> 默认策略，
    /// 序列化开销更小、可叠加 Service Worker）；而用户数据需要可靠持久化、可被查询，故存 IndexedDB。
    /// 仅浏览器/WASM 环境可用。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// await LocalStore.SetStringAsync("account", "player01");
    /// await LocalStore.SetStringAsync("password", "****");
    /// string account = await LocalStore.GetStringAsync("account");
    /// </code>
    /// </example>
    public static class LocalStore_IndexedDB
    {
        /// <summary>写入字符串值（账号/密码/设置等）。</summary>
        public static Task SetStringAsync(string key, string value)
            => JSBind_IndexedDB.SetStringAsync(key, value);

        /// <summary>读取字符串值；缺失返回空字符串（空字符串与缺失不可区分，需区分请用 <see cref="HasKeyAsync"/>）。</summary>
        public static Task<string> GetStringAsync(string key)
            => JSBind_IndexedDB.GetStringAsync(key);

        /// <summary>是否存在该键。</summary>
        public static Task<bool> HasKeyAsync(string key)
            => JSBind_IndexedDB.HasKeyAsync(key);

        /// <summary>删除键。</summary>
        public static Task RemoveAsync(string key)
            => JSBind_IndexedDB.RemoveKeyAsync(key);

        /// <summary>写入字节值（用户存档、序列化 blob 等）。</summary>
        public static Task SetBytesAsync(string key, byte[] bytes)
            => JSBind_IndexedDB.SetBytesAsync(key, bytes);

        /// <summary>读取字节值；缺失/长度为 0 返回 null。</summary>
        public static async Task<byte[]?> GetBytesAsync(string key)
        {
            int len = await JSBind_IndexedDB.BytesSizeAsync(key).ConfigureAwait(false);
            if (len <= 0) return null;
            byte[] buf = new byte[len];
            int written = await JSBind_IndexedDB.LoadBytesIntoAsync(key, new ArraySegment<byte>(buf)).ConfigureAwait(false);
            return written == len ? buf : null;
        }
    }
}
