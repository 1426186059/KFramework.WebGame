// =====================================================================
// PixiJS 渲染桥（薄 API，与 C# 侧 Pixi.cs 一一对应）
//
// 架构：立即模式 + 整帧批提交。
//   - C# 每帧把图元（矩形/圆角矩形/圆）累积进 Float64Array 缓冲，
//     帧末一次 pixi.submit(shapes, shadows) 交给本桥绘制；
//   - 线 / 文本 / 图片 / 视频频率低，走独立轻量通道；
//   - 坐标已由 C# 预变换到世界空间，本桥无需状态机。
//
// 渲染后端：PixiJS v8 自动选择 WebGPU（preference:'webgpu'），
// 设备不支持时自动回退 WebGL2 —— 一个工程覆盖两条 GPU 管线。
//
// 层级（自底向上）：
//   frameGraphics —— 批图元（每帧一个共享 Graphics）
//   spriteLayer   —— 图片/视频 Sprite + 线段 Graphics
//   textLayer     —— 文本 Text
// =====================================================================

import { Application, Container, Graphics, Text, Sprite, Texture } from './vendor/pixi.min.mjs'

// 每个图元的 float 数（须与 Pixi.cs 的 Stride 一致）
const SHAPE_STRIDE = 13

let app = null
let frameGraphics = null
let spriteLayer = null
let textLayer = null
let _measureCtx = null
let _initStarted = false
let _ready = false
let _width = 960
let _height = 540

// ---------------------------------------------------------------------
//  生命周期
// ---------------------------------------------------------------------
export const pixi = {
  init(selector) {
    // C# 同步调用；实际初始化是 async，就绪前 submit/fillText 全部静默跳过
    if (_initStarted) return
    _initStarted = true
    initAsync(selector)
  },

  clear(color) {
    if (!app) return
    app.stage.removeChildren()
    frameGraphics = new Graphics()
    spriteLayer = new Container()
    textLayer = new Container()
    app.stage.addChild(frameGraphics)
    app.stage.addChild(spriteLayer)
    app.stage.addChild(textLayer)
    app.renderer.background.color = cssToInt(color)
  },

  submit(shapes, shadows) {
    if (!app || !frameGraphics) return
    // 阴影层（同形同位，半透明模拟发光；位置已由 C# 预计算）
    for (let i = 0; i < shadows.length; i += SHAPE_STRIDE) drawShape(frameGraphics, shadows, i)
    // 主体
    for (let i = 0; i < shapes.length; i += SHAPE_STRIDE) drawShape(frameGraphics, shapes, i)
    app.render()
  },

  drawLine(x1, y1, x2, y2, width, r, g, b, a, hasShadow, sr, sg, sb, sa, blur) {
    if (!app) return
    if (hasShadow) {
      const sgfx = new Graphics()
      sgfx.moveTo(x1, y1).lineTo(x2, y2)
      sgfx.stroke({ width: width + Math.min(blur, 14), color: (sr * 255 << 16) | (sg * 255 << 8) | (sb * 255 | 0), alpha: Math.max(0, Math.min(1, sa)) })
      spriteLayer.addChild(sgfx)
    }
    const gfx = new Graphics()
    gfx.moveTo(x1, y1).lineTo(x2, y2)
    gfx.stroke({ width, color: (r * 255 << 16) | (g * 255 << 8) | (b * 255 | 0), alpha: Math.max(0, Math.min(1, a)) })
    spriteLayer.addChild(gfx)
  },

  fillText(text, x, y, font, color, align) {
    if (!app) return
    const t = new Text({ text, style: Object.assign(parseFont(font), { fill: color }) })
    if (align === 'center') t.anchor.set(0.5, 0.5)
    else if (align === 'right') t.anchor.set(1, 0.5)
    else t.anchor.set(0, 0.5)
    t.x = x; t.y = y
    textLayer.addChild(t)
  },

  measureText(text, font) {
    // PixiJS min 构建不导出 TextMetrics；用 canvas 2D 测量（标准 API）
    if (!_measureCtx) _measureCtx = document.createElement('canvas').getContext('2d')
    _measureCtx.font = font || '16px system-ui, sans-serif'
    return _measureCtx.measureText(text).width
  },

  // 图片 / 视频 / 动态纹理 Sprite（由 main.js 的 assets 桥调用）
  addSprite(tex, x, y, w, h) {
    if (!app || !spriteLayer) return
    const s = new Sprite(tex)
    s.x = x; s.y = y; s.width = w; s.height = h
    spriteLayer.addChild(s)
  },

  get ready() { return _ready },
  get width() { return _width },
  get height() { return _height },
}

async function initAsync(selector) {
  try {
    const host = document.querySelector(selector)
    _width = host?.width || 960
    _height = host?.height || 540

    app = new Application()
    const options = {
      preference: 'webgpu',           // WebGPU 优先，自动回退 WebGL2
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
      console.warn('[Pixi] WebGPU init failed, fallback WebGL2:', e.message || e)
      await app.init({ ...options, preference: 'webgl' })
    }

    app.ticker.stop() // 关自动渲染，由 C# rAF 主循环驱动 app.render()

    // 用 Pixi canvas 替换占位 canvas，并保持原有 id（继承 index.html 样式）
    const cv = app.canvas
    if (host && host.parentNode) {
      cv.id = host.id || 'game'
      host.replaceWith(cv)
    }
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

    _ready = true
    document.getElementById('loading')?.remove()
    console.log(`[Pixi] ready: ${app.renderer.type === 3 ? 'WebGPU' : 'WebGL2'}`)
  }
  catch (err) {
    console.error('[Pixi] init failed:', err)
    const el = document.getElementById('loading')
    if (el) { el.textContent = 'PixiJS 初始化失败: ' + (err && err.message ? err.message : err); el.style.color = '#ff6b6b' }
  }
}

// ---------------------------------------------------------------------
//  图元绘制（写入共享 Graphics）
// ---------------------------------------------------------------------
function drawShape(g, buf, i) {
  const x = buf[i], y = buf[i + 1], w = buf[i + 2], h = buf[i + 3]
  const cx = buf[i + 4], cy = buf[i + 5], radius = buf[i + 6], type = buf[i + 7]
  const rr = Math.round(buf[i + 8] * 255), gg = Math.round(buf[i + 9] * 255), bb = Math.round(buf[i + 10] * 255)
  const a = Math.max(0, Math.min(1, buf[i + 11]))
  const color = (rr << 16) | (gg << 8) | bb
  g.beginFill(color, a)
  if (type === 0) g.drawRect(x, y, w, h)
  else if (type === 1) g.roundRect(x, y, w, h, radius)
  else g.circle(cx, cy, radius)
  g.endFill()
}

// ---------------------------------------------------------------------
//  工具
// ---------------------------------------------------------------------
function cssToInt(c) {
  if (!c) return 0x10141c
  c = c.trim()
  if (c[0] === '#') {
    if (c.length === 4) return parseInt(c.slice(1).split('').map(ch => ch + ch).join(''), 16)
    return parseInt(c.slice(1, 7), 16)
  }
  if (c.startsWith('rgb')) {
    const p = c.slice(c.indexOf('(') + 1).split(',').map(s => parseFloat(s.trim()))
    return ((p[0] | 0) << 16) | ((p[1] | 0) << 8) | (p[2] | 0)
  }
  return 0x10141c
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

// 供 main.js 使用的纹理缓存（图片 + 视频 + 动态纹理共用）
export const texCache = new Map()
