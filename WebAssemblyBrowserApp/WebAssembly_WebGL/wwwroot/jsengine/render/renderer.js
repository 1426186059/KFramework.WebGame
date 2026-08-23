// =====================================================================
// 渲染层核心：WebGL 2.0 初始化 + 实例化批处理。
// 本模块持有全部共享 GL 状态（画布、上下文、实例缓冲、矩阵/透明度/阴影
// 状态栈），并通过 export let 活绑定暴露给 shapes.js / text.js / 入口。
//
// 关键设计：每个实例在「绘制调用当时」把当前变换矩阵烘焙进实例数据
// （attribute mat3），因此 Save/Translate/Rotate 块内绘制的图形在任何
// 时刻 flush 都保持正确位置，与 Canvas2D 的立即应用语义一致。
// 本工程仅使用 WebGL 2.0（getContext('webgl2')），可直接使用
// gl.vertexAttribDivisor / gl.drawArraysInstanced 等原生实例化 API。
// =====================================================================

import { SHAPE_VERT, SHAPE_FRAG } from './shaders/shape.js'
import { IMG_VERT, IMG_FRAG } from './shaders/image.js'
import { BLUR_VERT, BLUR_FRAG } from './shaders/blur.js'

// ------------------------- 共享状态 -------------------------
export let _gl = null
export let _canvas = null
export let _fontCanvas = null
export let _fontCtx = null

// 每帧可变的绘制状态（矩阵栈 / 透明度 / 阴影）。
// 用「可变对象」而非 export let 暴露：ES module 的 import 绑定本身只读，
// import 方（shapes.js）直接给绑定赋值会抛 TypeError，改对象属性则合法。
export const state = {
    matrixStack: [identity()],
    alphaStack: [1],
    shadowStack: [[null, 0]],
    globalAlpha: 1,
    shadowColor: null,
    shadowBlur: 0,
}

export const _textures = {}

// 实例属性：
//  0-3   rect (x, y, w, h)
//  4-7   color (r, g, b, a)
//  8     radius（归一化到短边）
//  9     kind（0=圆角矩形, 1=圆）
//  10    shadowBlur（仅阴影批次使用，普通实例恒为 0）
//  11-19 实例矩阵（列主序 9 个 float，attribute mat3）
export const FLOATS_PER_INST = 20
export const MAX_INSTANCES = 4096
export let _instData = new Float32Array(MAX_INSTANCES * 4 * FLOATS_PER_INST)
export let _instCount = 0
let _shadowData = new Float32Array(MAX_INSTANCES * 4 * FLOATS_PER_INST)
let _shadowCount = 0

// ------------------------- 矩阵工具 -------------------------
export function identity() { return new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1]) }

// 标准矩阵乘法 r = a·b（行主序）。Canvas2D 语义：M_new = M_old · M_transform（右乘），
// 因此 translate/rotate 等后调用的变换先作用于顶点（旋转围绕局部原点/物体中心）。
export function multiply(a, b) {
    const r = new Float32Array(9)
    for (let i = 0; i < 3; i++)
        for (let j = 0; j < 3; j++)
            r[i * 3 + j] = a[i * 3 + 0] * b[0 * 3 + j] + a[i * 3 + 1] * b[1 * 3 + j] + a[i * 3 + 2] * b[2 * 3 + j]
    return r
}

export function currentMatrix() { return state.matrixStack[state.matrixStack.length - 1] }

// 把仿射矩阵按「列主序」写入实例数据（GLSL attribute mat3 的布局）
export function storeMatrix(arr, base, m) {
    arr[base] = m[0]; arr[base + 1] = m[3]; arr[base + 2] = m[6]
    arr[base + 3] = m[1]; arr[base + 4] = m[4]; arr[base + 5] = m[7]
    arr[base + 6] = m[2]; arr[base + 7] = m[5]; arr[base + 8] = m[8]
}

export function hexToRgb(hex) {
    if (typeof hex !== 'string') return [1, 1, 1, 1]
    let h = hex.replace('#', '')
    if (h.length === 3) h = h.split('').map(c => c + c).join('')
    let a = 1
    if (h.length === 8) { a = parseInt(h.slice(6, 8), 16) / 255; h = h.slice(0, 6) }
    const n = parseInt(h, 16)
    return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255, a]
}

