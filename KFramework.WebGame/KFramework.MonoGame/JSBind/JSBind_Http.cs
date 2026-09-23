using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// http_func 模块绑定：通用取字节——先查 Cache Storage，未命中再 fetch 下载并写回缓存。
    /// 整条链路都在 JS 侧完成，省掉 HttpClient 方案里
    /// “下载 → 进 WASM byte[] → 再传回 JS 存 Cache”的那次来回搬运。
    /// 实际逻辑见 KFramework.TSEngine/src/http_func.ts（"http_func" 模块）；产物 http_func.js 随 jsengine 进 wwwroot。
    ///
    /// 与 C# 交换字节走「两步」：<see cref="LoadCacheOrDownloadAsync"/> 异步加载完只回长度，
    /// C# 按长度精确分配，再用同步的 <see cref="TakePending"/> 一次拷走。
    /// 这样既不需要预先知道文件大小，也绕开了 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
    /// 业务不要直接调用，请用上层封装（ContentFunc.LoadCacheOrDownloadJsAsync）。
    /// </summary>
    public static partial class JSBind_Http
    {
        /// <summary>
        /// 第一步（异步）：把 <paramref name="name"/>（资源的键 / URL，相对路径按 document.baseURI 解析）
        /// 对应的字节加载好（缓存优先，未命中则下载并写回 Cache Storage），字节暂存在 JS 侧。
        /// </summary>
        /// <param name="useCache">是否启用 Cache Storage（false = 纯下载）。</param>
        /// <returns>
        /// &gt;=0 字节长度——C# 按此值精确分配 byte[]，再调 <see cref="TakePending"/>；
        /// -1 失败（非 2xx / 网络错误）。
        /// </returns>
        [JSImport("loadCacheOrDownloadAsync", "http_func")]
        public static partial Task<int> LoadCacheOrDownloadAsync(string name, bool useCache);

        /// <summary>
        /// 纯下载（不碰 Cache Storage）：只做 fetch，字节暂存在 JS 侧，随后用 <see cref="TakePending"/> 取走。
        ///
        /// 与 <see cref="LoadCacheOrDownloadAsync"/> 的区别：
        /// 1) 不做任何缓存读写 —— 缓存统一交给 C# 侧 Caching 管理（JS 的 Caching.current 与 C# 侧开的
        ///    缓存名不同，这边写只会多占一份磁盘且 C# 读不到）；
        /// 2) 省掉一次 Cache 写入 IO，大文件下载更快。
        /// </summary>
        /// <param name="name">资源 URL（绝对 URL 直接用）。</param>
        /// <returns>&gt;=0 字节长度——C# 按此值分配后调 <see cref="TakePending"/>；-1 失败（非 2xx / 网络错误）。</returns>
        [JSImport("fetchBytesAsync", "http_func")]
        public static partial Task<int> FetchBytesAsync(string name);

        /// <summary>
        /// 第二步（同步）：把 <see cref="LoadCacheOrDownloadAsync"/> 暂存的字节拷进 <paramref name="buffer"/> 并释放暂存。
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
        [JSImport("Dispose", "http_func")]
        public static partial void Dispose();
    }
}
