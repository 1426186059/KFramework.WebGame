using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// indexeddb 模块绑定：把「用户本地数据」持久化到浏览器 IndexedDB——账号、密码、用户存档、装备数据、设置等。
    /// IndexedDB 支持事务与键值存储，适合需要可靠持久化、可被查询的用户数据；不要在这里存资源包/纹理
    /// （那应走 Cache Storage，见 <see cref="JSBind_CacheStorage"/>）。
    /// 实际逻辑见 KFramework.TSEngine/src/indexeddb.ts（"indexeddb" 模块）。
    ///
    /// 字符串 KV（账号/密码等）直接返回 string；字节 KV（用户存档等）采用「预分配缓冲 + 写回」模式：
    /// 先 <see cref="BytesSizeAsync"/> 探长度，C# 按长度分配 byte[] 后交给 <see cref="LoadBytesIntoAsync"/> 写入。
    /// </summary>
    internal static partial class JSBind_IndexedDB
    {
        /// <summary>写入字符串值（账号/密码/设置等）。</summary>
        [JSImport("setString", "indexeddb")]
        internal static partial Task SetStringAsync(string key, string value);

        /// <summary>读取字符串值；缺失返回空字符串（空字符串与缺失不可区分，需区分请用 <see cref="HasKeyAsync"/>）。</summary>
        [JSImport("getString", "indexeddb")]
        internal static partial Task<string> GetStringAsync(string key);

        /// <summary>是否存在该键。</summary>
        [JSImport("hasKey", "indexeddb")]
        internal static partial Task<bool> HasKeyAsync(string key);

        /// <summary>删除键。</summary>
        [JSImport("removeKey", "indexeddb")]
        internal static partial Task RemoveKeyAsync(string key);

        /// <summary>写入字节值（用户存档、序列化 blob 等）。</summary>
        [JSImport("setBytes", "indexeddb")]
        internal static partial Task SetBytesAsync(string key, byte[] bytes);

        /// <summary>返回已存字节长度；不存在返回 -1。</summary>
        [JSImport("bytesSize", "indexeddb")]
        internal static partial Task<int> BytesSizeAsync(string key);

        /// <summary>把已存字节写入 <paramref name="buffer"/>；返回实际写入长度（缺失返回 -1）。</summary>
        [JSImport("loadBytesInto", "indexeddb")]
        internal static partial Task<int> LoadBytesIntoAsync(string key, byte[] buffer);
    }
}
