// mirengine/render/webgl/glcore.ts
// WebGL 2D 渲染核心：着色器、纹理 / 离屏封装、混合状态、三角形绘制。
// 全链路使用预乘 alpha（premultiplied）：精灵上传时 UNPACK_PREMULTIPLY_ALPHA、离屏 FBO 经预乘混合，
// 采样时不再二次预乘。混合模式：
//   source-over -> (ONE, ONE_MINUS_SRC_ALPHA)
//   lighter      -> (ONE, ONE)                                  （加色）
//   screen       -> EXT_blend_equation_advanced SCREEN_KHR
//   multiply     -> EXT_blend_equation_advanced MULTIPLY_KHR
// 坐标：画布像素 (x 右、y 下) 转裁剪空间；主画布 y 翻转，离屏 FBO 不翻转（与精灵数据行序一致）。
import { dom, gfx } from '../../shared.js';
let program = null, quadBuf = null, vao = null, loc = {};
let advancedBlend = null;
let initialized = false;
const VS = `#version 300 es
in vec2 aPos;
in vec2 aUv;
out vec2 vUv;
void main() {
  vUv = aUv;
  gl_Position = vec4(aPos, 0.0, 1.0);
}`;
const FS = `#version 300 es
precision mediump float;
in vec2 vUv;
out vec4 fragColor;
uniform sampler2D uTex;
uniform float uAlpha;
uniform vec4 uColor;   // straight rgba 0..1
uniform bool uUseTex;
uniform bool uPremult; // 源纹理已是预乘 alpha（精灵上传 / 离屏 FBO 均为 true）
void main() {
  if (uUseTex) {
    vec4 t = texture(uTex, vUv);
    float a = t.a * uAlpha;
    vec3 rgb = t.rgb * (uPremult ? 1.0 : t.a) * uAlpha;
    fragColor = vec4(rgb, a);
  } else {
    float a = uColor.a * uAlpha;
    fragColor = vec4(uColor.rgb * uColor.a * uAlpha, a);
  }
}`;
/** 惰性完成一次性初始化（编译着色器、建 VAO、取uniform位置、开Blend）。 */
export function ensureGL() {
    if (initialized)
        return !!program;
    const gl = dom.gl;
    if (!gl) {
        console.error('[gl] 无 WebGL 上下文');
        return false;
    }
    // WebGL 的 create* 在上下文丢失时会返回 null，这里显式拦住，避免后续调用拿到 null 参数。
    const vs = gl.createShader(gl.VERTEX_SHADER);
    const fs = gl.createShader(gl.FRAGMENT_SHADER);
    if (!vs || !fs) {
        console.error('[gl] 创建着色器失败');
        return false;
    }
    gl.shaderSource(vs, VS);
    gl.compileShader(vs);
    if (!gl.getShaderParameter(vs, gl.COMPILE_STATUS))
        console.error('[gl] VS', gl.getShaderInfoLog(vs));
    gl.shaderSource(fs, FS);
    gl.compileShader(fs);
    if (!gl.getShaderParameter(fs, gl.COMPILE_STATUS))
        console.error('[gl] FS', gl.getShaderInfoLog(fs));
    const prog = gl.createProgram();
    if (!prog) {
        console.error('[gl] 创建程序失败');
        return false;
    }
    program = prog;
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS))
        console.error('[gl] link', gl.getProgramInfoLog(program));
    loc.aPos = gl.getAttribLocation(program, 'aPos');
    loc.aUv = gl.getAttribLocation(program, 'aUv');
    loc.uTex = gl.getUniformLocation(program, 'uTex');
    loc.uAlpha = gl.getUniformLocation(program, 'uAlpha');
    loc.uColor = gl.getUniformLocation(program, 'uColor');
    loc.uUseTex = gl.getUniformLocation(program, 'uUseTex');
    loc.uPremult = gl.getUniformLocation(program, 'uPremult');
    const buf = gl.createBuffer();
    // WebGL2：用 VAO 固化四边形顶点布局（位置 + UV），绘制时只需绑定一次。
    const vaoObj = gl.createVertexArray();
    if (!buf || !vaoObj) {
        console.error('[gl] 创建顶点缓冲 / VAO 失败');
        return false;
    }
    quadBuf = buf;
    vao = vaoObj;
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(6 * 4), gl.DYNAMIC_DRAW);
    setAttribs();
    gl.bindVertexArray(null);
    advancedBlend = gl.getExtension('EXT_blend_equation_advanced');
    if (!advancedBlend)
        console.warn('[gl] 缺少 EXT_blend_equation_advanced：screen/multiply 回退为 source-over');
    gl.enable(gl.BLEND);
    gl.disable(gl.DEPTH_TEST);
    initialized = true;
    return true;
}
// 把纹理句柄（精灵或离屏）解析为 { tex, w, h }；离屏已预乘 alpha。
/**
 * 把手柄号解析成可绘制的源对象。
 * @param tex textures / offscreens 里的句柄 id
 * @returns { tex, w, h }；句柄不存在返回 null
 */
