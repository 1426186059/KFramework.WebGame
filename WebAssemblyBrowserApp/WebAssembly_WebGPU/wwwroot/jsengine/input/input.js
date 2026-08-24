// =====================================================================
// 输入层：键盘 / 鼠标 / 触摸。
// 坐标换算到逻辑分辨率（逻辑像素），与渲染层坐标系一致。
// =====================================================================

import { audio } from '../audio/audio.js'

let _keys = {}
let _pressed = {}
const _mouse = { x: 0, y: 0, down: false, pressed: false }
let _canvasSelector = '#game'

export const input = {
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
            updateMouse(e.touches[0])
            _mouse.down = true; _mouse.pressed = true; audio.ensure()
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

function updateMouse(e) {
    const cv = document.querySelector(_canvasSelector)
    if (!cv) return
    const rect = cv.getBoundingClientRect()
    // 逻辑分辨率（渲染层挂到 canvas dataset；canvas 物理像素 = 逻辑 × CSS 缩放）
    // 鼠标 CSS 像素 → 逻辑像素：× 逻辑分辨率 / CSS 显示尺寸
    const vw = parseInt(cv.dataset.vw || '960', 10)
    const vh = parseInt(cv.dataset.vh || '540', 10)
    _mouse.x = (e.clientX - rect.left) * (vw / rect.width)
    _mouse.y = (e.clientY - rect.top) * (vh / rect.height)
}
