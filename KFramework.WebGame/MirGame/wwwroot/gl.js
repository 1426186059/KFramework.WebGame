// WebGL 2.0 绑定层。C# 侧通过 [JSImport("xxx", "gl")] 调用这里的导出函数。
// 说明：.NET 传入的 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），
// 因此统一用 toBytes / toFloats 转换后再交给 WebGL。

let canvas = null;
let gl = null;

export function initContext(selector) {
    canvas = document.querySelector(selector);
    if (!canvas) {
        console.error('[gl] 找不到画布元素:', selector);
        return false;
    }

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
    return gl.getParameter(pname) | 0;
}

export function getParameterString(pname) {
    return String(gl.getParameter(pname));
}

export function getError() {
    return gl.getError() | 0;
}

// ---------- 字节视图转换 ----------

function toBytes(view) {
    if (view == null) return null;
    if (view instanceof Uint8Array) return view;

    // .NET 传入的 Span<byte> 在 JS 侧是 MemoryView，只能靠 copyTo / slice 读出 Uint8Array
    if (typeof view.copyTo === 'function') {
        const buffer = new Uint8Array(view.byteLength);
        view.copyTo(buffer);
        return buffer;
    }
    if (typeof view.slice === 'function') {
        const sliced = view.slice();
        return sliced instanceof Uint8Array ? sliced : new Uint8Array(sliced);
    }
    throw new Error('[gl] 无法把参数转换为 Uint8Array');
}

function toFloats(view) {
    if (view instanceof Float32Array) return view;
    if (typeof view.copyTo === 'function') {
        const buffer = new Float32Array(view.length);
        view.copyTo(buffer);
        return buffer;
    }
    if (typeof view.slice === 'function') return view.slice();
    throw new Error('[gl] 无法把参数转换为 Float32Array');
}

// ---------- 着色器 ----------

export function createShader(type) { return gl.createShader(type); }
export function shaderSource(shader, source) { gl.shaderSource(shader, source); }
export function compileShader(shader) { gl.compileShader(shader); }
export function getShaderParameter(shader, pname) { return gl.getShaderParameter(shader, pname) ? 1 : 0; }
export function getShaderInfoLog(shader) { return gl.getShaderInfoLog(shader) ?? ''; }
export function deleteShader(shader) { gl.deleteShader(shader); }

export function createProgram() { return gl.createProgram(); }
export function attachShader(program, shader) { gl.attachShader(program, shader); }
export function linkProgram(program) { gl.linkProgram(program); }
export function getProgramParameter(program, pname) { return gl.getProgramParameter(program, pname) ? 1 : 0; }
export function getProgramInfoLog(program) { return gl.getProgramInfoLog(program) ?? ''; }
export function useProgram(program) { gl.useProgram(program); }
export function deleteProgram(program) { gl.deleteProgram(program); }

export function getUniformLocation(program, name) { return gl.getUniformLocation(program, name); }
export function getAttribLocation(program, name) { return gl.getAttribLocation(program, name); }

export function uniform1i(location, v) { gl.uniform1i(location, v); }
export function uniform1f(location, v) { gl.uniform1f(location, v); }
export function uniform4f(location, x, y, z, w) { gl.uniform4f(location, x, y, z, w); }
export function uniformMatrix4fv(location, transpose, value) {
    // C# 侧以 16 个 float 的小端字节流传入，这里拷一份对齐的缓冲再还原
    const bytes = toBytes(value);
    const aligned = new Uint8Array(bytes.length);
    aligned.set(bytes);
    gl.uniformMatrix4fv(location, transpose !== 0, new Float32Array(aligned.buffer));
}

// ---------- 缓冲 ----------

export function createBuffer() { return gl.createBuffer(); }
export function bindBuffer(target, buffer) { gl.bindBuffer(target, buffer); }
export function bufferDataSize(target, size, usage) { gl.bufferData(target, size, usage); }
export function bufferData(target, data, usage) { gl.bufferData(target, toBytes(data), usage); }
export function bufferSubData(target, offset, data) { gl.bufferSubData(target, offset, toBytes(data)); }
export function deleteBuffer(buffer) { gl.deleteBuffer(buffer); }

export function createVertexArray() { return gl.createVertexArray(); }
export function bindVertexArray(vao) { gl.bindVertexArray(vao); }
export function enableVertexAttribArray(index) { gl.enableVertexAttribArray(index); }
export function vertexAttribPointer(index, size, type, normalized, stride, offset) {
    gl.vertexAttribPointer(index, size, type, !!normalized, stride, offset);
}

// ---------- 纹理 ----------

export function createTexture() { return gl.createTexture(); }
export function bindTexture(target, texture) { gl.bindTexture(target, texture); }
export function texImage2D(target, level, internalFormat, width, height, border, format, type, data) {
    gl.texImage2D(target, level, internalFormat, width, height, border, format, type, toBytes(data));
}
export function texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, data) {
    gl.texSubImage2D(target, level, xoffset, yoffset, width, height, format, type, toBytes(data));
}
export function texParameteri(target, pname, param) { gl.texParameteri(target, pname, param); }
export function activeTexture(unit) { gl.activeTexture(unit); }
export function deleteTexture(texture) { gl.deleteTexture(texture); }
export function pixelStorei(pname, param) { gl.pixelStorei(pname, param); }
export function generateMipmap(target) { gl.generateMipmap(target); }

// ---------- 状态与绘制 ----------

export function enable(cap) { gl.enable(cap); }
export function disable(cap) { gl.disable(cap); }
export function blendFuncSeparate(srcRGB, dstRGB, srcA, dstA) { gl.blendFuncSeparate(srcRGB, dstRGB, srcA, dstA); }
export function blendEquation(mode) { gl.blendEquation(mode); }
export function clearColor(r, g, b, a) { gl.clearColor(r, g, b, a); }
export function clear(mask) { gl.clear(mask); }
export function viewport(x, y, width, height) { gl.viewport(x, y, width, height); }
export function scissor(x, y, width, height) { gl.scissor(x, y, width, height); }
export function drawElements(mode, count, type, offset) { gl.drawElements(mode, count, type, offset); }
export function drawArrays(mode, first, count) { gl.drawArrays(mode, first, count); }