export function resolveSource(tex) {
    const t = gfx.textures.get(tex);
    if (t)
        return { tex: t.tex, w: t.w, h: t.h };
    const o = gfx.offscreens.get(tex);
    if (o && o.tex)
        return { tex: o.tex, w: o.w, h: o.h };
    return null;
}
/** 绑定当前绘制目标（离屏 FBO 或主画布）并设置 viewport。 */
function bindTarget() {
    const gl = dom.gl;
    gl.bindVertexArray(vao);
    if (gfx.cur && gfx.cur.fbo) {
        gl.bindFramebuffer(gl.FRAMEBUFFER, gfx.cur.fbo);
        gl.viewport(0, 0, gfx.cur.w, gfx.cur.h);
    }
    else {
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, dom.canvas.width, dom.canvas.height);
    }
}
/** 当前目标尺寸：主画布取 canvas 尺寸，离屏取自身尺寸。 */
function targetWH() {
    const isMain = !(gfx.cur && gfx.cur.fbo);
    return { isMain, W: isMain ? dom.canvas.width : gfx.cur.w, H: isMain ? dom.canvas.height : gfx.cur.h };
}
/**
 * 画布像素 x -> 裁剪空间 x。
 * @param px 画布像素 x
 * @param W 目标宽度
 */
const clipX = (px, W) => px / W * 2 - 1;
// 主画布 y 向下翻转；离屏 FBO 不翻转（与精灵数据行序一致，确保合成后正立）。
/**
 * 画布像素 y -> 裁剪空间 y。
 * @param py 画布像素 y
 * @param H 目标高度
 * @param isMain 是否主画布（决定要不要翻转）
 */
const clipY = (py, H, isMain) => isMain ? (1 - py / H * 2) : (py / H * 2 - 1);
/** 按 gfx.blendOp / gfx.blendRate 应用对应的 GL 混合方程。 */
export function applyBlend() {
    const gl = dom.gl;
    gl.enable(gl.BLEND);
    const basic = () => { gl.blendEquation(gl.FUNC_ADD); gl.blendFuncSeparate(gl.ONE, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA); };
    const additive = () => { gl.blendEquation(gl.FUNC_ADD); gl.blendFunc(gl.ONE, gl.ONE); };
    switch (gfx.blendOp) {
        case 'source-over':
            basic();
            break;
        case 'lighter':
            additive();
            break;
        // INVLIGHT（Crystal）：Src = BlendFactor(rate)，Dst = 1 - SrcColor
        case 'invlight': {
            const r = gfx.blendRate;
            gl.blendEquation(gl.FUNC_ADD);
            gl.blendColor(r, r, r, r);
            gl.blendFunc(gl.CONSTANT_COLOR, gl.ONE_MINUS_SRC_COLOR);
            break;
        }
        // KHR 高级混合方程要求 mix 因子为 (ONE, ZERO)。若不显式设置，驱动可能沿用上一次的
        // blendFunc（甚至判定状态非法而丢掉 blendEquation），结果就是特效变成"不透明黑底"。
        case 'screen':
            if (advancedBlend) {
                gl.blendFunc(gl.ONE, gl.ZERO);
                gl.blendEquation(advancedBlend.SCREEN_KHR);
            }
            else
                basic();
            break;
        case 'multiply':
            if (advancedBlend) {
                gl.blendFunc(gl.ONE, gl.ZERO);
                gl.blendEquation(advancedBlend.MULTIPLY_KHR);
            }
            // 没有扩展时用核心 WebGL 也能表达乘法：dst * src
            else {
                gl.blendEquation(gl.FUNC_ADD);
                gl.blendFunc(gl.DST_COLOR, gl.ZERO);
            }
            break;
        default:
            basic();
            break;
    }
}
/** 重新绑定四边形顶点着色器的 aPos / aUv 属性布局。 */
function setAttribs() {
    const gl = dom.gl;
    gl.enableVertexAttribArray(loc.aPos);
    gl.vertexAttribPointer(loc.aPos, 2, gl.FLOAT, false, 16, 0);
    gl.enableVertexAttribArray(loc.aUv);
    gl.vertexAttribPointer(loc.aUv, 2, gl.FLOAT, false, 16, 8);
}
// 绘制带源矩形裁剪的纹理四边形。
/**
 * 绘制带源矩形裁剪的纹理四边形。
 * @param src 绘制源（精灵纹理或离屏目标）
 * @param sx 源矩形 x
 * @param sy 源矩形 y
 * @param sw 源矩形宽
 * @param sh 源矩形高
 * @param dx 目标位置 x（画布/目标像素）
 * @param dy 目标位置 y
 * @param dw 目标宽
 * @param dh 目标高
 * @param alpha 整体透明度 0..1
 * @param premult 源是否已预乘 alpha
 */
