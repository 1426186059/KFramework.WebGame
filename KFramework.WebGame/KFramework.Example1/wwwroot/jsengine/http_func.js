// 【依赖 C#】由 KFramework.MonoGame.JSBind_Http 经 [JSImport(module: "http_func")] 调用；
// 产物 http_func.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// JS 层版的 ContentFunc.LoadCacheOrDownloadAsync：「先查 Cache Storage，未命中再 fetch，
// 拿到后写回 Cache」。整条链路都在 JS 侧完成，省掉 HttpClient 方案里
// “下载 → 进 WASM byte[] → 再传回 JS 存 Cache”的那次来回搬运。
//
// 与 C# 换字节用「两步走」，而不是「预分配 + 猜大小」：
//   1. loadCacheOrDownloadAsync(name, useCache) —— 异步，加载完把字节暂存在这里，只把长度回给 C#；
//   2. takePending(name, buffer) —— 同步，C# 按长度精确分配后，一次拷走并释放暂存。
// 好处：C# 不需要预先知道文件大小、也不会出现「缓冲不够重分配再来一次」的二次往返，
// 下载/读缓存始终只发生一次；第二步是同步调用，字节此时已在内存里，没有 await，
// 因此可以直接用 Span<byte> 的 MemoryView（Span 只在同步调用期间有效，异步必须用 ArraySegment）。
//
// 接线：main.ts 里 setModuleImports('http_func', httpFunc)；
// C# 侧 KFramework.MonoGame.JSBind_Http 用 [JSImport("...", "http_func")] 绑定。
import { read, save } from './storage_cachestorage.js';
// 第一步加载完到第二步取走之间的暂存区。
// 设上限，避免调用方取完/放弃后长期占着内存（FIFO 淘汰）。
const PENDING_MAX = 8;
const pending = new Map();
// 存进 pending 并做上限淘汰
function stash(name, bytes) {
    // 先删再插，保证新条目排在队尾（FIFO 淘汰）
    pending.delete(name);
    pending.set(name, bytes);
    while (pending.size > PENDING_MAX) {
        const oldest = pending.keys().next();
        if (oldest.done)
            break;
        pending.delete(oldest.value);
    }
}
/**
 * 第一步（异步）：把字节加载好，只把长度回给 C#。
 * 缓存优先（useCache），未命中则 fetch 下载并写回 Cache Storage；字节暂存在本模块，
 * 随后由同步的 takePending 取走。
 * @param name 资源的键 / URL（相对路径按 document.baseURI 解析，与 Cache Storage 的存键规则一致）
 * @param useCache 是否启用 Cache Storage（false = 纯下载，对应 C# 的 bUseCache）
 * @returns >=0 字节长度（C# 按此值精确分配缓冲）；-1 失败（非 2xx / 网络错误）。
 */
export async function loadCacheOrDownloadAsync(name, useCache) {
    // 缓存命中
    if (useCache) {
        const hit = await read(name);
        if (hit) {
            stash(name, hit);
            return hit.byteLength;
        }
    }
    // 下载
    let res;
    try {
        res = await fetch(name);
    }
    catch {
        return -1;
    }
    if (!res.ok)
        return -1;
    const bytes = new Uint8Array(await res.arrayBuffer());
    // 写回缓存（失败只影响下次命中，不影响本次结果：配额满 / 无痕模式等退化为每次走网络）
    if (useCache) {
        try {
            await save(name, bytes);
        }
        catch {
            // 忽略
        }
    }
    stash(name, bytes);
    return bytes.byteLength;
}
/**
 * 第二步（同步）：把第一步加载好的字节拷进 C# 的缓冲并释放暂存。
 * 同步调用期间没有 await，故 Span<byte> 的 MemoryView 是有效的（异步场景必须用 ArraySegment）。
 * @param buffer C# 按第一步返回的长度分配的缓冲（Span<byte> → MemoryView）
 * @throws 没有待取字节（未加载过 / 已被取走 / 被淘汰），或缓冲装不下——都是调用方用错了，直接抛。
 */
export function takePending(name, buffer) {
    const bytes = pending.get(name);
    if (!bytes)
        throw new Error(`[http_func] takePending: ${name} 没有待取字节`);
    if (bytes.byteLength > buffer.byteLength)
        throw new Error(`[http_func] takePending: ${name} 缓冲不足（需要 ${bytes.byteLength}，实际 ${buffer.byteLength}）`);
    // 同步调用期间没有 await，直接拷进 C# 的缓冲（MemoryView / Uint8Array 的 set 同签名）
    buffer.set(bytes, 0);
    pending.delete(name); // 交付完成，释放
}
// 放弃取字节时释放暂存（如加载成功但业务侧取消）；不传 name 则清空全部。
export function releasePending(name) {
    if (name)
        pending.delete(name);
    else
        pending.clear();
}
export function Dispose() {
    pending.clear();
}
