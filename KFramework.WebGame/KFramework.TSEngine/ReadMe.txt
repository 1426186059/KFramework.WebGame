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


【Release 去 console 的实现提示（尚未接入）】
当前 build 脚本是 `tsc && copy_deps.mjs`，tsc 本身不去 console。
要 Release 剥离 console，需在 tsc 之后再跑 terser/esbuild 的 drop_console
（或 babel-plugin-transform-remove-console），并区分 build / build:release 两个脚本。
注意：无论用哪种方案，都不要依赖 console.* 做断言（一律用本文件的 assert）。
