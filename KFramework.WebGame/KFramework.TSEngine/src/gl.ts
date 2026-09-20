// 【依赖 C#】由 KFramework.MonoGame.JSBind_GL 经 [JSImport(module: "gl")] 调用；编译产物 gl.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
// WebGL 2.0 绑定层。C# 侧通过 [JSImport("函数名", "gl")] 调用这里的导出函数。
//
// 重要：.NET 传入的 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），
// 必须经 toBytes / toFloats 转换后才能交给 WebGL。
// 另外 C# 侧的 [JSImport] 函数名必须与这里的导出名完全一致，且不能带点号。

import { getOrCreateCanvasElement } from './html_canvas.js';

let canvas: HTMLCanvasElement | null = null;
let gl: WebGL2RenderingContext | null = null;

function gpu(): WebGL2RenderingContext {
    if (!gl) throw new Error('[gl] WebGL2 上下文尚未初始化');
    return gl;
}

// 画布元素本身由 html_canvas 统一管理：页面里有就用它，没有则自动创建一块全屏默认画布。
// 照 MonoGame（Platform/GraphicsDeviceManager.SDL.cs:52-56）：MSAA 必须在**创建上下文之前**设好属性，
// 建完就改不了。浏览器同理 —— antialias 是 getContext 的参数，所以入口是 Game / GraphicsDevice 的构造参数。
const contextAttributes: WebGLContextAttributes = {
    alpha: false,
    antialias: false,
    depth: true,
    stencil: false,
    premultipliedAlpha: false,
    preserveDrawingBuffer: false,
    powerPreference: 'high-performance',
};

/** 设置是否启用 MSAA（必须在 initContext 之前调用，上下文建好后改无效）。 */
export function setAntialias(enabled: boolean): void {
    contextAttributes.antialias = !!enabled;
}

/** 当前是否启用 MSAA。 */
export function getAntialias(): boolean {
    return !!contextAttributes.antialias;
}

