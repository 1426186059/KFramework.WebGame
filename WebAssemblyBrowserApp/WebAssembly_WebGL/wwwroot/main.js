// =====================================================================
// .NET 10 WebAssembly 纯 WebGL 游戏引擎 —— JS 桥接层
// 渲染后端为 WebGL：所有图元（矩形 / 圆角 / 圆 / 文本 / 图片）都由着色器批量绘制。
// 亮点：
//   1. 精确圆角 SDF（片元着色器里用有符号距离场绘制真实圆角矩形）
//   2. 真阴影（离屏 FBO 渲染发光体 → 双 pass 高斯模糊 → 合成，非近似色块）
//   3. 真正的图片精灵贴图（独立纹理着色器通道）
// 游戏逻辑全部在 C# 中实现，通过 [JSImport]/[JSExport] 与本层双向互通。
// =====================================================================
import { dotnet } from './_framework/dotnet.js'

// ------------------------- 全局状态 -------------------------
let _gl = null
let _canvas = null
let _fontTex = null     // 离屏 2D 画布（文字烘焙为纹理）

let _matrixStack = [identity()]
let _globalAlpha = 1
let _shakeX = 0, _shakeY = 0

// 当前阴影设置
let _shadowColor = null
let _shadowBlur = 0

// 纹理缓存
const _textures = {}

// 实例属性：x, y, w, h, r, g, b, a, radius, kind
const FLOATS_PER_INST = 10
const MAX_INSTANCES = 4096
let _instData = new Float32Array(MAX_INSTANCES * 4 * FLOATS_PER_INST)
let _instCount = 0
// 阴影实例（独立批次，先渲到 FBO 模糊）
let _shadowData = new Float32Array(MAX_INSTANCES * 4 * FLOATS_PER_INST)
let _shadowCount = 0

let _keys = {}
let _pressed = {}
const _mouse = { x: 0, y: 0, down: false, pressed: false }
let _audioCtx = null
let _rafStarted = false
let _lastTs = 0

// ------------------------- 矩阵工具 -------------------------
function identity() { return new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1]) }
function multiply(a, b) {
    const r = new Float32Array(9)
    for (let i = 0; i < 3; i++)
        for (let j = 0; j < 3; j++)
            r[i * 3 + j] = a[0 * 3 + j] * b[i * 3 + 0] + a[1 * 3 + j] * b[i * 3 + 1] + a[2 * 3 + j] * b[i * 3 + 2]
    return r
}
function currentMatrix() { return _matrixStack[_matrixStack.length - 1] }

// ------------------------- 着色器源码 -------------------------
const SHAPE_VERT = `
attribute vec2 a_pos;
attribute vec4 a_rect;     // x, y, w, h (逻辑像素)
attribute vec4 a_color;    // r, g, b, a
attribute vec2 a_params;   // radius(归一化), kind(0=rect,1=circle)
uniform mat3 u_matrix;
uniform vec2 u_resolution;
varying vec2 v_uv;
varying vec4 v_color;
varying vec2 v_params;
void main() {
    vec2 local = vec2(a_rect.z * a_pos.x, a_rect.w * a_pos.y);
    vec2 world = vec2(a_rect.x, a_rect.y) + local;
    vec3 t = u_matrix * vec3(world, 1.0);
    vec2 clip = (t.xy / (u_resolution * 0.5)) - 1.0;
    clip.y = -clip.y;
    gl_Position = vec4(clip, 0.0, 1.0);
    v_uv = a_pos;
    v_color = a_color;
    v_params = a_params;
}
`

const SHAPE_FRAG = `
precision mediump float;
varying vec2 v_uv;
varying vec4 v_color;
varying vec2 v_params;

// 圆角矩形 SDF（局部坐标 -1..1，radius 为归一化圆角半径 0..1）
float sdRoundRect(vec2 p, float r) {
    vec2 q = abs(p) - (vec2(1.0) - r);
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

void main() {
    float kind = v_params.y;
    float alpha = 1.0;
    if (kind > 0.5) {
        float d = length(v_uv);          // 圆
        alpha = smoothstep(1.0, 0.96, d);
    } else {
        float sd = sdRoundRect(v_uv, v_params.x);
        alpha = smoothstep(0.015, -0.015, sd);   // 精确圆角矩形
    }
    if (alpha <= 0.001) discard;
    gl_FragColor = vec4(v_color.rgb, v_color.a * alpha);
}
`

