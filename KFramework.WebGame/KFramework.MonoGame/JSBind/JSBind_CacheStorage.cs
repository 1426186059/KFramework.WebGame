using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// cachestorage 模块绑定：把资源包字节（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源）持久化到
    /// 浏览器 Cache Storage。相较于 IndexedDB，Cache Storage 以 Response 形式存储二进制资源，序列化开销更小，
    /// 后续可叠加 Service Worker 拦截 fetch 直接返回缓存响应，做到“零解析开销”。
    /// 实际逻辑见 KFramework.TSEngine/src/storage_cachestorage.ts（"cachestorage" 模块）。
    ///
    /// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 <see cref="GetCacheSizeAsync"/>
    /// 探长度，C# 按长度分配 byte[] 后交给 <see cref="LoadCacheAsync"/> 写入，绕开 .NET WASM 不支持 byte[]
    /// 作为返回值的限制（SYSLIB1072）。
    ///
    /// 这里的每个方法都按“缓存名”操作（对应 TS 侧同名模块函数），业务侧请直接用 <see cref="Caching"/> 封装，
    /// 不必直接调用本类（例：<c>Caching.Default.SaveAsync(key, bytes)</c>）。
    /// </summary>
    public static partial class JSBind_CacheStorage
    {
        /// <summary>返回某缓存中已存字节长度；不存在返回 -1。</summary>
        [JSImport("GetCacheSizeAsync", "cachestorage")]
        public static partial Task<int> GetCacheSizeAsync(string cacheName, string key);

        /// <summary>返回某缓存中的条目数。</summary>
        [JSImport("GetCacheCountAsync", "cachestorage")]
        public static partial Task<int> GetCacheCountAsync(string cacheName);

        /// <summary>把已存字节写入 buffer（必须是 MemoryView/ArraySegment，否则 byte[] 复制语义会让写回字节丢在 JS 副本）；返回写入长度（-1 缺失，&lt;=-2 缓冲不足）。</summary>
        [JSImport("LoadCacheAsync", "cachestorage")]
        public static partial Task<int> LoadCacheAsync(string cacheName, string key, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);

        /// <summary>把字节以 Response 形式写入缓存（覆盖式；必须走 MemoryView/ArraySegment，byte[] 会被按字符串存坏）。</summary>
        [JSImport("SaveCacheAsync", "cachestorage")]
        public static partial Task SaveCacheAsync(string cacheName, string key, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

        /// <summary>删除某缓存里的单个键；返回是否真的删掉了（原本不存在返回 false）。</summary>
        [JSImport("RemoveCacheAsync", "cachestorage")]
        public static partial Task<bool> RemoveCacheAsync(string cacheName, string key);

        /// <summary>通用 GC：删除 removeList 中列出的条目，返回实际删除条数。</summary>
        [JSImport("RemoveCacheListAsync", "cachestorage")]
        public static partial Task<int> RemoveCacheListAsync(string cacheName, string[] removeList);

        /// <summary>清空并删除整个缓存（含其中的全部条目）。</summary>
        [JSImport("RemoveAllCacheAsync", "cachestorage")]
        public static partial Task RemoveAllCacheAsync(string cacheName);
    }
}
