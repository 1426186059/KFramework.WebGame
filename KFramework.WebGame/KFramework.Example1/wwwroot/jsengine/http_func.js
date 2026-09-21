// 【依赖 C#】由 KFramework.MonoGame.JSBind_Http 经 [JSImport(module: "http_func")] 调用；
// 产物 http_func.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// JS 层版的 ContentFunc.LoadCacheOrDownloadAsync：「先查 Cache Storage，未命中再 fetch，
// 拿到后写回 Cache」。整条链路都在 JS 侧完成，字节只在最后一刻写进 C# 预分配的缓冲，
// 省掉 HttpClient 方案里“下载 → 进 WASM byte[] → 再传回 JS 存 Cache”的那次来回搬运。
//
// 与 C# 交换字节沿用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：C# 先备好 byte[]，
// JS 往里写，绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
//
// 接线：main.ts 里 setModuleImports('http_func', httpFunc)；
// C# 侧 KFramework.MonoGame.JSBind_Http 用 [JSImport("loadCacheOrDownloadAsync", "http_func")] 绑定。
import { read, save } from './storage_cachestorage.js';
// 缓冲不足时暂存已取到的字节：调用方按返回的长度重新分配后再调一次即可取回，不会重复下载。
// 设上限，避免调用方放弃重试后长期占着内存。
const PENDING_MAX = 8;
const pending = new Map();
// 写入缓冲；缓冲不够就暂存起来，并返回 -(所需长度)
function writeInto(name, bytes, buffer) {
    if (bytes.byteLength > buffer.length) {
        // 先删再插，保证新条目排在队尾（FIFO 淘汰）
        pending.delete(name);
        pending.set(name, bytes);
        while (pending.size > PENDING_MAX) {
            const oldest = pending.keys().next();
            if (oldest.done)
                break;
            pending.delete(oldest.value);
        }
        return -bytes.byteLength;
    }
    buffer.set(bytes);
    return bytes.byteLength;
}
/**
 * 通用取字节：缓存优先，未命中则下载并写回缓存（对齐 C# ContentFunc.LoadCacheOrDownloadAsync）。
 * @param name 资源的键 / URL（相对路径按 document.baseURI 解析，与 Cache Storage 的存键规则一致）
 * @param buffer C# 预分配的缓冲
 * @param useCache 是否启用 Cache Storage（false = 纯下载，对应 C# 的 bUseCache）
 * @returns >=0 实际写入字节数；-1 失败（非 2xx / 网络错误）；
 *          <=-2 缓冲不足，-(返回值) 即所需长度，本次字节已暂存，
 *          按该长度重新分配后再调一次即可立即取回。
 */
export async function loadCacheOrDownloadAsync(name, buffer, useCache = false) {
    // 1) 上一次「缓冲不足」留下的字节，优先直接交付（不重新下载 / 不重新读缓存）
    const stashed = pending.get(name);
    if (stashed) {
        pending.delete(name);
        return writeInto(name, stashed, buffer);
    }
    // 2) 缓存命中
    if (useCache) {
        const hit = await read(name);
        if (hit)
            return writeInto(name, hit, buffer);
    }
    // 3) 下载
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
    // 4) 写回缓存（失败只影响下次命中，不影响本次结果）
    if (useCache) {
        try {
            await save(name, bytes);
        }
        catch {
            // 配额满 / 无痕模式等：忽略，退化为「每次走网络」
        }
    }
    return writeInto(name, bytes, buffer);
}
// 丢弃暂存的字节（调用方放弃重试时释放内存）；不传 name 则清空全部。
export function releasePending(name) {
    if (name)
        pending.delete(name);
    else
        pending.clear();
}