const IMG_VERT = `
attribute vec2 a_pos;
attribute vec4 a_rect;
attribute vec4 a_color;
uniform mat3 u_matrix;
uniform vec2 u_resolution;
varying vec2 v_uv;
varying vec4 v_color;
void main() {
    vec2 local = vec2(a_rect.z * a_pos.x, a_rect.w * a_pos.y);
    vec2 world = vec2(a_rect.x, a_rect.y) + local;
    vec3 t = u_matrix * vec3(world, 1.0);
    vec2 clip = (t.xy / (u_resolution * 0.5)) - 1.0;
    clip.y = -clip.y;
    gl_Position = vec4(clip, 0.0, 1.0);
    v_uv = vec2(a_pos.x * 0.5 + 0.5, a_pos.y * 0.5 + 0.5);
    v_color = a_color;
}
`

const IMG_FRAG = `
precision mediump float;
varying vec2 v_uv;
varying vec4 v_color;
uniform sampler2D u_tex;
void main() {
    vec4 tex = texture2D(u_tex, v_uv);
    gl_FragColor = tex * v_color;
}
`

const BLUR_VERT = `
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
    v_uv = vec2(a_pos.x * 0.5 + 0.5, a_pos.y * 0.5 + 0.5);
    gl_Position = vec4(a_pos, 0.0, 1.0);
}
`
const BLUR_FRAG = `
precision mediump float;
varying vec2 v_uv;
uniform sampler2D u_tex;
uniform vec2 u_texel;
uniform vec2 u_dir;
void main() {
    vec4 sum = vec4(0.0);
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -4.0) * 0.05;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -3.0) * 0.09;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -2.0) * 0.12;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -1.0) * 0.15;
    sum += texture2D(u_tex, v_uv) * 0.18;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 1.0) * 0.15;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 2.0) * 0.12;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 3.0) * 0.09;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 4.0) * 0.05;
    gl_FragColor = sum;
}
`

// ------------------------- WebGL 初始化 -------------------------
let _shapeProg = null, _imgProg = null, _blurProg = null
let _quadBuf = null
let _instBuf = null
let _fbBuf = null
let _uShapeMatrix = null, _uShapeRes = null
let _uImgMatrix = null, _uImgRes = null, _uImgTex = null
let _uBlurTex = null, _uBlurTexel = null, _uBlurDir = null

let _shadowFBO = null, _shadowTex = null
let _blurFBO = null, _blurTex = null
let _shadowW = 0, _shadowH = 0

function compileShader(gl, type, src) {
    const sh = gl.createShader(type)
    gl.shaderSource(sh, src)
    gl.compileShader(sh)
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS))
        throw new Error('Shader compile error: ' + gl.getShaderInfoLog(sh))
    return sh
}
function linkProgram(gl, vsSrc, fsSrc) {
    const vs = compileShader(gl, gl.VERTEX_SHADER, vsSrc)
    const fs = compileShader(gl, gl.FRAGMENT_SHADER, fsSrc)
    const p = gl.createProgram()
    gl.attachShader(p, vs)
    gl.attachShader(p, fs)
    gl.linkProgram(p)
    if (!gl.getProgramParameter(p, gl.LINK_STATUS))
        throw new Error('Program link error: ' + gl.getProgramInfoLog(p))
    return p
}

