// 【依赖 C#】由 KFramework.MonoGame.JSBind_CacheStorage 经 [JSImport(module: "cachestorage")] 调用；
// 产物 storage_cachestorage.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 把资源包（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源字节）持久化到浏览器 Cache Storage。
// 与 IndexedDB 相比：Cache Storage 以 Response 形式存储，专为二进制资源设计、序列化/反序列化开销更小，
// 后续若要叠加 Service Worker 拦截 fetch 直接返回缓存响应，可做到“零解析开销”的无缝升级。
//
// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 size() 探长度，
// C# 按长度分配 byte[] 后交给 loadInto() 写入，绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
const CACHE_NAME = 'kframework-bundles';
function openCache() {
    return caches.open(CACHE_NAME);
}
// 返回已存字节长度；不存在返回 -1。
export async function size(name) {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res)
        return -1;
    const buf = await res.arrayBuffer();
    return buf.byteLength;
}
// 把已存字节写入 buffer；返回实际写入长度（正常等于 size），失败/缺失返回 -1。
export async function loadInto(name, buffer) {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res)
        return -1;
    const buf = new Uint8Array(await res.arrayBuffer());
    buffer.set(buf.subarray(0, buffer.length));
    return buf.byteLength;
}
// 把资源包字节（byte[]）以 Response 形式写入 Cache Storage（按 name 键，覆盖式）。
export async function save(name, bytes) {
    const cache = await openCache();
    const res = new Response(bytes, {
        headers: { 'Content-Type': 'application/octet-stream' },
    });
    await cache.put(name, res);
}
// ==================== 缓存 GC（通用） ====================
// Cache Storage 没有 LRU，也没有条数上限：put 过的条目会一直留着。而资源文件名带内容哈希
// （热更一次就换一个文件名），不做清理的话历史版本会无限堆积，直到撑爆 origin 配额（QuotaExceededError）。
// 做法：以「当前生效的清单」为白名单做一次 GC——白名单外的条目（旧版本 / 已下线的包）一律删除。
// 归一化为绝对 URL：cache.match / cache.put 内部按 document.baseURI 解析相对键，
// 这里保持一致，才能和 cache.keys() 返回的 Request.url 直接比对。
function toAbsoluteUrl(name) {
    try {
        return new URL(name, document.baseURI).href;
    }
    catch {
        return name;
    }
}
// 列出当前 Cache 中所有已存的键（绝对 URL 形式）。
export async function keys() {
    const cache = await openCache();
    const reqs = await cache.keys();
    return reqs.map((r) => r.url);
}
// 删除单个键；返回是否真的删掉了（原本不存在返回 false）。
export async function remove(name) {
    const cache = await openCache();
    return await cache.delete(name);
}
/**
 * 通用 GC：删除不在白名单里的缓存条目，返回实际删除条数。
 * @param keep 需要保留的键（相对路径或绝对 URL 均可，会归一化后比对）。
 * @param prefix 可选路径前缀（如 "hot_update_res/"），只清理该前缀下的条目；留空表示整个 Cache 都参与 GC。
 */
export async function prune(keep, prefix = '') {
    const cache = await openCache();
    const keepSet = new Set(keep.map(toAbsoluteUrl));
    let prefixPath = '';
    if (prefix) {
        try {
            prefixPath = new URL(prefix, document.baseURI).pathname;
        }
        catch {
            prefixPath = prefix;
        }
    }
    const reqs = await cache.keys();
    let removed = 0;
    for (const req of reqs) {
        if (keepSet.has(req.url))
            continue;
        if (prefixPath) {
            let path = req.url;
            try {
                path = new URL(req.url, document.baseURI).pathname;
            }
            catch {
                // 解析不了就按原串比对
            }
            if (!path.startsWith(prefixPath))
                continue;
        }
        try {
            // 单条失败（如并发写占用）不中断整体 GC
            if (await cache.delete(req))
                removed++;
        }
        catch {
            // 忽略
        }
    }
    return removed;
}
