// =====================================================================
// JSEngine 入口：装配各层模块（渲染 / 输入 / 音效 / 存储），
// 注册 C# 桥接对象（setModuleImports），驱动 requestAnimationFrame 主循环。
//
// 模块结构：
//   jsengine/
//     render/          渲染层
//       shaders/       着色器（shape / image / blur 各一个文件）
//       renderer.js    WebGL 初始化 + 实例化批处理 + 共享 GL 状态
//       shapes.js      gl.* 绘制 API
//       text.js        文本渲染（离屏 Canvas2D → 纹理）
//     input/           输入层（键盘 / 鼠标 / 触摸）
//     audio/           音效层（WebAudio）
//     core/            本地存储
// =====================================================================

import { dotnet } from './_framework/dotnet.js'
import { flushShapes, resetFrameState, getCanvas } from './jsengine/render/renderer.js'
import { gl } from './jsengine/render/shapes.js'
import { input } from './jsengine/input/input.js'
import { audio } from './jsengine/audio/audio.js'
import { storage } from './jsengine/core/storage.js'

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0

const engine = {
    initCanvas(selector, width, height) {
        gl.init(selector, width, height)
        const fit = () => {
            const cv = getCanvas()
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
    },

    startLoop() {
        if (_rafStarted) return
        _rafStarted = true
        requestAnimationFrame(frame)
    },
}

// 主循环：每帧重置变换栈 → 驱动 C# Tick → flush 实例化绘制
function frame(ts) {
    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    try {
        resetFrameState()
        exports.GameBridge.Tick(dt)
        flushShapes()
    } catch (err) {
        console.error('[Engine] Tick 异常：', err)
    }
    requestAnimationFrame(frame)
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', { gl, input, audio, storage, engine })

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

await runMain()
