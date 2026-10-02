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

// 计时辅助：连打 n 次，返回耗时（毫秒）
const timeIt = (n, fn) => {
    const t0 = performance.now();
    for (let i = 0; i < n; i++) fn(i);
    return performance.now() - t0;
};

setModuleImports('main.js', {
    dom: {
        setInnerText: (selector, text) => document.querySelector(selector).innerText = text,
        setInnerHTML: (selector, html) => document.querySelector(selector).innerHTML = html
    },

    // 互操作基准所需的 JS 侧实现，声明见 BenchInterop.cs
    bench: {
        // ---- C# → JS：C# 调这些，测 C#→JS 的纯开销与封送 ----
        noop: () => { },
        echoInt: (v) => v,
        echoString: (s) => s,

        // MemoryView：C# 提供缓冲，JS 直接往里写（零拷贝路径）
        fillSpan: (view, n) => {
            for (let i = 0; i < n; i++) view[i] = i & 0xff;
            return n;
        },

        // byte[]：JS 建好数组交给 C#（拷贝路径，且每次都会分配）
        makeArray: (n) => makeBytes(n),

        // ---- JS → C#：连打 N 次并回报耗时（毫秒）----
        callTickN: (n) => cs ? timeIt(n, () => cs.CsTick()) : -1,
        callIntN: (n) => cs ? timeIt(n, (i) => cs.CsEchoInt(i)) : -1,

        callStringN: (n, len) => {
            if (!cs) return -1;
            const s = makeText(len);
            return timeIt(n, () => cs.CsEchoString(s));
        },

        callBytesN: (n, len) => {
            if (!cs) return -1;
            const a = makeBytes(len);
            return timeIt(n, () => cs.CsEchoBytes(a));
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
