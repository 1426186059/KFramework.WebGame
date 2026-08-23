// =====================================================================
// 渲染层：gl.* 绘制 API（C# 通过 setModuleImports('main.js') 桥接调用）。
// 绘制调用把当前变换/颜色/阴影烘焙进实例缓冲（见 renderer.js pushInstance），
// 由入口帧循环统一 flush，语义与 Canvas2D 的立即模式一致。
// =====================================================================

import {
    pushInstance, flushShapes, hexToRgb, currentMatrix, identity, multiply,
    initGL, getGL, getCanvas, setCanvas,
    state, _textures,
} from './renderer.js'
import { drawTextSprite, drawTexturedQuad } from './text.js'

export const gl = {
    init(selector, width, height) {
        const el = document.querySelector(selector)
        el.width = width
        el.height = height
        setCanvas(el)
        initGL(el)
        document.getElementById('loading')?.remove()
    },

    clear(color) {
        flushShapes()
        const g = getGL()
        const cv = getCanvas()
        const rgb = hexToRgb(color)
        g.bindFramebuffer(g.FRAMEBUFFER, null)
        g.viewport(0, 0, cv.width, cv.height)
        g.clearColor(rgb[0], rgb[1], rgb[2], 1)
        g.clear(g.COLOR_BUFFER_BIT)
    },

    fillRect(x, y, w, h, color) { pushInstance(x, y, w, h, color, 0, 0) },

    strokeRect(x, y, w, h, color, lineWidth) {
        const t = lineWidth || 1
        gl.fillRect(x, y, w, t, color)
        gl.fillRect(x, y + h - t, w, t, color)
        gl.fillRect(x, y, t, h, color)
        gl.fillRect(x + w - t, y, t, h, color)
    },

    roundedRect(x, y, w, h, r, color) {
        // 直接传像素半径，着色器里 clamp 到短边一半（胶囊形），与 Canvas2D 语义一致
        pushInstance(x, y, w, h, color, r, 0)
    },

    fillCircle(x, y, r, color) { pushInstance(x - r, y - r, r * 2, r * 2, color, r, 1) },

    line(x1, y1, x2, y2, color, lineWidth) {
        const t = lineWidth || 1
        const dx = x2 - x1, dy = y2 - y1
        const len = Math.hypot(dx, dy)
        if (len < 0.001) return
        // 用临时变换把线段转成旋转细矩形，保证斜线与 Canvas2D 一致
        gl.save()
        gl.translate(x1, y1)
        gl.rotate(Math.atan2(dy, dx))
        gl.fillRect(0, -t / 2, len, t, color)
        gl.restore()
    },

    fillText(text, x, y, font, color, align)
    {
        flushShapes()
        drawTextSprite(text, x, y, font, color, align)
    },

    save() {
        state.matrixStack.push(new Float32Array(currentMatrix()))
        state.alphaStack.push(state.globalAlpha)
        state.shadowStack.push([state.shadowColor, state.shadowBlur])
    },
    restore() {
        if (state.matrixStack.length > 1) {
            state.matrixStack.pop()
            state.globalAlpha = state.alphaStack.pop()
            const s = state.shadowStack.pop()
            state.shadowColor = s[0]; state.shadowBlur = s[1]
        }
    },
    translate(x, y) {
        const m = currentMatrix()
        const t = identity()
        t[2] = x; t[5] = y
        state.matrixStack[state.matrixStack.length - 1] = multiply(m, t)
    },
    rotate(rad) {
        const m = currentMatrix()
        const c = Math.cos(rad), s = Math.sin(rad)
        const r = identity()
        r[0] = c; r[1] = -s; r[3] = s; r[4] = c
        state.matrixStack[state.matrixStack.length - 1] = multiply(m, r)
    },
    alpha(a) { state.globalAlpha = a },
    shadow(color, blur) { state.shadowColor = color; state.shadowBlur = blur },
    noShadow() { state.shadowColor = null; state.shadowBlur = 0 },

    loadImage(id, url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => {
                const g = getGL()
                const tex = g.createTexture()
                g.bindTexture(g.TEXTURE_2D, tex)
                g.pixelStorei(g.UNPACK_FLIP_Y_WEBGL, false)
                g.texImage2D(g.TEXTURE_2D, 0, g.RGBA, g.RGBA, g.UNSIGNED_BYTE, img)
                g.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR)
                g.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE)
                g.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE)
                _textures[id] = tex
                resolve(true)
            }
            img.onerror = () => resolve(false)
            img.src = url
        })
    },

    drawImage(id, dx, dy, dw, dh) {
        flushShapes()
        const tex = _textures[id]
        if (tex) drawTexturedQuad(tex, dx, dy, dw, dh)
    },
}
