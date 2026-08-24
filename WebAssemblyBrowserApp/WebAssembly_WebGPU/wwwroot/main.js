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
import { platform } from './jsengine/core/platform.js'
import { diag } from './jsengine/core/diag.js'

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0
// headless 环境 / 页面后台时 rAF 可能永远不触发，用 setInterval 兜底
let _fallbackTimer = null

const engine = {
    startLoop() {
        if (_rafStarted) return
        _rafStarted = true
        _lastTs = 0
        // 首选：rAF
        try { requestAnimationFrame(frame) } catch (e) { console.error('[JS] rAF request failed:', e) }
        // 兜底：setInterval 每 16ms 驱动一次（≈60fps），避免 headless/后台 tab 时 rAF 永远不回。
        // 浏览器里 rAF 正常触发时两者同时驱动，frame() 内用毫秒时间戳去重。
        _fallbackTimer = setInterval(() => {
          try {
            const now = (typeof performance !== 'undefined') ? performance.now() : Date.now()
            frame(now)
          } catch (err) { console.error('[JS] fallback interval -> frame failed:', err) }
        }, 16)
        // 进程/导航关闭时清理
        try {
          if (typeof addEventListener === 'function') {
            addEventListener('beforeunload', () => {
              if (_fallbackTimer) { clearInterval(_fallbackTimer); _fallbackTimer = null }
            })
          }
        } catch {}
    },
}

let _lastFrameTs = 0  // 去重：同一毫秒内多次请求只执行 1 次
function frame(ts) {
    if (!ts) ts = (typeof performance !== 'undefined') ? performance.now() : (Date.now() - 0)
    const ms = Math.floor(ts)
    if (_lastFrameTs === ms) return
    _lastFrameTs = ms

    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    try {
        exports.GameBridge.Tick(dt)
    } catch (err) {
        console.error('[Engine] Tick 异常：', err)
    }
    // rAF 下一帧（若可用）
    try { requestAnimationFrame(frame) } catch {}
}

if (typeof window !== 'undefined') {
    window.__engine = {
        get rafStarted() { return _rafStarted },
        frame,
        engine,
        exports: null,
        gpu,
    }
}

// ------------------------- 资源加载（统一 assets 桥） -------------------------
// C# 侧 Assets 模块调用（共享引擎 WebEngineCommon/Engine/Assets.cs）。
// 返回 { id, w, h }（失败 id=-1），纹理 ready 后才 resolve。
const assets = {
    loadImage(url) {
        return gpu.loadImage(url)
    },
    drawImage(id, x, y, w, h) {
        gpu.drawImage(id, x, y, w, h)
    },
    // Texture2D：像素重传（内部走 'dyn:'+id 命名空间）
    uploadTexture(id, w, h, argb) {
        gpu.uploadTexture(id, w, h, argb)
    },
    disposeTexture(id) {
        gpu.disposeTexture(id)
    },
}

// ------------------------- 启动 .NET WASM 运行时 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
  gpu,
  assets,
  input,
  audio,
  storage,
  platform,
  engine,
  diag,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

// 关键：Program.cs 的 Main 使用 `await tcs.Task` 永不返回，
// 如果用 `await runMain()` 就会永远等下去。所以不 await runMain()。
runMain()
  .then(() => console.log('[JS] runMain RESOLVED (Program.cs exited)'))
  .catch(err => console.error('[JS] runMain REJECTED：', err))

// dotnet.js 在 runMain()（Main Task）未返回前可能压制 requestAnimationFrame 回调，
// 因此即使 C# EngineLoop.Start 已在 Main 里调用 engine.startLoop，rAF frame 也可能得不到执行机会。
// 2 秒后在 JS 侧显式地重新 requestAnimationFrame 一次，从 JS 事件循环侧驱动主循环。
setTimeout(() => {
  if (typeof requestAnimationFrame === 'function') {
    _rafStarted = true
    requestAnimationFrame(frame)
  }
}, 2000)
