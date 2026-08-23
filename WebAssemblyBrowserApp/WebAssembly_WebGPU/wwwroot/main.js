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
import { diag } from './jsengine/core/diag.js'

// --------------------- 验证 rAF 是否工作（headless/前台通用） ---------------------
if (typeof requestAnimationFrame === 'function') {
  let rafN = 0
  function rafProbe(t) {
    rafN++
    if (rafN <= 3) console.log('[info] [JS] early rAF probe#', rafN, 'ts=', t)
    if (rafN < 3) requestAnimationFrame(rafProbe)
  }
  requestAnimationFrame(rafProbe)
} else {
  console.log('[error] [JS] requestAnimationFrame is NOT available!')
}

// ------------------------- 调试工具：主循环计数器（轻量） -------------------------
const _dbg = {
  tickEnterCount: 0,
  tickReturnCount: 0,
  oneShotWillRun: false,
  oneShotDidEnter: false,
  oneShotDidReturn: false,
  oneShotSecond: false,
  oneShotErr: null,
  lastTickError: null,
  probe: {
    1: null, 2: null, 3: null, 4: null,
    5: null, 6: null, 7: null, 8: null,
    failedAt: null,
    failedMsg: null,
    failedStack: null,
  }
}
const gpuProxy     = gpu
const inputProxy   = input
const audioProxy   = audio
const storageProxy = storage

// ------------------------- 引擎生命周期 -------------------------
let _rafStarted = false
let _lastTs = 0
// headless 环境 / 页面后台时 rAF 可能永远不触发，用 setInterval 兜底
let _fallbackTimer = null

const engine = {
    startLoop() {
        const t = (typeof performance !== 'undefined') ? performance.now() : Date.now()
        if (typeof window !== 'undefined') {
          window.__engineStartCalledAt = t
          window.__engineStartRafStarted = _rafStarted
        }
        console.log('[info] [JS] engine.startLoop() @', t, 'rafStarted_before=', _rafStarted)
        if (_rafStarted) return
        _rafStarted = true
        _lastTs = 0
        // 首选：rAF
        try { requestAnimationFrame(frame) } catch (e) { console.error('[JS] rAF request failed:', e) }
        // 兜底：setInterval 每 16ms 驱动一次（≈60fps），避免 headless/后台 tab 时 rAF 永远不回
        // 但浏览器里 rAF 正常触发时，rAF 会和 setInterval 同时驱动，导致双倍 Tick。
        // 解决：frame 里用时间戳去重：同一毫秒内 frame 只执行 1 次。
        _fallbackTimer = setInterval(() => {
          try {
            const now = (typeof performance !== 'undefined') ? performance.now() : Date.now()
            frame(now)
          } catch (err) { console.error('[JS] fallback interval -> frame failed:', err) }
        }, 16)
        // 进程/导航关闭时清理（可选）
        try {
          if (typeof addEventListener === 'function') {
            addEventListener('beforeunload', () => {
              if (_fallbackTimer) { clearInterval(_fallbackTimer); _fallbackTimer = null }
            })
          }
        } catch {}
        console.log('[info] [JS] engine.startLoop() -> rAF + 16ms setInterval both armed')
    },
}

let _tickCount = 0
let _lastFrameTs = 0  // 去重：同一毫秒内多次请求只执行 1 次
function frame(ts) {
    if (!ts) ts = (typeof performance !== 'undefined') ? performance.now() : (Date.now() - 0)
    // 去重：同一毫秒不重复（rAF + setInterval 双驱动时）
    const ms = Math.floor(ts)
    if (_lastFrameTs === ms) return
    _lastFrameTs = ms

    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    _tickCount++
    _dbg.tickEnterCount++
    if (_tickCount <= 25) console.log('[info] [JS] frame#', _tickCount, ' start, dt=', dt)
    try {
        exports.GameBridge.Tick(dt)
        _dbg.tickReturnCount++
        if (_tickCount <= 25) console.log('[info] [JS] frame#', _tickCount, ' Tick returned OK, renders=', (window.__renderDbg?.clears ?? 0))
    } catch (err) {
        console.error('[Engine] Tick 异常（frame#' + _tickCount + '）：', err)
        _dbg.lastTickError = {
            message: err?.message ?? String(err),
            stack: (err?.stack ?? '').substring(0, 800),
            atTick: _tickCount,
        }
    }
    // 不在这里 requestAnimationFrame 了：因为 rAF 如果能工作会被 startLoop 一次性 request，然后 frame 末尾再 request。
    // 但我们现在已经有 setInterval 兜底，并且 startLoop 已经 request 一次。
    // 为了 rAF 正常工作下仍然走 rAF：在这里再次 requestAnimationFrame（若可用）。
    try { requestAnimationFrame(frame) } catch {}
}

if (typeof window !== 'undefined') {
    window.__engine = {
        get rafStarted() { return _rafStarted },
        frame,
        engine,
        exports: null,
        gpu,
        _dbg,
    }
    window._dbg = _dbg
}

// ------------------------- 启动 .NET 10 WASM 运行时 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
  gpu: gpuProxy,
  input: inputProxy,
  audio: audioProxy,
  storage: storageProxy,
  engine,
  diag,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

try {
  if (exports?.Diagnostics?.Ping) {
    const pingRet = exports.Diagnostics.Ping('hello')
    console.log('[JS] C# Diagnostics.Ping -> ' + pingRet)
  }
} catch (e) { console.error('[JS] Diagnostics.Ping failed:', e) }

