// =====================================================================
// 原生 Pixi API 桥：把 PixiJS v8 的对象模型（Container / Graphics /
// Sprite / Text / Texture）按句柄注册表方式导出给 C#。
//
// C# 侧创建对象 → pixiApi.create(type) 返回句柄 id；
// 对象保存在 registry 中，后续所有操作按 id 寻址：
//   - 属性 / 层级 / 图形命令：即时 [JSImport] 通道
//   - 大批量图形绘制：gfxBatch(id, Float64Array) 一次提交（O(1) 跨边界）
// 每帧末 C# 调 pixiApi.render() 触发一次 Pixi 渲染。
//
// 与"立即模式批提交"（pixi-bridge.js）不同：本桥保留 PixiJS 场景图
// 语义（对象是持久的、可组织成父子层级、变换由 Pixi 自动合成）。
// =====================================================================

import { Application, Container, Graphics, Text, Sprite, Texture, Rectangle } from './vendor/pixi.min.mjs'
import { audio } from './core/audio.js'

// 每条图形命令的 float 数（须与 C# 侧 Graphics.cs 的 BATCH_STRIDE 一致）
const BATCH_STRIDE = 11

const registry = new Map()
let nextId = 1
let app = null
let _initStarted = false
let _ready = false
let _width = 960
let _height = 540
let _readyResolve = null
const _readyPromise = new Promise((resolve) => { _readyResolve = resolve })
const texIdByUrl = new Map()   // url → 纹理句柄 id（避免重复加载）

// ---------------------------------------------------------------------
// 输入：鼠标/触摸走 Pixi 的 EventSystem（FederatedPointerEvent，挂在
// app.canvas 上，由 Pixi 处理命中与坐标）；键盘 Pixi 未提供事件封装，
// 故使用 DOM 键盘事件，统一在本桥内管理（浏览器唯一可靠来源）。
// ---------------------------------------------------------------------
const _keys = {}          // code → 是否按住（KeyW / ArrowUp ...）
const _pressed = {}       // code → 本帧新按下（EndFrame 清除）
const _mouse = { x: 0, y: 0, down: false, pressed: false }
let _inputInited = false

function mouseFromPixi(e) {
  if (!app) return
  const res = app.renderer.resolution || 1
  _mouse.x = e.global.x / res     // Pixi screen 坐标 → 逻辑坐标
  _mouse.y = e.global.y / res
}

function initInput() {
  if (_inputInited) return
  // 守卫：new Application() 后、await app.init() 完成前 renderer 为 undefined，
  // 而 app.screen 的 getter 读 this.renderer.screen 会抛错。未就绪则等
  // initAsync 内 app 就绪后再挂（见 registry.set(0, app.stage) 之后）。
  if (!app || !app.renderer) return
  _inputInited = true

  window.addEventListener('keydown', (e) => {
    if (!e.repeat) { _keys[e.code] = true; _pressed[e.code] = true }
    if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) e.preventDefault()
    audio.ensure()   // 用户手势解锁 @pixi/sound
  })
  window.addEventListener('keyup', (e) => { _keys[e.code] = false })
  window.addEventListener('blur', () => {
    for (const k in _keys) _keys[k] = false
    for (const k in _pressed) delete _pressed[k]
    _mouse.down = false
  })

  // 指针输入走 Pixi EventSystem：stage 作为命中根（hitArea=逻辑屏幕），
  // 事件冒泡到 stage，坐标经 FederatedPointerEvent.global 换算。
  // hitArea 用显式 Rectangle，避免依赖 app.screen（renderer 就绪前不可用）。
  app.stage.eventMode = 'static'
  app.stage.hitArea = new Rectangle(0, 0, _width, _height)
  app.stage.on('pointermove', mouseFromPixi)
  app.stage.on('pointerdown', (e) => { _mouse.down = true; _mouse.pressed = true; audio.ensure(); mouseFromPixi(e) })
  app.stage.on('pointerup', () => { _mouse.down = false })
}

// ---------------------------------------------------------------------
// 存储：Pixi 不提供持久化 API，使用浏览器 localStorage，统一经本桥暴露。
// ---------------------------------------------------------------------
const storage = {
  get(key, fallback) {
    try { const v = localStorage.getItem(key); return v === null ? fallback : v } catch { return fallback }
  },
  set(key, value) {
    try { localStorage.setItem(key, value) } catch { /* 隐私模式等场景静默忽略 */ }
  },
}

