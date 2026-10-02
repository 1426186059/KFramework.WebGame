// 【测试模块 Bench_CrossBoundary 的 JS 侧】同一操作三种走法，声明见 Bench_CrossBoundary/JSBind_CrossBoundary.cs。
//
// 本模块两个方向都要，所以这里<b>自带两套</b>：C#→JS 那半（cbEcho* / cbMakeArray / cbFillSpan）
// 与 JS→C# 那半（cbCall*N 连打 Cb_*）。不去借用 CsToJs / JsToCs 两个模块的函数 ——
// 借用就会把别人那边的写法细节混进本模块的对比里，而本模块测的正是"方向差异"。
//
// 接线：main.js 里 setModuleImports('bench_crossboundary', crossBoundary) 并 setCs(本模块的导出)。

// ---- 本模块自己的数据源与计时（与别的模块各写一份，互不 import）----

// 只此一块源缓冲：按最大档开足 2048 字节，初始化时填一次，之后全程复用。
// 不每次 new（那样把"生成数据"的成本反复计入），也不建 Map 按长度存好几份（没必要 —— 各档共用这一块）。
const SRC_CAP = 2048;
const src = new Uint8Array(SRC_CAP);
for (let i = 0; i < SRC_CAP; i++) src[i] = i & 0xff;

/**
 * 取源缓冲前 n 字节（subarray 是<b>视图</b>，不复制）。
 * 真正的拷贝发生在交付那一刻：view.set() 写进 C# 缓冲，或过界时运行时复制成托管 byte[]。
 */
const bytesOf = (n) => src.subarray(0, n);

const makeText = (len) => 'x'.repeat(len);

/** 连打 n 次 × rounds 轮，返回 "最快ms|最慢ms"。规则与 C# 侧 BenchKit.MeasureFixed 一致。 */
const timeIt = (n, fn, rounds = 5) => {
    const warm = Math.min(2000, Math.max(100, n >> 3));
    for (let i = 0; i < warm; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        if (cs && typeof cs.Cb_GcCollect === 'function') cs.Cb_GcCollect();   // 每轮前强制回收，不计入

        const t0 = performance.now();
        for (let i = 0; i < n; i++) fn(i);
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
        if (dt > worst) worst = dt;
    }
    return best + '|' + worst;
};

const timed = (n, fn) => timeIt(n, fn) + '|';

let cs = null;

/** main.js 注入 JSBind_CrossBoundary 的导出。 */
export function setCs(exports) { cs = exports; }

// ================= C# → JS（本模块自己的一份）=================

export function cbEchoInt(v) { return v; }

export function cbEchoString(s) { return s; }

export function cbMakeArray(n) { return bytesOf(n); }

/**
 * MemoryView 零拷贝：C# 提供缓冲，JS 用 view.set() 直写。
 * 【坑】视图没有 [] 索引器，view[i] = ... 只是给 JS 对象挂属性，托管内存一个字节都不会被写。
 */
export function cbFillSpan(view, n) {
    view.set(bytesOf(n), 0);
    return n;
}

// ================= JS → C#（连打本模块的 Cb_* 并回报）=================

export function cbCallTickN(n) {
    return cs ? timed(n, () => cs.Cb_Tick()) : '-1|-1|Cb_Tick 未导出';
}

export function cbCallIntN(n) {
    return cs ? timed(n, (i) => cs.Cb_Int(i)) : '-1|-1|Cb_Int 未导出';
}

export function cbCallStringN(n, len) {
    if (!cs) return '-1|-1|Cb_String 未导出';
    const s = makeText(len);
    return timed(n, () => cs.Cb_String(s));
}

export function cbCallBytesN(n, len) {
    if (!cs) return '-1|-1|Cb_Bytes 未导出';
    return timed(n, () => cs.Cb_Bytes(bytesOf(len)));
}

// JS→C# 的 MemoryView（预期被拒绝）：如实回报错误段，让这一格有结论而不是留白。
export function cbCallSpanN(n, len) {
    if (!cs || typeof cs.Cb_FillSpan !== 'function') return '-1|-1|Cb_FillSpan 未导出';
    try {
        return timed(n, () => cs.Cb_FillSpan(bytesOf(len), len));
    } catch (e) {
        return '-1|-1|' + String(e);
    }
}
