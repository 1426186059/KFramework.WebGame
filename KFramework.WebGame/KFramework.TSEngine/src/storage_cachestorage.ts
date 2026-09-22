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

function openCache(): Promise<Cache> {
    return caches.open(CACHE_NAME);
}

// 返回已存字节长度；不存在返回 -1。
export async function size(name: string): Promise<number> {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res) return -1;
    const buf = await res.arrayBuffer();
    return buf.byteLength;
}

// 把已存字节写入 buffer；返回实际写入长度（正常等于 size），失败/缺失返回 -1，缓冲不足返回 -(所需长度)。
// buffer 是 C# 的 ArraySegment<byte> → MemoryView（零拷贝视图，写入直接落在托管 byte[] 上）。
export async function loadInto(name: string, buffer: MemoryView | Uint8Array): Promise<number> {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res) return -1;
    const buf = new Uint8Array(await res.arrayBuffer());
    try {
        if (buf.byteLength > buffer.byteLength) return -buf.byteLength;
        (buffer as Uint8Array).set(buf, 0);
        return buf.byteLength;
    } finally {
        (buffer as MemoryView).dispose?.(); // ArraySegment 的视图 pin 着托管数组，用完解 pin
    }
}

// 读出已存字节（供 http_func 等模块复用同一个 Cache，避免各模块各写一份 CACHE_NAME）；不存在返回 null。
export async function read(name: string): Promise<Uint8Array | null> {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res) return null;
    return new Uint8Array(await res.arrayBuffer());
}

// 把 C# 传进来的字节转成真正的 ArrayBufferView。
// C# 的 ArraySegment<byte> 在 JS 侧是 MemoryView —— 它是 dotnet 的包装对象，**不是** TypedArray，
// 直接塞进 new Response() 不会被当成 BufferSource，而会按 USVString 转成 "[object Object]"，
// 于是缓存里存的是这段文本而不是原始字节：下一次命中缓存拿到的就是垃圾（zip 会报 EOCDNotFound）。
function toBody(src: Uint8Array | MemoryView): Uint8Array<ArrayBuffer> {
    // 一律拷进自己新开的 ArrayBuffer：既拿到真正的 BufferSource，也保证类型上就是 Uint8Array<ArrayBuffer>
    const out = new Uint8Array(src.byteLength);
    if (src instanceof Uint8Array) out.set(src);
    else (src as MemoryView).copyTo(out); // MemoryView → 目标 TypedArray（要求 constructor 一致，byte 视图即 Uint8Array）
    return out;
}

// 把资源包字节以 Response 形式写入 Cache Storage（按 name 键，覆盖式）。
// 注意：必须先把参数归一化成 Uint8Array，否则 Response 会把 MemoryView 当字符串存进去。
export async function save(name: string, bytes: Uint8Array | MemoryView): Promise<void> {
    try {
        const cache = await openCache();
        const res = new Response(toBody(bytes), {
            headers: { 'Content-Type': 'application/octet-stream' },
        });
        await cache.put(name, res);
    } finally {
        // ArraySegment 的视图 pin 着托管数组，用完解 pin
        (bytes as MemoryView).dispose?.();
    }
}

// ==================== 缓存 GC（通用） ====================
// Cache Storage 没有 LRU，也没有条数上限：put 过的条目会一直留着。而资源文件名带内容哈希
// （热更一次就换一个文件名），不做清理的话历史版本会无限堆积，直到撑爆 origin 配额（QuotaExceededError）。
// 做法：以「当前生效的清单」为白名单做一次 GC——白名单外的条目（旧版本 / 已下线的包）一律删除。

// 归一化为绝对 URL：cache.match / cache.put 内部按 document.baseURI 解析相对键，
// 这里保持一致，才能和 cache.keys() 返回的 Request.url 直接比对。
function toAbsoluteUrl(name: string): string {
    try {
        return new URL(name, document.baseURI).href;
    } catch {
        return name;
    }
}

// 列出当前 Cache 中所有已存的键（绝对 URL 形式）。
export async function keys(): Promise<string[]> {
    const cache = await openCache();
    const reqs = await cache.keys();
    return reqs.map((r) => r.url);
}

// 删除单个键；返回是否真的删掉了（原本不存在返回 false）。
export async function remove(name: string): Promise<boolean> {
    const cache = await openCache();
    return await cache.delete(name);
}

// 取 URL 的 pathname（去掉 origin 与查询串），用于前缀 / 后缀比对；解析不了就按原串。
function toPath(url: string): string {
    try {
        return new URL(url, document.baseURI).pathname;
    } catch {
        return url;
    }
}

/**
 * 通用 GC：删除不在白名单里的缓存条目，返回实际删除条数。
 * @param keep 需要保留的键（相对路径或绝对 URL 均可，会归一化后比对）。
 * @param prefix 可选路径前缀（如 "hot_update_res/"），只清理该前缀下的条目；留空表示不限前缀。
 * @param suffix 可选后缀（如 ".web.lib"），只清理该后缀的条目；留空表示不限后缀。
 * @remarks 前缀与后缀是「与」关系：两个都给了，必须同时命中才会被清理。
 *          例如 prefix="hot_update_res/" + suffix=".web.lib" —— 只回收资源包，
 *          同目录下的 version.manifest、零散图片等其它缓存条目不受影响。
 */
export async function prune(
    keep: string[],
    prefix: string = '',
    suffix: string = '',
): Promise<number> {
    const cache = await openCache();
    const keepSet = new Set(keep.map(toAbsoluteUrl));

    // 前缀按 pathname 归一（传绝对 URL 或相对目录都行），后缀直接按字符串比对
    const prefixPath = prefix ? toPath(prefix) : '';

    const reqs = await cache.keys();
    let removed = 0;
    for (const req of reqs) {
        if (keepSet.has(req.url)) continue;
        const path = toPath(req.url);
        if (prefixPath && !path.startsWith(prefixPath)) continue;
        if (suffix && !path.endsWith(suffix)) continue;
        try {
            // 单条失败（如并发写占用）不中断整体 GC
            if (await cache.delete(req)) removed++;
        } catch {
            // 忽略
        }
    }
    return removed;
}