function initGL(canvas) {
    const gl = canvas.getContext('webgl', { alpha: false, antialias: true, premultipliedAlpha: false })
        || canvas.getContext('experimental-webgl')
    if (!gl) throw new Error('WebGL not supported')
    _gl = gl

    _shapeProg = linkProgram(gl, SHAPE_VERT, SHAPE_FRAG)
    _imgProg = linkProgram(gl, IMG_VERT, IMG_FRAG)
    _blurProg = linkProgram(gl, BLUR_VERT, BLUR_FRAG)

    _quadBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
        -1, -1, 1, -1, 1, 1,
        -1, -1, 1, 1, -1, 1,
    ]), gl.STATIC_DRAW)

    _instBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferData(gl.ARRAY_BUFFER, MAX_INSTANCES * 4 * FLOATS_PER_INST * 4, gl.DYNAMIC_DRAW)

    _fbBuf = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, _fbBuf)
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([
        -1, -1, 1, -1, 1, 1,
        -1, -1, 1, 1, -1, 1,
    ]), gl.STATIC_DRAW)

    gl.enable(gl.BLEND)
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA)

    _uShapeMatrix = gl.getUniformLocation(_shapeProg, 'u_matrix')
    _uShapeRes = gl.getUniformLocation(_shapeProg, 'u_resolution')
    _uImgMatrix = gl.getUniformLocation(_imgProg, 'u_matrix')
    _uImgRes = gl.getUniformLocation(_imgProg, 'u_resolution')
    _uImgTex = gl.getUniformLocation(_imgProg, 'u_tex')
    _uBlurTex = gl.getUniformLocation(_blurProg, 'u_tex')
    _uBlurTexel = gl.getUniformLocation(_blurProg, 'u_texel')
    _uBlurDir = gl.getUniformLocation(_blurProg, 'u_dir')

    _fontTex = document.createElement('canvas')
    _fontTex.width = 1024
    _fontTex.height = 256

    initOffscreenFBOs(canvas.width, canvas.height)
}

function initOffscreenFBOs(w, h) {
    const gl = _gl
    _shadowW = w
    _shadowH = h
    ;[['shadow', '_shadowTex', '_shadowFBO'], ['blur', '_blurTex', '_blurFBO']].forEach(([nm, texVar, fboVar]) => {
        const tex = gl.createTexture()
        gl.bindTexture(gl.TEXTURE_2D, tex)
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE)
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE)
        const fbo = gl.createFramebuffer()
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo)
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0)
        if (texVar === '_shadowTex') _shadowTex = tex; else _blurTex = tex
        if (fboVar === '_shadowFBO') _shadowFBO = fbo; else _blurFBO = fbo
    })
    gl.bindFramebuffer(gl.FRAMEBUFFER, null)
}

function setupShapeAttribs() {
    const gl = _gl
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    const aPos = gl.getAttribLocation(_shapeProg, 'a_pos')
    gl.enableVertexAttribArray(aPos)
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0)
}
function setupImgAttribs() {
    const gl = _gl
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    const aPos = gl.getAttribLocation(_imgProg, 'a_pos')
    gl.enableVertexAttribArray(aPos)
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0)
}

// ------------------------- 实例批处理 -------------------------
function pushInstance(x, y, w, h, color, radius, kind) {
    const rgb = hexToRgb(color)
    const a = _globalAlpha

    // 正常实例
    if (_instCount >= MAX_INSTANCES) flushShapes()
    let base = _instCount * 4 * FLOATS_PER_INST
    for (let i = 0; i < 4; i++) {
        const o = base + i * FLOATS_PER_INST
        _instData[o] = x; _instData[o + 1] = y; _instData[o + 2] = w; _instData[o + 3] = h
        _instData[o + 4] = rgb[0]; _instData[o + 5] = rgb[1]; _instData[o + 6] = rgb[2]; _instData[o + 7] = a
        _instData[o + 8] = radius; _instData[o + 9] = kind
    }
    _instCount++

    // 阴影副本（独立批次，稍作偏移）
    if (_shadowColor) {
        if (_shadowCount >= MAX_INSTANCES) flushShapes()
        const srgb = hexToRgb(_shadowColor)
        const ox = x + 2, oy = y + 3
        base = _shadowCount * 4 * FLOATS_PER_INST
        for (let i = 0; i < 4; i++) {
            const o = base + i * FLOATS_PER_INST
            _shadowData[o] = ox; _shadowData[o + 1] = oy; _shadowData[o + 2] = w; _shadowData[o + 3] = h
            _shadowData[o + 4] = srgb[0]; _shadowData[o + 5] = srgb[1]; _shadowData[o + 6] = srgb[2]; _shadowData[o + 7] = 1
            _shadowData[o + 8] = radius; _shadowData[o + 9] = kind
        }
        _shadowCount++
    }
}

