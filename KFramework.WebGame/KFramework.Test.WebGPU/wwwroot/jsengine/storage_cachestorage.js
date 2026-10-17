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
import { sharedBytesOf } from "./custom_data_byte_cache.js";
export class Caching {
    name;
    static _pool = new Map();
    static _current = null;
    /** 打开（或获取已缓存的）一个命名缓存；同名复用同一 Cache 句柄。 */
    static open(name) {
        return new Caching(name);
    }
    /** 当前写入缓存（对应 Unity 的 Caching.currentCacheForWriting）；未设置时回退到默认缓存。 */
    static get current() {
        return Caching._current;
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
    /** 把字节以 Response 形式写入（覆盖式）。MemoryView_ArraySegment 必须归一化成 Uint8Array，否则会被当字符串存。 */
    async save(key, bytes) {
        try {
            const res = new Response(sharedBytesOf(bytes), { headers: { 'Content-Type': 'application/octet-stream' } });
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
