// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// 浏览器启动器：<b>只做引导</b> —— 启动 dotnet、取出 C# 的导出、分发给各测试模块的 js、然后跑 Main。
//
// 这里<b>不写任何测试逻辑</b>：每个测试模块一个 js 文件（bench_cstojs / bench_jstocs /
// bench_crossboundary / bench_frameloop），各自自带数据源与计时、互不 import。
// 只有宿主设施（dom 读写、当前页面名）留在本文件，它们不属于任何测试模块。

import { dotnet } from './_framework/dotnet.js';
import * as csToJs from './bench_cstojs.js';
import * as jsToCs from './bench_jstocs.js';
import * as crossBoundary from './bench_crossboundary.js';
import * as frameLoop from './bench_frameloop.js';

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

// 每个模块自带一份 [JSExport]，方法名带模块前缀（JsToCs_* / Cb_* / Fl_*），
// 因此这里按前缀各取一份、各交各的 —— 取错一份就会"测的数字不是这个模块的"。
// CsToJs 模块只有 [JSImport]（C# 单向调出去），不需要回调。
const jsToCsExp = findExport(exports, 'JsToCs_Int');
const cbExp = findExport(exports, 'Cb_Int');
const flExp = findExport(exports, 'Fl_Push');

console.log("[bench] 顶层导出键:", Object.keys(exports));
console.log("[bench] BenchRunner:", runner ? "已找到" : "未找到");
console.log("[bench] JSBind_JsToCs:", jsToCsExp ? "已找到" : "未找到");
console.log("[bench] JSBind_CrossBoundary:", cbExp ? "已找到" : "未找到");
console.log("[bench] JSBind_FrameLoop:", flExp ? "已找到" : "未找到");

if (!runner) console.error("[bench] 找不到 RunOne 导出，总纲按钮将无法运行");
if (!jsToCsExp) console.error("[bench] 找不到 JsToCs_Int 导出，JS→C# 测试会拿不到数据");
if (!cbExp) console.error("[bench] 找不到 Cb_Int 导出，跨界方向对比会拿不到数据");
if (!flExp) console.error("[bench] 找不到 Fl_Push 导出，帧循环测试会拿不到数据");

// C# 的导出不是全局变量，需要回调 C# 的模块必须显式拿到自己那份
jsToCs.setCs(jsToCsExp);
crossBoundary.setCs(cbExp);
frameLoop.setCs(flExp);

// 注册 C# [JSImport] 使用的模块。模块名必须与 C# 里 [JSImport("函数名", "模块名")] 一致。
setModuleImports('main.js', {
    // 宿主设施：结果区渲染。不属于任何测试模块，故留在启动器里。
    dom: {
        setInnerText: (selector, text) => document.querySelector(selector).innerText = text,
        setInnerHTML: (selector, html) => document.querySelector(selector).innerHTML = html
    },

    // 当前页面文件名（不含扩展名）—— C# 入口据此决定自动运行哪个模块。
    // index.html 会得到 "index"，不匹配任何模块的 Page，于是只显示总纲。
    currentPage: () => {
        const file = (location.pathname.split('/').pop() || 'index').toLowerCase();
        return file.replace(/\.html$/, '');
    },
});

// 四个测试模块，各自的 js 文件（Reflection 是纯 C# 反射，不需要 js）
setModuleImports('bench_cstojs', csToJs);
setModuleImports('bench_jstocs', jsToCs);
setModuleImports('bench_crossboundary', crossBoundary);
setModuleImports('bench_frameloop', frameLoop);

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