// ------------------------- 程序 / 缓冲 / uniform 位置 -------------------------
export let _shapeProg = null, _imgProg = null, _blurProg = null
export let _quadBuf = null, _instBuf = null, _fbBuf = null
export let _uShapeRes = null
export let _uImgRes = null, _uImgTex = null, _uImgUvRect = null
let _uBlurTex = null, _uBlurTexel = null, _uBlurDir = null
let _shadowTex = null, _shadowFBO = null, _blurTex = null, _blurFBO = null
let _shadowW = 0, _shadowH = 0

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

export function initGL(canvas) {
    const gl = canvas.getContext('webgl2', { alpha: false, antialias: true, premultipliedAlpha: false })
    if (!gl) throw new Error('WebGL2 not supported')
    _gl = gl

    _shapeProg = link(gl, SHAPE_VERT, SHAPE_FRAG)
    _imgProg = link(gl, IMG_VERT, IMG_FRAG)
    _blurProg = link(gl, BLUR_VERT, BLUR_FRAG)

    _quadBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1]), gl.STATIC_DRAW)

    _instBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferData(gl.ARRAY_BUFFER, MAX_INSTANCES * 4 * FLOATS_PER_INST * 4, gl.DYNAMIC_DRAW)

    _fbBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _fbBuf)
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, 1, 1, -1, -1, 1, 1, -1, 1]), gl.STATIC_DRAW)

    gl.enable(gl.BLEND)
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA)

    _uShapeRes = gl.getUniformLocation(_shapeProg, 'u_resolution')
    _uImgRes = gl.getUniformLocation(_imgProg, 'u_resolution')
    _uImgTex = gl.getUniformLocation(_imgProg, 'u_tex')
    _uImgUvRect = gl.getUniformLocation(_imgProg, 'u_uvRect')
    _uBlurTex = gl.getUniformLocation(_blurProg, 'u_tex')
    _uBlurTexel = gl.getUniformLocation(_blurProg, 'u_texel')
    _uBlurDir = gl.getUniformLocation(_blurProg, 'u_dir')

    _fontCanvas = document.createElement('canvas')
    _fontCanvas.width = 1024
    _fontCanvas.height = 256
    _fontCtx = _fontCanvas.getContext('2d')

    initFBOs(canvas.width, canvas.height)
}

function initFBOs(w, h) {
    const gl = _gl
    _shadowW = w; _shadowH = h
    _shadowTex = gl.createTexture()
    gl.bindTexture(gl.TEXTURE_2D, _shadowTex)
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
    _shadowFBO = gl.createFramebuffer()
    gl.bindFramebuffer(gl.FRAMEBUFFER, _shadowFBO)
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, _shadowTex, 0)

    _blurTex = gl.createTexture()
    gl.bindTexture(gl.TEXTURE_2D, _blurTex)
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
    _blurFBO = gl.createFramebuffer()
    gl.bindFramebuffer(gl.FRAMEBUFFER, _blurFBO)
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, _blurTex, 0)
    gl.bindFramebuffer(gl.FRAMEBUFFER, null)
}

// a_pos 固定为 location 0（各 shader 的显式 layout 一致）
export const LOC_POS = 0
export const LOC_RECT = 1
export const LOC_COLOR = 2
export const LOC_PARAMS = 3
export const LOC_MATRIX_SHAPE = 4   // SHAPE：mat3 占 4,5,6
export const LOC_MATRIX_IMG = 3     // IMG：mat3 占 3,4,5

export function bindQuad(prog) {
    const gl = _gl
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    gl.enableVertexAttribArray(LOC_POS)
    gl.vertexAttribPointer(LOC_POS, 2, gl.FLOAT, false, 0, 0)
}

