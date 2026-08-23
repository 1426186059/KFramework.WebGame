// =====================================================================
// 只提供 WebGL 2.0 API
// 渲染层核心（薄 API）：只暴露原始 WebGL 操作，不做任何业务状态/合批。
// 合批逻辑、矩阵栈、状态管理全部由 C# 层（WebGL.cs）处理。
// 本模块只负责：
//   - 初始化 GL 上下文、编译着色器、创建 VBO/实例缓冲
//   - 上传 C# 准备好的批量实例数据并 drawArraysInstanced
//   - 上传单个纹理/文本纹理并绘制纹理四边形
//   - 基础 gl.* 调用（清屏、视口、blend）
// =====================================================================

import { SHAPE_VERT, SHAPE_FRAG } from './shaders/shape.js'
import { IMG_VERT, IMG_FRAG } from './shaders/image.js'
import { BLUR_VERT, BLUR_FRAG } from './shaders/blur.js'

// ------------------------- 共享 GL 对象 -------------------------
let _gl = null
let _canvas = null
let _fontCanvas = null
let _fontCtx = null

// Program / uniform 位置
let _shapeProg = null, _imgProg = null, _blurProg = null
let _quadBuf = null, _instBuf = null
let _uShapeRes = null
let _uImgRes = null, _uImgTex = null, _uImgUvRect = null

// 纹理缓存：id -> WebGLTexture
const _textures = new Map()
let _nextTexId = 1

// 文本纹理缓存：key -> { texId, tw, th, ascent, pad }，LRU
const _textCache = new Map()
const TEXT_CACHE_LIMIT = 128

// ------------------------- 常量（与 C# 保持一致） -------------------------
// FLOATS_PER_INST = 20 （每实例 1 份，紧凑布局，不再是旧版本的 4 份重复）
//   0-3   rect (x, y, w, h)
//   4-7   color (r, g, b, a)
//   8     radius
//   9     kind（0=圆角矩形, 1=圆）
//   10    reserved (shadowBlur 或 padding)
//   11-19 实例矩阵 mat3（列主序 9 个 float）
export const FLOATS_PER_INST = 20
export const MAX_INSTANCES = 4096

export const LOC_POS = 0
export const LOC_RECT = 1
export const LOC_COLOR = 2
export const LOC_PARAMS = 3
export const LOC_MATRIX_SHAPE = 4   // SHAPE：mat3 占 4,5,6
export const LOC_MATRIX_IMG = 3     // IMG：mat3 占 3,4,5

// ------------------------- 着色器编译 -------------------------
function compile(gl, type, src) {
    const sh = gl.createShader(type)
    gl.shaderSource(sh, src)
    gl.compileShader(sh)
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS))
        throw new Error('Shader compile: ' + gl.getShaderInfoLog(sh))
    return sh
}
function link(gl, vs, fs) {
    const p = gl.createProgram()
    gl.attachShader(p, compile(gl, gl.VERTEX_SHADER, vs))
    gl.attachShader(p, compile(gl, gl.FRAGMENT_SHADER, fs))
    gl.linkProgram(p)
    if (!gl.getProgramParameter(p, gl.LINK_STATUS))
        throw new Error('Program link: ' + gl.getProgramInfoLog(p))
    return p
}

function bindQuad(prog) {
    const gl = _gl
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    gl.enableVertexAttribArray(LOC_POS)
    gl.vertexAttribPointer(LOC_POS, 2, gl.FLOAT, false, 0, 0)
}