// ---------------------------------------------------------------------
// 平台：Pixi 不提供平台能力 API，使用浏览器能力，统一经本桥暴露。
// ---------------------------------------------------------------------
const platform = {
  innerWidth: () => (typeof window !== 'undefined' ? window.innerWidth : 960),
  innerHeight: () => (typeof window !== 'undefined' ? window.innerHeight : 540),
  devicePixelRatio: () => (typeof window !== 'undefined' ? window.devicePixelRatio : 1),
  now: () => (typeof performance !== 'undefined' ? performance.now() : 0),
  userAgent: () => (typeof navigator !== 'undefined' ? navigator.userAgent : ''),
  language: () => (typeof navigator !== 'undefined' ? (navigator.language || '') : ''),
  setTitle: (t) => { if (typeof document !== 'undefined') document.title = t },
}

export const pixiApi = {
  // -----------------------------------------------------------------
  // 生命周期
  // -----------------------------------------------------------------
  init(selector) {
    if (_initStarted) return
    _initStarted = true
    initAsync(selector)
  },

  // app.init() 完成前 app 已存在但 renderer/stage 未就绪，须用 _ready 守卫
  render() {
    if (!_ready || !app) return
    try {
      app.render()
    } catch (e) {
      // WebGPU/WebGL 下 HTMLVideoElement 帧上传失败（视频无 back resource）只影响该视频纹理，
      // 吞掉避免中断整个渲染循环；其余错误继续抛出。
      const msg = (e && e.message) ? String(e.message) : String(e)
      if (/video|copyExternalImageToTexture|back resource/i.test(msg)) {
        console.warn('[PixiApi] render video-frame skipped:', msg)
      } else {
        throw e
      }
    }
  },

  get ready() { return _ready },
  waitReady() { return _readyPromise },
  get width() { return _width },
  get height() { return _height },

  // -----------------------------------------------------------------
  // 对象工厂 / 销毁 / 层级（原生 Pixi 对象模型）
  // -----------------------------------------------------------------
  create(type) {
    let obj
    switch (type) {
      case 'container': obj = new Container(); break
      case 'graphics':  obj = new Graphics(); break
      case 'text':      obj = new Text({ text: '', style: { fill: '#ffffff', fontSize: 16 } }); break
      case 'sprite':    obj = new Sprite(); break
      default: return -1
    }
    const id = nextId++
    registry.set(id, obj)
    return id
  },

  destroy(id) {
    const o = registry.get(id)
    if (!o) return
    try { o.destroy({ children: true }) } catch { }
    registry.delete(id)
    texIdByUrl.forEach((v, k) => { if (v === id) texIdByUrl.delete(k) })
  },

  addChild(pid, cid) {
    const p = registry.get(pid), c = registry.get(cid)
    if (p && c) { try { p.addChild(c) } catch { } }
  },
  removeChild(pid, cid) {
    const p = registry.get(pid), c = registry.get(cid)
    if (p && c) { try { p.removeChild(c) } catch { } }
  },
  removeChildren(pid) {
    const p = registry.get(pid)
    if (p) { try { p.removeChildren() } catch { } }
  },

  // -----------------------------------------------------------------
  // 通用属性
  // -----------------------------------------------------------------
  setProp(id, prop, v) {
    const o = registry.get(id)
    if (!o) return
    o[prop] = v   // x / y / alpha / visible / rotation / width / height ...
  },
  setProp2(id, prop, a, b) {
    const o = registry.get(id)
    if (!o) return
    if (prop === 'scale') o.scale.set(a, b)
    else if (prop === 'pivot') o.pivot.set(a, b)
    else if (prop === 'anchor') o.anchor.set(a, b)
  },

  // -----------------------------------------------------------------
  // Graphics：即时单命令
  // -----------------------------------------------------------------
  gfx(id, op, a0, a1, a2, a3, a4, a5, a6, a7, a8, a9) {
    const g = registry.get(id)
    if (!g) return
    applyGfx(g, op, a0, a1, a2, a3, a4, a5, a6, a7, a8, a9)
  },

  // Graphics：批量命令（Float64Array，帧末一次提交）
  gfxBatch(id, ops) {
    const g = registry.get(id)
    if (!g) return
    for (let i = 0; i < ops.length; i += BATCH_STRIDE) {
      applyGfx(g, ops[i], ops[i + 1], ops[i + 2], ops[i + 3], ops[i + 4],
        ops[i + 5], ops[i + 6], ops[i + 7], ops[i + 8], ops[i + 9], ops[i + 10])
    }
  },

  // -----------------------------------------------------------------
  // Text
  // -----------------------------------------------------------------
  textSet(id, text) {
    const o = registry.get(id)
    if (o) o.text = text
  },
  textStyle(id, font, fill, align) {
    const o = registry.get(id)
    if (!o) return
    const st = parseFont(font)
    st.fill = fill || '#ffffff'
    o.style = st
    if (align === 'center') o.anchor.set(0.5, 0.5)
    else if (align === 'right') o.anchor.set(1, 0.5)
    else o.anchor.set(0, 0.5)
  },

  // -----------------------------------------------------------------
  // Sprite / Texture
  // -----------------------------------------------------------------
  spriteTex(id, texId) {
    const s = registry.get(id), t = registry.get(texId)
    if (s && t) {
      s.texture = t
      s.width = t.width
      s.height = t.height
    }
  },

  async loadTexture(url) {
    try {
      let id = texIdByUrl.get(url)
      let tex = id != null ? registry.get(id) : null
      if (!tex) {
        tex = await Texture.fromURL(url)
        id = nextId++
        registry.set(id, tex)
        texIdByUrl.set(url, id)
      }
      return { id, w: tex.width, h: tex.height }
    } catch (e) {
      console.warn('[PixiApi] loadTexture failed:', url, e)
      return { id: -1, w: 0, h: 0 }
    }
  },

  // 视频纹理（muted + loop + playsinline 保证自动播放；等首帧真正渲染后再建纹理，
  // 避免 WebGPU/WebGL 上传"无 back resource"的视频帧抛异常）
  loadVideo(url) {
    return new Promise((resolve) => {
      try {
        const existing = texIdByUrl.get(url)
        if (existing != null) {
          const t = registry.get(existing)
          return resolve({ id: existing, w: t.width, h: t.height })
        }
        const video = document.createElement('video')
        video.src = url
        video.muted = true
        video.loop = true
        video.playsInline = true
        video.crossOrigin = 'anonymous'
        const fail = (why) => {
          console.warn('[PixiApi] loadVideo failed:', url, why)
          resolve({ id: -1, w: 0, h: 0 })
        }
        video.addEventListener('error', () => fail('media error'))
        video.addEventListener('loadeddata', () => {
          video.play().catch(() => { })
          const build = () => {
            try {
              if (video.videoWidth <= 0 || video.readyState < 2) { setTimeout(build, 120); return }
              const tex = Texture.from(video)
              const id = nextId++
              registry.set(id, tex)
              texIdByUrl.set(url, id)
              resolve({ id, w: video.videoWidth, h: video.videoHeight })
            } catch (e) { fail(e) }
          }
          if (typeof video.requestVideoFrameCallback === 'function') {
            try { video.requestVideoFrameCallback(() => build()) } catch (_) { setTimeout(build, 200) }
          } else {
            requestAnimationFrame(build)
          }
        })
      } catch (e) {
        resolve({ id: -1, w: 0, h: 0 })
      }
    })
  },

  // -----------------------------------------------------------------
  // 输入（鼠标/触摸走 Pixi EventSystem；键盘 DOM，Pixi 无封装）
  // -----------------------------------------------------------------
  inputInit() { initInput() },
  inputIsKeyDown(code) { return !!_keys[code] },
  inputIsKeyPressed(code) { return !!_pressed[code] },
  inputMouseX() { return _mouse.x },
  inputMouseY() { return _mouse.y },
  inputIsMouseDown() { return _mouse.down },
  inputIsMousePressed() { return _mouse.pressed },
  inputEndFrame() { for (const k in _pressed) delete _pressed[k]; _mouse.pressed = false },

  // -----------------------------------------------------------------
  // 存储（localStorage，Pixi 无持久化 API）
  // -----------------------------------------------------------------
  storageGet(key, fallback) { return storage.get(key, fallback) },
  storageSet(key, value) { storage.set(key, value) },

  // -----------------------------------------------------------------
  // 平台（浏览器能力，Pixi 无平台 API）
  // -----------------------------------------------------------------
  platformInnerWidth() { return platform.innerWidth() },
  platformInnerHeight() { return platform.innerHeight() },
  platformDevicePixelRatio() { return platform.devicePixelRatio() },
  platformNow() { return platform.now() },
  platformUserAgent() { return platform.userAgent() },
  platformLanguage() { return platform.language() },
  platformSetTitle(t) { platform.setTitle(t) },
}

