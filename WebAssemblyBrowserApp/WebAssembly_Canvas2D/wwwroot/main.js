// =====================================================================
// .NET 10 WebAssembly 2D 游戏引擎 —— JS 桥接层
// 提供 Canvas2D 渲染、输入、音频、本地存储等浏览器底层能力。
// 游戏逻辑全部在 C# 中实现，通过 [JSImport]/[JSExport] 与本层双向互通。
// =====================================================================
import { dotnet } from './_framework/dotnet.js'

// ------------------------- 全局状态 -------------------------
let _ctx = null
let _canvas = null
const _images = {}
let _keys = {}
let _pressed = {}
const _mouse = { x: 0, y: 0, down: false, pressed: false }
let _audioCtx = null
let _rafStarted = false
let _lastTs = 0

// 鼠标/触摸坐标 → 画布逻辑坐标（考虑 CSS 缩放）
function updateMouse(e) {
    if (!_canvas) return
    const rect = _canvas.getBoundingClientRect()
    _mouse.x = (e.clientX - rect.left) * (_canvas.width / rect.width)
    _mouse.y = (e.clientY - rect.top) * (_canvas.height / rect.height)
}

// ------------------------- Canvas 2D -------------------------
const canvas = {
    init(selector, width, height) {
        const el = document.querySelector(selector)
        el.width = width
        el.height = height
        _canvas = el
        _ctx = el.getContext('2d')
    },

    clear(color) {
        const w = _canvas.width, h = _canvas.height
        _ctx.clearRect(0, 0, w, h)
        if (color) {
            _ctx.fillStyle = color
            _ctx.fillRect(0, 0, w, h)
        }
    },

    fillRect(x, y, w, h, color) {
        _ctx.fillStyle = color
        _ctx.fillRect(x, y, w, h)
    },

    strokeRect(x, y, w, h, color, lineWidth) {
        _ctx.strokeStyle = color
        _ctx.lineWidth = lineWidth
        _ctx.strokeRect(x, y, w, h)
    },

    roundedRect(x, y, w, h, r, color) {
        _ctx.fillStyle = color
        _ctx.beginPath()
        _ctx.roundRect(x, y, w, h, r)
        _ctx.fill()
    },

    fillCircle(x, y, r, color) {
        _ctx.fillStyle = color
        _ctx.beginPath()
        _ctx.arc(x, y, r, 0, Math.PI * 2)
        _ctx.fill()
    },

    fillText(text, x, y, font, color, align) {
        _ctx.fillStyle = color
        _ctx.font = font
        _ctx.textAlign = align || 'center'
        _ctx.textBaseline = 'middle'
        _ctx.fillText(text, x, y)
    },

    line(x1, y1, x2, y2, color, lineWidth) {
        _ctx.strokeStyle = color
        _ctx.lineWidth = lineWidth
        _ctx.beginPath()
        _ctx.moveTo(x1, y1)
        _ctx.lineTo(x2, y2)
        _ctx.stroke()
    },

    save() { _ctx.save() },
    restore() { _ctx.restore() },
    translate(x, y) { _ctx.translate(x, y) },
    rotate(rad) { _ctx.rotate(rad) },
    alpha(a) { _ctx.globalAlpha = a },
    shadow(color, blur) { _ctx.shadowColor = color; _ctx.shadowBlur = blur },
    noShadow() { _ctx.shadowColor = 'transparent'; _ctx.shadowBlur = 0 },
}

// ------------------------- 输入 -------------------------
const input = {
    init() {
        window.addEventListener('keydown', (e) => {
            if (!e.repeat) { _keys[e.code] = true; _pressed[e.code] = true }
            if (['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space'].includes(e.code)) e.preventDefault()
            audio.ensure()
        })
        window.addEventListener('keyup', (e) => { _keys[e.code] = false })
        window.addEventListener('mousemove', updateMouse)
        window.addEventListener('mousedown', (e) => { _mouse.down = true; _mouse.pressed = true; audio.ensure() })
        window.addEventListener('mouseup', () => { _mouse.down = false })
        window.addEventListener('touchstart', (e) => {
            const t = e.touches[0]
            updateMouse(t)
            _mouse.down = true
            _mouse.pressed = true
            audio.ensure()
        }, { passive: true })
        window.addEventListener('touchmove', (e) => { updateMouse(e.touches[0]) }, { passive: true })
        window.addEventListener('touchend', () => { _mouse.down = false })
        window.addEventListener('blur', () => { _keys = {}; _pressed = {}; _mouse.down = false })
    },

    isKeyDown(code) { return !!_keys[code] },
    isKeyPressed(code) { return !!_pressed[code] },
    mouseX() { return _mouse.x },
    mouseY() { return _mouse.y },
    isMouseDown() { return _mouse.down },
    isMousePressed() { return _mouse.pressed },
    endFrame() { _pressed = {}; _mouse.pressed = false },
}

