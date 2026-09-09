// WebGL 2.0 绑定层。C# 侧通过 [JSImport("函数名", "gl")] 调用这里的导出函数。
//
// 重要：.NET 传入的 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），
// 必须经 toBytes / toFloats 转换后才能交给 WebGL。
// 另外 C# 侧的 [JSImport] 函数名必须与这里的导出名完全一致，且不能带点号。

let canvas: HTMLCanvasElement | null = null;
let gl: WebGL2RenderingContext | null = null;

function gpu(): WebGL2RenderingContext {
    if (!gl) throw new Error('[gl] WebGL2 上下文尚未初始化');
    return gl;
}

export function initContext(selector: string): boolean {
    const element = document.querySelector(selector);
    if (!(element instanceof HTMLCanvasElement)) {
        console.error('[gl] 找不到画布元素:', selector);
        return false;
    }

    canvas = element;
    gl = canvas.getContext('webgl2', {
        alpha: false,
        antialias: false,
        depth: true,
        stencil: false,
        premultipliedAlpha: false,
        preserveDrawingBuffer: false,
        powerPreference: 'high-performance',
    });

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
export function uniformMatrix4fv(location: WebGLUniformLocation | null, transpose: number, value: MemoryView | Float32Array): void {
    // C# 侧以 16 个 float 的小端字节流传入，这里拷一份对齐的缓冲再还原
    const bytes = toBytes(value as MemoryView);
    if (!bytes) return;
    const aligned = new Uint8Array(bytes.length);
    aligned.set(bytes);
    gpu().uniformMatrix4fv(location, transpose !== 0, new Float32Array(aligned.buffer));
}

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
