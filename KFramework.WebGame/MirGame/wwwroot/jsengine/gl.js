// WebGL 2.0 绑定层。C# 侧通过 [JSImport("函数名", "gl")] 调用这里的导出函数。
//
// 重要：.NET 传入的 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），
// 必须经 toBytes / toFloats 转换后才能交给 WebGL。
// 另外 C# 侧的 [JSImport] 函数名必须与这里的导出名完全一致，且不能带点号。
let canvas = null;
let gl = null;
function gpu() {
    if (!gl)
        throw new Error('[gl] WebGL2 上下文尚未初始化');
    return gl;
}
export function initContext(selector) {
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
// ---------- 字节视图转换 ----------
function toBytes(view) {
    if (view == null)
        return null;
    if (view instanceof Uint8Array)
        return view;
    const memory = view;
    if (typeof memory.copyTo === 'function') {
        const buffer = new Uint8Array(memory.byteLength);
        memory.copyTo(buffer);
        return buffer;
    }
    if (typeof memory.slice === 'function') {
        const sliced = memory.slice();
        return sliced instanceof Uint8Array ? sliced : new Uint8Array(sliced);
    }
    throw new Error('[gl] 无法把参数转换为 Uint8Array');
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
export function uniformMatrix4fv(location, transpose, value) {
    // C# 侧以 16 个 float 的小端字节流传入，这里拷一份对齐的缓冲再还原
    const bytes = toBytes(value);
    if (!bytes)
        return;
    const aligned = new Uint8Array(bytes.length);
    aligned.set(bytes);
    gpu().uniformMatrix4fv(location, transpose !== 0, new Float32Array(aligned.buffer));
}
// ---------- 缓冲 ----------
export function createBuffer() { return gpu().createBuffer(); }
export function bindBuffer(target, buffer) { gpu().bindBuffer(target, buffer); }
export function bufferDataSize(target, size, usage) { gpu().bufferData(target, size, usage); }
export function bufferData(target, data, usage) {
    gpu().bufferData(target, toBytes(data), usage);
}
export function bufferSubData(target, offset, data) {
    gpu().bufferSubData(target, offset, toBytes(data) ?? new Uint8Array(0));
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
    gpu().texImage2D(target, level, internalFormat, width, height, border, format, type, toBytes(data) ?? new Uint8Array(0));
}
export function texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, data) {
    gpu().texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, toBytes(data));
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
export function clearColor(r, g, b, a) { gpu().clearColor(r, g, b, a); }
export function clear(mask) { gpu().clear(mask); }
export function viewport(x, y, width, height) { gpu().viewport(x, y, width, height); }
export function scissor(x, y, width, height) { gpu().scissor(x, y, width, height); }
export function drawElements(mode, count, type, offset) {
    gpu().drawElements(mode, count, type, offset);
}
export function drawArrays(mode, first, count) { gpu().drawArrays(mode, first, count); }