// ---------------------------------------------------------------------
//  图形命令分发
//  槽位布局（11 个 float）：[op, a0..a9]
//    op 0 rect       [0, x,y,w,h, r,g,b,a, 0,0]
//    op 1 roundRect  [1, x,y,w,h, radius, r,g,b,a, 0]
//    op 2 circle     [2, cx,cy,r, 0, r,g,b,a, 0,0]
//    op 3 clear      [3]
//    op 4 moveTo     [4, x,y]
//    op 5 lineTo     [5, x,y]
//    op 6 stroke     [6, width, r,g,b,a]
//    op 7 line       [7, x1,y1,x2,y2, width, r,g,b,a]
//    op 8 triangle   [8, x1,y1,x2,y2,x3,y3, r,g,b,a]
//    op 9 ellipse    [9, cx,cy, rx,ry, 0, r,g,b,a, 0]
// ---------------------------------------------------------------------
function applyGfx(g, op, a0, a1, a2, a3, a4, a5, a6, a7, a8, a9) {
  const col = (r, g2, b2) => ((Math.round(r * 255) & 255) << 16) | ((Math.round(g2 * 255) & 255) << 8) | (Math.round(b2 * 255) & 255)
  switch (op) {
    case 0: g.rect(a0, a1, a2, a3).fill({ color: col(a4, a5, a6), alpha: a7 }); break
    case 1: g.roundRect(a0, a1, a2, a3, a4).fill({ color: col(a5, a6, a7), alpha: a8 }); break
    case 2: g.circle(a0, a1, a2).fill({ color: col(a4, a5, a6), alpha: a7 }); break
    case 3: g.clear(); break
    case 4: g.moveTo(a0, a1); break
    case 5: g.lineTo(a0, a1); break
    case 6: g.stroke({ width: a0, color: col(a1, a2, a3), alpha: a4 }); break
    case 7: g.moveTo(a0, a1).lineTo(a2, a3).stroke({ width: a4, color: col(a5, a6, a7), alpha: a8 }); break
    case 8: g.poly([a0, a1, a2, a3, a4, a5]).fill({ color: col(a6, a7, a8), alpha: a9 }); break
    case 9: g.ellipse(a0, a1, a2, a3).fill({ color: col(a5, a6, a7), alpha: a8 }); break
  }
}

