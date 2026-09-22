using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// cachestorage
    /// </summary>
    public static partial class JSBind_CacheStorage
    {
        /// <summary>返回 Cache Storage 中已存字节长度；不存在返回 -1。</summary>
        [JSImport("size", "cachestorage")]
        public static partial Task<int> GetCacheSizeAsync(string name);

        [JSImport("loadInto", "cachestorage")]
        public static partial Task<int> LoadCacheAsync(string name, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);

        [JSImport("save", "cachestorage")]
        public static partial Task SaveCacheAsync(string name, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

        /// <summary>删除单个键；返回是否真的删掉了（原本不存在返回 false）。</summary>
        [JSImport("remove", "cachestorage")]
        public static partial Task<bool> RemoveCacheAsync(string name);
        
        [JSImport("prune", "cachestorage")]
        public static partial Task<int> DeleteCacheListAsync(string[] keep);

        [JSImport("prune", "cachestorage")]
        public static partial Task<int> DeleteAllCacheAsync();
    }
}