// 关键：Program.cs 的 Main 使用 `await tcs.Task` 永不返回，
// 如果用 `await runMain()` 就会永远等下去，后面的代码永远不执行。
// 所以：(1) 不 await runMain()；(2) 用 .catch() 兜底异常即可
runMain()
  .then(() => console.log('[JS] runMain RESOLVED (Program.cs exited)'))
  .catch(err => console.error('[JS] runMain REJECTED：', err))
console.log('[JS] runMain() invoked, not await — continuing to setTimeout/probes')

// 2 秒后把关键状态通过 console 打出来（不受卡死影响）
setTimeout(() => {
  console.log('[JS] after-2s state:')
  console.log('  __engineStartCalledAt=', window.__engineStartCalledAt)
  console.log('  __engine.rafStarted=', window.__engine?.rafStarted)
  console.log('  _dbg.tickIn=', window._dbg?.tickEnterCount, '/ tickOut=', window._dbg?.tickReturnCount)
  const rd = window.__renderDbg
  console.log('  render clears=', rd?.clears, '/ shapeBatches=', rd?.shapeBatches, '/ totalShapes=', rd?.totalShapes)

  // 核心修复：dotnet.js 在 runMain()（Main Task）没返回之前可能压制了 requestAnimationFrame
  // 回调的执行。因此，即使 C# EngineLoop.Start 已经在 Main 里调用 engine.startLoop，
  // rAF frame 也永远得不到执行机会。
  // 这里在 JS 侧显式地重新 requestAnimationFrame 一次，从 JS 事件循环侧驱动。
  if (typeof requestAnimationFrame === 'function') {
    _rafStarted = true
    console.log('[JS] typeof frame=', typeof frame, '→ re-requestAnimationFrame')
    // 先用一个安全 rAF 回调（不调任何 C# 代码）验证浏览器会不会真的触发 rAF
    requestAnimationFrame(function safeFrame(t) {
      console.log('[JS] safe rAF callback fired @', t, '→ calling real frame')
      try { frame(t) }
      catch (err) { console.error('[JS] safe rAF -> frame threw:', err) }
    })
  }
}, 2000)

// runMain 之后的代码（这里在 C# Main 进入 tcs.Task 等待之前就会执行）
_dbg.oneShotWillRun = true

function runProbe(idx, fn) {
  try {
    console.log('[JS] probe' + idx + ' start')
    _dbg.probe[idx] = 'STARTED'
    const result = fn()
    console.log('[JS] probe' + idx + ' returned OK:', result)
    _dbg.probe[idx] = 'OK:' + String(result).slice(0, 100)
    return true
  } catch (err) {
    console.error('[JS] probe' + idx + ' FAILED:', err)
    _dbg.probe[idx] = 'ERR'
    _dbg.probe.failedAt = idx
    _dbg.probe.failedMsg = (err?.message ?? String(err)).substring(0, 500)
    _dbg.probe.failedStack = (err?.stack ?? '').substring(0, 1500)
    return false
  }
}

setTimeout(() => {
  // 先把 exports 的树形结构打印一层 + 二层 keys
  const topKeys = Object.keys(exports || {}).sort()
  console.log('[JS] exports top keys:', topKeys)
  for (const k of topKeys) {
    try {
      const sub = Object.keys(exports[k] || {}).sort()
      console.log('[JS] exports.' + k + ' keys:', sub.slice(0, 80))
    } catch { /* 非对象忽略 */ }
  }
  const geGame =
    exports?.WebAssemblyBrowserApp?.GameEngine ||
    exports?.WebAssemblyBrowserApp?.Engine?.GameEngine ||
    exports?.GameEngine ||
    exports?.WebAssemblyBrowserApp
  const ge = new Proxy({}, {
    get(_t, prop) {
      if (geGame && typeof geGame[prop] === 'function') return geGame[prop].bind(geGame)
      if (typeof exports?.WebAssemblyBrowserApp?.[prop] === 'function') return exports.WebAssemblyBrowserApp[prop].bind(exports.WebAssemblyBrowserApp)
      if (typeof exports?.[prop] === 'function') return exports[prop].bind(exports)
      return undefined
    },
    has(_t, prop) {
      return (geGame && prop in geGame) || (exports?.WebAssemblyBrowserApp && prop in exports.WebAssemblyBrowserApp) || (exports && prop in exports)
    }
  })
  let ok = true
  ok = ok && runProbe(1, () => ge.__probe01_echo(3.14))
  ok = ok && runProbe(2, () => ge.__probe02_isInit())
  ok = ok && runProbe(3, () => ge.__probe03_checkInput('ArrowLeft'))
  if (!ok) return
  ok = ok && runProbe(8, () => ge.__probe08_measureTextOnly())
  if (!ok) return
  ok = ok && runProbe(4, () => ge.__probe04_updateOnly(0.016))
  if (!ok) return
  ok = ok && runProbe(5, () => ge.__probe05_renderOnly())
  if (!ok) return
  ok = ok && runProbe(6, () => ge.__probe06_flushOnly())
  if (!ok) return
  ok = ok && runProbe(7, () => ge.__probe07_endFrameOnly())
  if (!ok) return
  _dbg.oneShotDidEnter = true
  try {
    exports.GameBridge.Tick(0.016)
    _dbg.oneShotDidReturn = true
    exports.GameBridge.Tick(0.016)
    _dbg.oneShotSecond = true
    console.log('[JS] probe T1/T2 OK')
  } catch (err) {
    console.error('[JS] probe T FAILED:', err)
    _dbg.oneShotErr = { message: err?.message, stack: (err?.stack ?? '').substring(0, 1500) }
  }
}, 1500)
