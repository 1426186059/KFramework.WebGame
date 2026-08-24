// =====================================================================
// PixiJS 版 JSEngine 入口：装配各层模块（渲染 / 输入 / 音效 / 存储），
// 注册 C# 桥接对象（setModuleImports），驱动 requestAnimationFrame 主循环。
//
// 渲染后端 = PixiJS v8（WebGPU 优先，自动回退 WebGL2）。
// 桥接面与 WebGL/WebGPU 版保持一致：C# 侧共享引擎（Assets/Input/Audio/
// Storage/Platform）零改动复用。
// =====================================================================

import { dotnet } from '../_framework/dotnet.js'
import { pixi, texCache } from './pixi-bridge.js'
import { Texture } from './vendor/pixi.min.mjs'
import { input } from './core/input.js'
import { audio } from './core/audio.js'
import { storage } from './core/storage.js'
import { platform } from './core/platform.js'

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
    // 兜底：headless / 后台 tab 时 rAF 可能不回，用 setInterval 驱动（帧内按时间戳去重）
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
    exports.GameBridge.Tick(dt)
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
    pixi,
  }
}

// ------------------------- 资源加载（统一 assets 桥） -------------------------
// C# 侧共享引擎 Assets 模块调用。返回 { id, w, h }（失败 id=-1）。
const assets = {
  async loadImage(url) {
    try {
      let tex = texCache.get(url)
      if (!tex) {
        tex = await Texture.fromURL(url)
        texCache.set(url, tex)
      }
      return { id: url, w: tex.width, h: tex.height }
    } catch (err) {
      console.warn('[Assets] loadImage failed:', url, err)
      return { id: -1, w: 0, h: 0 }
    }
  },

  // 视频纹理（Pixi 视频源；muted+loop+playsinline 保证自动播放）
  loadVideo(url) {
    return new Promise((resolve) => {
      try {
        if (texCache.has(url)) {
          const t = texCache.get(url)
          return resolve({ id: url, w: t.width, h: t.height })
        }
        const video = document.createElement('video')
        video.src = url
        video.muted = true
        video.loop = true
        video.playsInline = true
        video.crossOrigin = 'anonymous'
        video.addEventListener('loadeddata', () => {
          try {
            const tex = Texture.from(video)
            texCache.set(url, tex)
            video.play().catch(() => { })
            resolve({ id: url, w: video.videoWidth, h: video.videoHeight })
          } catch (e) {
            console.warn('[Assets] loadVideo texture failed:', url, e)
            resolve({ id: -1, w: 0, h: 0 })
          }
        })
        video.addEventListener('error', () => {
          console.warn('[Assets] loadVideo failed:', url)
          resolve({ id: -1, w: 0, h: 0 })
        })
      } catch (e) {
        resolve({ id: -1, w: 0, h: 0 })
      }
    })
  },

  drawImage(id, x, y, w, h) {
    const tex = texCache.get(id)
    if (!tex) return
    pixi.addSprite(tex, x, y, w, h)
  },

  // Texture2D：像素重传（动态纹理）
  uploadTexture(id, w, h, argb) {
    try {
      let tex = texCache.get(id)
      if (tex) tex.destroy(true)
      tex = Texture.fromBuffer(new Uint8Array(argb), w, h)
      texCache.set(id, tex)
    } catch (e) {
      console.warn('[Assets] uploadTexture failed:', id, e)
    }
  },

  disposeTexture(id) {
    const tex = texCache.get(id)
    if (tex) { tex.destroy(true); texCache.delete(id) }
  },
}

// ------------------------- 启动 .NET WASM 运行时 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
  pixi,
  assets,
  input,
  audio,
  storage,
  platform,
  engine,
  diag: {
    step: (label) => console.log(label),
  },
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

if (window.__engine) window.__engine.exports = exports

// 不 await runMain()：Program.cs 的 Main 用 `await tcs.Task` 永不返回
runMain()
  .then(() => console.log('[JS] runMain RESOLVED (Program.cs exited)'))
  .catch(err => console.error('[JS] runMain REJECTED：', err))

// dotnet.js 在 Main Task 未返回前可能压制 rAF 回调；2s 后在 JS 侧显式驱动主循环
setTimeout(() => {
  if (typeof requestAnimationFrame === 'function') {
    _rafStarted = true
    requestAnimationFrame(frame)
  }
}, 2000)
