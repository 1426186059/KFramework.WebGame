using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// 与 C# 交换字节走「两步」：<see cref="FetchBytesAsync"/> 异步下载完只回长度，
    /// C# 按长度精确分配，再用同步的 <see cref="TakePending"/> 一次拷走。
    /// 这样既不需要预先知道文件大小，也绕开了 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
    /// 业务不要直接调用，请用上层封装（ContentFunc.LoadCacheOrDownloadJsAsync）。
    public static partial class JSBind_Http
    {
        /// <summary>
        /// 第一步（异步）：只做 fetch 下载 <paramref name="name"/>，字节暂存在 JS 侧，
        /// 随后用同步的 <see cref="TakePending"/> 取走；完全不碰 Cache Storage。
        ///
        /// 缓存刻意不在 JS 侧读写：JS 的 Caching.current 与 C# 侧开的缓存（如 "WebGame.Mir2.Cache"）
        /// 是两个不同的 Cache Storage 名字，这边写只会多占一份磁盘且 C# 读不到。
        /// 缓存统一由 C# 侧 Caching 管理，同时也省掉一次大文件的 Cache 写入 IO。
        /// </summary>
        /// <param name="name">资源 URL（绝对 URL 直接用）。</param>
        /// <returns>&gt;=0 字节长度——C# 按此值分配后调 <see cref="TakePending"/>；-1 失败（非 2xx / 网络错误）。</returns>
        [JSImport("fetchBytesAsync", "http_func")]
        public static partial Task<int> FetchBytesAsync(string name);

        /// <summary>
        /// 第二步（同步）：把 <see cref="FetchBytesAsync"/> 暂存的字节拷进 <paramref name="buffer"/> 并释放暂存。
        /// 同步调用期间没有 await，所以可以直接用 <c>Span&lt;byte&gt;</c> 的 MemoryView（零拷贝、无需 pin）；
        /// 异步场景才必须用 ArraySegment&lt;byte&gt;。
        /// </summary>
        /// <exception cref="System.Runtime.InteropServices.JavaScript.JSException">
        /// 没有待取字节（没加载过 / 已取走 / 被 FIFO 淘汰），或缓冲装不下——都是调用方用错，JS 直接抛。
        /// </exception>
        [JSImport("takePending", "http_func")]
        public static partial void TakePending(string name, [JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

        /// <summary>
        /// 丢弃暂存在 JS 侧的字节（加载成功后放弃取用，或 <see cref="TakePending"/> 失败时释放内存）。
        /// </summary>
        /// <param name="name">资源键；传空字符串则清空全部暂存。</param>
        [JSImport("releasePending", "http_func")]
        public static partial void ReleasePending(string name);

        //清理所有缓存下载的字节
        [JSImport("releaseAllPending", "http_func")]
        public static partial void releaseAllPending();
    }
}
