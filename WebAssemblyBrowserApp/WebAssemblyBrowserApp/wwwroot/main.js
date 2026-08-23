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

    loadImage(id, url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => { _images[id] = img; resolve(true) }
            img.onerror = () => resolve(false)
            img.src = url
        })
    },

    drawImage(id, dx, dy, dw, dh) {
        const img = _images[id]
        if (img) _ctx.drawImage(img, dx, dy, dw, dh)
    },
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
    try {
        exports.GameBridge.Tick(dt)
    } catch (err) {
        console.error('[Engine] Tick 异常：', err)
    }
    requestAnimationFrame(frame)
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    engine,
    canvas,
    input,
    audio,
    storage,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

// 运行 C# Main()：引擎初始化完成后会回调 engine.startLoop 启动主循环
await runMain()
