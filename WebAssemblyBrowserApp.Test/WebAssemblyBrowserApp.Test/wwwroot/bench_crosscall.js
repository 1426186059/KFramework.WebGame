// 【测试模块 Bench_CrossCall 的 JS 侧】"每帧跨 JS 调用 N 次"要付多少钱。
// 声明见 Bench_CrossCall/JSBind_CrossCall.cs。
//
// 一个测试模块一个 js，不共用其它模块的函数（理由见 bench_cstojs.js）。
// 接线：main.js 里 setModuleImports('bench_crosscall', crossCall) + crossCall.setCs(导出对象)。
//
// 【为什么单独测这个】
// Bench_ZeroCopy 实测：一次【空】跨界调用就要 ~3333 ns，而拷贝 4 MB 才 287 μs ——
// 换算下来，一次空跨界 ≈ 拷贝 47 KB。马里奥每帧传的顶点只有几十 KB（≈几 μs），
// 却可能跨几十上百次界（≈上百 μs）。也就是说：
//   **"每帧跨了多少次界"往往比"每帧拷了多少字节"更决定帧率。**
// 本模块把这件事量成一条曲线：N 从 0 到 1,000,000，看每帧耗时与占 60fps 预算的比例，
// 并直接给出"跨界占到预算 100% 时 N 是多少"—— 那就是每帧跨界的天花板。
//
// 与 Bench_CrossBoundary 的分工：那边比的是【方向】（C#→JS / JS→C#）与【数据类型】，
// 每次调用都带负载；这里只测【频率】，调用体是空的，纯粹量固定成本 × 次数。

/** 空跨界调用：什么都不做立刻返回。测的就是"过一次界"这个动作本身。 */
export function noop() {
    return 0;
}

// ============================================================ 交互部分

// C# 的导出（[JSExport] 的 CcRun / CcRunAll），由 main.js 经 setCs 注入。
let cs = null;

export function setCs(exports) {
    cs = exports || null;
}

const MAX_N = 1000000;

// 上一次实测出来的"每次跨界"成本（ns）。首次用 Bench_ZeroCopy 测到的 3333 ns 做预估，
// 实测后换成真值，让"预估耗时"越来越准。
let nsPerCallMeasured = 0;
const NS_PER_CALL_GUESS = 3333;

function clampN(v) {
    const n = Number.isFinite(v) ? Math.round(v) : 0;
    return Math.min(MAX_N, Math.max(0, n));
}

function fmtMs(ms) {
    if (ms >= 1000) return (ms / 1000).toFixed(2) + ' s';
    if (ms >= 1) return ms.toFixed(2) + ' ms';
    return (ms * 1000).toFixed(1) + ' μs';
}

function refreshEstimate() {
    const n = clampN(document.getElementById('ccInput').value);
    const per = nsPerCallMeasured > 0 ? nsPerCallMeasured : NS_PER_CALL_GUESS;
    const ms = n * per / 1e6;
    const pct = ms / (1000 / 60) * 100;

    const el = document.getElementById('ccEstimate');
    if (!el) return;
    el.textContent = n + ' 次 × ' + per.toFixed(0) + ' ns ≈ ' + fmtMs(ms) +
        '（约 60fps 预算的 ' + pct.toFixed(1) + '%）' +
        (nsPerCallMeasured > 0 ? '' : ' —— 按上次实测的 3333 ns/次估算，点一下"测这个次数"换成真值');
}

function setCount(n) {
    const v = clampN(n);
    document.getElementById('ccSlider').value = String(v);
    document.getElementById('ccInput').value = String(v);
    refreshEstimate();
}

async function runOne() {
    const out = document.getElementById('ccOut');
    if (!cs || typeof cs.CcRun !== 'function') {
        out.innerHTML = '<i>C# 导出未接上（main.js 的 setCs 没拿到 CcRun）</i>';
        return;
    }
    const n = clampN(document.getElementById('ccInput').value);
    out.innerHTML = '<i>运行中：每帧 ' + n.toLocaleString() + ' 次跨 JS 调用 ...</i>';
    try {
        const html = await cs.CcRun(n);
        out.innerHTML = html;
        // 从返回里把实测的单次成本抠出来，用于刷新预估（越来越准）
        const m = /每次跨界<\/td><td[^>]*>([\d.]+)\s*ns/.exec(html);
        if (m) nsPerCallMeasured = parseFloat(m[1]);
        refreshEstimate();
    } catch (e) {
        out.innerHTML = '<i>运行失败：' + (e && e.message ? e.message : String(e)) + '</i>';
    }
}

async function runAll() {
    const out = document.getElementById('ccOut');
    if (!cs || typeof cs.CcRunAll !== 'function') {
        out.innerHTML = '<i>C# 导出未接上（main.js 的 setCs 没拿到 CcRunAll）</i>';
        return;
    }
    out.innerHTML = '<i>运行中：全套预设档位（0 → 1,000,000），大档较慢请稍候 ...</i>';
    try {
        out.innerHTML = await cs.CcRunAll();
    } catch (e) {
        out.innerHTML = '<i>运行失败：' + (e && e.message ? e.message : String(e)) + '</i>';
    }
}

/**
 * 绑定滑块 / 输入框 / 按钮。由 main.js 在 runMain 之前调用
 * （runMain 不返回，其后不能写代码 —— 与事件委托同一个道理）。
 */
export function bindUI() {
    const slider = document.getElementById('ccSlider');
    const input = document.getElementById('ccInput');
    if (!slider || !input) return;   // 不在 crosscall 页面就直接跳过

    slider.addEventListener('input', () => {
        input.value = slider.value;
        refreshEstimate();
    });
    input.addEventListener('input', () => {
        slider.value = String(clampN(input.value));
        refreshEstimate();
    });

    document.getElementById('ccRun')?.addEventListener('click', runOne);
    document.getElementById('ccRunAll')?.addEventListener('click', runAll);

    document.querySelectorAll('[data-cc]').forEach(btn => {
        btn.addEventListener('click', () => setCount(parseInt(btn.dataset.cc, 10)));
    });

    refreshEstimate();
}
