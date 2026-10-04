// 【测试模块 Bench_RuntimeApi 的 JS 侧】探测 .NET WASM 运行时【公开】的内存 API，
// 声明见 Bench_RuntimeApi/JSBind_RuntimeApi.cs。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_runtimeapi', runtimeApi) + runtimeApi.setRuntimeApi(runtime)。
//
// 【本模块存在的理由】
// Bench_MemoryView 的 ⑨ 得出"全局扫不到 WASM 裸堆入口"，Bench_HeapView 因此绕道
// MemoryView._unsafe_create_view()（带 _unsafe 前缀、属内部方法）。
// 而 dotnet/runtime 源码（src/mono/browser/runtime）里其实有一整套【公开】的内存 API：
//   export-api.ts        → localHeapViewU8 / setHeapU8 / getHeapU8 …（MemoryAPIType）
//   exports.ts:57-69     → create() 返回的 RuntimeAPI 上同时挂着 Module 与那套 API
//   exports.ts:71-77     → 还有个全局入口 globalThis.getDotnetRuntime(runtimeId)
//   marshal.ts:481-493   → _unsafe_create_view() 内部就是 new Uint8Array(localHeapViewU8().buffer, …)
// 本模块逐条实测它们：存不存在、拿到的是什么、彼此是不是同一块内存、
// 以及能不能直接用来访问 C# 钉住的数组（判据仍是 C# 回读自己的数组）。

// main.js 注入的 RuntimeAPI（create() 的返回值）。最稳的一条路，不依赖任何全局符号。
let injectedApi = null;

export function setRuntimeApi(api) {
    injectedApi = api || null;
}

function msg(e) {
    return e && e.message ? e.message : String(e);
}

function ctorName(v) {
    return v && v.constructor ? v.constructor.name : typeof v;
}

/** RuntimeAPI 的两条来路：main.js 注入（稳）→ 全局 getDotnetRuntime（官方全局入口）。 */
function resolveRuntimeApi() {
    if (injectedApi) return { api: injectedApi, via: 'main.js 注入的 RuntimeAPI' };

    if (typeof globalThis.getDotnetRuntime === 'function') {
        try {
            const a = globalThis.getDotnetRuntime(0);
            if (a) return { api: a, via: 'globalThis.getDotnetRuntime(0)' };
        } catch (e) {
            // 落到下面统一报"拿不到"
        }
    }
    return { api: null, via: '' };
}

/** 一个 RuntimeAPI 上有没有我们关心的那几个成员。 */
const INTERESTING = ['Module', 'localHeapViewU8', 'setHeapU8', 'getHeapU8', 'runtimeId', 'INTERNAL'];

function describeApi(a) {
    const has = INTERESTING.filter(k => typeof a[k] !== 'undefined');
    const miss = INTERESTING.filter(k => typeof a[k] === 'undefined');
    return '含：' + (has.length ? has.join(' / ') : '（无）') +
        '；缺：' + (miss.length ? miss.join(' / ') : '（无）') +
        (typeof a.runtimeId !== 'undefined' ? '；runtimeId=' + a.runtimeId : '');
}

// ---------------------------------------------------------------- ① 入口

/**
 * ①-a 官方全局入口：globalThis.getDotnetRuntime(runtimeId)。
 * 源码 exports.ts:71-77 明确写了它就是为了"能在页面全局找到 dotnet runtime"，
 * dotnet.d.ts:740 也把它声明成了全局函数。⑨ 之所以"一无所获"，是候选名单里没它。
 */
export function probeGlobalRuntime() {
    if (typeof globalThis.getDotnetRuntime !== 'function') {
        return "全局没有 getDotnetRuntime 函数";
    }
    try {
        const a = globalThis.getDotnetRuntime(0);
        if (!a) return "getDotnetRuntime(0) 返回 undefined（runtimeId 可能不是 0）";
        return "拿到 RuntimeAPI：" + describeApi(a);
    } catch (e) {
        return "调用抛错：" + msg(e);
    }
}

/** ①-b main.js 把 create() 的返回值注入进来 —— 不依赖全局符号，最稳。 */
export function probeInjectedApi() {
    if (!injectedApi) return "main.js 没注入 RuntimeAPI";
    return "拿到 RuntimeAPI：" + describeApi(injectedApi);
}