export function drawTexQuad(src, sx, sy, sw, sh, dx, dy, dw, dh, alpha, premult) {
    const gl = dom.gl;
    if (!gl || !src)
        return;
    ensureGL();
    bindTarget();
    applyBlend();
    gl.useProgram(program);
    gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
    const { isMain, W, H } = targetWH();
    const u0 = sx / src.w, v0 = sy / src.h;
    const u1 = (sx + sw) / src.w, v1 = (sy + sh) / src.h;
    const x0 = clipX(dx, W), x1 = clipX(dx + dw, W);
    const y0 = clipY(dy, H, isMain), y1 = clipY(dy + dh, H, isMain);
    const verts = new Float32Array([
        x0, y0, u0, v0,
        x1, y0, u1, v0,
        x0, y1, u0, v1,
        x0, y1, u0, v1,
        x1, y0, u1, v0,
        x1, y1, u1, v1,
    ]);
    gl.bufferData(gl.ARRAY_BUFFER, verts, gl.DYNAMIC_DRAW);
    setAttribs();
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, src.tex);
    gl.uniform1i(loc.uTex, 0);
    gl.uniform1f(loc.uAlpha, alpha);
    gl.uniform1i(loc.uUseTex, 1);
    gl.uniform1i(loc.uPremult, premult ? 1 : 0);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
}
// 矩阵变换绘制：源矩形映射为 (0,0,sw,sh)，经 2D 仿射矩阵变换后再绘制。
/**
 * 矩阵变换绘制：把源矩形当作 (0,0,sw,sh) 基础几何，经 2D 仿射矩阵变换后再绘制。
 * @param src 绘制源（精灵纹理或离屏目标）
 * @param sx 源矩形 x
 * @param sy 源矩形 y
 * @param sw 源矩形宽
 * @param sh 源矩形高
 * @param m11 仿射矩阵 [0][0]
 * @param m12 仿射矩阵 [1][0]
 * @param m21 仿射矩阵 [0][1]
 * @param m22 仿射矩阵 [1][1]
 * @param m31 平移 x
 * @param m32 平移 y
 * @param alpha 整体透明度 0..1
 * @param premult 源是否已预乘 alpha
 */
