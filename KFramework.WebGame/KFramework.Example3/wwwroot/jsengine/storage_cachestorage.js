// 【依赖 C#】由 KFramework.MonoGame.JSBind_CacheStorage 经 [JSImport(module: "cachestorage")] 调用；
// 产物 storage_cachestorage.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 把资源包（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源字节）持久化到浏览器 Cache Storage。
// 与 IndexedDB 相比：Cache Storage 以 Response 形式存储，专为二进制资源设计、序列化/反序列化开销更小，
// 后续若要叠加 Service Worker 拦截 fetch 直接返回缓存响应，可做到“零解析开销”的无缝升级。
//
// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 GetCacheSizeAsync 探长度，
// C# 按长度分配 byte[] 后交给 LoadCacheAsync 写入，绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
//
// 对外只暴露两类东西，命名与 C# 侧一一对应：
//   * export class Caching —— 通用封装（类似 Unity 的 Caching），支持任意命名缓存；实例方法用 TS 习惯的小驼峰。
//   * CachingXxxAsync(...) 模块函数 —— 供 C# JSBind_CacheStorage 静态绑定，PascalCase + Async 后缀，与 C# 方法同名。
const DEFAULT_CACHE = 'kframework-bundles';
// ArraySegment<byte> 在 JS 侧是 MemoryView（非 TypedArray），必须拷成 Uint8Array 才能当 Response body，否则会被当字符串存成垃圾。
function toBody(src) {
    const out = new Uint8Array(src.byteLength);
    if (src instanceof Uint8Array)
        out.set(src);
    else
        src.copyTo(out);
    return out;
}
// 相对/绝对键归一化为绝对 URL：cache.match / cache.put 内部按 document.baseURI 解析相对键，
// 这里保持一致，才能和 cache.keys() 返回的 Request.url 直接比对。
function toAbsoluteUrl(name) {
    try {
        return new URL(name, document.baseURI).href;
    }
    catch {
        return name;
    }
}
// 取 URL 的 pathname（去掉 origin 与查询串），用于前缀 / 后缀比对；解析不了就按原串。
function toPath(url) {
    try {
        return new URL(url, document.baseURI).pathname;
    }
    catch {
        return url;
    }
}
/**
 * 通用 Cache Storage 封装（类似 Unity 的 Caching）。
 * 不再写死单个缓存名：支持任意命名缓存，默认缓存名为 {@link DEFAULT_CACHE}。
 *
 * 用法：
 *   const c = Caching.open('level-1');                       // 打开（复用）一个命名缓存
 *   await c.save(key, bytes);                                // 写入
 *   const len = await c.size(key);                           // 查询长度
 *   await c.prune(keepList, 'hot_update_res/', '.web.lib');  // 按白名单 GC
 *   await c.clear();                                         // 清空并删除该缓存
 *
 * Caching.current 对应 Unity 的 Caching.currentCacheForWriting（当前写入缓存）。
 */
export class Caching {
    name;
    static DefaultName = DEFAULT_CACHE;
    static _pool = new Map();
    static _current = null;
    /** 打开（或获取已缓存的）一个命名缓存；同名复用同一 Cache 句柄。 */
    static open(name = DEFAULT_CACHE) {
        return new Caching(name);
    }
    /** 当前写入缓存（对应 Unity 的 Caching.currentCacheForWriting）；未设置时回退到默认缓存。 */
    static get current() {
        return Caching._current ?? (Caching._current = new Caching(DEFAULT_CACHE));
    }
    static set current(c) { Caching._current = c; }
    /** 设置“当前写入缓存”（按名字），返回该缓存句柄。 */
    static setCurrentCacheForWriting(name) {
        return (Caching._current = new Caching(name));
    }
    constructor(name) {
        this.name = name;
    }
    _open() {
        let p = Caching._pool.get(this.name);
        if (!p) {
            p = caches.open(this.name);
            Caching._pool.set(this.name, p);
        }
        return p;
    }
    /** 已存字节长度；不存在返回 -1。 */
    async size(key) {
        const res = await (await this._open()).match(key);
        if (!res)
            return -1;
        return (await res.arrayBuffer()).byteLength;
    }
    /** 把已存字节写入 buffer；返回实际写入长度（正常等于 size），缺失返回 -1，缓冲不足返回 -(所需长度)。 */
    async loadInto(key, buffer) {
        const res = await (await this._open()).match(key);
        if (!res)
            return -1;
        const buf = new Uint8Array(await res.arrayBuffer());
        try {
            if (buf.byteLength > buffer.byteLength)
                return -buf.byteLength;
            buffer.set(buf, 0);
            return buf.byteLength;
        }
        finally {
            buffer.dispose?.(); // ArraySegment 的视图 pin 着托管数组，用完解 pin
        }
    }
    /** 读出已存字节；不存在返回 null。 */
    async read(key) {
        const res = await (await this._open()).match(key);
        if (!res)
            return null;
        return new Uint8Array(await res.arrayBuffer());
    }
    /** 把字节以 Response 形式写入（覆盖式）。MemoryView 必须归一化成 Uint8Array，否则会被当字符串存。 */
    async save(key, bytes) {
        try {
            const res = new Response(toBody(bytes), { headers: { 'Content-Type': 'application/octet-stream' } });
            await (await this._open()).put(key, res);
        }
        finally {
            bytes.dispose?.(); // ArraySegment 的视图 pin 着托管数组，用完解 pin
        }
    }
    /** 列出当前缓存所有键（绝对 URL 形式）。 */
    async keys() {
        const reqs = await (await this._open()).keys();
        return reqs.map((r) => r.url);
    }
    /** 删除单个键；返回是否真的删掉了（原本不存在返回 false）。 */
    async remove(key) {
        return await (await this._open()).delete(key);
    }
    /** 通用 GC：保留 keep 白名单中的键、删除其余条目，返回实际删除条数。 */
    async prune(keep, prefix = '', suffix = '') {
        const cache = await this._open();
        const keepSet = new Set(keep.map(toAbsoluteUrl));
        const prefixPath = prefix ? toPath(prefix) : '';
        const reqs = await cache.keys();
        let removed = 0;
        for (const req of reqs) {
            if (keepSet.has(req.url))
                continue;
            const path = toPath(req.url);
            if (prefixPath && !path.startsWith(prefixPath))
                continue;
            if (suffix && !path.endsWith(suffix))
                continue;
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
    /** 清空并删除整个缓存（含其中的全部条目）。 */
    async clear() {
        await caches.delete(this.name);
        Caching._pool.delete(this.name);
    }
}
// ==================== 供 C# JSBind_CacheStorage 静态绑定的模块函数：名字与 C# 侧方法一一对应 ====================
export async function GetCacheSizeAsync(cacheName, key) {
    return Caching.open(cacheName).size(key);
}
export async function GetCacheCountAsync(cacheName) {
    return (await Caching.open(cacheName).keys()).length;
}
export async function LoadCacheAsync(cacheName, key, buffer) {
    return Caching.open(cacheName).loadInto(key, buffer);
}
export async function SaveCacheAsync(cacheName, key, bytes) {
    return Caching.open(cacheName).save(key, bytes);
}
export async function RemoveCacheAsync(cacheName, key) {
    return Caching.open(cacheName).remove(key);
}
export async function RemoveCacheListAsync(cacheName, removeList) {
    const c = Caching.open(cacheName);
    let removed = 0;
    for (const key of removeList) {
        if (await c.remove(key))
            removed++;
    }
    return removed;
}
export async function RemoveAllCacheAsync(cacheName) {
    return Caching.open(cacheName).clear();
}
