// 【依赖 C#】由 KFramework.MonoGame.JSBind_CacheStorage 经 [JSImport(module: "cachestorage")] 调用；
// 产物 storage_cachestorage.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 把资源包（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源字节）持久化到浏览器 Cache Storage。
// 与 IndexedDB 相比：Cache Storage 以 Response 形式存储，专为二进制资源设计、序列化/反序列化开销更小，
// 后续若要叠加 Service Worker 拦截 fetch 直接返回缓存响应，可做到“零解析开销”的无缝升级。
//
// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 CachingSizeAsync 探长度，
// C# 按长度分配 byte[] 后交给 CachingLoadIntoAsync 写入，绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
//
// 对外只暴露两类东西，命名与 C# 侧一一对应：
//   * export class Caching —— 通用封装（类似 Unity 的 Caching），支持任意命名缓存；实例方法用 TS 习惯的小驼峰。
//   * CachingXxxAsync(...) 模块函数 —— 供 C# JSBind_CacheStorage 静态绑定，PascalCase + Async 后缀，与 C# 方法同名。

const DEFAULT_CACHE = 'kframework-bundles';

// C# 的 ArraySegment<byte> 在 JS 侧是 MemoryView —— 它是 dotnet 的包装对象，**不是** TypedArray，
// 直接塞进 new Response() 不会被当成 BufferSource，而会按 USVString 转成 "[object Object]"，
// 于是缓存里存的是这段文本而不是原始字节：下一次命中缓存拿到的就是垃圾（zip 会报 EOCDNotFound）。
function toBody(src: Uint8Array | MemoryView): Uint8Array {
    const out = new Uint8Array(src.byteLength);
    if (src instanceof Uint8Array) out.set(src);
    else (src as MemoryView).copyTo(out);
    return out;
}

// 相对/绝对键归一化为绝对 URL：cache.match / cache.put 内部按 document.baseURI 解析相对键，
// 这里保持一致，才能和 cache.keys() 返回的 Request.url 直接比对。
function toAbsoluteUrl(name: string): string {
    try { return new URL(name, document.baseURI).href; } catch { return name; }
}

// 取 URL 的 pathname（去掉 origin 与查询串），用于前缀 / 后缀比对；解析不了就按原串。
function toPath(url: string): string {
    try { return new URL(url, document.baseURI).pathname; } catch { return url; }
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
    static readonly DefaultName = DEFAULT_CACHE;
    private static _pool = new Map<string, Promise<Cache>>();
    private static _current: Caching | null = null;

    /** 打开（或获取已缓存的）一个命名缓存；同名复用同一 Cache 句柄。 */
    static open(name: string = DEFAULT_CACHE): Caching {
        return new Caching(name);
    }

    /** 当前写入缓存（对应 Unity 的 Caching.currentCacheForWriting）；未设置时回退到默认缓存。 */
    static get current(): Caching {
        return Caching._current ?? (Caching._current = new Caching(DEFAULT_CACHE));
    }
    static set current(c: Caching) { Caching._current = c; }

    /** 设置“当前写入缓存”（按名字），返回该缓存句柄。 */
    static setCurrentCacheForWriting(name: string): Caching {
        return (Caching._current = new Caching(name));
    }

    private constructor(public readonly name: string) {}

    private _open(): Promise<Cache> {
        let p = Caching._pool.get(this.name);
        if (!p) { p = caches.open(this.name); Caching._pool.set(this.name, p); }
        return p;
    }

    /** 已存字节长度；不存在返回 -1。 */
    async size(key: string): Promise<number> {
        const res = await (await this._open()).match(key);
        if (!res) return -1;
        return (await res.arrayBuffer()).byteLength;
    }

    /** 把已存字节写入 buffer；返回实际写入长度（正常等于 size），缺失返回 -1，缓冲不足返回 -(所需长度)。 */
    async loadInto(key: string, buffer: MemoryView | Uint8Array): Promise<number> {
        const res = await (await this._open()).match(key);
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

    /** 读出已存字节；不存在返回 null。 */
    async read(key: string): Promise<Uint8Array | null> {
        const res = await (await this._open()).match(key);
        if (!res) return null;
        return new Uint8Array(await res.arrayBuffer());
    }

    /** 把字节以 Response 形式写入（覆盖式）。MemoryView 必须归一化成 Uint8Array，否则会被当字符串存。 */
    async save(key: string, bytes: Uint8Array | MemoryView): Promise<void> {
        try {
            const res = new Response(toBody(bytes), { headers: { 'Content-Type': 'application/octet-stream' } });
            await (await this._open()).put(key, res);
        } finally {
            (bytes as MemoryView).dispose?.(); // ArraySegment 的视图 pin 着托管数组，用完解 pin
        }
    }

    /** 列出当前缓存所有键（绝对 URL 形式）。 */
    async keys(): Promise<string[]> {
        const reqs = await (await this._open()).keys();
        return reqs.map((r) => r.url);
    }

    /** 删除单个键；返回是否真的删掉了（原本不存在返回 false）。 */
    async remove(key: string): Promise<boolean> {
        return await (await this._open()).delete(key);
    }

    /**
     * 通用 GC：删除不在白名单里的条目，返回实际删除条数。
     * @param keep 需要保留的键（相对路径或绝对 URL 均可，会归一化后比对）。
     * @param prefix 可选路径前缀（如 "hot_update_res/"），只清理该前缀下的条目；留空表示不限前缀。
     * @param suffix 可选后缀（如 ".web.lib"），只清理该后缀的条目；留空表示不限后缀。
     * @remarks 前缀与后缀是「与」关系：两个都给了，必须同时命中才会被清理。
     */
    async prune(keep: string[], prefix = '', suffix = ''): Promise<number> {
        const cache = await this._open();
        const keepSet = new Set(keep.map(toAbsoluteUrl));
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

    /** 清空并删除整个缓存（含其中的全部条目）。 */
    async clear(): Promise<void> {
        await caches.delete(this.name);
        Caching._pool.delete(this.name);
    }
}

// ==================== 供 C# JSBind_CacheStorage 静态绑定的模块函数（按缓存名操作，与 C# 方法同名） ====================
export async function CachingSizeAsync(cacheName: string, key: string): Promise<number> {
    return Caching.open(cacheName).size(key);
}
export async function CachingLoadIntoAsync(cacheName: string, key: string, buffer: MemoryView | Uint8Array): Promise<number> {
    return Caching.open(cacheName).loadInto(key, buffer);
}
export async function CachingSaveAsync(cacheName: string, key: string, bytes: Uint8Array | MemoryView): Promise<void> {
    return Caching.open(cacheName).save(key, bytes);
}
export async function CachingRemoveAsync(cacheName: string, key: string): Promise<boolean> {
    return Caching.open(cacheName).remove(key);
}
export async function CachingRemoveListAsync(cacheName: string, keep: string[], prefix = '', suffix = ''): Promise<number> {
    return Caching.open(cacheName).prune(keep, prefix, suffix);
}
export async function CachingClearAsync(cacheName: string): Promise<void> {
    return Caching.open(cacheName).clear();
}
