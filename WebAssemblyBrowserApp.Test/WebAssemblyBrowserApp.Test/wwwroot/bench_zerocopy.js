// 【测试模块 Bench_ZeroCopy 的 JS 侧】零拷贝 vs 非零拷贝的性能对比，
// 声明见 Bench_ZeroCopy/JSBind_ZeroCopy.cs。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_zerocopy', zeroCopy) + zeroCopy.setRuntimeApi(runtime)。
//
// 【测的是什么】
// C# 手里有一块 byte[]（引擎里最典型的形态：纹理 / 帧数据 / 顶点），JS 要【读】它。
// 把这份数据交到 JS 手上，有两条路：
//   零拷贝  —— C# 用 GCHandle 钉住数组，把（地址, 长度）交给 JS，JS 用公开 API
//              runtime.localHeapViewU8().buffer 直接建出指向同一块内存的视图。一次 memcpy 都没有。
//   非零拷贝 —— C# 把 Span<byte> 传过来（运行时封送成 MemoryView），JS 用 copyTo 或 slice 拷一份。
// 两条路的【差别】就是那一次 memcpy（slice 还多一次分配）。
//
// 【关键：JS 侧拿到之后只做 O(1) 的动作】
// 早先的设计让 JS 在拿到后"把整块求和"，结果求和循环的 O(n) 成本把 memcpy 的 O(n) 成本完全淹没了
// —— memcpy 是高度优化的，JS 逐字节循环比它慢得多，于是几条路线测出来几乎一样快，
// 那正是"看着跑通了、其实没测到点子上"。所以这里改成只读首字节与末字节各一个（O(1)），
// 让耗时差异纯粹反映【搬运方式】本身。

// main.js 注入的 RuntimeAPI，公开内存 API 在它身上。
let injectedApi = null;

export function setRuntimeApi(api) {
    injectedApi = api || null;
}

function heapBuffer() {
    if (injectedApi && typeof injectedApi.localHeapViewU8 === 'function') {
        const b = injectedApi.localHeapViewU8().buffer;
        if (b && b.byteLength > 0) return b;
    }
    // 兜底：官方全局入口
    if (typeof globalThis.getDotnetRuntime === 'function') {
        try {
            const a = globalThis.getDotnetRuntime(0);
            if (a && typeof a.localHeapViewU8 === 'function') {
                const b = a.localHeapViewU8().buffer;
                if (b && b.byteLength > 0) return b;
            }
        } catch (e) { /* 下面统一抛 */ }
    }
    throw new Error('拿不到 WASM memory.buffer（RuntimeAPI 未注入）');
}

/** 校验值：首字节 | (末字节 << 8)。四条路线都用它，返回值必须一致，否则就是某条读错了数据。 */
function peek(v) {
    return v[0] | (v[v.length - 1] << 8);
}

// ---------------------------------------------------------------- 零拷贝

/**
 * 零拷贝：公开 API 的 buffer + C# 钉住的地址 → 建视图 → 读两个字节。
 * 视图是【现场建、当场用、用完扔】—— 官方注释写得明白：
 * Don't store the reference, don't use it after await.
 */
export function peekZeroCopy(ptr, length) {
    if (!ptr) throw new Error('ptr 为 0 —— C# 侧没钉住数组');
    const v = new Uint8Array(heapBuffer(), ptr, length);
    return peek(v);
}

// 缓存下来的视图。既然用的是指针，(ptr, length) 没变，视图就【不必每次重建】——
// 这正是引擎里最典型的形态：一块固定的帧缓冲 / 顶点缓冲，逐帧反复传。
// 省掉的不只是建对象的开销，还有它带来的 JS 垃圾（每次 new 一个 TypedArray 都在喂 V8 的新生代）。
// 顺带处理了 detach：堆增长后旧 buffer 被摘掉，视图 length 会归零，此时必须重建。
let cachedView = null, cachedPtr = 0, cachedLen = 0;

export function peekZeroCopyCached(ptr, length) {
    if (!ptr) throw new Error('ptr 为 0 —— C# 侧没钉住数组');
    if (!cachedView || cachedPtr !== ptr || cachedLen !== length || cachedView.length === 0) {
        cachedView = new Uint8Array(heapBuffer(), ptr, length);
        cachedPtr = ptr;
        cachedLen = length;
    }
    return peek(cachedView);
}

/**
 * 跨界底噪：什么都不做，立刻返回一个常数。
 * 有了它才能把"过一次界"这件事本身的价钱量出来 —— 各路线都含它，
 * 扣掉之后才是各自真正的增量成本。
 */
export function peekNoop() {
    return 7;
}

// ---------------------------------------------------------------- 非零拷贝

// ---- 复用缓冲：一次分配到够用的最大尺寸，之后只覆盖、不重建 ----
// 这就是 ByteCache 的路子，三句要点：
//   ① 缓冲区本身复用 —— 只有尺寸不够时才分配一次（正常一辈子只分配一次）；
//   ② 用 usedLength 标出【本次的有效范围】；
//   ③ 取用时只取 [0, usedLength)，否则会把上一轮的残留数据一起带出去。
// 用完不必清零：下次写入直接覆盖，并把 usedLength 设对即可。
//
// 顺带说清一个坑：缓冲一旦比本次数据长，"直接对整个缓冲取首尾字节"就是错的 ——
// 末字节会读到上一轮的残留，校验当场挂掉。所以下面一律按 usedLength 取。
// slice() 则【不复用】：它每次都新分配一块，那本来就是它的成本之一，要如实计入。
let reuseBuf = null;
let usedLength = 0;

function getReuse(maxSize) {
    if (!reuseBuf || reuseBuf.length < maxSize) reuseBuf = new Uint8Array(maxSize);
    return reuseBuf;
}

/**
 * 按有效长度取首末字节。
 * 刻意【不用 subarray(0, usedLength)】—— 那会再分配一个视图对象，
 * 而"每次调用都产生 JS 垃圾"正是本模块要避免、也在度量的东西。
 */
function peekRange(v, len) {
    return v[0] | (v[len - 1] << 8);
}

/** 非零拷贝之一：MemoryView.copyTo 拷进复用缓冲（1 次 memcpy，无分配）。 */
export function peekCopyTo(view) {
    const dst = getReuse(view.length);
    view.copyTo(dst);          // 覆盖写入，不 new
    usedLength = view.length;  // 本次实际用了多少
    return peekRange(dst, usedLength);
}

/** 非零拷贝之二：MemoryView.slice() 拿一份新副本（1 次 memcpy + 每调用一次就新分配一块）。 */
export function peekSlice(view) {
    const copy = view.slice();
    return peek(copy);
}

// ---------------------------------------------------------------- 基线

// JS 内部纯 memcpy（dst.set(src)），全程不碰 WASM 内存。
// 它是【解释性基线】：零拷贝省掉的，正是这一块的量级。
let jsSrc = null, jsDst = null;

export function peekJsMemcpy(length) {
    if (!jsSrc || jsSrc.length < length) {
        jsSrc = new Uint8Array(length);
        for (let i = 0; i < length; i++) jsSrc[i] = i & 0xff;
        jsDst = new Uint8Array(length);
    }
    jsDst.set(jsSrc.subarray(0, length));   // 只拷本次有效的那一段
    return peekRange(jsDst, length);
}
