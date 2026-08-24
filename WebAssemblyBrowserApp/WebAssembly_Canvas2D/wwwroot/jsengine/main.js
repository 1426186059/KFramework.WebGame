// =====================================================================
// .NET 10 WebAssembly 2D 游戏引擎 —— JS 桥接层（组装入口）
//
// 各底层能力已拆分到 jsengine/ 下：
//   jsengine/render/renderer.js    Canvas2D 绘制原语 + canvas 生命周期
//   jsengine/render/textures.js    图片 / 动态纹理（Texture2D）与统一绘制
//   jsengine/render/video.js       视频纹理（GPU 硬解，独立文件）
//   jsengine/core/input.js         键盘 / 鼠标 / 触摸
//   jsengine/core/audio.js         WebAudio 音效
//   jsengine/core/storage.js       本地存储
//   jsengine/core/platform.js      平台能力薄层
// 本文件只做：组装对象 + 主循环 + 注册 JSInterop 模块。
// =====================================================================

import { dotnet } from '../_framework/dotnet.js'
import { canvas2d } from './render/renderer.js'
import { textures } from './render/textures.js'
import { videoTex } from './render/video.js'
import { input } from './core/input.js'
import { audio } from './core/audio.js'
import { storage } from './core/storage.js'
import { platform } from './core/platform.js'

// ------------------------- 全局状态 -------------------------
let _rafStarted = false
let _lastTs = 0

// ------------------------- 引擎生命周期 -------------------------
const engine = {
    initCanvas(selector, width, height) {
        canvas2d.init(selector, width, height)
        const cv = canvas2d.getCanvas()
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
        document.getElementById('loading')?.remove()
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

// ------------------------- 资源加载（统一 assets 桥） -------------------------
// C# 侧 Assets / Texture2D 模块调用（共享引擎 WebEngineCommon/Engine/）。
// 统一返回 { id, w, h }（失败 id=-1），加载完成才 resolve。
const assets = {
    loadImage(url) { return textures.loadImage(url) },
    loadVideo(url) { return videoTex.load(url) },
    drawImage(id, dx, dy, dw, dh) { textures.draw(id, dx, dy, dw, dh) },
    uploadTexture(id, w, h, argb) { textures.uploadTexture(id, w, h, argb) },
    disposeTexture(id) { textures.disposeTexture(id) },
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    engine,
    canvas: canvas2d,
    assets,
    input,
    audio,
    storage,
    platform,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

// 运行 C# Main()：引擎初始化完成后会回调 engine.startLoop 启动主循环
await runMain()
