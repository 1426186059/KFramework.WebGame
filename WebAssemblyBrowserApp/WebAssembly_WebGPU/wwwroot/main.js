// =====================================================================
// JSEngine 入口：装配各层模块（渲染 / 输入 / 音效 / 存储），
// 注册 C# 桥接对象（setModuleImports），驱动 requestAnimationFrame 主循环。
//
// 薄 API 模式（与 WebGL 层一致）：
//   - C# 侧（WebGPU.cs / GameEngine.cs）负责合批缓冲、状态栈、场景调度
//   - JS 侧仅暴露 gpu / input / audio / storage 等薄 API
// =====================================================================

import { dotnet } from './_framework/dotnet.js'
import { gpu } from './jsengine/render/renderer.js'
import { input } from './jsengine/input/input.js'
import { audio } from './jsengine/audio/audio.js'
import { storage } from './jsengine/core/storage.js'

// ------------------------- 调试工具：主循环计数器（轻量） -------------------------
const _dbg = {
  tickEnterCount: 0,
  tickReturnCount: 0,
}

// 无代理模式（保持最小开销）
const gpuProxy     = gpu
const inputProxy   = input
const audioProxy   = audio
const storageProxy = storage

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0

const engine = {
    startLoop() {
        console.log('[JS] engine.startLoop() called (rafStarted=' + _rafStarted + ')')
        if (_rafStarted) return
        _rafStarted = true
        requestAnimationFrame(frame)
    },
}

// 主循环：每帧只驱动 C# Tick。
// C# 侧 WebGPU.cs 每帧结束后自己 Flush() 提交整批。
let _tickCount = 0
function frame(ts) {
    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    _tickCount++
    _dbg.tickEnterCount++
    try {
        // ----- 诊断：先空跑 rAF（不调 C# Tick），确认 rAF/JS 层不挂 -----
        if (_tickCount % 30 === 0) {
            if (window._dbg) window._dbg.clears = (window._dbg.clears || 0) + 1
        }
        _dbg.tickReturnCount++
        // exports.GameBridge.Tick(dt)
    } catch (err) {
        console.error('[Engine] Tick 异常：', err)
        if (window._dbg) {
            window._dbg.lastTickError = {
                message: err?.message ?? String(err),
                stack: (err?.stack ?? '').substring(0, 800),
                atTick: _tickCount,
            }
        }
    }
    requestAnimationFrame(frame)
}

// 暴露到 window 方便调试
if (typeof window !== 'undefined') {
    window.__engine = {
        get rafStarted() { return _rafStarted },
        frame,
        engine,
        exports: null,
        gpu,
        _dbg,
    }
    // 让 window._dbg 始终指向 main.js 里唯一的这一份 _dbg 对象，
    // 这样 renderer.js 内部写 window._dbg.clears 等字段时也会落到同一对象上。
    if (window._dbg) {
      // 把 renderer 之前已经写过的字段合并进来（不覆盖 main.js 自己的字段）
      for (const k of Object.keys(window._dbg)) {
        if (!(k in _dbg)) _dbg[k] = window._dbg[k]
      }
    }
    window._dbg = _dbg
}

// ------------------------- 启动 .NET 10 WASM 运行时 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

// 将 gpu / input / audio / storage / engine 薄 API 注册为 C# [JSImport] 模块
setModuleImports('main.js', {
  gpu: gpuProxy,
  input: inputProxy,
  audio: audioProxy,
  storage: storageProxy,
  engine,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

// 探针：刚 runMain 之后，调用 C# 导出的 Diagnostics.Ping 看看 C# 是否活了
try {
  if (exports?.Diagnostics?.Ping) {
    const pingRet = exports.Diagnostics.Ping('hello')
    console.log('[JS] C# Diagnostics.Ping -> ' + pingRet)
  } else {
    console.log('[JS] no Diagnostics.Ping exported; exports keys:', Object.keys(exports || {}))
  }
} catch (e) {
  console.error('[JS] Diagnostics.Ping failed:', e)
}

await runMain()
