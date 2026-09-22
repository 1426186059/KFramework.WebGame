namespace KFramework.MonoGame
{
    public sealed class Caching : IDisposable
    {
        /// <summary>该缓存的 Cache Storage 名字。</summary>
        public string Name { get; }

        /// <summary>打开（复用）一个命名缓存；同名复用同一 Cache 句柄。</summary>
        public Caching(string name) => Name = name;

        /// <summary>已存字节长度；不存在返回 0（≤0 视为未缓存）。</summary>
        public Task<int> GetSizeAsync(string key)
            => JSBind_CacheStorage.GetCacheSizeAsync(Name, key);

        /// <summary>当前缓存里的条目数。</summary>
        public Task<int> GetCountAsync()
            => JSBind_CacheStorage.GetCacheCountAsync(Name);

        /// <summary>把字节以 Response 形式写入该缓存（按 key，覆盖式）。</summary>
        public Task SaveAsync(string key, ArraySegment<byte> bytes)
            => JSBind_CacheStorage.SaveCacheAsync(Name, key, bytes);

        /// <summary>读出已存字节；不存在返回 null。内部用 size + loadInto（byte[] 不能直接从 JS 返回）。</summary>
        public async Task<byte[]?> LoadAsync(string key)
        {
            int len = await JSBind_CacheStorage.GetCacheSizeAsync(Name, key).ConfigureAwait(false);
            if (len <= 0) return null;
            var buf = new byte[len];
            int written = await JSBind_CacheStorage.LoadCacheAsync(Name, key, new ArraySegment<byte>(buf)).ConfigureAwait(false);
            return written == len ? buf : null;
        }

        /// <summary>删除单个键；返回是否真的删掉了（原本不存在返回 false）。</summary>
        public Task<bool> RemoveAsync(string key)
            => JSBind_CacheStorage.RemoveCacheAsync(Name, key);
        
        public Task<int> RemoveListAsync(string[] removeList)
            => JSBind_CacheStorage.RemoveCacheListAsync(Name, removeList);

        /// <summary>清空并删除整个缓存（含其中的全部条目）。</summary>
        public Task ClearAsync()
            => JSBind_CacheStorage.RemoveAllCacheAsync(Name);

        public void Dispose()
        {
            
        }
    }
}
