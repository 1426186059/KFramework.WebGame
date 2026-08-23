// =====================================================================
// JSEngine 入口：装配各层模块（渲染 / 输入 / 音效 / 存储），
// 注册 C# 桥接对象（setModuleImports），驱动 requestAnimationFrame 主循环。
//
// 重构后：渲染合批逻辑完全由 C#（WebGL.cs）侧管理。
// JS 层只暴露薄 API：glCore.init / gl.clear / gl.drawShapeBatch /
// gl.drawImageInstance / gl.loadImage / gl.bakeTextTexture。
// 不再由 JS 侧调用 resetFrameState 或 flushShapes。
// =====================================================================

import { dotnet } from './_framework/dotnet.js'
import { glCore } from './jsengine/render/renderer.js'
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
            const cv = glCore.getCanvas()
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

// 主循环：每帧只驱动 C# Tick。
// C# 侧 WebGL.cs 每帧开始时自己重置状态（ResetFrameState），
// 每帧 Render() 结束后自己 FlushShapes()。
function frame(ts) {
    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    try {
        exports.GameBridge.Tick(dt)
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