// 'bold 18px system-ui, sans-serif' → Pixi TextStyle 字段
function parseFont(font) {
  let fontWeight = 'normal', fontSize = 16, fontFamily = 'system-ui, sans-serif'
  if (font) {
    const m = font.match(/^(bold|normal|bolder|lighter)\s+(\d+)px\s+(.+)$/) || font.match(/^(\d+)px\s+(.+)$/)
    if (m) {
      if (m[1] !== undefined && !/^\d+$/.test(m[1])) fontWeight = m[1]
      const sizeIdx = /^\d+$/.test(m[1]) ? 1 : 2
      fontSize = parseInt(m[sizeIdx], 10)
      fontFamily = m[sizeIdx + 1].trim()
    }
  }
  return { fontFamily, fontSize, fontWeight }
}

// ---------------------------------------------------------------------
//  初始化：PixiJS v8 自动选择 WebGPU，失败回退 WebGL2
// ---------------------------------------------------------------------
async function initAsync(selector) {
  try {
    const host = document.querySelector(selector)
    _width = host?.width || 960
    _height = host?.height || 540

    app = new Application()
    const options = {
      preference: 'webgpu',
      width: _width,
      height: _height,
      background: 0x10141c,
      antialias: true,
      autoDensity: true,
      resolution: Math.min(window.devicePixelRatio || 1, 2),
    }
    try {
      await app.init(options)
    }
    catch (e) {
      console.warn('[PixiApi] WebGPU init failed, fallback WebGL2:', e.message || e)
      await app.init({ ...options, preference: 'webgl' })
    }

    app.ticker.stop()   // 由 C# rAF 主循环驱动 app.render()

    // 用 Pixi canvas 替换占位 canvas，保留 id 与逻辑尺寸（input 换算用）
    const cv = app.canvas
    if (host && host.parentNode) {
      cv.id = host.id || 'game'
      host.replaceWith(cv)
    }
    cv.dataset.vw = String(_width)
    cv.dataset.vh = String(_height)

    const fit = () => {
      const scale = Math.min(
        (window.innerWidth - 24) / _width,
        (window.innerHeight - 24) / _height
      )
      const s = Math.min(1.6, Math.max(0.25, scale))
      cv.style.width = (_width * s) + 'px'
      cv.style.height = (_height * s) + 'px'
    }
    window.addEventListener('resize', fit)
    fit()

    // 根舞台注册为句柄 0（C# 侧 PixiApp.Stage 固定使用）
    registry.set(0, app.stage)

    initInput()   // 鼠标/触摸挂 Pixi EventSystem，键盘挂 DOM

    _ready = true
    _readyResolve?.()
    document.getElementById('loading')?.remove()
    console.log(`[PixiApi] ready: ${app.renderer.type === 3 ? 'WebGPU' : 'WebGL2'}`)
  }
  catch (err) {
    console.error('[PixiApi] init failed:', err)
    _readyResolve?.()   // 失败也放行，避免 C# 侧永远等待
    const el = document.getElementById('loading')
    if (el) {
      el.textContent = 'PixiJS 初始化失败: ' + (err && err.message ? err.message : err)
      el.style.color = '#ff6b6b'
    }
  }
}
