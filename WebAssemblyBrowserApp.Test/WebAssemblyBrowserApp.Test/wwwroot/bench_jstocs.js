// 【测试模块 Bench_JsToCs 的 JS 侧】让 JS 连打 C#，声明见 Bench_JsToCs/JSBind_JsToCs.cs。
//
// 一个测试模块一个 js，且<b>不共用任何其它模块的函数</b>：数据源、计时都各写一份。
// 只调本模块自己的导出（JsToCs_*）—— 调别人那份就等于在测别人的写法。
//
// 接线：main.js 里 setModuleImports('bench_jstocs', jsToCs) 并 setCs(本模块的导出)。

// ---- 本模块自己的数据源与计时（与别的模块各写一份，互不 import）----

// 只此一块源缓冲：按最大档开足 2048 字节，初始化时填一次，之后全程复用。
// 不每次 new（那样把"生成数据"的成本反复计入），也不建 Map 按长度存好几份（没必要 —— 各档共用这一块）。
const SRC_CAP = 2048;
const src = new Uint8Array(SRC_CAP);
for (let i = 0; i < SRC_CAP; i++) src[i] = i & 0xff;

/**
 * 取源缓冲前 n 字节（subarray 是<b>视图</b>，不复制）。
 * 真正的拷贝发生在交付那一刻：过界时运行时复制成托管 byte[]。
 */
const bytesOf = (n) => src.subarray(0, n);

const makeText = (len) => 'x'.repeat(len);

/**
 * 连打 n 次 × rounds 轮，返回 "最快ms|最慢ms"（两个值一起过界，故返回字符串）。
 *
 * 规则与 C# 侧 BenchKit.MeasureFixed 对齐，两边才可比：
 *   * 预热不计入：首次跨界含绑定解析；
 *   * 取最快轮：干扰（GC、主线程被调度走）只会让某轮变慢，不会让它变快；
 *   * 每轮开始前强制回收 —— JS 触发不了 WASM 的 GC，只能借 cs 跨界叫 C# 收一次
 *     （这一次跨界发生在该轮计时【之前】，不计入）。
 */
const timeIt = (n, fn, rounds = 5) => {
    const warm = Math.min(2000, Math.max(100, n >> 3));
    for (let i = 0; i < warm; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        if (cs && typeof cs.JsToCs_GcCollect === 'function') cs.JsToCs_GcCollect();

        const t0 = performance.now();
        for (let i = 0; i < n; i++) fn(i);
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
        if (dt > worst) worst = dt;
    }
    return best + '|' + worst;
};

/** 统一格式 "最快ms|最慢ms|错误"（错误段留空 = 这条路线走得通）。 */
const timed = (n, fn) => timeIt(n, fn) + '|';

// ---- C# 的导出对象：由 main.js 注入（不是全局变量，必须显式传进来）----

let cs = null;

/** main.js 注入 JSBind_JsToCs 的导出。 */
export function setCs(exports) { cs = exports; }

export function callTickN(n) {
    return cs ? timed(n, () => cs.JsToCs_Tick()) : '-1|-1|JsToCs_Tick 未导出';
}

export function callIntN(n) {
    return cs ? timed(n, (i) => cs.JsToCs_Int(i)) : '-1|-1|JsToCs_Int 未导出';
}

export function callStringN(n, len) {
    if (!cs) return '-1|-1|JsToCs_String 未导出';
    // 字符串复用同一份：它没有"先拷进缓冲"这一步，封送由运行时直接完成，复用不会失真
    // （字节块走 bytesOf 取源缓冲的视图，拷贝发生在交付那一刻 —— 见 bytesOf 的注释）。
    const s = makeText(len);
    return timed(n, () => cs.JsToCs_String(s));
}

export function callBytesN(n, len) {
    if (!cs) return '-1|-1|JsToCs_Bytes 未导出';
    return timed(n, () => cs.JsToCs_Bytes(bytesOf(len)));   // 每次都拷一份新的
}

// JS → C# 的 MemoryView：C# 侧声明 Span<byte> + JSMarshalAs<MemoryView>，
// 而 JS 只能给出 Uint8Array —— 运行时要求的是它内部的 MemoryView 对象，
// 于是断言 "Expected MemoryViewType.Byte" 并抛错。
// 这里如实回报错误段（留空 = 走得通），让表格这一格有结论，而不是留白。
export function callSpanN(n, len) {
    if (!cs || typeof cs.JsToCs_FillSpan !== 'function') return '-1|-1|JsToCs_FillSpan 未导出';
    try {
        return timed(n, () => cs.JsToCs_FillSpan(bytesOf(len), len));
    } catch (e) {
        return '-1|-1|' + String(e);
    }
}