/**
 * ①-c 对照：老办法扫全局。
 * 这里刻意把候选分成"⑨ 当初扫的那 5 个"和"补上 getDotnetRuntime 之后"，
 * 让"⑨ 的结论错在候选名单不全"这件事在同一行里自证。
 */
export function probeLegacyScan() {
    const legacy = [
        () => globalThis.Module && globalThis.Module.wasmMemory && globalThis.Module.wasmMemory.buffer,
        () => globalThis.wasmMemory && globalThis.wasmMemory.buffer,
        () => globalThis.dotnet && globalThis.dotnet.wasmMemory && globalThis.dotnet.wasmMemory.buffer,
        () => globalThis.HEAPU8 && globalThis.HEAPU8.buffer,
        () => globalThis.Module && globalThis.Module.HEAPU8 && globalThis.Module.HEAPU8.buffer,
    ];

    let found = null;
    for (const get of legacy) {
        try {
            const b = get();
            if (b && b.byteLength > 0) { found = b; break; }
        } catch (e) { /* 继续找下一个 */ }
    }

    const hasGlobalFn = typeof globalThis.getDotnetRuntime === 'function';

    return "⑨ 当初那 5 个候选：" + (found ? "扫到了（byteLength=" + found.byteLength + "）" : "一个都没有") +
        "；补上 getDotnetRuntime 后：" + (hasGlobalFn ? "有 —— 入口一直在，只是没被扫到" : "也没有");
}

// ---------------------------------------------------------------- ② localHeapViewU8

/**
 * ② localHeapViewU8() —— 公开 API 里的主角（export-api.ts:51，dotnet.d.ts:629）。
 * 源码注释原话：Returns a short term view of the WASM linear memory.
 *               Don't store the reference, don't use it after await.
 *
 * 这里额外看一眼 buffer 是不是 SharedArrayBuffer —— 这直接决定"堆增长会不会 detach"：
 * SAB 是【不可 detach】的，若 buffer 是 SAB，则 Bench_HeapView ⑦ 担心的那个风险根本不成立。
 */
export function probeHeapViewU8() {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const fn = r.api.localHeapViewU8;
    if (typeof fn !== 'function') return "api 上没有 localHeapViewU8";

    try {
        const v = fn();
        const buf = v.buffer;
        const isSab = typeof SharedArrayBuffer !== 'undefined' && buf instanceof SharedArrayBuffer;
        return [
            '途径=' + r.via,
            '构造函数=' + ctorName(v),
            'instanceof Uint8Array=' + (v instanceof Uint8Array),
            'length=' + v.length,
            'byteOffset=' + v.byteOffset,
            'buffer.byteLength=' + buf.byteLength,
            'SharedArrayBuffer=' + isSab +
            (isSab ? '（SAB 不可 detach → 堆增长不会让视图失效）' : '（非 SAB → grow 会 detach，视图会静默失效）'),
        ].join('，');
    } catch (e) {
        return "调用抛错：" + msg(e);
    }
}

// ---------------------------------------------------------------- ③ 全套堆视图

const VIEW_FNS = [
    'localHeapViewU8', 'localHeapViewU16', 'localHeapViewU32',
    'localHeapViewI8', 'localHeapViewI16', 'localHeapViewI32', 'localHeapViewI64Big',
    'localHeapViewF32', 'localHeapViewF64',
];

/** ③ 全套 localHeapViewXxx：各返回什么类型、长度是否等于"字节数 ÷ 元素宽度"。 */
export function probeAllHeapViews() {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const parts = [];
    for (const name of VIEW_FNS) {
        const fn = r.api[name];
        if (typeof fn !== 'function') { parts.push(shortName(name) + '=缺失'); continue; }
        try {
            const v = fn();
            parts.push(shortName(name) + '=' + ctorName(v) + '/len ' + v.length);
        } catch (e) {
            parts.push(shortName(name) + '=抛错(' + msg(e) + ')');
        }
    }
    return parts.join('，');

    function shortName(n) {
        return n.startsWith('localHeapView') ? n.substring('localHeapView'.length) : n;
    }
}

// ---------------------------------------------------------------- ④ api.Module