function drawShapeBatch(data, count) {
    const gl = _gl
    gl.useProgram(_shapeProg)
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    setupShapeAttribs()
    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, data.subarray(0, count * 4 * FLOATS_PER_INST))
    const stride = FLOATS_PER_INST * 4
    const aRect = gl.getAttribLocation(_shapeProg, 'a_rect')
    const aColor = gl.getAttribLocation(_shapeProg, 'a_color')
    const aParams = gl.getAttribLocation(_shapeProg, 'a_params')
    gl.enableVertexAttribArray(aRect)
    gl.vertexAttribPointer(aRect, 4, gl.FLOAT, false, stride, 0)
    gl.vertexAttribDivisor(aRect, 1)
    gl.enableVertexAttribArray(aColor)
    gl.vertexAttribPointer(aColor, 4, gl.FLOAT, false, stride, 4 * 4)
    gl.vertexAttribDivisor(aColor, 1)
    gl.enableVertexAttribArray(aParams)
    gl.vertexAttribPointer(aParams, 2, gl.FLOAT, false, stride, 8 * 4)
    gl.vertexAttribDivisor(aParams, 1)
    gl.uniformMatrix3fv(_uShapeMatrix, false, currentMatrix())
    gl.uniform2f(_uShapeRes, _canvas.width, _canvas.height)
    gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, count)
}

// 真阴影：阴影批次 → FBO → 双 pass 高斯模糊 → 合成到屏幕
function renderShadowPass() {
    const gl = _gl
    // 1) 渲阴影实例到离屏 FBO
    gl.bindFramebuffer(gl.FRAMEBUFFER, _shadowFBO)
    gl.viewport(0, 0, _shadowW, _shadowH)
    gl.clearColor(0, 0, 0, 0)
    gl.clear(gl.COLOR_BUFFER_BIT)
    drawShapeBatch(_shadowData, _shadowCount)
    gl.bindFramebuffer(gl.FRAMEBUFFER, null)

    // 2) 模糊：H (shadowTex → blurTex) → V (blurTex → 屏幕)
    gl.useProgram(_blurProg)
    gl.bindBuffer(gl.ARRAY_BUFFER, _fbBuf)
    const aPos = gl.getAttribLocation(_blurProg, 'a_pos')
    gl.enableVertexAttribArray(aPos)
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0)
    gl.uniform2f(_uBlurTexel, 1 / _shadowW, 1 / _shadowH)

    // H pass
    gl.bindFramebuffer(gl.FRAMEBUFFER, _blurFBO)
    gl.viewport(0, 0, _shadowW, _shadowH)
    gl.activeTexture(gl.TEXTURE0)
    gl.bindTexture(gl.TEXTURE_2D, _shadowTex)
    gl.uniform1i(_uBlurTex, 0)
    gl.uniform2f(_uBlurDir, _shadowBlur, 0)
    gl.drawArrays(gl.TRIANGLES, 0, 6)

    // V pass 直接输出到屏幕（半透明叠加）
    gl.bindFramebuffer(gl.FRAMEBUFFER, null)
    gl.viewport(0, 0, _canvas.width, _canvas.height)
    gl.activeTexture(gl.TEXTURE0)
    gl.bindTexture(gl.TEXTURE_2D, _blurTex)
    gl.uniform1i(_uBlurTex, 0)
    gl.uniform2f(_uBlurDir, 0, _shadowBlur)
    gl.drawArrays(gl.TRIANGLES, 0, 6)
}

