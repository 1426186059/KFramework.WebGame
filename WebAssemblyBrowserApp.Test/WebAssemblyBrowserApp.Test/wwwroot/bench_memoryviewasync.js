// 【测试模块 Bench_MemoryViewAsync 的 JS 侧】验证 MemoryView 在「跨 await（以 GC 间隙模拟）」时内存会不会变。
// 声明见 Bench_MemoryViewAsync/JSBind_MemoryViewAsync.cs（C# 传进来的是 Span<byte>/ArraySegment<byte> → MemoryView）。
//
// 一个测试模块一个 js，不共用其它模块的函数。接线：main.js 里 setModuleImports('bench_memoryviewasync', ...)。
//
// 本模块【不计时】：每条只跑一次，把「JS 侧当前看到的字节」回报成 "tag|<csv>" 字符串。
// 真正的判据在 C# 侧 —— 它比对读回的字节与当初已知内容是否一致。

// 两个模块级变量：分别存「unpinned Span」与「pinned ArraySegment」视图（只留引用，不做任何拷贝）。
let _stored = null;
let _storedPinned = null;

/** 存【unpinned Span】视图。绑定对 Span 只在本次调用期间 pin，调用返回即释放。 */
export function probeStore(view) {
    _stored = view;
    return "已把【unpinned Span】视图存进 JS 模块级变量（不做任何拷贝）";
}

/** 存【pinned ArraySegment】视图。绑定会 pin 住托管数组，跨调用仍有效。 */
export function probeStorePinned(view) {
    _storedPinned = view;
    return "已把【pinned ArraySegment】视图存进 JS 模块级变量";
}

/** 读回存下的【unpinned】视图，回报当前字节（格式 "tag|<csv>"）。不写入，避免踩坏被迁走后的内存。 */
export function probeReadStored() {
    return readView(_stored, "_stored");
}

/** 读回存下的【pinned】视图，回报当前字节（格式 "tag|<csv>"）。 */
export function probeReadStoredPinned() {
    return readView(_storedPinned, "_storedPinned");
}

function readView(v, tag) {
    if (v === null) return tag + "|null";
    try {
        const n = v.byteLength;
        const out = new Uint8Array(n);
        v.copyTo(out); // 用官方读出路径，避免索引器歧义
        return tag + "|" + Array.from(out).join(",");
    } catch (e) {
        // 视图已 detached / 失效时，copyTo 会抛 —— 这也算「内存变了 / 视图死了」的证据
        return tag + "|ERR:" + (e && e.message ? e.message : String(e));
    }
}
