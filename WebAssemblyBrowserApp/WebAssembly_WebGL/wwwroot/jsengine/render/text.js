// =====================================================================
// 渲染层：文本渲染（离屏 Canvas2D 绘制 → 纹理 → IMG 着色器实例化绘制）。
// 同一 (text, font, color) 的纹理缓存复用，避免每帧建删纹理导致的卡顿/闪烁。
// =====================================================================

import {
    getGL, currentMatrix, storeMatrix, bindQuad,
    FLOATS_PER_INST, LOC_RECT, LOC_COLOR, LOC_MATRIX_IMG,
    _canvas, _fontCanvas, _fontCtx, _instBuf, _imgProg,
    _uImgRes, _uImgTex, _uImgUvRect, state,
} from './renderer.js'

const _textCache = new Map()
const _textCacheLimit = 128

export function drawTextSprite(text, x, y, font, color, align) {
    const key = text + '\u0000' + font + '\u0000' + color
    let entry = _textCache.get(key)
    if (!entry) {
        entry = renderTextSprite(text, font, color)
        _textCache.set(key, entry)
        if (_textCache.size > _textCacheLimit) {
            const oldest = _textCache.keys().next().value
            getGL().deleteTexture(_textCache.get(oldest).tex)
            _textCache.delete(oldest)
        }
    }
    const { tex, tw, th, ascent, pad } = entry
    let ox = x
    if (align === 'center') ox = x - tw / 2
    else if (align === 'right') ox = x - tw
    // Canvas2D 语义：y 为基线(baseline)。quad 顶部 = y - ascent - pad
    drawTexturedQuad(tex, ox, y - ascent - pad, tw, th,
        tw / _fontCanvas.width, th / _fontCanvas.height)
}

export function renderTextSprite(text, font, color) {
    const ctx = _fontCtx
    ctx.clearRect(0, 0, _fontCanvas.width, _fontCanvas.height)
    ctx.fillStyle = color
    ctx.font = font
    ctx.textBaseline = 'alphabetic'
    ctx.textAlign = 'left'

    // 按字体边界框裁剪纹理区域，quad 以 1:1 显示，避免文本被整体压缩或裁切。
    // actualBoundingBox 只含字形墨迹，字体行高更大，文字顶部/底部会被裁掉；
    // 优先用 fontBoundingBox（完整字体边界），缺省时按字号估算。
    const fontSize = parseFloat((font.match(/(\d+(?:\.\d+)?)px/) || [,'16'])[1])
    const metrics = ctx.measureText(text)
    const ascent = Math.max(1, Math.ceil(metrics.fontBoundingBoxAscent || metrics.actualBoundingBoxAscent || fontSize * 0.85))
    const descent = Math.max(1, Math.ceil(metrics.fontBoundingBoxDescent || metrics.actualBoundingBoxDescent || fontSize * 0.25))
    const pad = 4
    ctx.fillText(text, pad, ascent + pad)

    const tw = Math.max(2, Math.min(1024, Math.ceil(metrics.width) + pad * 2))
    const th = Math.max(2, Math.min(256, ascent + descent + pad * 2))

    const g = getGL()
    const tex = g.createTexture()
    g.bindTexture(g.TEXTURE_2D, tex)
    g.pixelStorei(g.UNPACK_FLIP_Y_WEBGL, false)
    g.texImage2D(g.TEXTURE_2D, 0, g.RGBA, g.RGBA, g.UNSIGNED_BYTE, _fontCanvas)
    g.texParameteri(g.TEXTURE_2D, g.TEXTURE_MIN_FILTER, g.LINEAR)
    g.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_S, g.CLAMP_TO_EDGE)
    g.texParameteri(g.TEXTURE_2D, g.TEXTURE_WRAP_T, g.CLAMP_TO_EDGE)
    return { tex, tw, th, ascent, pad }
}

export function drawTexturedQuad(tex, dx, dy, dw, dh, uvW = 1, uvH = 1) {
    const gl = getGL()
    const m = currentMatrix()

    gl.useProgram(_imgProg)
    bindQuad(_imgProg)
    gl.activeTexture(gl.TEXTURE0)
    gl.bindTexture(gl.TEXTURE_2D, tex)
    gl.uniform1i(_uImgTex, 0)
    gl.uniform4f(_uImgUvRect, 0, 0, uvW, uvH)

    const data = new Float32Array(FLOATS_PER_INST)
    data[0] = dx; data[1] = dy; data[2] = dw; data[3] = dh
    data[4] = 1; data[5] = 1; data[6] = 1; data[7] = state.globalAlpha
    data[8] = 0; data[9] = 0; data[10] = 0
    storeMatrix(data, 11, m)

    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, data)
    const stride = FLOATS_PER_INST * 4
    gl.enableVertexAttribArray(LOC_RECT)
    gl.vertexAttribPointer(LOC_RECT, 4, gl.FLOAT, false, stride, 0)
    gl.vertexAttribDivisor(LOC_RECT, 1)
    gl.enableVertexAttribArray(LOC_COLOR)
    gl.vertexAttribPointer(LOC_COLOR, 4, gl.FLOAT, false, stride, 4 * 4)
    gl.vertexAttribDivisor(LOC_COLOR, 1)
    for (let c = 0; c < 3; c++) {
        gl.enableVertexAttribArray(LOC_MATRIX_IMG + c)
        gl.vertexAttribPointer(LOC_MATRIX_IMG + c, 3, gl.FLOAT, false, stride, (11 + c * 3) * 4)
        gl.vertexAttribDivisor(LOC_MATRIX_IMG + c, 1)
    }

    gl.uniform2f(_uImgRes, _canvas.width, _canvas.height)
    gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, 1)
}