export function initContext(selector: string): boolean {
    const element = getOrCreateCanvasElement(selector);
    if (!element) {
        console.error('[gl] 无法创建或找到画布元素:', selector);
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

export function getCanvasElement(): HTMLCanvasElement | null {
    return canvas;
}

export function isContextLost(): boolean {
    return !gl || gl.isContextLost();
}

export function getParameterInt(pname: number): number {
    return (gpu().getParameter(pname) as number) | 0;
}

export function getParameterString(pname: number): string {
    return String(gpu().getParameter(pname));
}

export function getError(): number {
    return gpu().getError() | 0;
}

/** 读取一个像素（x/y 为 WebGL 坐标，原点在左下角），用于自检"到底画出来没有"。 */
export function readPixel(x: number, y: number, out: MemoryView | Uint8Array): void {
    const pixels = new Uint8Array(4);
    gpu().readPixels(x, y, 1, 1, gpu().RGBA, gpu().UNSIGNED_BYTE, pixels);

    if (out instanceof Uint8Array) {
        out.set(pixels);
        return;
    }
    out.set(pixels, 0);
}

// ---------- 字节视图转换 ----------

function toBytes(view: MemoryView | Uint8Array | null): Uint8Array | null {
    if (view == null) return null;
    if (view instanceof Uint8Array) return view;

    const memory = view as MemoryView;
    if (typeof memory.copyTo === 'function') {
        const buffer = new Uint8Array(memory.byteLength);
        memory.copyTo(buffer);
        return buffer;
    }
    if (typeof memory.slice === 'function') {
        const sliced = memory.slice();
        return sliced instanceof Uint8Array ? sliced : new Uint8Array(sliced as unknown as ArrayLike<number>);
    }
    throw new Error('[gl] 无法把参数转换为 Uint8Array');
}

// ---------- 着色器 ----------

export function createShader(type: number): WebGLShader | null { return gpu().createShader(type); }
export function shaderSource(shader: WebGLShader, source: string): void { gpu().shaderSource(shader, source); }
export function compileShader(shader: WebGLShader): void { gpu().compileShader(shader); }
export function getShaderParameter(shader: WebGLShader, pname: number): number {
    return gpu().getShaderParameter(shader, pname) ? 1 : 0;
}
export function getShaderInfoLog(shader: WebGLShader): string { return gpu().getShaderInfoLog(shader) ?? ''; }
export function deleteShader(shader: WebGLShader): void { gpu().deleteShader(shader); }

export function createProgram(): WebGLProgram | null { return gpu().createProgram(); }
export function attachShader(program: WebGLProgram, shader: WebGLShader): void { gpu().attachShader(program, shader); }
export function linkProgram(program: WebGLProgram): void { gpu().linkProgram(program); }
export function getProgramParameter(program: WebGLProgram, pname: number): number {
    return gpu().getProgramParameter(program, pname) ? 1 : 0;
}
export function getProgramInfoLog(program: WebGLProgram): string { return gpu().getProgramInfoLog(program) ?? ''; }
export function useProgram(program: WebGLProgram | null): void { gpu().useProgram(program); }
export function deleteProgram(program: WebGLProgram | null): void { gpu().deleteProgram(program); }

export function getUniformLocation(program: WebGLProgram, name: string): WebGLUniformLocation | null {
    return gpu().getUniformLocation(program, name);
}
export function getAttribLocation(program: WebGLProgram, name: string): number {
    return gpu().getAttribLocation(program, name);
}

export function uniform1i(location: WebGLUniformLocation | null, v: number): void { gpu().uniform1i(location, v); }
export function uniform1f(location: WebGLUniformLocation | null, v: number): void { gpu().uniform1f(location, v); }
export function uniform4f(location: WebGLUniformLocation | null, x: number, y: number, z: number, w: number): void {
    gpu().uniform4f(location, x, y, z, w);
}
// 矩阵固定为 16 个 float（64 字节），复用一份对齐缓冲，避免每帧 uniformMatrix4fv 分配。
let _matrixBytes: Uint8Array | null = null;
let _matrixF32: Float32Array | null = null;

export function uniformMatrix4fv(location: WebGLUniformLocation | null, transpose: number, value: MemoryView | Float32Array): void {
    // C# 侧以 16 个 float 的小端字节流传入，这里拷一份对齐的缓冲再还原
    const bytes = toBytes(value as MemoryView);
    if (!bytes) return;

    let matrix: Float32Array;
    if (bytes.length === 64 && _matrixBytes) {
        _matrixBytes.set(bytes);
        matrix = _matrixF32!;
    } else {
        const aligned = new Uint8Array(bytes.length);
        aligned.set(bytes);
        matrix = new Float32Array(aligned.buffer);
        if (bytes.length === 64) { _matrixBytes = aligned; _matrixF32 = matrix; }
    }

    if (!uniformLogged) {
        uniformLogged = true;
        console.log('[gl] 上传矩阵:', Array.from(matrix).map((n) => n.toFixed(4)).join(','));
    }

    gpu().uniformMatrix4fv(location, transpose !== 0, matrix);
}

let uniformLogged = false;

// ---------- 缓冲 ----------

export function createBuffer(): WebGLBuffer | null { return gpu().createBuffer(); }
export function bindBuffer(target: number, buffer: WebGLBuffer | null): void { gpu().bindBuffer(target, buffer); }
export function bufferDataSize(target: number, size: number, usage: number): void { gpu().bufferData(target, size, usage); }
export function bufferData(target: number, data: MemoryView | Uint8Array, usage: number): void {
    gpu().bufferData(target, toBytes(data), usage);
}
export function bufferSubData(target: number, offset: number, data: MemoryView | Uint8Array): void {
    gpu().bufferSubData(target, offset, toBytes(data) ?? new Uint8Array(0));
}
export function deleteBuffer(buffer: WebGLBuffer | null): void { gpu().deleteBuffer(buffer); }

export function createVertexArray(): WebGLVertexArrayObject | null { return gpu().createVertexArray(); }
export function bindVertexArray(vao: WebGLVertexArrayObject | null): void { gpu().bindVertexArray(vao); }
export function enableVertexAttribArray(index: number): void { gpu().enableVertexAttribArray(index); }
export function vertexAttribPointer(index: number, size: number, type: number, normalized: number | boolean, stride: number, offset: number): void {
    gpu().vertexAttribPointer(index, size, type, !!normalized, stride, offset);
}

// ---------- 纹理 ----------

export function createTexture(): WebGLTexture | null { return gpu().createTexture(); }
export function bindTexture(target: number, texture: WebGLTexture | null): void { gpu().bindTexture(target, texture); }
export function texImage2D(
    target: number, level: number, internalFormat: number,
    width: number, height: number, border: number,
    format: number, type: number, data: MemoryView | Uint8Array | null,
): void {
    gpu().texImage2D(target, level, internalFormat, width, height, border, format, type, toBytes(data) ?? new Uint8Array(0));
}
export function texSubImage2D(
    target: number, level: number, xoffset: number, yoffset: number,
    width: number, height: number, format: number, type: number,
    data: MemoryView | Uint8Array,
): void {
    gpu().texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, toBytes(data));
}

// ---------- 压缩纹理（KTX2 / Basis Universal） ----------

/** 查询 WebGL 扩展是否可用（如 'WEBGL_compressed_texture_astc'）。返回扩展对象或 null。 */
export function hasExtension(name: string): unknown {
    return gpu().getExtension(name);
}

/** 上传一块 GPU 压缩纹理数据（WebGL2 compressedTexImage2D）。 */
export function compressedTexImage2D(
    target: number, level: number, internalFormat: number,
    width: number, height: number, border: number,
    data: MemoryView | Uint8Array | null,
): void {
    gpu().compressedTexImage2D(target, level, internalFormat, width, height, border, toBytes(data) ?? new Uint8Array(0));
}
export function texParameteri(target: number, pname: number, param: number): void { gpu().texParameteri(target, pname, param); }
export function activeTexture(unit: number): void { gpu().activeTexture(unit); }
export function deleteTexture(texture: WebGLTexture | null): void { gpu().deleteTexture(texture); }
export function pixelStorei(pname: number, param: number): void { gpu().pixelStorei(pname, param); }
export function generateMipmap(target: number): void { gpu().generateMipmap(target); }

// ---------- 状态与绘制 ----------

export function enable(cap: number): void { gpu().enable(cap); }
export function disable(cap: number): void { gpu().disable(cap); }
export function blendFuncSeparate(srcRGB: number, dstRGB: number, srcA: number, dstA: number): void {
    gpu().blendFuncSeparate(srcRGB, dstRGB, srcA, dstA);
}
export function blendEquation(mode: number): void { gpu().blendEquation(mode); }
export function clearColor(r: number, g: number, b: number, a: number): void { gpu().clearColor(r, g, b, a); }
export function clear(mask: number): void { gpu().clear(mask); }
export function viewport(x: number, y: number, width: number, height: number): void { gpu().viewport(x, y, width, height); }
export function scissor(x: number, y: number, width: number, height: number): void { gpu().scissor(x, y, width, height); }
export function drawElements(mode: number, count: number, type: number, offset: number): void {
    gpu().drawElements(mode, count, type, offset);
}
export function drawArrays(mode: number, first: number, count: number): void { gpu().drawArrays(mode, first, count); }

/** 分配一张未初始化的 2D 纹理存储（渲染目标用：内容由 GPU 绘制，不传像素数据）。 */
export function texImage2DStorage(
    target: number, level: number, internalFormat: number,
    width: number, height: number, format: number, type: number,
): void {
    gpu().texImage2D(target, level, internalFormat, width, height, 0, format, type, null);
}

// ---------- 帧缓冲（离屏渲染 / RenderTarget，照 MonoGame 的 FramebufferHelper） ----------

export function createFramebuffer(): WebGLFramebuffer | null { return gpu().createFramebuffer(); }
export function bindFramebuffer(target: number, framebuffer: WebGLFramebuffer | null): void {
    gpu().bindFramebuffer(target, framebuffer);
}
export function deleteFramebuffer(framebuffer: WebGLFramebuffer | null): void { gpu().deleteFramebuffer(framebuffer); }
/** 把一张纹理挂到 FBO 的颜色附着点（attachment = COLOR_ATTACHMENT0 + i）。 */
export function framebufferTexture2D(
    target: number, attachment: number, texTarget: number,
    texture: WebGLTexture | null, level: number,
): void {
    gpu().framebufferTexture2D(target, attachment, texTarget, texture, level);
}
/** 返回 FBO 完整性状态（FRAMEBUFFER_COMPLETE 表示可用）。 */
export function checkFramebufferStatus(target: number): number { return gpu().checkFramebufferStatus(target); }

export function createRenderbuffer(): WebGLRenderbuffer | null { return gpu().createRenderbuffer(); }
export function bindRenderbuffer(target: number, renderbuffer: WebGLRenderbuffer | null): void {
    gpu().bindRenderbuffer(target, renderbuffer);
}
export function renderbufferStorage(target: number, internalFormat: number, width: number, height: number): void {
    gpu().renderbufferStorage(target, internalFormat, width, height);
}
/** 把 renderbuffer（深度 / 模板）挂到 FBO 的对应附着点。 */
export function framebufferRenderbuffer(
    target: number, attachment: number, rbTarget: number, renderbuffer: WebGLRenderbuffer | null,
): void {
    gpu().framebufferRenderbuffer(target, attachment, rbTarget, renderbuffer);
}
export function deleteRenderbuffer(renderbuffer: WebGLRenderbuffer | null): void { gpu().deleteRenderbuffer(renderbuffer); }

/**
 * 分配多重采样 renderbuffer 存储（MSAA 颜色 / 深度附件）。samples 为每像素采样数。
 * 这是「离屏 FBO 级别」的多重采样：锯齿在渲染源头被磨平，
 * 与画布 getContext 的 antialias 无关（画布 antialias 只作用直接画到画布的几何体轮廓，够不到离屏纹理内容）。
 */
export function renderbufferStorageMultisample(
    target: number, samples: number, internalFormat: number, width: number, height: number,
): void {
    gpu().renderbufferStorageMultisample(target, samples, internalFormat, width, height);
}

/**
 * 把多重采样帧缓冲解析（resolve）到单采样帧缓冲。
 * 用途：把 MSAA 离屏目标 resolve 进普通纹理（RenderTarget2D.GLTexture），使其可被采样 / 画到屏幕。
 * 注意这只是「把多重采样结果拷成单采样纹理」——画布最终的 antialias 设置不会重算这一步的结果。
 */
export function blitFramebuffer(
    srcX0: number, srcY0: number, srcX1: number, srcY1: number,
    dstX0: number, dstY0: number, dstX1: number, dstY1: number,
    mask: number, filter: number,
): void {
    gpu().blitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter);
}

// ---------- 剔除 / 深度（照 MonoGame 的 RasterizerState / DepthStencilState 下发） ----------

export function cullFace(mode: number): void { gpu().cullFace(mode); }
export function frontFace(mode: number): void { gpu().frontFace(mode); }
export function depthMask(flag: boolean): void { gpu().depthMask(flag); }
export function depthFunc(func: number): void { gpu().depthFunc(func); }
