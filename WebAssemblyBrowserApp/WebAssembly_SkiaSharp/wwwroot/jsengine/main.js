// =====================================================================
// .NET 10 WebAssembly + SkiaSharp 2D 游戏引擎 —— JS 桥接层
//
//   jsengine/render/skia.js   Skia 光栅化结果的像素呈现（putImageData）
//   jsengine/core/input.js    键盘 / 鼠标 / 触摸
//   jsengine/core/audio.js    WebAudio 音效
//   jsengine/core/storage.js  本地存储
// 本文件只做：组装对象 + 主循环 + 注册 JSInterop 模块。
// =====================================================================

import { dotnet } from '../_framework/dotnet.js'
import { skia } from './render/skia.js'
import { input } from './core/input.js'
import { audio } from './core/audio.js'
import { storage } from './core/storage.js'

// ------------------------- 全局状态 -------------------------
let _rafStarted = false
let _lastTs = 0

// ------------------------- 引擎生命周期 -------------------------
const engine = {
    initCanvas(selector, width, height) {
        skia.init(selector, width, height)
        const cv = skia.getCanvas()
        const fit = () => {
            if (!cv) return
            const scale = Math.min(
                (window.innerWidth - 24) / width,
                (window.innerHeight - 24) / height
            )
            const s = Math.min(1.6, Math.max(0.25, scale))
            cv.style.width = (width * s) + 'px'
            cv.style.height = (height * s) + 'px'
        }
        window.addEventListener('resize', fit)
        fit()
        // 启动遮罩移除：先隐藏再彻底删除，确保 headless 截图等场景也不残留
        const loading = document.getElementById('loading')
        if (loading) { loading.style.display = 'none'; loading.remove() }
    },

    // 由 C# Main 在初始化完成后调用，启动主循环
    startLoop() {
        if (_rafStarted) return
        _rafStarted = true
        requestAnimationFrame(frame)
    },
}

// 主循环：requestAnimationFrame → C# GameBridge.Tick(dt)
function frame(ts) {
    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    try {
        exports.GameBridge.Tick(dt)
    }
    catch (err) {
        console.error('[Engine] Tick 异常：', err)
    }
    requestAnimationFrame(frame)
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    engine,
    skia,
    input,
    audio,
    storage,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

// 运行 C# Main()：引擎初始化完成后会回调 engine.startLoop 启动主循环
await runMain()
