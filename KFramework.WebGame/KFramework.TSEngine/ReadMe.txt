KFramework.TSEngine · 断言约定 ReadMe
====================================

本引擎所有契约检查（断言）统一使用 src/cusotm_func.ts 导出的 assert(cond, msg)。


【为什么不用 console.assert】
本引擎发布 Release 版本时会剥离所有 console.*（drop_console / 把 console 替换成空 stub），
console.assert 属于 console.*，会被一并删除 —— 断言逻辑（条件为假才报错）随之整体消失。
因此任何“断言”都不得直接写 console.assert，必须走统一 assert。


【统一 assert 的特点】
- 只 throw，不依赖 console，所以 Release 剥离 console 后依然生效。
- 条件为假时抛 Error('[assert] ' + msg)，调用方照常捕获/崩溃，便于暴露 bug。


【用法】
    import { assert } from './cusotm_func.js';
    assert(w < 32767 && h < 32767,
        'decodeImageToRgbaAsync1 尺寸越界：w=' + w + ' h=' + h + '（须 < 32767 / short.MaxValue）');


【已统一接入的断言点】
- src/texture.ts                ：decodeImageToRgbaAsync1 检查 w/h < short.MaxValue(32767)
- src/html_canvas.ts            ：IsFocus() 检查 canvas 非空
- src/input_window_event.ts     ：bindWindowEvents() / reportWindowFocus() 检查 canvas 非空
- src/custom_data_byte_cache.ts ：assertPowerOfTwo() 内部改用统一 assert


【普通日志 console.log 怎么办】
console.log 是日志而非断言，Release 下会被正常剥离（这是预期行为，不影响断言）。
不要把断言写成 console.log + 条件判断，统一用 assert。


【Release 去 console / 压缩 —— 已接入】
tsc 本身既不去 console 也不压缩，所以 Release 走的是另一条脚本：
    npm run build           → dist/Debug/jsengine   ：未压产物，保留注释与 console，供开发调试
    npm run build:release   → dist/Release/jsengine ：额外跑 node minify.mjs，terser 压缩 + drop_console
两种产物【分配置并存、互不覆盖】，Debug / Release 反复构建不会互相踩。
各工程 csproj 的 BuildTypeScript 按 $(Configuration) 自动选脚本，SyncJsEngine 按同一配置取源；
在 VS 里直接点 KFramework.TSEngine 工程「生成」也按配置走（见 .esproj 的 BuildCommand）。
实现见 KFramework.TSEngine/minify.mjs（只压该目录顶层的 *.js，不碰第三方 deps/）。
注意（不变）：断言一律走本文件的 assert（只 throw），不要用 console.* ——
它在 Release 下会被整体删除，用它写的"断言"会连逻辑一起消失。
