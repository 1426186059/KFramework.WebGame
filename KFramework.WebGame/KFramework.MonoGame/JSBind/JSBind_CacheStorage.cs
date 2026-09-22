using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// cachestorage 模块绑定：把资源包字节（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源）持久化到
    /// 浏览器 Cache Storage。相较于 IndexedDB，Cache Storage 以 Response 形式存储二进制资源，序列化开销更小，
    /// 后续可叠加 Service Worker 拦截 fetch 直接返回缓存响应，做到“零解析开销”。
    /// 实际逻辑见 KFramework.TSEngine/src/storage_cachestorage.ts（"cachestorage" 模块）。
    ///
    /// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 <see cref="GetSizeAsync"/>
    /// 探长度，C# 按长度分配 byte[] 后交给 <see cref="LoadIntoAsync"/> 写入，绕开 .NET WASM 不支持 byte[]
    /// 作为返回值的限制（SYSLIB1072）。
    /// </summary>
    public static partial class JSBind_CacheStorage
    {
        /// <summary>返回 Cache Storage 中已存字节长度；不存在返回 -1。</summary>
        [JSImport("size", "cachestorage")]
        public static partial Task<int> GetSizeAsync(string name);

        /// <summary>
        /// 把 Cache Storage 中已存字节写入 <paramref name="buffer"/>；返回实际写入长度（-1 缺失，&lt;=-2 缓冲不足）。
        /// </summary>
        /// <remarks>
        /// 缓冲必须是 <b>MemoryView</b>：<c>byte[]</c> 走 <c>JSType.Array</c> 是<b>复制</b>语义，JS 写进副本的字节
        /// 不会回到托管数组（表现就是拿到全 0）；且本方法跨 await，只能用 ArraySegment（Span 在异步里无效）。
        /// 调用：<c>LoadIntoAsync(name, new ArraySegment&lt;byte&gt;(buf))</c>。
        /// </remarks>
        [JSImport("loadInto", "cachestorage")]
        public static partial Task<int> LoadIntoAsync(string name, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);

        /// <summary>把资源包字节以 Response 形式写入 Cache Storage（按 name 键，覆盖式）。</summary>
        /// <remarks>
        /// 必须走 <b>MemoryView</b>：<c>byte[]</c> 默认按 <c>JSType.Array</c> 逐字节复制成 JS 数组
        ///（大包极慢，且 Array 也不是合法 Response body，会被按字符串存成 “80,75,3,4,...”）。
        /// 本方法跨 await，故用 ArraySegment（Span 在异步里无效）；JS 侧用完会 dispose 解 pin。
        /// 调用：<c>SaveAsync(name, new ArraySegment&lt;byte&gt;(data))</c>。
        /// </remarks>
        [JSImport("save", "cachestorage")]
        public static partial Task SaveAsync(string name, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

        /// <summary>删除单个键；返回是否真的删掉了（原本不存在返回 false）。</summary>
        [JSImport("remove", "cachestorage")]
        public static partial Task<bool> RemoveAsync(string name);

        /// <summary>
        /// 通用 GC：删除不在 <paramref name="keep"/> 白名单里的条目，返回实际删除条数。
        /// 资源文件名带内容哈希（热更一次换一个名字），不 GC 则历史版本无限堆积、最终撑爆 origin 配额。
        /// </summary>
        /// <param name="keep">保留白名单（相对路径或绝对 URL 均可，JS 侧统一归一化后比对）。</param>
        /// <param name="prefix">可选路径前缀（如 "hot_update_res/"），只清理该前缀下的条目；留空表示不限前缀。</param>
        /// <param name="suffix">可选后缀（如 ".web.lib"），只清理该后缀的条目；留空表示不限后缀。</param>
        /// <remarks>前缀与后缀是「与」关系：都给了就必须同时命中，用于只回收资源包而不动同目录下的其它缓存。</remarks>
        [JSImport("prune", "cachestorage")]
        public static partial Task<int> PruneAsync(string[] keep, string prefix, string suffix);
    }
}