function flushShapes() {
    const gl = _gl
    if (_instCount === 0 && _shadowCount === 0) return

    // 阴影在最底层
    if (_shadowColor && _shadowCount > 0) {
        renderShadowPass()
    }

    // 正常实例
    if (_instCount > 0) {
        gl.bindFramebuffer(gl.FRAMEBUFFER, null)
        gl.viewport(0, 0, _canvas.width, _canvas.height)
        drawShapeBatch(_instData, _instCount)
    }

    _instCount = 0
    _shadowCount = 0
}

function hexToRgb(hex) {
    if (typeof hex !== 'string') return [1, 1, 1]
    let h = hex.replace('#', '')
    if (h.length === 3) h = h.split('').map(c => c + c).join('')
    if (h.length === 8) h = h.slice(0, 6)
    const n = parseInt(h, 16)
    return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255]
}

// ------------------------- gl.* 桥接 API -------------------------
const gl = {
    init(selector, width, height) {
        const el = document.querySelector(selector)
        el.width = width
        el.height = height
        _canvas = el
        initGL(el)
        document.getElementById('loading')?.remove()
    },

    clear(color) {
        flushShapes()
        const rgb = hexToRgb(color)
        _gl.bindFramebuffer(_gl.FRAMEBUFFER, null)
        _gl.viewport(0, 0, _canvas.width, _canvas.height)
        _gl.clearColor(rgb[0], rgb[1], rgb[2], 1)
        _gl.clear(_gl.COLOR_BUFFER_BIT)
        _shakeX = 0
        _shakeY = 0
    },

    fillRect(x, y, w, h, color) { pushInstance(x, y, w, h, color, 0, 0) },
    roundedRect(x, y, w, h, r, color) {
        const shortSide = Math.min(w, h)
        const nr = Math.min(1, r / (shortSide / 2))
        pushInstance(x, y, w, h, color, nr, 0)
    },
    strokeRect(x, y, w, h, color, lineWidth) { pushInstance(x, y, w, h, color, 0, 0) },
    fillCircle(x, y, r, color) { pushInstance(x - r, y - r, r * 2, r * 2, color, 0, 1) },
    line(x1, y1, x2, y2, color, lineWidth) { pushInstance(x1, y1, x2 - x1, y2 - y1, color, 0, 0) },

    fillText(text, x, y, font, color, align) {
        flushShapes()
        drawTextSprite(text, x, y, font, color, align)
    },

    save() { _matrixStack.push(new Float32Array(currentMatrix())) },
    restore() { if (_matrixStack.length > 1) _matrixStack.pop() },
    translate(x, y) {
        const m = currentMatrix()
        const t = identity()
        t[2] = x
        t[5] = y
        _matrixStack[_matrixStack.length - 1] = multiply(m, t)
    },
    rotate(rad) {
        const m = currentMatrix()
        const c = Math.cos(rad), s = Math.sin(rad)
        const r = identity()
        r[0] = c; r[1] = -s; r[3] = s; r[4] = c
        _matrixStack[_matrixStack.length - 1] = multiply(m, r)
    },
    alpha(a) { _globalAlpha = a },

    // 真阴影：设置阴影色与模糊半径，后续图元会先渲染到离屏 FBO 并模糊合成
    shadow(color, blur) { _shadowColor = color; _shadowBlur = blur },
    noShadow() { _shadowColor = null; _shadowBlur = 0 },

    loadImage(id, url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => {
                const tex = _gl.createTexture()
                _gl.bindTexture(_gl.TEXTURE_2D, tex)
                _gl.texImage2D(_gl.TEXTURE_2D, 0, _gl.RGBA, _gl.RGBA, _gl.UNSIGNED_BYTE, img)
                _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_MIN_FILTER, _gl.LINEAR)
                _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_WRAP_S, _gl.CLAMP_TO_EDGE)
                _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_WRAP_T, _gl.CLAMP_TO_EDGE)
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
        if (!tex) return
        drawTexturedQuad(tex, dx, dy, dw, dh)
    },
}

