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

import { Application, Container, Graphics, Text, Sprite, Texture } from './vendor/pixi.min.mjs'

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
  render() { if (_ready && app) app.render() },

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
  gfx(id, op, a0, a1, a2, a3, a4, a5, a6, a7, a8) {
    const g = registry.get(id)
    if (!g) return
    applyGfx(g, op, a0, a1, a2, a3, a4, a5, a6, a7, a8)
  },

  // Graphics：批量命令（Float64Array，帧末一次提交）
  gfxBatch(id, ops) {
    const g = registry.get(id)
    if (!g) return
    for (let i = 0; i < ops.length; i += BATCH_STRIDE) {
      applyGfx(g, ops[i], ops[i + 1], ops[i + 2], ops[i + 3], ops[i + 4],
        ops[i + 5], ops[i + 6], ops[i + 7], ops[i + 8], ops[i + 9])
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

  // 视频纹理（muted + loop + playsinline 保证自动播放）
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
        video.addEventListener('loadeddata', () => {
          try {
            const tex = Texture.from(video)
            const id = nextId++
            registry.set(id, tex)
            texIdByUrl.set(url, id)
            video.play().catch(() => { })
            resolve({ id, w: video.videoWidth, h: video.videoHeight })
          } catch (e) {
            console.warn('[PixiApi] loadVideo texture failed:', url, e)
            resolve({ id: -1, w: 0, h: 0 })
          }
        })
        video.addEventListener('error', () => {
          console.warn('[PixiApi] loadVideo failed:', url)
          resolve({ id: -1, w: 0, h: 0 })
        })
      } catch (e) {
        resolve({ id: -1, w: 0, h: 0 })
      }
    })
  },
}

// ---------------------------------------------------------------------
//  图形命令分发
//  槽位布局（11 个 float）：[op, a0..a8, pad]
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
function applyGfx(g, op, a0, a1, a2, a3, a4, a5, a6, a7, a8) {
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
