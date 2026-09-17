// 【依赖 C#】由 KFramework.MonoGame.JSBind_CacheStorage 经 [JSImport(module: "cachestorage")] 调用；
// 产物 cachestorage.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
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

// 把已存字节写入 buffer；返回实际写入长度（正常等于 size），失败/缺失返回 -1。
export async function loadInto(name: string, buffer: Uint8Array): Promise<number> {
    const cache = await openCache();
    const res = await cache.match(name);
    if (!res) return -1;
    const buf = new Uint8Array(await res.arrayBuffer());
    buffer.set(buf.subarray(0, buffer.length));
    return buf.byteLength;
}

// 把资源包字节（byte[]）以 Response 形式写入 Cache Storage（按 name 键，覆盖式）。
export async function save(name: string, bytes: Uint8Array): Promise<void> {
    const cache = await openCache();
    const res = new Response(bytes as unknown as BodyInit, {
        headers: { 'Content-Type': 'application/octet-stream' },
    });
    await cache.put(name, res);
}
