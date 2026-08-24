// =====================================================================
// JSEngine 入口：装配各层模块（渲染 / 输入 / 音效 / 存储），
// 注册 C# 桥接对象（setModuleImports），驱动 requestAnimationFrame 主循环。
//
// 重构后：渲染合批逻辑完全由 C#（WebGL.cs）侧管理。
// JS 层只暴露薄 API：glCore.init / gl.clear / gl.drawShapeBatch /
// gl.drawImageInstance / gl.loadImage / gl.bakeTextTexture。
// 不再由 JS 侧调用 resetFrameState 或 flushShapes。
// =====================================================================

import { dotnet } from '../_framework/dotnet.js'
import { gl } from './render/shapes.js'
import { input } from './core/input.js'
import { audio } from './core/audio.js'
import { storage } from './core/storage.js'
import { platform } from './core/platform.js'

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0
let _videoSeq = 0

// 注意：engine.initCanvas 原来的 fit() 逻辑已经移到 renderer.js
// glCore.init() 里，因为 C# GameEngine.Initialize 是直接调 gl.init
// 而不是 engine.initCanvas。这里保留 engine 对象的最小接口：startLoop。
const engine = {
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

// 暴露到 window 方便调试（仅调试用）
if (typeof window !== 'undefined') {
    window.__engine = {
        get rafStarted() { return _rafStarted },
        frame,
        engine,
        exports: null, // 下面赋值
        gl,
    }
}

// ------------------------- 资源加载（统一 assets 桥） -------------------------
// C# 侧 Assets 模块调用（共享引擎 WebEngineCommon/Engine/Assets.cs）。
// 内部以 url 作为纹理 id，返回 { id, w, h }（失败 id=-1），加载完成才 resolve。
const assets = {
    loadImage(url) {
        return gl.loadImage(url, url)
    },
    // 视频纹理（GPU 硬解）：数字 id 与图片 url id 隔离
    loadVideo(url) {
        return gl.loadVideo(_videoSeq++, url)
    },
    // 绘制图片 / 动态纹理（单位矩阵 + 不透明）
    drawImage(id, dx, dy, dw, dh) {
        gl.drawImageById(id, dx, dy, dw, dh, [1, 0, 0, 0, 1, 0, 0, 0, 1], 1)
    },
    // Texture2D：像素重传（内部走 'dyn:'+id 命名空间）
    uploadTexture(id, w, h, argb) {
        gl.uploadTexture(id, w, h, argb)
    },
    disposeTexture(id) {
        gl.disposeTexture(id)
    },
}

// ------------------------- 启动 -------------------------
// 着色器源码是独立 .vert / .frag 文件：与 dotnet 启动并行预取，
// 确保 C# 的 gl.init（GameEngine.Initialize）同步编译着色器时已就绪。
const shadersReady = gl.preloadShaders()

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', { gl, assets, input, audio, storage, platform, engine })

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

// 着色器就绪后再进入 C# 入口，避免 init 编译时缺失源码
await shadersReady
await runMain()
