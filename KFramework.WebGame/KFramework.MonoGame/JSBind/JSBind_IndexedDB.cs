using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// indexeddb 模块绑定：把「用户本地数据」持久化到浏览器 IndexedDB——账号、密码、用户存档、装备数据、设置等。
    /// IndexedDB 支持事务与键值存储，适合需要可靠持久化、可被查询的用户数据；不要在这里存资源包/纹理
    /// （那应走 Cache Storage，见 <see cref="JSBind_CacheStorage"/>）。
    /// 实际逻辑见 KFramework.TSEngine/src/storage_indexeddb.ts（"indexeddb" 模块）。
    ///
    /// 字符串 KV（账号/密码等）直接返回 string；字节 KV（用户存档等）采用「预分配缓冲 + 写回」模式：
    /// 先 <see cref="BytesSizeAsync"/> 探长度，C# 按长度分配 byte[] 后交给 <see cref="LoadBytesIntoAsync"/> 写入。
    /// </summary>
    public static partial class JSBind_IndexedDB
    {
        /// <summary>写入字符串值（账号/密码/设置等）。</summary>
        [JSImport("setString", "indexeddb")]
        public static partial Task SetStringAsync(string key, string value);

        /// <summary>读取字符串值；缺失返回空字符串（空字符串与缺失不可区分，需区分请用 <see cref="HasKeyAsync"/>）。</summary>
        [JSImport("getString", "indexeddb")]
        public static partial Task<string> GetStringAsync(string key);

        /// <summary>是否存在该键。</summary>
        [JSImport("hasKey", "indexeddb")]
        public static partial Task<bool> HasKeyAsync(string key);

        /// <summary>删除键。</summary>
        [JSImport("removeKey", "indexeddb")]
        public static partial Task RemoveKeyAsync(string key);

        /// <summary>写入字节值（用户存档、序列化 blob 等）。</summary>
        [JSImport("setBytes", "indexeddb")]
        public static partial Task SetBytesAsync(string key, byte[] bytes);

        /// <summary>返回已存字节长度；不存在返回 -1。</summary>
        [JSImport("bytesSize", "indexeddb")]
        public static partial Task<int> BytesSizeAsync(string key);

        /// <summary>把已存字节写入 buffer（MemoryView/ArraySegment，避免 byte[] 复制语义丢字节）；返回写入长度（缺失 -1）。</summary>
        [JSImport("loadBytesInto", "indexeddb")]
        public static partial Task<int> LoadBytesIntoAsync(string key, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);
    }
}