// ------------------------- 音频 (WebAudio) -------------------------
const audio = {
    init() { _audioCtx = null },

    ensure() {
        if (!_audioCtx) {
            const AC = window.AudioContext || window.webkitAudioContext
            if (AC) _audioCtx = new AC()
        }
        if (_audioCtx && _audioCtx.state === 'suspended') _audioCtx.resume()
    },

    beep(freq, dur, waveType, vol) {
        if (!_audioCtx) return
        try {
            const osc = _audioCtx.createOscillator()
            const gain = _audioCtx.createGain()
            osc.type = waveType || 'square'
            osc.frequency.value = freq
            const t = _audioCtx.currentTime
            const v = vol || 0.08
            gain.gain.setValueAtTime(v, t)
            gain.gain.exponentialRampToValueAtTime(0.0001, t + (dur || 0.1))
            osc.connect(gain)
            gain.connect(_audioCtx.destination)
            osc.start(t)
            osc.stop(t + (dur || 0.1) + 0.02)
        } catch { /* 忽略音频异常 */ }
    },
}

// ------------------------- 本地存储 -------------------------
const storage = {
    get(key, fallback) {
        try {
            const v = localStorage.getItem(key)
            return v === null ? fallback : v
        } catch { return fallback }
    },
    set(key, value) {
        try { localStorage.setItem(key, value) } catch { /* 忽略 */ }
    },
}

// ------------------------- 引擎生命周期 -------------------------
const engine = {
    initCanvas(selector, width, height) {
        canvas.init(selector, width, height)
        const fit = () => {
            if (!_canvas) return
            const scale = Math.min(
                (window.innerWidth - 24) / width,
                (window.innerHeight - 24) / height
            )
            const s = Math.min(1.6, Math.max(0.25, scale))
            _canvas.style.width = (width * s) + 'px'
            _canvas.style.height = (height * s) + 'px'
        }
        window.addEventListener('resize', fit)
        fit()
        document.getElementById('loading')?.remove()
    },

    // 由 C# Main 在初始化完成后调用，启动主循环
    startLoop() {
        if (_rafStarted) return
        _rafStarted = true
        requestAnimationFrame(frame)
    },
}

// 主循环：requestAnimationFrame → C# GameBridge.Tick(dt)
function frame(ts) {
    const dt = _lastTs ? (ts - _lastTs) / 1000 : 0.016
    _lastTs = ts
    try
    {
        exports.GameBridge.Tick(dt)
    }
    catch (err)
    {
        console.error('[Engine] Tick 异常：', err)
    }
    requestAnimationFrame(frame)
}

// ------------------------- 平台能力薄层 -------------------------
const platform = {
    innerWidth: () => (typeof window !== 'undefined' && window.innerWidth) || 960,
    innerHeight: () => (typeof window !== 'undefined' && window.innerHeight) || 540,
    devicePixelRatio: () => (typeof window !== 'undefined' && window.devicePixelRatio) || 1,
    now: () => (typeof performance !== 'undefined') ? performance.now() : 0,
    userAgent: () => (typeof navigator !== 'undefined') ? navigator.userAgent : '',
    language: () => (typeof navigator !== 'undefined') ? (navigator.language || '') : '',
    setTitle: (t) => { if (typeof document !== 'undefined') document.title = t },
}

// ------------------------- 资源加载（统一 assets 桥） -------------------------
// C# 侧 Assets / Texture2D 模块调用（共享引擎 WebEngineCommon/Engine/）。
// 图片加载统一返回 { id, w, h }（失败 id=-1），加载完成才 resolve。
// 动态纹理（Texture2D）走独立的 'dyn:<id>' 命名空间，与图片 url id 互不冲突。
const _texCanvases = new Map()  // 'dyn:<id>' -> { canvas, ctx, w, h }

// ARGB8888 int[]（C# int[] → JS Int32Array）→ RGBA Uint8ClampedArray（canvas 像素顺序）
function argbToRgba(argb) {
    const n = argb.length
    const rgba = new Uint8ClampedArray(n * 4)
    for (let i = 0; i < n; i++) {
        const c = argb[i]
        rgba[i * 4] = (c >> 16) & 0xff
        rgba[i * 4 + 1] = (c >> 8) & 0xff
        rgba[i * 4 + 2] = c & 0xff
        rgba[i * 4 + 3] = (c >>> 24) & 0xff
    }
    return rgba
}

const assets = {
    loadImage(url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => { _images[url] = img; resolve({ id: url, w: img.width, h: img.height }) }
            img.onerror = () => resolve({ id: -1, w: 0, h: 0 })
            img.src = url
        })
    },
    drawImage(id, dx, dy, dw, dh) {
        const img = _images[id]
        if (img) { _ctx.drawImage(img, dx, dy, dw, dh); return }
        const entry = _texCanvases.get('dyn:' + id)
        if (entry) _ctx.drawImage(entry.canvas, dx, dy, dw, dh)
    },

    // Texture2D：把 ARGB 像素重传到离屏 canvas（按 id 缓存，尺寸变化时重建）
    uploadTexture(id, w, h, argb) {
        const key = 'dyn:' + id
        let entry = _texCanvases.get(key)
        if (!entry || entry.w !== w || entry.h !== h) {
            const canvas = document.createElement('canvas')
            canvas.width = w; canvas.height = h
            entry = { canvas, ctx: canvas.getContext('2d'), w, h }
            _texCanvases.set(key, entry)
        }
        const img = entry.ctx.createImageData(w, h)
        img.data.set(argbToRgba(argb))
        entry.ctx.putImageData(img, 0, 0)
    },
    disposeTexture(id) {
        _texCanvases.delete('dyn:' + id)
    },
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    engine,
    canvas,
    assets,
    input,
    audio,
    storage,
    platform,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

// 运行 C# Main()：引擎初始化完成后会回调 engine.startLoop 启动主循环
await runMain()
