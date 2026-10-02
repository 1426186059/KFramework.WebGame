// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

import { dotnet } from './_framework/dotnet.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

/**
 * 在导出树里递归查找"含有指定方法"的对象。
 *
 * 为什么不直接写 exports.BenchRunner：不同 .NET 版本 / 有无命名空间的情况下，
 * 导出的层级并不固定，写死路径容易静默失效（表现就是点了按钮没反应）。
 * 这里照 KFramework 的 main.ts 里 findHost 的兜底策略，按方法名下钻查找。
 */
function findExport(root, methodName) {
    if (!root || typeof root !== 'object') return null;
    const stack = [root];
    while (stack.length > 0) {
        const node = stack.pop();
        if (!node || typeof node !== 'object') continue;
        const fn = node[methodName];
        if (typeof fn === 'function') return node;
        for (const v of Object.values(node)) {
            if (v && typeof v === 'object') stack.push(v);
        }
    }
    return null;
}

const runner = findExport(exports, 'RunOne');
const cs = findExport(exports, 'CsEchoInt');   // C# 侧导出的互操作入口（BenchInterop）

console.log("[bench] 顶层导出键:", Object.keys(exports));
console.log("[bench] BenchRunner:", runner ? "已找到" : "未找到");
console.log("[bench] BenchInterop:", cs ? "已找到" : "未找到");

if (!runner) console.error("[bench] 找不到 RunOne 导出，总纲按钮将无法运行");
if (!cs) console.error("[bench] 找不到 CsEchoInt 导出，互操作测试会拿不到数据");

// 生成指定长度的测试载荷（字符串 / 字节数组）
const makeText = (len) => 'x'.repeat(len);
const makeBytes = (len) => {
    const a = new Uint8Array(len);
    for (let i = 0; i < len; i++) a[i] = i & 0xff;
    return a;
};

// 按长度缓存一份"待交付的 n 字节"，供下面两条 byte 路线共用。
// 这样 MemoryView 与 byte[] 两档的差别就只剩【交付方式】，而不是"谁顺便多填了一次数组"。
const srcCache = new Map();
const spanSrc = (n) => {
    let a = srcCache.get(n);
    if (!a) { a = makeBytes(n); srcCache.set(n, a); }
    return a;
};

// 计时辅助：连打 n 次 × rounds 轮，返回 "最快ms|最慢ms"（字符串 —— 两个值要一起过界）。
//
// 预热与多轮取最快，都与 C# 侧的 BenchKit.MeasureFixed 对齐，两边规则一致才有可比性：
//   * 预热不计入：首次跨界含绑定解析；
//   * 取最快轮：浏览器里的干扰（GC、主线程被调度走）只会让某一轮变慢，不会让它变快。
//     只跑一轮时这种干扰无处可查 —— 实测出现过"2048B 比 16B 还快、且比空调用便宜"这种不合物理的数字。
//
// 【次数固定】n 由 C# 传入，与同一组里其它行（含 C# 侧计时的基线）用的是同一个数 ——
// 各行次数必须相同，否则"耗时(ms)"这一列毫无意义。
// rounds 与 C# 侧 BenchKit.Rounds 一致（都是 5 轮）
const timeIt = (n, fn, rounds = 5) => {
    const warm = Math.min(2000, Math.max(100, n >> 3));
    for (let i = 0; i < warm; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        const t0 = performance.now();
        for (let i = 0; i < n; i++) fn(i);
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
        if (dt > worst) worst = dt;
    }
    return best + '|' + worst;
};

// 把 timeIt 的结果拼成统一的 "最快|最慢|错误" 三段（错误段留空 = 这条路线走得通）
const timed = (n, fn) => timeIt(n, fn) + '|';

