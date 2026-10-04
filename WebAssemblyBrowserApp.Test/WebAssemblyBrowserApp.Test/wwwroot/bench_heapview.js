// 【测试模块 Bench_HeapView 的 JS 侧】"GCHandle pin + 裸地址 → TypedArray"零拷贝方案，
// 声明见 Bench_HeapView/JSBind_HeapView.cs。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_heapview', heapView) + heapView.setRuntimeApi(runtime)。
//
// 【buffer 从哪来 —— 本模块最容易踩空的一处，已改过两次】
//   第一版：扫全局找 wasmMemory / HEAPU8 → 必然一无所获（⑨ 的实测），于是 ③④⑤ 全部假阴性。
//   第二版：借【引子】MemoryView 的 _unsafe_create_view().buffer → 能用了，但那是个 _unsafe 内部方法。
//   现在  ：直接用【公开 API】—— RuntimeAPI 上的 localHeapViewU8()，它的 .buffer 就是整块 WASM 线性内存。
//           dotnet/runtime 源码（export-api.ts:51 / dotnet.d.ts:629）里它是正式的公开接口，
//           而 marshal.ts:481 显示 _unsafe_create_view() 内部就是 new Uint8Array(localHeapViewU8().buffer, …)，
//           两者本就是同一块内存（Bench_RuntimeApi ⑤ 已实测三路同一）。
//   所以建视图的函数【不再收引子参数】；引子只剩一个对照项（①-c），用来坐实"两条路拿到的是同一块"。

// main.js 注入的 RuntimeAPI（create() 的返回值），公开内存 API 都在它身上。
let injectedApi = null;

export function setRuntimeApi(api) {
    injectedApi = api || null;
}

function msg(e) {
    return e && e.message ? e.message : String(e);
}

function resolveRuntimeApi() {
    if (injectedApi) return { api: injectedApi, via: 'main.js 注入的 RuntimeAPI' };

    // 兜底：官方全局入口（exports.ts:71-77）
    if (typeof globalThis.getDotnetRuntime === 'function') {
        try {
            const a = globalThis.getDotnetRuntime(0);
            if (a) return { api: a, via: 'globalThis.getDotnetRuntime(0)' };
        } catch (e) { /* 落到下面统一报拿不到 */ }
    }
    return { api: null, via: '' };
}

const CTORS = {
    Uint8Array: Uint8Array,
    Int8Array: Int8Array,
    Uint16Array: Uint16Array,
    Int16Array: Int16Array,
    Uint32Array: Uint32Array,
    Int32Array: Int32Array,
    Float32Array: Float32Array,
    Float64Array: Float64Array,
};

function getCtor(name) {
    const c = Object.prototype.hasOwnProperty.call(CTORS, name) ? CTORS[name] : null;
    if (!c) throw new Error('Unsupported typed array: ' + name);
    return c;
}

/**
 * 扫全局找 WASM memory.buffer —— 【已被 ⑨ 判定为必然失败的一条路】。
 * 保留它只为作对照：让"入口不在那几个全局符号上"这个结论有据可依，而不是听说的。
 */
function scanGlobal() {
    const candidates = [
        () => globalThis.Module && globalThis.Module.wasmMemory && globalThis.Module.wasmMemory.buffer,
        () => globalThis.wasmMemory && globalThis.wasmMemory.buffer,
        () => globalThis.dotnet && globalThis.dotnet.wasmMemory && globalThis.dotnet.wasmMemory.buffer,
        () => globalThis.HEAPU8 && globalThis.HEAPU8.buffer,
        () => globalThis.Module && globalThis.Module.HEAPU8 && globalThis.Module.HEAPU8.buffer,
    ];

    for (const get of candidates) {
        try {
            const b = get();
            if (b && b.byteLength > 0) return b;
        } catch (e) {
            // 某个入口抛错不影响继续找下一个
        }
    }
    return null;
}

