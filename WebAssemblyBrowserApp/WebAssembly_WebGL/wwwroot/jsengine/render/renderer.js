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

// ---------- 调试探针（仅开发期有效） ----------
// 暴露到 window._dbg，在浏览器 Console 里可直接看每帧状态
const _dbg = {
    clears: 0,
    shapeBatches: 0,   // 调用 drawShapeBatch 次数
    totalShapes: 0,    // 累计绘制形状实例数
    imgDraws: 0,
    bakeCalls: 0,      // bakeTextTexture 调用次数
    bakeFailures: 0,   // bakeTextTexture 返回 null 次数
    lastImgPrefix: null,
    lastClearColor: null,
    lastShapeCount: 0,
    lastErrors: [],    // 最近的 gl.getError() 值
    uShapeResValue: null, // 最近一次 shape batch 用的 u_resolution
    canvasSize: null,
    viewportValue: null,
}
if (typeof window !== 'undefined') window._dbg = _dbg

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
        // --- 调试探针 ---
        _dbg.clears++
        _dbg.lastClearColor = [r, g, b, a]
        _dbg.canvasSize = [_canvas.width, _canvas.height]
        _dbg.viewportValue = gl.getParameter(gl.VIEWPORT)
        _checkGlError('after clear')
    },

    // ------------------------- 形状：批量实例化绘制 -------------------------
    // data: Float32Array，C# 已组装好的实例数据（每实例 FLOATS_PER_INST floats，紧凑布局）
    // instanceCount: 实例数
    drawShapeBatch(data, instanceCount) {
        if (instanceCount <= 0) return
        const gl = _gl
        const prog = _shapeProg

        // 保险：强制确认 WebGL 收到的是 Float32Array（byteLength 才匹配 float32）。
        // .NET 10 JSInterop 的 double[] 会被包装成 Float64Array，绝不能直接 bufferSubData。
        if (!(data instanceof Float32Array)) data = new Float32Array(data)

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
        // --- 调试探针（绘制前读一次 attrib 真实 location 验证） ---
        _dbg.shapeBatches++
        _dbg.totalShapes += instanceCount
        _dbg.lastShapeCount = instanceCount
        _dbg.uShapeResValue = [_canvas.width, _canvas.height]
        _dbg._uShapeResLoc = _uShapeRes
        _dbg._uShapeResIsValid = (_uShapeRes !== null && _uShapeRes !== undefined && _uShapeRes !== -1)
        if (instanceCount > 0) {
            // 读取首个实例的前 8 个浮点数验证上传数据正确性
            try {
                gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
                // 读回前 8 floats（rect x,y,w,h + color r,g,b,a）
                const fb = new Float32Array(8)
                gl.getBufferSubData(gl.ARRAY_BUFFER, 0, fb)
                _dbg.firstInstancePrefix = Array.from(fb)
                // 查一下真实 attrib location（与硬编码做对比）
                _dbg.attribLocs = {
                    pos: gl.getAttribLocation(prog, 'a_pos'),
                    rect: gl.getAttribLocation(prog, 'a_rect'),
                    color: gl.getAttribLocation(prog, 'a_color'),
                    params: gl.getAttribLocation(prog, 'a_params'),
                    matrix_col0: gl.getAttribLocation(prog, 'a_matrix[0]'),
                    matrix_col1: gl.getAttribLocation(prog, 'a_matrix[1]'),
                    matrix_col2: gl.getAttribLocation(prog, 'a_matrix[2]'),
                }
                // 颜色写掩码 + 剪刀测试 + 深度
                _dbg.colorMask = gl.getParameter(gl.COLOR_WRITEMASK)
                _dbg.scissorEnabled = gl.isEnabled(gl.SCISSOR_TEST)
                _dbg.scissorBox = _dbg.scissorEnabled ? gl.getParameter(gl.SCISSOR_BOX) : null
                _dbg.depthEnabled = gl.isEnabled(gl.DEPTH_TEST)
                _dbg.cullFaceEnabled = gl.isEnabled(gl.CULL_FACE)
                _dbg.currentProgramBound = gl.getParameter(gl.CURRENT_PROGRAM) === prog
                _dbg.vertexArrayBinding = gl.getParameter(gl.VERTEX_ARRAY_BINDING)
                _dbg.blendEnabled = gl.isEnabled(gl.BLEND)
            } catch (_) {}
        }
        _checkGlError('before shape drawArraysInstanced')
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, instanceCount)
        _checkGlError('after shape drawArraysInstanced n=' + instanceCount)
    },

    // ------------------------- 图片/文本：单实例纹理绘制 -------------------------
    // data: Float32Array[FLOATS_PER_INST]，单实例数据（rect/color/matrix）
    // texId: 纹理 id（由 loadImage 或 bakeTextTexture 返回）
    // uvW, uvH: UV 区域宽高（0..1，用于图集裁剪）
    drawImageInstance(data, texId, uvW, uvH) {
        const gl = _gl
        const tex = _textures.get(texId)
        if (!tex) return
        // 保险：强制 Float32Array（见 drawShapeBatch 注释）
        if (!(data instanceof Float32Array)) data = new Float32Array(data)

        gl.useProgram(_imgProg)
        bindQuad(_imgProg)
        gl.activeTexture(gl.TEXTURE0)
        gl.bindTexture(gl.TEXTURE_2D, tex)
        gl.uniform1i(_uImgTex, 0)
        gl.uniform4f(_uImgUvRect, 0, 0, uvW ?? 1, uvH ?? 1)

        gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
        // 与 shape 批次一致：只传实际用到的 FLOATS_PER_INST floats，避免 buffer 越界
        const floatCount = Math.min(data.length, FLOATS_PER_INST)
        if (data.byteLength === floatCount * 4) {
            gl.bufferSubData(gl.ARRAY_BUFFER, 0, data)
        } else {
            gl.bufferSubData(gl.ARRAY_BUFFER, 0, data.subarray(0, floatCount))
        }
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
        _dbg.imgDraws++
        try {
            // 读一下首个实例的 4 个字段前缀（rect x,y,w,h）验证
            gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
            const fb = new Float32Array(8)
            gl.getBufferSubData(gl.ARRAY_BUFFER, 0, fb)
            _dbg.lastImgPrefix = Array.from(fb)
            _dbg.imgAttribLocs = {
                rect: gl.getAttribLocation(_imgProg, 'a_rect'),
                color: gl.getAttribLocation(_imgProg, 'a_color'),
                matrix0: gl.getAttribLocation(_imgProg, 'a_matrix[0]'),
            }
            _dbg.uImgResValid = _uImgRes !== null && _uImgRes !== undefined
            _dbg.imgTexValid = gl.isTexture(_textures.get(texId))
            const imgProgOk = _imgProg && gl.getParameter(gl.CURRENT_PROGRAM) === _imgProg
            _dbg.imgProgBound = imgProgOk
        } catch (_) {}
        _checkGlError('before img drawArraysInstanced')
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, 1)
        _checkGlError('after img drawArraysInstanced')
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
        _dbg.bakeCalls++
        const key = text + '\u0000' + font + '\u0000' + color
        let entry = _textCache.get(key)
        if (!entry) {
            entry = bakeTextImpl(text, font, color)
            if (!entry) {
                _dbg.bakeFailures++
                return null
            }
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
        // 保险：强制 Float32Array（C# double[] 会被 JSInterop 包装成 Float64Array）
        const m = (matrixArr instanceof Float32Array) ? matrixArr : new Float32Array(matrixArr)
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

// ------------------------- GL 错误检查（调试期用） -------------------------
function _checkGlError(label) {
    const gl = _gl
    if (!gl) return
    let e = gl.getError()
    while (e !== gl.NO_ERROR) {
        const map = { [gl.INVALID_ENUM]: 'INVALID_ENUM', [gl.INVALID_VALUE]: 'INVALID_VALUE', [gl.INVALID_OPERATION]: 'INVALID_OPERATION', [gl.INVALID_FRAMEBUFFER_OPERATION]: 'INVALID_FRAMEBUFFER_OPERATION', [gl.OUT_OF_MEMORY]: 'OUT_OF_MEMORY' }
        const msg = (map[e] || ('0x' + e.toString(16))) + ' @ ' + label
        console.error('[GL Error]', msg)
        if (_dbg.lastErrors.length > 20) _dbg.lastErrors.shift()
        _dbg.lastErrors.push(msg)
        e = gl.getError()
    }
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