/**
 * ④ api.Module（exports.ts:59 把它挂在了 RuntimeAPI 上）。
 * 它若是 Emscripten Module，上面就该有 HEAPU8 与 wasmMemory ——
 * 后者是 WebAssembly.Memory 本身，能直接读出 buffer.byteLength，
 * 于是"堆到底涨没涨"可以精确观测，不用再靠旧视图长度去猜。
 */
export function probeModule() {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const M = r.api.Module;
    if (!M) return "api 上没有 Module";

    const parts = [];

    parts.push('HEAPU8=' + (M.HEAPU8 ? ctorName(M.HEAPU8) + '/len ' + M.HEAPU8.length : '不存在'));

    // localHeapViewU8() 按源码就是 Module.HEAPU8，这里实测是不是同一个对象
    if (M.HEAPU8 && typeof r.api.localHeapViewU8 === 'function') {
        try {
            parts.push('HEAPU8===localHeapViewU8()=' + (M.HEAPU8 === r.api.localHeapViewU8()));
        } catch (e) { /* 忽略 */ }
    }

    parts.push('wasmMemory=' + (M.wasmMemory ? '有' : '不存在'));
    if (M.wasmMemory) {
        parts.push('wasmMemory.buffer.byteLength=' + M.wasmMemory.buffer.byteLength);
        if (typeof r.api.localHeapViewU8 === 'function') {
            try {
                parts.push('wasmMemory.buffer===localHeapViewU8().buffer=' +
                    (M.wasmMemory.buffer === r.api.localHeapViewU8().buffer));
            } catch (e) { /* 忽略 */ }
        }
    }

    return parts.join('，');
}

// ---------------------------------------------------------------- ⑤ 三路 buffer 是不是同一块

/**
 * ⑤ 一致性：三条来路取到的 buffer 是不是同一块 WASM 线性内存。
 *   路1：公开 API  localHeapViewU8().buffer
 *   路2：Module.wasmMemory.buffer
 *   路3：Bench_HeapView 现在在用的绕道 —— 引子 MemoryView._unsafe_create_view().buffer
 * 按 marshal.ts:481-493，路3 内部就是路1，所以三者应当【同一个对象】。
 * 同一，就说明绕道可以整个拆掉：直接用公开 API 即可。
 */
export function probeBufferIdentity(key) {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    let b1 = null, b2 = null, b3 = null;
    try { b1 = r.api.localHeapViewU8().buffer; } catch (e) { /* 下面会报 */ }
    try { b2 = r.api.Module && r.api.Module.wasmMemory ? r.api.Module.wasmMemory.buffer : null; } catch (e) { /* */ }

    let keyErr = '';
    if (key && typeof key._unsafe_create_view === 'function') {
        try { b3 = key._unsafe_create_view().buffer; } catch (e) { keyErr = msg(e); }
    } else {
        keyErr = '引子不是 MemoryView（没有 _unsafe_create_view）';
    }

    const all = b1 && b2 && b3;
    return [
        '路1 公开 localHeapViewU8().buffer=' + (b1 ? b1.byteLength : '无'),
        '路2 Module.wasmMemory.buffer=' + (b2 ? b2.byteLength : '无'),
        '路3 引子 _unsafe_create_view().buffer=' + (b3 ? b3.byteLength : '无（' + keyErr + '）'),
        '三者同一对象=' + (all ? (b1 === b2 && b2 === b3) : '无法比较（有缺失）'),
    ].join('；');
}

// ---------------------------------------------------------------- ⑥ 灵魂测试

/**
 * ⑥ 用【公开 API】重做 Bench_MemoryView 的 ⑪：
 * 拿 localHeapViewU8().buffer 配上 C# 钉住的数组地址建视图、写入。
 * 注意签名里【没有引子参数】 —— 这正是本模块想证明的：公开 API 足够，不必再绕 MemoryView。
 */
export function writeViaPublicApi(ptr, length, value) {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const fn = r.api.localHeapViewU8;
    if (typeof fn !== 'function') return "api 上没有 localHeapViewU8";
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住数组";

    try {
        const v = new Uint8Array(fn().buffer, ptr, length);
        for (let i = 0; i < v.length; i++) v[i] = value & 0xff;
        return "用公开 API 的 buffer 建视图成功（途径=" + r.via + "，byteOffset=" + v.byteOffset +
            "），已写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "建视图/写入失败：" + msg(e);
    }
}