export function drawTexQuadTransform(src, sx, sy, sw, sh, m11, m12, m21, m22, m31, m32, alpha, premult) {
    const gl = dom.gl;
    if (!gl || !src)
        return;
    ensureGL();
    bindTarget();
    applyBlend();
    gl.useProgram(program);
    gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
    const { isMain, W, H } = targetWH();
    const u0 = sx / src.w, v0 = sy / src.h, u1 = (sx + sw) / src.w, v1 = (sy + sh) / src.h;
    const tf = (x, y) => [m11 * x + m21 * y + m31, m12 * x + m22 * y + m32];
    const p00 = tf(0, 0), p10 = tf(sw, 0), p01 = tf(0, sh), p11 = tf(sw, sh);
    const verts = new Float32Array([
        clipX(p00[0], W), clipY(p00[1], H, isMain), u0, v0,
        clipX(p10[0], W), clipY(p10[1], H, isMain), u1, v0,
        clipX(p01[0], W), clipY(p01[1], H, isMain), u0, v1,
        clipX(p01[0], W), clipY(p01[1], H, isMain), u0, v1,
        clipX(p10[0], W), clipY(p10[1], H, isMain), u1, v0,
        clipX(p11[0], W), clipY(p11[1], H, isMain), u1, v1,
    ]);
    gl.bufferData(gl.ARRAY_BUFFER, verts, gl.DYNAMIC_DRAW);
    setAttribs();
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, src.tex);
    gl.uniform1i(loc.uTex, 0);
    gl.uniform1f(loc.uAlpha, alpha);
    gl.uniform1i(loc.uUseTex, 1);
    gl.uniform1i(loc.uPremult, premult ? 1 : 0);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
}
// 纯色填充四边形。
/**
 * 纯色填充矩形。
 * @param x 左上角 x
 * @param y 左上角 y
 * @param w 宽
 * @param h 高
 * @param argb 颜色，32 位 ARGB
 */
export function drawSolidQuad(x, y, w, h, argb) {
    const gl = dom.gl;
    if (!gl)
        return;
    ensureGL();
    bindTarget();
    applyBlend();
    gl.useProgram(program);
    gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
    const { isMain, W, H } = targetWH();
    const x0 = clipX(x, W), x1 = clipX(x + w, W);
    const y0 = clipY(y, H, isMain), y1 = clipY(y + h, H, isMain);
    const verts = new Float32Array([
        x0, y0, 0, 0,
        x1, y0, 0, 0,
        x0, y1, 0, 0,
        x0, y1, 0, 0,
        x1, y0, 0, 0,
        x1, y1, 0, 0,
    ]);
    gl.bufferData(gl.ARRAY_BUFFER, verts, gl.DYNAMIC_DRAW);
    setAttribs();
    gl.uniform1i(loc.uTex, 0);
    gl.uniform1f(loc.uAlpha, 1);
    gl.uniform1i(loc.uUseTex, 0);
    gl.uniform4f(loc.uColor, ((argb >> 16) & 255) / 255, ((argb >> 8) & 255) / 255, (argb & 255) / 255, ((argb >>> 24) & 255) / 255);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
}
// 任意四点纯色四边形（用于 drawLine 等）。
/**
 * 任意四点纯色四边形（用于 drawLine 等）。
 * @param pts 4 个顶点，每项为 [x, y]
 * @param argb 颜色，32 位 ARGB
 */
export function drawSolidQuadPts(pts, argb) {
    const gl = dom.gl;
    if (!gl || !pts || pts.length < 4)
        return;
    ensureGL();
    bindTarget();
    applyBlend();
    gl.useProgram(program);
    gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
    const { isMain, W, H } = targetWH();
    const v = [];
    for (let i = 0; i < 4; i++) {
        v.push(clipX(pts[i][0], W), clipY(pts[i][1], H, isMain), 0, 0);
    }
    const verts = new Float32Array([
        v[0], v[1], v[2], v[3],
        v[4], v[5], v[6], v[7],
        v[8], v[9], v[10], v[11],
        v[8], v[9], v[10], v[11],
        v[4], v[5], v[6], v[7],
        v[12], v[13], v[14], v[15],
    ]);
    gl.bufferData(gl.ARRAY_BUFFER, verts, gl.DYNAMIC_DRAW);
    setAttribs();
    gl.uniform1i(loc.uTex, 0);
    gl.uniform1f(loc.uAlpha, 1);
    gl.uniform1i(loc.uUseTex, 0);
    gl.uniform4f(loc.uColor, ((argb >> 16) & 255) / 255, ((argb >> 8) & 255) / 255, (argb & 255) / 255, ((argb >>> 24) & 255) / 255);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
}