// ------------------------- 薄 API 导出 -------------------------
export const glCore = {
    // ------------------------- 初始化 -------------------------
    init(selector, width, height) {
        const el = document.querySelector(selector)
        el.width = width
        el.height = height
        _canvas = el

        const gl = el.getContext('webgl2', { alpha: false, antialias: true, premultipliedAlpha: false })
        if (!gl) throw new Error('WebGL2 not supported')
        _gl = gl

        _shapeProg = link(gl, SHAPE_VERT, SHAPE_FRAG)
        _imgProg = link(gl, IMG_VERT, IMG_FRAG)
        _blurProg = link(gl, BLUR_VERT, BLUR_FRAG)

        // 全屏 quad（-1..1）：2 个三角形 = 6 个顶点
        _quadBuf = gl.createBuffer()
        gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1]), gl.STATIC_DRAW)

        // 实例缓冲：预留足够空间（MAX_INSTANCES=4096，每实例 FLOATS_PER_INST floats，紧凑布局）
        _instBuf = gl.createBuffer()
        gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
        gl.bufferData(gl.ARRAY_BUFFER, MAX_INSTANCES * FLOATS_PER_INST * 4, gl.DYNAMIC_DRAW)

        gl.enable(gl.BLEND)
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA)

        _uShapeRes = gl.getUniformLocation(_shapeProg, 'u_resolution')
        _uImgRes = gl.getUniformLocation(_imgProg, 'u_resolution')
        _uImgTex = gl.getUniformLocation(_imgProg, 'u_tex')
        _uImgUvRect = gl.getUniformLocation(_imgProg, 'u_uvRect')

        // 字体烘焙用离屏 Canvas
        _fontCanvas = document.createElement('canvas')
        _fontCanvas.width = 1024
        _fontCanvas.height = 256
        _fontCtx = _fontCanvas.getContext('2d')

        // 画布自适应到窗口（逻辑分辨率固定为 width×height，CSS 像素做缩放）
        // 这部分原来定义在 main.js engine.initCanvas 里，但 C# 只调 gl.init，
        // 所以直接合并进 glCore.init，保证无论谁初始化都正确缩放。
        const fit = () => {
            const scale = Math.min(
                (window.innerWidth - 24) / width,
                (window.innerHeight - 24) / height
            )
            const s = Math.min(1.6, Math.max(0.25, scale))
            el.style.width = (width * s) + 'px'
            el.style.height = (height * s) + 'px'
        }
        window.addEventListener('resize', fit)
        fit()

        document.getElementById('loading')?.remove()
    },

    // ------------------------- 清屏 -------------------------
    clear(r, g, b, a) {
        const gl = _gl
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.viewport(0, 0, _canvas.width, _canvas.height)
        gl.clearColor(r, g, b, a ?? 1)
        gl.clear(gl.COLOR_BUFFER_BIT)
    },

    // ------------------------- 形状：批量实例化绘制 -------------------------
    // data: Float32Array，C# 已组装好的实例数据（每实例 FLOATS_PER_INST floats，紧凑布局）
    // instanceCount: 实例数
    drawShapeBatch(data, instanceCount) {
        if (instanceCount <= 0) return
        const gl = _gl
        const prog = _shapeProg
        gl.useProgram(prog)
        bindQuad(prog)

        // 上传实例数据（只上传 instanceCount × FLOATS_PER_INST floats）
        gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
        const floatCount = instanceCount * FLOATS_PER_INST
        const byteLen = floatCount * 4
        if (data.length >= floatCount && data.byteLength === byteLen) {
            gl.bufferSubData(gl.ARRAY_BUFFER, 0, data)
        } else {
            gl.bufferSubData(gl.ARRAY_BUFFER, 0, data.subarray(0, floatCount))
        }

        const stride = FLOATS_PER_INST * 4
        // a_rect (vec4, location=1)
        gl.enableVertexAttribArray(LOC_RECT)
        gl.vertexAttribPointer(LOC_RECT, 4, gl.FLOAT, false, stride, 0)
        gl.vertexAttribDivisor(LOC_RECT, 1)
        // a_color (vec4, location=2)
        gl.enableVertexAttribArray(LOC_COLOR)
        gl.vertexAttribPointer(LOC_COLOR, 4, gl.FLOAT, false, stride, 4 * 4)
        gl.vertexAttribDivisor(LOC_COLOR, 1)
        // a_params (vec2: radius, kind, location=3)
        gl.enableVertexAttribArray(LOC_PARAMS)
        gl.vertexAttribPointer(LOC_PARAMS, 2, gl.FLOAT, false, stride, 8 * 4)
        gl.vertexAttribDivisor(LOC_PARAMS, 1)
        // a_matrix (mat3, location=4,5,6)
        for (let c = 0; c < 3; c++) {
            const loc = LOC_MATRIX_SHAPE + c
            gl.enableVertexAttribArray(loc)
            gl.vertexAttribPointer(loc, 3, gl.FLOAT, false, stride, (11 + c * 3) * 4)
            gl.vertexAttribDivisor(loc, 1)
        }

        gl.uniform2f(_uShapeRes, _canvas.width, _canvas.height)
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, instanceCount)
    },

    // ------------------------- 图片/文本：单实例纹理绘制 -------------------------
    // data: Float32Array[FLOATS_PER_INST]，单实例数据（rect/color/matrix）
    // texId: 纹理 id（由 loadImage 或 bakeTextTexture 返回）
    // uvW, uvH: UV 区域宽高（0..1，用于图集裁剪）
    drawImageInstance(data, texId, uvW, uvH) {
        const gl = _gl
        const tex = _textures.get(texId)
        if (!tex) return

        gl.useProgram(_imgProg)
        bindQuad(_imgProg)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, tex)
        gl.uniform1i(_uImgTex, 0)
        gl.uniform4f(_uImgUvRect, 0, 0, uvW ?? 1, uvH ?? 1)

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
            const loc = LOC_MATRIX_IMG + c
            gl.enableVertexAttribArray(loc)
            gl.vertexAttribPointer(loc, 3, gl.FLOAT, false, stride, (11 + c * 3) * 4)
            gl.vertexAttribDivisor(loc, 1)
        }

        gl.uniform2f(_uImgRes, _canvas.width, _canvas.height)
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, 1)
    },

    // ------------------------- 图片加载 -------------------------
    loadImage(id, url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => {
                const gl = _gl
                const tex = gl.createTexture()
                gl.bindTexture(gl.TEXTURE_2D, tex)
                gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false)
                gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, img)
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR)
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
                gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
                _textures.set(id, tex)
                resolve(true)
            }
            img.onerror = () => resolve(false)
            img.src = url
        })
    },

    // ------------------------- 文本纹理烘焙 -------------------------
    // C# 层不会处理字体测量/离屏 Canvas（这是浏览器特有 API），
    // 所以文本纹理的「创建 + 缓存」仍由 JS 薄 API 负责，但绘制调用由 C# 控制。
    // 返回：{ texId, tw, th, ascent, pad } 或 null
    bakeTextTexture(text, font, color) {
        const key = text + '\u0000' + font + '\u0000' + color
        let entry = _textCache.get(key)
        if (!entry) {
            entry = bakeTextImpl(text, font, color)
            if (!entry) return null
            _textCache.set(key, entry)
            if (_textCache.size > TEXT_CACHE_LIMIT) {
                const oldest = _textCache.keys().next().value
                const old = _textCache.get(oldest)
                const tex = _textures.get(old.texId)
                if (tex) _gl.deleteTexture(tex)
                _textures.delete(old.texId)
                _textCache.delete(oldest)
            }
        }
        return { texId: entry.texId, tw: entry.tw, th: entry.th, ascent: entry.ascent, pad: entry.pad }
    },

    // ------------------------- 辅助 -------------------------
    getCanvas() { return _canvas },
    getWidth() { return _canvas?.width ?? 0 },
    getHeight() { return _canvas?.height ?? 0 },

    // ------------------------- 图片 DrawImage（通过 string id） -------------------------
    // C# 侧传矩阵（行主序 float[9]）+ alpha；本函数内部组装实例数据并绘制。
    drawImageById(id, dx, dy, dw, dh, matrixArr, alpha) {
        const tex = _textures.get(id)
        if (!tex) return
        const gl = _gl
        const m = matrixArr
        const data = new Float32Array(FLOATS_PER_INST)
        data[0] = dx; data[1] = dy; data[2] = dw; data[3] = dh
        data[4] = 1; data[5] = 1; data[6] = 1; data[7] = alpha
        data[8] = 0; data[9] = 0; data[10] = 0
        // 行主序 → 列主序
        data[11] = m[0]; data[12] = m[3]; data[13] = m[6]
        data[14] = m[1]; data[15] = m[4]; data[16] = m[7]
        data[17] = m[2]; data[18] = m[5]; data[19] = m[8]

        gl.useProgram(_imgProg)
        bindQuad(_imgProg)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, tex)
        gl.uniform1i(_uImgTex, 0)
        gl.uniform4f(_uImgUvRect, 0, 0, 1, 1)

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
            const loc = LOC_MATRIX_IMG + c
            gl.enableVertexAttribArray(loc)
            gl.vertexAttribPointer(loc, 3, gl.FLOAT, false, stride, (11 + c * 3) * 4)
            gl.vertexAttribDivisor(loc, 1)
        }
        gl.uniform2f(_uImgRes, _canvas.width, _canvas.height)
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, 1)
    },
}

