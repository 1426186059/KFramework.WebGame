// =====================================================================
// 独立工程入口：装配原生 Pixi API 桥 + 音效，注册 C# 桥接对象，
// 驱动 requestAnimationFrame 主循环。
//
// 本工程不依赖 Canvas2D/WebGL/WebGPU 三端的任何代码（含 WebEngineCommon），
// C# 侧统一通过 pixiApi.* 走 Pixi 接口：对象模型 / 渲染 / 输入(EventSystem)
// / 存储 / 平台 / 音频(@pixi/sound) 全部由 Pixi 侧提供。
// =====================================================================

import { dotnet } from '../_framework/dotnet.js'
import { pixiApi } from './pixi-api.js'
import { audio } from './core/audio.js'

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0
let _fallbackTimer = null

const engine = {
  startLoop() {
    if (_rafStarted) return
    _rafStarted = true
    _lastTs = 0
    try { requestAnimationFrame(frame) } catch (e) { console.error('[JS] rAF request failed:', e) }
    // headless / 后台 tab 兜底
    _fallbackTimer = setInterval(() => {
      try {
        const now = (typeof performance !== 'undefined') ? performance.now() : Date.now()
        frame(now)
      } catch (err) { console.error('[JS] fallback interval -> frame failed:', err) }
    }, 16)
    try {
      if (typeof addEventListener === 'function') {
        addEventListener('beforeunload', () => {
          if (_fallbackTimer) { clearInterval(_fallbackTimer); _fallbackTimer = null }
        })
      }
    } catch { }
  },
}

let _lastFrameTs = 0
function frame(ts) {
  if (!ts) ts = (typeof performance !== 'undefined') ? performance.now() : Date.now()
  const ms = Math.floor(ts)
  if (_lastFrameTs === ms) return
  _lastFrameTs = ms

  const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
  _lastTs = ts
  try {
    // getAssemblyExports 按 命名空间.类名 挂载：Program 里 GameApp.TickBridge 导出在 PixiGame.GameApp
    exports.PixiGame.GameApp.TickBridge(dt)
  } catch (err) {
    console.error('[Engine] Tick 异常：', err)
  }
  try { requestAnimationFrame(frame) } catch { }
}

if (typeof window !== 'undefined') {
  window.__engine = {
    get rafStarted() { return _rafStarted },
    frame,
    engine,
    exports: null,
    pixiApi,
  }
}

// ------------------------- 启动 .NET WASM 运行时 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
  pixiApi,
  audio,
  engine,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

// Program.cs 的 Main 用 `await tcs.Task` 永不返回，所以不能 await runMain()
runMain()
  .then(() => console.log('[JS] runMain RESOLVED (Program.cs exited)'))
  .catch(err => console.error('[JS] runMain REJECTED：', err))

// 诊断：URL ?scene=X 在 1.2s 后让 GameApp 直接进入指定场景（绕过主菜单），
// 用于定位"黑屏"是场景切换逻辑还是目标场景本身的问题。
try {
  const _autoScene = new URLSearchParams(location.search).get('scene')
  if (_autoScene && _autoScene !== 'main') {
    setTimeout(() => {
      try {
        console.log('[AutoScene] switching to', _autoScene)
        exports.PixiGame.GameApp.StartStatic(_autoScene)
      } catch (e) { console.error('[AutoScene] StartStatic failed:', e) }
    }, 1200)
  }
} catch (_) {}

// dotnet.js 在 Main 未返回前可能压制 rAF；2s 后从 JS 侧显式驱动主循环
setTimeout(() => {
  if (typeof requestAnimationFrame === 'function') {
    _rafStarted = true
    requestAnimationFrame(frame)
  }
}, 2000)