/**
 * 取 WASM 的 memory.buffer —— 本方案的命门。
 *
 * 主路：公开 API runtime.localHeapViewU8().buffer（源码里它就是 Module.HEAPU8，覆盖整块线性内存）。
 * 兜底：扫全局（⑨ 已证明扫不到，留着只为把失败原因讲清楚）。
 */
function acquireBuffer() {
    const r = resolveRuntimeApi();

    if (r.api && typeof r.api.localHeapViewU8 === 'function') {
        try {
            const b = r.api.localHeapViewU8().buffer;
            if (b && b.byteLength > 0) {
                return { buffer: b, via: '公开 API runtime.localHeapViewU8().buffer' };
            }
        } catch (e) {
            // 落到扫全局
        }
    }

    const b = scanGlobal();
    if (b) return { buffer: b, via: '全局兜底扫描（公开 API 不可用）' };
    return { buffer: null, via: '', reason: r.api ? 'localHeapViewU8 取不到有效 buffer' : '拿不到 RuntimeAPI（main.js 没注入）' };
}

// ---------------------------------------------------------------- ① 取 buffer 的三条路

/** ①-a 对照：只扫全局。（⑨ 的结论：拿不到。） */
export function probeGlobal() {
    const b = scanGlobal();
    if (!b) return "一个都没有（.NET 不把裸堆视图挂到全局）";
    return "全局扫到 memory.buffer，byteLength=" + b.byteLength;
}

/** ①-b 正解：公开 API。 */
export function probeViaPublic() {
    const got = acquireBuffer();
    if (!got.buffer) return "拿不到：" + (got.reason || "未知原因");
    return "拿到 memory.buffer，byteLength=" + got.buffer.byteLength + "，途径=" + got.via;
}

/**
 * ①-c 对照：第二版用过的引子绕道（_unsafe_create_view）。
 * 它不是必须的了 —— 留着只为与 ①-b 摆在一起看：两条路拿到的是同一块 buffer。
 */
export function probeViaKey(key) {
    if (!key || typeof key._unsafe_create_view !== 'function') {
        return "引子不是 MemoryView（没有 _unsafe_create_view）";
    }
    try {
        const b = key._unsafe_create_view().buffer;
        if (b && b.byteLength > 0) {
            return "引子拿到 memory.buffer，byteLength=" + b.byteLength + "（_unsafe 内部方法，仅供对照）";
        }
        return "引子取不到有效 .buffer";
    } catch (e) {
        return "引子取 buffer 抛错：" + msg(e);
    }
}

// ---------------------------------------------------------------- 建视图与读写

/**
 * ② 附加校验：这个地址到底落不落在这块 buffer 里。
 * 光看"ptr != 0"说明不了任何问题 —— 地址非零却越界，建视图一样会失败，
 * 而那种失败很容易被误读成"零拷贝不成立"。
 */
export function checkRange(ptr, byteLength) {
    const got = acquireBuffer();
    if (!got.buffer) return "拿不到 buffer，无法校验";
    if (!ptr) return "ptr 为 0（没钉住），无意义";

    const end = ptr + byteLength;
    const inRange = ptr >= 0 && end <= got.buffer.byteLength;
    return "ptr=0x" + ptr.toString(16) +
        "，需 " + byteLength + " 字节，buffer.byteLength=" + got.buffer.byteLength +
        " → " + (inRange ? "落在范围内 ✓" : "越界 ✘（ptr+" + byteLength + " > " + got.buffer.byteLength + "）");
}

/**
 * 建视图。三道校验，每一道都是为了【不让失败被误判成"方案不成立"】：
 *   1）ptr 非 0；
 *   2）ptr+字节数 不越界；
 *   3）ptr 按元素宽度对齐（Uint32Array 等要求 byteOffset 是其字节宽度的整数倍，否则 RangeError）。
 */
