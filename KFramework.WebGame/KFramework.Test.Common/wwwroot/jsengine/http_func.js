// 【依赖 C#】由 KFramework.MonoGame.JSBind_Http 经 [JSImport(module: "http_func")] 调用；
// 产物 http_func.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// JS 层版的取字节链路：只做 fetch，字节先留在 JS 堆，再由同步的 takePending 一次拷进 C#，
// 绕开 .NET WASM 自带 HttpClient（http_wasm_fetch）对大响应体断崖式变慢的问题
// （实测 41.6MB 要 19 秒，而 6.8MB 只要 0.09 秒，超过约 8MB 后急剧劣化）。
//
// 与 C# 交换字节走「两步走」，而不是「预分配 + 猜大小」：
//   1. fetchBytesAsync(name) —— 异步，下载完把字节暂存在这里，只把长度回给 C#；
//   2. takePending(name, buffer) —— 同步，C# 按长度精确分配后，一次拷走并释放暂存。
// 好处：C# 不需要预先知道文件大小、也不会出现「缓冲不够重分配再来一次」的二次往返，
// 下载始终只发生一次；第二步是同步调用，字节此时已在内存里，没有 await，
// 因此可以直接用 Span<byte> 的 MemoryView_Span（Span 只在同步调用期间有效，异步必须用 ArraySegment）。
//
// 缓存刻意不在 JS 侧读写：JS 的 Caching.current 与 C# 侧开的缓存（如 "WebGame.Mir2.Cache"）
// 是两个不同的 Cache Storage 名字，这边写只会多占一份磁盘且 C# 读不到，纯属浪费。
// 缓存统一由 C# 侧 Caching 管理，同时也少了一次大文件的 Cache 写入 IO。
//
// 接线：main.ts 里 setModuleImports('http_func', httpFunc)；
// C# 侧 KFramework.MonoGame.JSBind_Http 用 [JSImport("...", "http_func")] 绑定。
// 第一步下载完到第二步取走之间的暂存区。刻意不做数量上限与淘汰：
// 条目生命周期极短（fetch 完 → C# 立刻 takePending 取走），设上限淘汰反而可能
// 误伤「已下载但还没取走」的条目，让 takePending 抛「没有待取字节」。
// 释放靠三条路径：takePending 交付后删除、releasePending 主动放弃、Dispose 清空。
const pending = new Map();
/**
 * fetch 的 RequestCache 取值（规范定义的全集），用常量代替魔法字符串 —— 拼错值时编译当场报错。
 * C# 侧若要按名字指定，也应当用这几个字符串（见 JSBind_Http 的参数注释）。
 */
export const CacheMode = {
    /** 浏览器默认规则：按 HTTP 响应头（Cache-Control / ETag…）决定要不要复用缓存。 */
    Default: 'default',
    /** 不读也不写 HTTP 缓存，每次都走网络。 */
    NoStore: 'no-store',
    /** 跳过缓存直接回源，并把新响应写回缓存。 */
    Reload: 'reload',
    /** 每次都回源校验（服务端有 ETag 时可走 304，省流量）。 */
    NoCache: 'no-cache',
    /** 强制用缓存：命中就用（【过期也用】），没有才回源。 */
    ForceCache: 'force-cache',
    /** 只用缓存，没有就按失败处理（不发起网络请求）。 */
    OnlyIfCached: 'only-if-cached',
};
/**
 * 第一步（异步）：只做 fetch，字节暂存在 pending，等 takePending 取走；完全不碰 Cache Storage。
 *
 * @param name 资源 URL（绝对 URL 直接用；相对路径按 document.baseURI 解析）
 * @param bCache 是否使用 HTTP 缓存：
 *        true（默认）—— 强制走缓存（fetch 的 cache:'force-cache'）：缓存里有就用（【过期也用】），没有才回源。
 *                        注意这意味着：只要浏览器缓存里还留着旧的那份，就一直用旧的 ——
 *                        只有 URL 自带版本号 / 内容指纹（每次发版 URL 都变）时这样才是安全的。
 *        false       —— 完全不用缓存（fetch 的 cache:'no-store'）：不读也不写，每次都走网络。
 *                        代价是这次下载的新响应也不会被缓存，下次仍要重新下载。
 * @returns >=0 字节长度（C# 按此值分配后调 takePending）；-1 失败（非 2xx / 网络错误）
 */
export async function fetchBytesAsync(name, bCache = true) {
    let res;
    try {
        res = await fetch(name, { cache: bCache ? CacheMode.ForceCache : CacheMode.NoStore });
    }
    catch {
        return -1;
    }
    if (!res.ok)
        return -1;
    const bytes = new Uint8Array(await res.arrayBuffer());
    pending.set(name, bytes);
    return bytes.byteLength;
}
/**
 * 第二步（同步）：把第一步下载好的字节拷进 C# 的缓冲并释放暂存。
 * 同步调用期间没有 await，故 Span<byte> 的 MemoryView_Span 是有效的（异步场景必须用 ArraySegment）。
 * @param buffer C# 按第一步返回的长度分配的缓冲（Span<byte> → MemoryView_Span）
 * @throws 没有待取字节（没下载过 / 已被取走），或缓冲装不下——都是调用方用错了，直接抛。
 */
export function takePending(name, buffer) {
    const bytes = pending.get(name);
    if (!bytes)
        throw new Error(`[http_func] takePending: ${name} 没有待取字节`);
    if (bytes.byteLength > buffer.byteLength)
        throw new Error(`[http_func] takePending: ${name} 缓冲不足（需要 ${bytes.byteLength}，实际 ${buffer.byteLength}）`);
    // 同步调用期间没有 await，直接拷进 C# 的缓冲（MemoryView_Span / Uint8Array 的 set 同签名）
    buffer.set(bytes, 0);
    pending.delete(name); // 交付完成，释放
}
// 放弃取字节时释放暂存（如加载成功但业务侧取消）；不传 name 则清空全部。
export function releasePending(name) {
    if (name)
        pending.delete(name);
}
// 清空全部暂存字节（游戏退出 / 释放资源时调用）。
export function releaseAllPending() {
    pending.clear();
}