// 文本：用离屏 2D canvas 绘制后作为纹理上传
function drawTextSprite(text, x, y, font, color, align) {
    const ctx = _fontTex.getContext('2d')
    ctx.clearRect(0, 0, _fontTex.width, _fontTex.height)
    ctx.fillStyle = color
    ctx.font = font
    ctx.textBaseline = 'middle'
    ctx.textAlign = 'left'
    ctx.fillText(text, 4, _fontTex.height / 2)

    const metrics = ctx.measureText(text)
    const tw = Math.max(2, Math.ceil(metrics.width) + 8)
    const th = 64
    let ox = x
    if (align === 'center') ox = x - tw / 2
    else if (align === 'right') ox = x - tw

    const tex = _gl.createTexture()
    _gl.bindTexture(_gl.TEXTURE_2D, tex)
    _gl.pixelStorei(_gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false)
    _gl.texImage2D(_gl.TEXTURE_2D, 0, _gl.RGBA, _gl.RGBA, _gl.UNSIGNED_BYTE, _fontTex)
    _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_MIN_FILTER, _gl.LINEAR)
    _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_WRAP_S, _gl.CLAMP_TO_EDGE)
    _gl.texParameteri(_gl.TEXTURE_2D, _gl.TEXTURE_WRAP_T, _gl.CLAMP_TO_EDGE)
    drawTexturedQuad(tex, ox, y - th / 2, tw, th)
    _gl.deleteTexture(tex)
}

// 用图片着色器绘制一个带纹理的四边形
function drawTexturedQuad(tex, dx, dy, dw, dh) {
    const gl = _gl
    gl.useProgram(_imgProg)
    gl.bindBuffer(gl.ARRAY_BUFFER, _quadBuf)
    setupImgAttribs()

    gl.activeTexture(gl.TEXTURE0)
    gl.bindTexture(gl.TEXTURE_2D, tex)
    gl.uniform1i(_uImgTex, 0)

    const data = new Float32Array([
        dx, dy, dw, dh, 1, 1, 1, _globalAlpha, 2, 1
    ])
    gl.bindBuffer(gl.ARRAY_BUFFER, _instBuf)
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, data)

    const stride = FLOATS_PER_INST * 4
    const aRect = gl.getAttribLocation(_imgProg, 'a_rect')
    const aColor = gl.getAttribLocation(_imgProg, 'a_color')
    gl.enableVertexAttribArray(aRect)
    gl.vertexAttribPointer(aRect, 4, gl.FLOAT, false, stride, 0)
    gl.vertexAttribDivisor(aRect, 1)
    gl.enableVertexAttribArray(aColor)
    gl.vertexAttribPointer(aColor, 4, gl.FLOAT, false, stride, 4 * 4)
    gl.vertexAttribDivisor(aColor, 1)

    gl.uniformMatrix3fv(_uImgMatrix, false, currentMatrix())
    gl.uniform2f(_uImgRes, _canvas.width, _canvas.height)
    gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, 1)
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

function updateMouse(e) {
    if (!_canvas) return
    const rect = _canvas.getBoundingClientRect()
    _mouse.x = (e.clientX - rect.left) * (_canvas.width / rect.width)
    _mouse.y = (e.clientY - rect.top) * (_canvas.height / rect.height)
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
        gl.init(selector, width, height)
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
    },

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
        _matrixStack = [identity()]   // 每帧重置变换栈
        exports.GameBridge.Tick(dt)
        flushShapes()                 // 帧末刷新剩余实例
    } catch (err) {
        console.error('[Engine] Tick 异常：', err)
    }
    requestAnimationFrame(frame)
}

// ------------------------- 启动 -------------------------
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.create()

setModuleImports('main.js', {
    gl,
    input,
    audio,
    storage,
    engine,
})

const config = getConfig()
const exports = await getAssemblyExports(config.mainAssemblyName)

await runMain()