export function createView(ptr, length, typedArrayName) {
    const got = acquireBuffer();
    if (!got.buffer) {
        throw new Error('拿不到 WASM memory.buffer：' + (got.reason || '公开 API 与全局扫描都失败'));
    }
    if (!ptr) throw new Error('ptr 为 0（.NET 侧没钉住数组，或地址无效）');

    const ctor = getCtor(typedArrayName);
    const bytes = length * ctor.BYTES_PER_ELEMENT;
    const end = ptr + bytes;

    if (ptr < 0 || end > got.buffer.byteLength) {
        throw new Error('地址越界：需要 [' + ptr + ', ' + end + ')，而 buffer 只有 ' +
            got.buffer.byteLength + ' 字节');
    }
    if (ptr % ctor.BYTES_PER_ELEMENT !== 0) {
        throw new Error('地址未按 ' + ctor.BYTES_PER_ELEMENT + ' 字节对齐（ptr=0x' + ptr.toString(16) +
            '），' + typedArrayName + ' 要求 byteOffset 对齐，请改用 Uint8Array 视图');
    }

    return new ctor(got.buffer, ptr, length);
}

/** 拷贝：先建视图再 slice() 出来一份，脱离 WASM 内存（多一次 memcpy，但可长期持有）。 */
export function copyArray(ptr, length, typedArrayName) {
    return createView(ptr, length, typedArrayName).slice();
}

/**
 * 往视图里写入固定值。
 * 按视图的元素宽度决定怎么写：Uint8Array 按字节截断（& 0xff）；
 * 更宽的元素就写满整个元素 —— 否则 Uint32Array 上"逐字节写"的语义是错的，
 * 会把每个 32 位元素写成同一个小数，看了还以为写成功了。
 */
export function writeView(view, value) {
    try {
        const w = view.BYTES_PER_ELEMENT;
        const isFloat = view instanceof Float32Array || view instanceof Float64Array;
        const v = isFloat ? value : (w === 1 ? (value & 0xff) : value);
        for (let i = 0; i < view.length; i++) view[i] = v;
        return "view.length=" + view.length + "（" + view.constructor.name + "，元素宽 " + w +
            " 字节），已写入 " + v;
    } catch (e) {
        return "写入失败：" + msg(e);
    }
}

/** 从视图里读回前 count 个元素。 */
export function readView(view, count) {
    try {
        const n = Math.min(count, view.length);
        const out = [];
        for (let i = 0; i < n; i++) out.push(view[i]);
        return "读到 [" + out.join(",") + "]";
    } catch (e) {
        return "读取失败：" + msg(e);
    }
}

/**
 * ⑧ 跨数组验证：用公开 API 的 buffer，去访问【另一个】被 pin 住的数组。
 * 写成功就证明那块 buffer 是整块线性内存，而不只是某个视图的私有区域。
 */
export function writeOther(ptr, length, value) {
    const got = acquireBuffer();
    if (!got.buffer) return "拿不到 buffer：" + (got.reason || "未知原因");
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住那个数组";

    try {
        const v = new Uint8Array(got.buffer, ptr, length);
        for (let i = 0; i < v.length; i++) v[i] = value & 0xff;
        return "用公开 API 的 buffer + ptr=0x" + ptr.toString(16) + " 建视图成功（视图 byteOffset=" +
            v.byteOffset + "），已写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "建视图/写入失败：" + msg(e);
    }
}

// ---- ⑦ 生存期：memory.grow 会不会让 buffer detach、视图失效 ----
// 判据：detached 的 ArrayBuffer 其 byteLength 变 0，挂在它上面的 TypedArray 的 length 也变 0，
//       此后读写既不抛错也不生效 —— 正是那种"看着跑通了、其实没干活"的坑。

let kept = null; // { buffer, view, bufferLen0 }