// ------------------------- 文本纹理烘焙实现 -------------------------
function bakeTextImpl(text, font, color) {
    const ctx = _fontCtx
    ctx.clearRect(0, 0, _fontCanvas.width, _fontCanvas.height)
    ctx.fillStyle = color
    ctx.font = font
    ctx.textBaseline = 'alphabetic'
    ctx.textAlign = 'left'

    const fontSize = parseFloat((font.match(/(\d+(?:\.\d+)?)px/) || [, '16'])[1])
    const metrics = ctx.measureText(text)
    const ascent = Math.max(1, Math.ceil(metrics.fontBoundingBoxAscent || metrics.actualBoundingBoxAscent || fontSize * 0.85))
    const descent = Math.max(1, Math.ceil(metrics.fontBoundingBoxDescent || metrics.actualBoundingBoxDescent || fontSize * 0.25))
    const pad = 4
    ctx.fillText(text, pad, ascent + pad)

    const tw = Math.max(2, Math.min(1024, Math.ceil(metrics.width) + pad * 2))
    const th = Math.max(2, Math.min(256, ascent + descent + pad * 2))

    const gl = _gl
    const tex = gl.createTexture()
    gl.bindTexture(gl.TEXTURE_2D, tex)
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false)
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, _fontCanvas)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)

    const texId = _nextTexId++
    _textures.set(texId, tex)
    return { texId, tw, th, ascent, pad }
}
