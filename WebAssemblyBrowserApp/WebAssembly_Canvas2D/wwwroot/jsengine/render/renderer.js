// =====================================================================
// Canvas2D 渲染层：全部 2D 绘制原语 + canvas 生命周期。
// 纹理（图片/视频/动态）在 textures.js / video.js，不在这里。
// =====================================================================

let _ctx = null
let _canvas = null

export const canvas2d = {
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

    getContext() { return _ctx },
    getCanvas() { return _canvas },
}