// ---------------------------------------------------------------- ⑦ 单值读写

/** ⑦-a setHeapU8(offset, value)：按地址写一个字节 —— 连视图都不用建。 */
export function setU8At(ptr, value) {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const set = r.api.setHeapU8;
    if (typeof set !== 'function') return "api 上没有 setHeapU8";
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住数组";

    try {
        set(ptr, value & 0xff);
        return "setHeapU8(0x" + ptr.toString(16) + ", 0x" + (value & 0xff).toString(16).toUpperCase() + ") 已调用";
    } catch (e) {
        return "setHeapU8 失败：" + msg(e);
    }
}

/** ⑦-b getHeapU8(offset)：按地址读回一个字节。 */
export function getU8At(ptr) {
    const r = resolveRuntimeApi();
    if (!r.api) return "拿不到 RuntimeAPI";

    const get = r.api.getHeapU8;
    if (typeof get !== 'function') return "api 上没有 getHeapU8";
    if (!ptr) return "ptr 为 0 —— C# 侧没钉住数组";

    try {
        return "getHeapU8(0x" + ptr.toString(16) + ") = " + get(ptr);
    } catch (e) {
        return "getHeapU8 失败：" + msg(e);
    }
}

// ---------------------------------------------------------------- ⑧ 堆增长精确观测

let watch = null; // { mem, view, buf, memLen0 }

/**
 * ⑧-a 存下 wasmMemory 与一个视图，作为观测基线。
 * 有了 wasmMemory，就能直接读 buffer.byteLength —— 堆涨没涨是【读出来的】，不是猜出来的。
 */
export function startGrowWatch(ptr, length) {
    const r = resolveRuntimeApi();
    if (!r.api) return "存不下 —— 拿不到 RuntimeAPI";

    const mem = r.api.Module && r.api.Module.wasmMemory;
    if (!mem) return "存不下 —— api.Module 上没有 wasmMemory（无法精确观测，只能退回旧视图长度去猜）";
    if (!ptr) return "存不下 —— ptr 为 0";

    try {
        const buf = r.api.localHeapViewU8().buffer;
        const view = new Uint8Array(buf, ptr, length);
        watch = { mem: mem, view: view, buf: buf, memLen0: mem.buffer.byteLength };
        return "已存下：wasmMemory.buffer.byteLength=" + mem.buffer.byteLength + "，view.length=" + view.length;
    } catch (e) {
        return "存不下 —— 建视图失败：" + msg(e);
    }
}

/**
 * ⑧-b 只查询、不写入：精确回答"堆涨了没、旧 buffer detach 了没"。
 * 判据：wasmMemory.buffer.byteLength 变了 → 真涨了；旧 buffer.byteLength 归零 → detach。
 */
export function checkGrow() {
    if (!watch) return "没有存过观测基线";

    const memLen = watch.mem.buffer.byteLength;
    const bufLen = watch.buf.byteLength;
    const viewLen = watch.view.length;
    const memGrew = memLen !== watch.memLen0;
    const detached = bufLen === 0;

    return "初始 wasmMemory.buffer=" + watch.memLen0 + "，现在=" + memLen +
        "；旧 buffer.byteLength=" + bufLen + "，旧 view.length=" + viewLen +
        " → " + (detached
            ? "旧 buffer 已 detach ✘（视图随之静默失效）"
            : memGrew
                ? "内存确实增长了，但旧 buffer 未 detach"
                : "未增长，旧视图仍有效");
}

/** ⑧-c 真正往存的视图里写一次 —— 只有回读能定生死。 */
export function writeKeptView(value) {
    if (!watch) return "没有存过观测基线";

    const before = watch.view.length;
    try {
        for (let i = 0; i < watch.view.length; i++) watch.view[i] = value & 0xff;
        return "写之前 view.length=" + before + "，写之后 view.length=" + watch.view.length +
            "，已尝试写入 0x" + (value & 0xff).toString(16).toUpperCase();
    } catch (e) {
        return "写入失败：" + msg(e);
    }
}

/** ⑧-d 收尾：放掉跨调用持有的引用。 */
export function releaseGrowWatch() {
    watch = null;
    return "已释放";
}
