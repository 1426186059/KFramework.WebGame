// 【测试模块 Bench_FrameLoop 的 JS 侧】帧循环：一趟推 vs 两趟拉，声明见 Bench_FrameLoop/JSBind_FrameLoop.cs。
//
// 不共用其它模块的任何函数，数据源与计时都是本模块自己的一份。
//
// 三条（含参照）都由 JS 侧整体计时：一次 fn 调用 = 一帧的工作量。
// B 的【第二趟】发生在 C# 的导出方法内部（C# 再回头调 pullFill），而 JS 侧照样只调一次函数，
// 所以计到的是一帧的完整成本，与 A 口径一致。
//
// 接线：main.js 里 setModuleImports('bench_frameloop', frameLoop) 并 setCs(本模块的导出)。

// ---- 本模块自己的数据源与计时（与别的模块各写一份，互不 import）----

// 只此一块源缓冲：按最大档开足 2048 字节，初始化时填一次，之后全程复用。
// 不每次 new（那样把"生成数据"的成本反复计入），也不建 Map 按长度存好几份（没必要 —— 各档共用这一块）。
const SRC_CAP = 2048;
const src = new Uint8Array(SRC_CAP);

/**
 * 取源缓冲前 n 字节（subarray 是<b>视图</b>，不复制）。
 * 真正的拷贝发生在交付那一刻：view.set() 写进 C# 缓冲，或过界时运行时复制成托管 byte[]。
 */
const bytesOf = (n) => src.subarray(0, n);

/** 连打 n 次 × rounds 轮，返回 "最快ms|最慢ms"。规则与 C# 侧 BenchKit.MeasureFixed 一致。 */
const timeIt = (n, fn, rounds = 5) => {
    const warm = Math.min(2000, Math.max(100, n >> 3));
    for (let i = 0; i < warm; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        if (cs && typeof cs.Fl_GcCollect === 'function') cs.Fl_GcCollect();   // 每轮前强制回收，不计入

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

/** main.js 注入 JSBind_FrameLoop 的导出。 */
export function setCs(exports) { cs = exports; }

/** 参照行：连打一次不带载荷的跨界 —— 一次跨界的单价。 */
export function callFrameTickN(n) {
    return cs ? timed(n, () => cs.Fl_Tick()) : '-1|-1|Fl_Tick 未导出';
}

/**
 * 【A 一趟推】JS 把一帧的输入事件字节流随帧回调一起送进来：只过界一次。
 */
export function callFramePushN(n, len) {
    if (!cs || typeof cs.Fl_Push !== 'function') return '-1|-1|Fl_Push 未导出';
    try {
        return timed(n, () => cs.Fl_Push(0, bytesOf(len)));
    } catch (e) {
        return '-1|-1|' + String(e);
    }
}

/**
 * 【B 两趟拉】JS 只推帧（带 timestamp、不带数据），C# 再回头调 JS 取。
 * 第一趟照样带 timestamp：A 的 Frame(ts, data) 与 B 的 Frame(ts) 只差"带不带数据"，
 * 若 B 连 ts 都不带，A 就白多付一个参数的封送。
 */
export function callFramePullSpanN(n, len) {
    if (!cs || typeof cs.Fl_PullSpan !== 'function') return '-1|-1|Fl_PullSpan 未导出';
    try {
        return timed(n, () => cs.Fl_PullSpan(0, len));   // 第二趟 pullFill 在 C# 内部发生
    } catch (e) {
        // 第二趟是嵌套跨界（JS→C#→JS），万一运行时不允许，如实报告而不是让页面崩掉
        return '-1|-1|' + String(e);
    }
}

/** B 的【第二趟】：C# 把缓冲借给 JS 直写（零拷贝）。 */
export function pullFill(view, n) {
    view.set(bytesOf(n), 0);
    return n;
}

/**
 * 抽验：走<b>真实过界路径</b>各跑一次，再把 C# 侧的校验结果读回来。
 * 之所以不让 C# 自己调自己 —— 那样跳过了封送，验的就不是"过界"这件事。
 * @returns bit0 = 一趟推收到完整字节，bit1 = 两趟拉写进了复用缓冲；-1 = 未导出/失败
 */
export function verifyFrameOnce(len) {
    if (!cs || typeof cs.Fl_VerifyBegin !== 'function') return -1;
    try {
        cs.Fl_VerifyBegin();
        cs.Fl_Push(0, bytesOf(len));
        cs.Fl_PullSpan(0, len);
        return cs.Fl_VerifyEnd();
    } catch (e) {
        console.error('[bench] verifyFrameOnce 失败:', e);
        return -1;
    }
}