setModuleImports('main.js', {
    dom: {
        setInnerText: (selector, text) => document.querySelector(selector).innerText = text,
        setInnerHTML: (selector, html) => document.querySelector(selector).innerHTML = html
    },

    // 互操作基准所需的 JS 侧实现，声明见 BenchInterop.cs
    bench: {
        // 当前页面文件名（不含扩展名）—— C# 入口据此决定自动运行哪个模块。
        // index.html 会得到 "index"，不匹配任何模块的 Page，于是只显示总纲。
        currentPage: () => {
            const file = (location.pathname.split('/').pop() || 'index').toLowerCase();
            return file.replace(/\.html$/, '');
        },

        // ---- C# → JS：C# 调这些，测 C#→JS 的纯开销与封送 ----
        noop: () => { },
        echoInt: (v) => v,
        echoString: (s) => s,

        // MemoryView：C# 提供缓冲，JS 直接往里写（零拷贝路径）。
        // 【坑】MemoryView 不是 TypedArray：没有 [] 索引器、也不接受单元素 set(i, v)，
        // 只有 set(源, 偏移) / copyTo / slice。早先写成 view[i] = ... —— 那只是给 JS 对象挂普通属性，
        // 托管内存一个字节都没被写，计时却照常出数，属于"看着有结果、其实没干活"。
        fillSpan: (view, n) => {
            view.set(spanSrc(n), 0);
            return n;
        },

        // byte[]：JS 建好数组交给 C#（拷贝路径，每次都要分配 + 运行时再复制一次）。
        // 与 fillSpan 共用同一份源，两条路线唯一的差别就是"怎么把字节交到 C# 手里"。
        makeArray: (n) => new Uint8Array(spanSrc(n)),

        // C# 把 byte[] 传进来（封送时复制一次，JS 拿到的是副本）—— 测"传入"这条方向
        sendBytes: (bytes) => bytes.length,

        // ---- JS → C#：连打 N 次并回报 "最快ms|最慢ms|错误" ----
        // 找不到导出时返回 -1 并带上原因，C# 侧会把它标成"不可用"而不是当成耗时
        callTickN: (n) => cs ? timed(n, () => cs.CsTick()) : '-1|-1|CsTick 未导出',
        callIntN: (n) => cs ? timed(n, (i) => cs.CsEchoInt(i)) : '-1|-1|CsEchoInt 未导出',

        callStringN: (n, len) => {
            if (!cs) return '-1|-1|CsEchoString 未导出';
            const s = makeText(len);
            return timed(n, () => cs.CsEchoString(s));
        },

        callBytesN: (n, len) => {
            if (!cs) return '-1|-1|CsEchoBytes 未导出';
            const a = makeBytes(len);
            return timed(n, () => cs.CsEchoBytes(a));
        },

        // JS → C# 的 MemoryView：C# 侧声明 Span<byte> + JSMarshalAs<MemoryView>，
        // 而 JS 只能给出 Uint8Array —— 运行时要求的是它内部的 MemoryView 对象，
        // 于是断言 "Expected MemoryViewType.Byte" 并抛错。
        // 这里如实回报错误段（留空 = 走得通），让表格里这一格有结论，而不是留白。
        callSpanN: (n, len) => {
            if (!cs || typeof cs.CsFillSpan !== 'function') return '-1|-1|CsFillSpan 未导出';
            const a = makeBytes(len);
            try {
                return timed(n, () => cs.CsFillSpan(a, len));
            } catch (e) {
                return '-1|-1|' + String(e);
            }
        }
    }
});

// 统一包一层：C# 侧 [JSExport] 是 async，异常会变成 rejected Promise，
// 不 catch 的话控制台看不到，表现就是"点了没反应"。
function callCsharp(promise, label) {
    if (!promise || typeof promise.catch !== 'function') {
        console.error("[bench] " + label + " 未返回 Promise");
        return;
    }
    promise.catch(err => console.error("[bench] " + label + " 失败:", err));
}

// 事件委托：结果区会被反复重渲染，绑在 document 上才不会随着重绘失效。
//
// 【关键】必须挂在 runMain() 之前：C# 入口（Program.cs 顶层语句）末尾是
//   while (true) { await Task.Delay(1000); }
// 用于维持 wasm 运行时存活，因此它【永远不会返回】——
// await runMain() 之后的代码一行都不会执行（之前按钮无反应就是这个原因）。
document.addEventListener('click', (e) => {
    const t = e.target;
    if (!t || typeof t.closest !== 'function') return;

    const runBtn = t.closest('button[data-bench]');
    if (runBtn) {
        const idx = parseInt(runBtn.dataset.bench, 10);
        console.log("[bench] 点击运行 #" + idx);
        if (runner) callCsharp(runner.RunOne(idx), "RunOne(" + idx + ")");
        return;
    }

    if (t.id === 'runAll') {
        console.log("[bench] 点击运行全部");
        if (runner) callCsharp(runner.RunAll(), "RunAll");
    } else if (t.id === 'showMenu') {
        if (runner) runner.ShowMenu();
    }
});

console.log("[bench] 事件已挂载，即将启动 C# 入口");

// 跑 C# Main（渲染总纲）。注意：它不会返回，其后不要再写任何代码。
await runMain();
