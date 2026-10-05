// 【依赖 C#】由 KFramework.MonoGame.JSBind_WEBGL20 经 [JSImport(module: "render_webgl20")] 调用；编译产物 render_webgl20.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
// WebGL 2.0 绑定层。C# 侧通过 [JSImport("函数名", "gl")] 调用这里的导出函数。
//
// 重要：.NET 传入的 Span<T> 在 JS 侧是 MemoryView_Span（不是 TypedArray），
// 必须经 copyIntoCache（复用缓冲）转换后才能交给 WebGL。
// 另外 C# 侧的 [JSImport] 函数名必须与这里的导出名完全一致，且不能带点号。
import { getCanvas } from './html_canvas.js';
import { ByteCache } from './custom_data_byte_cache.js';
// ============ 模块级字段（本模块持有的全部可变状态，集中放在文件开头便于一眼看全）============
let canvas = null;
let gl = null;
// 画布元素本身由 html_canvas 统一管理：页面里有就用它，没有则自动创建一块全屏默认画布。
// 照 MonoGame（Platform/GraphicsDeviceManager.SDL.cs:52-56）：MSAA 必须在**创建上下文之前**设好属性，
// 建完就改不了。浏览器同理 —— antialias 是 getContext 的参数，所以入口是 Game / GraphicsDevice 的构造参数。
const contextAttributes = {
    alpha: false,
    antialias: false,
    depth: true,
    // 模板缓冲必须在建上下文时申请，之后改不了（与 antialias 同理）。
    // 默认帧缓冲没有模板缓冲时，STENCIL_TEST 开了也无处可写，StencilState 形同虚设。
    stencil: true,
    premultipliedAlpha: false,
    preserveDrawingBuffer: false,
    powerPreference: 'high-performance',
};
// 复用的字节缓冲：统一用 ByteCache（见 custom_data_byte_cache.ts）。
// MemoryView 不暴露 .buffer，只能 copyTo 出副本 —— 拷贝免不掉，但【分配】可以免。
// 刻意【按用途分开】而不是共用一个实例：三者大小差异极大（矩阵固定 64B / 顶点每帧几十 KB /
// 纹理可达数 MB），共用会让一次大纹理上传把顶点那块撑到 MB 级并一直占着，反之亦然；
// 分开才能各自稳定在自己该有的量级。
// 容量上限默认 65535（ushort 最大值），小用途够用；但顶点 / 纹理远超它，必须显式放宽：
//   顶点 —— 单批上限 = MaxBatchSize(4096) × 4 顶点 × 20 字节 ≈ 320KB，放宽到 1MB；
//   纹理 —— 1024² RGBA 就是 4MB，放宽到 32MB（覆盖到 2048²）。
const _cacheMatrix = new ByteCache(64); // uniformMatrix4fv：16 个 float，用默认上限
const _cacheVertex = new ByteCache(64 * 1024, 1024 * 1024); // bufferData / bufferSubData
const _cacheTexture = new ByteCache(2048, 32 * 1024 * 1024); // texImage2D / texSubImage2D / compressedTexImage2D
let _cacheMatrixF32 = null; // 矩阵还原用的 Float32Array 视图（建在 _cacheMatrix 的缓冲上）
let uniformLogged = false; // 矩阵上传只在首次打一条日志
// ============ 模块级字段结束 ============
function gpu() {
    if (!gl)
        throw new Error('[gl] WebGL2 上下文尚未初始化');
    return gl;
}
/** 设置是否启用 MSAA（必须在 initContext 之前调用，上下文建好后改无效）。 */
export function setAntialias(enabled) {
    contextAttributes.antialias = !!enabled;
}
/** 当前是否启用 MSAA。 */
export function getAntialias() {
    return !!contextAttributes.antialias;
}
export function initContext() {
    const element = getCanvas();
    if (!element) {
        console.error('[gl] 无法创建或找到画布元素');
        return false;
    }
    canvas = element;
    gl = canvas.getContext('webgl2', contextAttributes);
    if (!gl) {
        console.error('[gl] 当前浏览器不支持 WebGL 2.0');
        return false;
    }
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    return true;
}
export function getCanvasElement() {
    return canvas;
}
export function isContextLost() {
    return !gl || gl.isContextLost();
}
export function getParameterInt(pname) {
    return gpu().getParameter(pname) | 0;
}
export function getParameterString(pname) {
    return String(gpu().getParameter(pname));
}
export function getError() {
    return gpu().getError() | 0;
}
/** 读取一个像素（x/y 为 WebGL 坐标，原点在左下角），用于自检"到底画出来没有"。 */
export function readPixel(x, y, out) {
    const pixels = new Uint8Array(4);
    gpu().readPixels(x, y, 1, 1, gpu().RGBA, gpu().UNSIGNED_BYTE, pixels);
    if (out instanceof Uint8Array) {
        out.set(pixels);
        return;
    }
    out.set(pixels, 0);
}
/** 读取矩形区域像素（x/y 为 WebGL 坐标，原点在左下角），写入 out（Uint8Array，长度需 w*h*4）。 */
export function readPixels(x, y, w, h, out) {
    const dst = new Uint8Array(w * h * 4);
    gpu().readPixels(x, y, w, h, gpu().RGBA, gpu().UNSIGNED_BYTE, dst);
    out.set(dst);
}
// ---------- 字节视图转换 ----------
// 转换逻辑统一在 ByteCache（custom_data_byte_cache.ts）里，本节只按用途分流到对应的实例。
/** 顶点 / 索引缓冲用。 */
function toVertexBytes(view) {
    return _cacheVertex.copyFrom(view);
}
/** 纹理上传用。 */
function toTextureBytes(view) {
    return _cacheTexture.copyFrom(view);
}
// ---------- 着色器 ----------
export function createShader(type) { return gpu().createShader(type); }
export function shaderSource(shader, source) { gpu().shaderSource(shader, source); }
export function compileShader(shader) { gpu().compileShader(shader); }
export function getShaderParameter(shader, pname) {
    return gpu().getShaderParameter(shader, pname) ? 1 : 0;
}
export function getShaderInfoLog(shader) { return gpu().getShaderInfoLog(shader) ?? ''; }
export function deleteShader(shader) { gpu().deleteShader(shader); }
export function createProgram() { return gpu().createProgram(); }
export function attachShader(program, shader) { gpu().attachShader(program, shader); }
export function linkProgram(program) { gpu().linkProgram(program); }
export function getProgramParameter(program, pname) {
    return gpu().getProgramParameter(program, pname) ? 1 : 0;
}
export function getProgramInfoLog(program) { return gpu().getProgramInfoLog(program) ?? ''; }
export function useProgram(program) { gpu().useProgram(program); }
export function deleteProgram(program) { gpu().deleteProgram(program); }
export function getUniformLocation(program, name) {
    return gpu().getUniformLocation(program, name);
}
export function getAttribLocation(program, name) {
    return gpu().getAttribLocation(program, name);
}
export function uniform1i(location, v) { gpu().uniform1i(location, v); }
export function uniform1f(location, v) { gpu().uniform1f(location, v); }
export function uniform4f(location, x, y, z, w) {
    gpu().uniform4f(location, x, y, z, w);
}
// 矩阵固定为 16 个 float（64 字节）：直接把 MemoryView 拷进复用的缓冲再还原成 Float32Array，
// 省掉一次中间分配 + 一次 set 拷贝（缓冲见文件开头）。
export function uniformMatrix4fv(location, transpose, value) {
    // C# 侧以 16 个 float 的小端字节流传入，这里还原成 Float32Array。
    let matrix;
    if (value instanceof Float32Array) {
        matrix = value;
    }
    else {
        const n = value.byteLength;
        const buf = _cacheMatrix.copyFrom(value);
        if (buf === null)
            return;
        if (n === 64) {
            // 唯一实际会用到的尺寸：复用 Float32Array 视图，不必每次 new
            if (_cacheMatrixF32 === null || _cacheMatrixF32.buffer !== buf.buffer)
                _cacheMatrixF32 = new Float32Array(buf.buffer, buf.byteOffset, 16);
            matrix = _cacheMatrixF32;
        }
        else {
            // 非常规尺寸（不会出现）：老老实实拷一份对齐的
            const aligned = new Uint8Array(buf.subarray(0, n));
            matrix = new Float32Array(aligned.buffer);
        }
    }
    if (!uniformLogged) {
        uniformLogged = true;
        console.log('[gl] 上传矩阵:', Array.from(matrix).map((n) => n.toFixed(4)).join(','));
    }
    gpu().uniformMatrix4fv(location, transpose !== 0, matrix);
}
// ---------- 缓冲 ----------
export function createBuffer() { return gpu().createBuffer(); }
export function bindBuffer(target, buffer) { gpu().bindBuffer(target, buffer); }
export function bufferDataSize(target, size, usage) { gpu().bufferData(target, size, usage); }
export function bufferData(target, data, usage) {
    gpu().bufferData(target, toVertexBytes(data), usage);
}
export function bufferSubData(target, offset, data) {
    gpu().bufferSubData(target, offset, toVertexBytes(data) ?? new Uint8Array(0));
}
export function deleteBuffer(buffer) { gpu().deleteBuffer(buffer); }
export function createVertexArray() { return gpu().createVertexArray(); }
export function bindVertexArray(vao) { gpu().bindVertexArray(vao); }
export function enableVertexAttribArray(index) { gpu().enableVertexAttribArray(index); }
export function vertexAttribPointer(index, size, type, normalized, stride, offset) {
    gpu().vertexAttribPointer(index, size, type, !!normalized, stride, offset);
}
// ---------- 纹理 ----------
export function createTexture() { return gpu().createTexture(); }
export function bindTexture(target, texture) { gpu().bindTexture(target, texture); }
export function texImage2D(target, level, internalFormat, width, height, border, format, type, data) {
    gpu().texImage2D(target, level, internalFormat, width, height, border, format, type, toTextureBytes(data) ?? new Uint8Array(0));
}
export function texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, data) {
    gpu().texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, toTextureBytes(data));
}
// ---------- 压缩纹理（KTX2 / Basis Universal） ----------
/** 查询 WebGL 扩展是否可用（如 'WEBGL_compressed_texture_astc'）。返回是否存在（boolean）。 */
export function hasExtension(name) {
    return gpu().getExtension(name) != null;
}
/** 上传一块 GPU 压缩纹理数据（WebGL2 compressedTexImage2D）。 */
export function compressedTexImage2D(target, level, internalFormat, width, height, border, data) {
    gpu().compressedTexImage2D(target, level, internalFormat, width, height, border, toTextureBytes(data) ?? new Uint8Array(0));
}
export function texParameteri(target, pname, param) { gpu().texParameteri(target, pname, param); }
export function activeTexture(unit) { gpu().activeTexture(unit); }
export function deleteTexture(texture) { gpu().deleteTexture(texture); }
export function pixelStorei(pname, param) { gpu().pixelStorei(pname, param); }
export function generateMipmap(target) { gpu().generateMipmap(target); }
// ---------- 状态与绘制 ----------
export function enable(cap) { gpu().enable(cap); }
export function disable(cap) { gpu().disable(cap); }
export function blendFuncSeparate(srcRGB, dstRGB, srcA, dstA) {
    gpu().blendFuncSeparate(srcRGB, dstRGB, srcA, dstA);
}
export function blendEquation(mode) { gpu().blendEquation(mode); }
// RGB 与 Alpha 各自独立的混合方程（BlendOp 按下发需要它：颜色与 Alpha 的运算可能不同）。
export function blendEquationSeparate(modeRGB, modeAlpha) { gpu().blendEquationSeparate(modeRGB, modeAlpha); }
export function clearColor(r, g, b, a) { gpu().clearColor(r, g, b, a); }
export function clear(mask) { gpu().clear(mask); }
export function viewport(x, y, width, height) { gpu().viewport(x, y, width, height); }
export function scissor(x, y, width, height) { gpu().scissor(x, y, width, height); }
export function drawElements(mode, count, type, offset) {
    gpu().drawElements(mode, count, type, offset);
}
export function drawArrays(mode, first, count) { gpu().drawArrays(mode, first, count); }
/** 分配一张未初始化的 2D 纹理存储（渲染目标用：内容由 GPU 绘制，不传像素数据）。 */
export function texImage2DStorage(target, level, internalFormat, width, height, format, type) {
    gpu().texImage2D(target, level, internalFormat, width, height, 0, format, type, null);
}
// ---------- 帧缓冲（离屏渲染 / RenderTarget，照 MonoGame 的 FramebufferHelper） ----------
export function createFramebuffer() { return gpu().createFramebuffer(); }
export function bindFramebuffer(target, framebuffer) {
    gpu().bindFramebuffer(target, framebuffer);
}
export function deleteFramebuffer(framebuffer) { gpu().deleteFramebuffer(framebuffer); }
/** 把一张纹理挂到 FBO 的颜色附着点（attachment = COLOR_ATTACHMENT0 + i）。 */
export function framebufferTexture2D(target, attachment, texTarget, texture, level) {
    gpu().framebufferTexture2D(target, attachment, texTarget, texture, level);
}
/** 返回 FBO 完整性状态（FRAMEBUFFER_COMPLETE 表示可用）。 */
export function checkFramebufferStatus(target) { return gpu().checkFramebufferStatus(target); }
export function createRenderbuffer() { return gpu().createRenderbuffer(); }
export function bindRenderbuffer(target, renderbuffer) {
    gpu().bindRenderbuffer(target, renderbuffer);
}
export function renderbufferStorage(target, internalFormat, width, height) {
    gpu().renderbufferStorage(target, internalFormat, width, height);
}
/** 把 renderbuffer（深度 / 模板）挂到 FBO 的对应附着点。 */
export function framebufferRenderbuffer(target, attachment, rbTarget, renderbuffer) {
    gpu().framebufferRenderbuffer(target, attachment, rbTarget, renderbuffer);
}
export function deleteRenderbuffer(renderbuffer) { gpu().deleteRenderbuffer(renderbuffer); }
/**
 * 分配多重采样 renderbuffer 存储（MSAA 颜色 / 深度附件）。samples 为每像素采样数。
 * 这是「离屏 FBO 级别」的多重采样：锯齿在渲染源头被磨平，
 * 与画布 getContext 的 antialias 无关（画布 antialias 只作用直接画到画布的几何体轮廓，够不到离屏纹理内容）。
 */