// ------------------------- 实例批处理 -------------------------
export function pushInstance(x, y, w, h, color, radius, kind) {
    const rgb = hexToRgb(color)
    const a = rgb[3] * state.globalAlpha
    const m = currentMatrix()

    // 阴影：与本体同一批次，先画（偏移 +2,+3、半透明），再画本体盖住中心，
    // 效果等价 Canvas2D 的 shadow（无需 FBO/高斯模糊，杜绝帧缓冲串扰）
    if (state.shadowColor) {
        const srgb = hexToRgb(state.shadowColor)
        if (_instCount >= MAX_INSTANCES) flushShapes()
        let base = _instCount * 4 * FLOATS_PER_INST
        for (let i = 0; i < 4; i++) {
            const o = base + i * FLOATS_PER_INST
            _instData[o] = x + 2; _instData[o + 1] = y + 3
            _instData[o + 2] = w; _instData[o + 3] = h
            _instData[o + 4] = srgb[0]; _instData[o + 5] = srgb[1]; _instData[o + 6] = srgb[2]; _instData[o + 7] = srgb[3] * 0.35
            _instData[o + 8] = radius; _instData[o + 9] = kind; _instData[o + 10] = 0
            storeMatrix(_instData, o + 11, m)
        }
        _instCount++
    }

    if (_instCount >= MAX_INSTANCES) flushShapes()
    let base = _instCount * 4 * FLOATS_PER_INST
    for (let i = 0; i < 4; i++) {
        const o = base + i * FLOATS_PER_INST
        _instData[o] = x; _instData[o + 1] = y; _instData[o + 2] = w; _instData[o + 3] = h
        _instData[o + 4] = rgb[0]; _instData[o + 5] = rgb[1]; _instData[o + 6] = rgb[2]; _instData[o + 7] = a
        _instData[o + 8] = radius; _instData[o + 9] = kind; _instData[o + 10] = 0
        storeMatrix(_instData, o + 11, m)
    }
    _instCount++
}

function setupShapeAttribs(prog, data, count) {
    const gl = _gl
    gl.useProgram(prog)
    bindQuad(prog)
    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, data.subarray(0, count * 4 * FLOATS_PER_INST))
    const stride = FLOATS_PER_INST * 4
    gl.enableVertexAttribArray(LOC_RECT)
    gl.vertexAttribPointer(LOC_RECT, 4, gl.FLOAT, false, stride, 0)
    gl.vertexAttribDivisor(LOC_RECT, 1)
    gl.enableVertexAttribArray(LOC_COLOR)
    gl.vertexAttribPointer(LOC_COLOR, 4, gl.FLOAT, false, stride, 4 * 4)
    gl.vertexAttribDivisor(LOC_COLOR, 1)
    gl.enableVertexAttribArray(LOC_PARAMS)
    gl.vertexAttribPointer(LOC_PARAMS, 2, gl.FLOAT, false, stride, 8 * 4)
    gl.vertexAttribDivisor(LOC_PARAMS, 1)
    for (let c = 0; c < 3; c++) {
        gl.enableVertexAttribArray(LOC_MATRIX_SHAPE + c)
        gl.vertexAttribPointer(LOC_MATRIX_SHAPE + c, 3, gl.FLOAT, false, stride, (11 + c * 3) * 4)
        gl.vertexAttribDivisor(LOC_MATRIX_SHAPE + c, 1)
    }
}

function drawShapeBatch(data, count) {
    setupShapeAttribs(_shapeProg, data, count)
    const gl = _gl
    gl.uniform2f(_uShapeRes, _canvas.width, _canvas.height)
    gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, count)
}

export function flushShapes() {
    const gl = _gl
    if (_instCount === 0) return
    gl.bindFramebuffer(gl.FRAMEBUFFER, null)
    gl.viewport(0, 0, _canvas.width, _canvas.height)
    drawShapeBatch(_instData, _instCount)
    _instCount = 0
}

// 每帧开始重置变换 / 透明度 / 阴影状态（入口 frame 循环调用）
export function resetFrameState() {
    state.matrixStack = [identity()]
    state.alphaStack = [1]
    state.shadowStack = [[null, 0]]
    state.globalAlpha = 1
    state.shadowColor = null
    state.shadowBlur = 0
}

export function setCanvas(el) { _canvas = el }

export function getGL() { return _gl }
export function getCanvas() { return _canvas }