/** 存下 buffer 与视图，回报初始状态。 */
export function keepForGrow(ptr, length) {
    const got = acquireBuffer();
    if (!got.buffer) return "存不下 —— 拿不到 buffer：" + (got.reason || "未知原因");
    if (!ptr) return "存不下 —— ptr 为 0，无法建视图";

    try {
        const view = new Uint8Array(got.buffer, ptr, length);
        kept = { buffer: got.buffer, view: view, bufferLen0: got.buffer.byteLength };
        return "已存下：buffer.byteLength=" + got.buffer.byteLength +
            "，view.length=" + view.length + "，byteOffset=" + view.byteOffset;
    } catch (e) {
        return "存不下 —— 建视图失败：" + msg(e);
    }
}

/** 只查询、不写入：看存的 buffer / 视图有没有因为堆增长而失效。 */
export function inspectKept() {
    if (!kept) return "没有存过视图";

    const bufLen = kept.buffer.byteLength;
    const detached = bufLen === 0;
    const viewLen = kept.view.length;
    const grew = !detached && bufLen !== kept.bufferLen0;

    return "初始 buffer.byteLength=" + kept.bufferLen0 + "，现在=" + bufLen +
        "，view.length=" + viewLen +
        " → " + (detached
            ? "buffer 已 detach ✘（byteLength 归零，视图随之失效）"
            : grew
                ? "buffer 已增长且未 detach（视图可能指向旧位置，需实写验证）"
                : "buffer 未变，视图仍有效");
}

/** 真正往存的视图里写一次 —— 只有这一步能证明视图到底是活是死。 */
export function writeKept(value) {
    if (!kept) return "没有存过视图";

    const before = kept.view.length;
    try {
        for (let i = 0; i < kept.view.length; i++) kept.view[i] = value & 0xff;
        return "写之前 view.length=" + before + "，写之后 view.length=" + kept.view.length +
            "，已尝试写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "写入失败：" + msg(e);
    }
}

/** 收尾：放掉跨调用持有的引用（本模块的 store 不该活到下一轮）。 */
export function releaseKept() {
    kept = null;
    return "已释放";
}

// ---- ⑦ 的胁迫手段：怎么才能真正把堆逼大 ----

// 上一轮用 .NET 的 new byte[] 压了 96MB 也没逼出 grow —— 因为那走的是 .NET 的 GC 堆，
// GC 向 wasm 堆要内存有自己的策略，未必走到 sbrk / memory.grow。
// 真正会触发堆增长的是 emscripten 自己的 malloc：耗尽时 emscripten_resize_heap → sbrk
// → memory.grow → updateMemoryViews() 重建 Module.HEAPU8 → 【旧视图必然 detach】。
// 而 Module 就挂在 RuntimeAPI 上，于是这也是一处公开 API 的用武之地。

let pressurePtrs = [];

/** 用 emscripten 的 malloc 占一块并保持不放，逼堆增长。 */
export function mallocPressure(bytes) {
    const r = resolveRuntimeApi();
    if (!r.api) return "不可用 —— 拿不到 RuntimeAPI（main.js 没注入）";

    const M = r.api.Module;
    if (!M || typeof M._malloc !== 'function') return "不可用 —— api.Module 上没有 _malloc";

    try {
        const p = M._malloc(bytes);
        if (!p) return "失败 —— _malloc 返回 0";
        pressurePtrs.push(p);
        return "OK —— _malloc(" + (bytes / 1024 / 1024) + "MB) → 0x" + p.toString(16) +
            "，已持有 " + pressurePtrs.length + " 块";
    } catch (e) {
        return "失败 —— " + msg(e);
    }
}

/** 放掉所有胁迫分配（在写完、回读完之后再调）。 */
export function releasePressure() {
    const r = resolveRuntimeApi();
    const M = r.api && r.api.Module;

    let n = 0;
    if (M && typeof M._free === 'function') {
        for (const p of pressurePtrs) {
            try { M._free(p); n++; } catch (e) { /* 单块失败不影响其余 */ }
        }
    }
    pressurePtrs = [];
    return "已释放 " + n + " 块";
}