export function renderbufferStorageMultisample(target, samples, internalFormat, width, height) {
    gpu().renderbufferStorageMultisample(target, samples, internalFormat, width, height);
}
/**
 * 把多重采样帧缓冲解析（resolve）到单采样帧缓冲。
 * 用途：把 MSAA 离屏目标 resolve 进普通纹理（RenderTarget2D.GLTexture），使其可被采样 / 画到屏幕。
 * 注意这只是「把多重采样结果拷成单采样纹理」——画布最终的 antialias 设置不会重算这一步的结果。
 */
export function blitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter) {
    gpu().blitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter);
}
// ---------- 剔除 / 深度（照 MonoGame 的 RasterizerState / DepthStencilState 下发） ----------
export function cullFace(mode) { gpu().cullFace(mode); }
export function frontFace(mode) { gpu().frontFace(mode); }
export function depthMask(flag) { gpu().depthMask(flag); }
export function depthFunc(func) { gpu().depthFunc(func); }
// ---------- 模板（StencilState 下发） ----------
// 比较函数复用 depthFunc 那批常量：GL 的 stencilFunc 与 depthFunc 用的是同一组（NEVER/LESS/…/ALWAYS）。
export function stencilMask(mask) { gpu().stencilMask(mask); }
export function stencilFunc(func, reference, mask) { gpu().stencilFunc(func, reference, mask); }
export function stencilOp(fail, zfail, zpass) { gpu().stencilOp(fail, zfail, zpass); }
export function stencilFuncSeparate(face, func, reference, mask) { gpu().stencilFuncSeparate(face, func, reference, mask); }
export function stencilOpSeparate(face, fail, zfail, zpass) { gpu().stencilOpSeparate(face, fail, zfail, zpass); }
