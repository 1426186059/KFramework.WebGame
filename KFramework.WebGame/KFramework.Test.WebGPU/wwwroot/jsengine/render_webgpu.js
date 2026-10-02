// 【依赖 C#】由 KFramework.MonoGame.JSBind_WebGPU 经 [JSImport(module: "render_webgpu")] 调用；
// 编译产物 render_webgpu.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
//
// WebGPU 原生后端（非 WebGL 镜像）。本模块提供一套“即时模式外壳 + WebGPU 保留模式”的实现：
// C# 侧以原生 WebGPU 语义调用（init / createShaderModule / createPipeline / createBuffer /
// createTexture / createBindGroup → beginFrame / setPipeline / setVertexBuffer / setIndexBuffer /
// setBindGroup / draw / drawIndexed / endFrame），内部用 GPUDevice / GPURenderPipeline /
// GPUCommandEncoder 真正出图。
//
// 与 WebGL 的关键区别：
//   * WebGPU 是异步初始化（requestAdapter → requestDevice → context.configure），故 init 是 async；
//   * 没有全局即时状态，绘制走 GPURenderPipeline + 命令编码器；
//   * 着色器是 WGSL（GPUShaderModule），不是 GLSL；
//   * 所有 GPU 对象以整数句柄返回（不再用 JSObject），避免跨边界持有 JS 对象带来的生命周期问题。
//
// 重要：.NET 侧 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），必须经 toUint8Array 转换后才能交给 WebGPU。
import { getOrCreateCanvasElement } from './html_canvas.js';
// ---- 全局 WebGPU 状态 ----
let device = null; // GPUDevice
let adapter = null; // GPUAdapter
let context = null; // GPUCanvasContext
let canvas = null;
let format = 'bgra8unorm'; // 画布首选格式（GPUTextureFormat）
let antialiasEnabled = false;
let sampleCount = 1;
// 深度 / 多重采样附件（随画布尺寸变化，由 ensureAttachments 维护）
let depthTexture = null;
let msaaTexture = null;
// ---- GPU 对象句柄表（整数 id → GPU 对象）----
const shaders = new Map();
const pipelines = new Map();
const buffers = new Map();
const textures = new Map();
const samplers = new Map();
const bindGroups = new Map();
let nextId = 1;
function allocId() { return nextId++; }
// ---- 当前帧状态（命令编码器 / 渲染通道）----
let encoder = null;
let pass = null;
let curPipeline = null;
// 把 .NET MemoryView（Span<T>）或 TypedArray 统一转成 Uint8Array（字节视图）交给 WebGPU。
// MemoryView 只有 slice()（返回 ArrayBufferView 副本），没有 .buffer，故与 TypedArray 分开处理。
function toUint8Array(view) {
    if (view === null || view === undefined)
        return null;
    if (ArrayBuffer.isView(view)) {
        const v = view;
        return new Uint8Array(v.buffer, v.byteOffset, v.byteLength);
    }
    const mv = view;
    if (typeof mv.slice === 'function') {
        const copy = mv.slice();
        return new Uint8Array(copy.buffer, copy.byteOffset, copy.byteLength);
    }
    return null;
}
// 把 C# 侧序列化好的描述对象（JSON 字符串）解析回 JS 对象。
// 原因：本项目的 .NET 源码生成式 JS interop 不支持 JSMarshalAs<JSType.Object>/object，
// 复杂描述只能以 JSON 字符串跨边界传递（见 JSBind_WebGPU.cs 的 CreatePipeline/CreateBindGroup/CreateSampler）。
function parseJson(s) {
    try {
        return JSON.parse(s);
    }
    catch {
        return null;
    }
}
// ---------- 初始化 ----------
/**
 * 异步初始化 WebGPU：requestAdapter → requestDevice → 配置画布上下文。
 * 成功返回 true；浏览器不支持 / 取设备失败返回 false 并在控制台报错。
 */
export async function init(canvasId, antialias) {
    const element = getOrCreateCanvasElement(canvasId);
    if (!element) {
        console.error('[webgpu] 无法创建或找到画布:', canvasId);
        return false;
    }
    canvas = element;
    antialiasEnabled = !!antialias;
    sampleCount = antialiasEnabled ? 4 : 1;
    if (!('gpu' in navigator)) {
        console.error('[webgpu] 当前浏览器不支持 WebGPU（navigator.gpu 不存在）');
        return false;
    }
    try {
        adapter = await navigator.gpu.requestAdapter();
        if (!adapter) {
            console.error('[webgpu] requestAdapter 返回 null');
            return false;
        }
        device = await adapter.requestDevice();
        if (!device) {
            console.error('[webgpu] requestDevice 返回 null');
            return false;
        }
        device.lost.then((info) => {
            console.warn('[webgpu] device lost:', info?.reason, info?.message);
            device = null;
            context = null;
        });
        context = canvas.getContext('webgpu');
        if (!context) {
            console.error('[webgpu] getContext("webgpu") 返回 null');
            return false;
        }
        format = navigator.gpu.getPreferredCanvasFormat();
        configureContext();
        return true;
    }
    catch (e) {
        console.error('[webgpu] 初始化失败:', e);
        return false;
    }
}
function configureContext() {
    if (!context || !device || !canvas)
        return;
    context.configure({ device, format, alphaMode: 'premultiplied' });
    ensureAttachments();
}
// 依当前画布尺寸重建深度 / 多重采样附件。画布尺寸变化（resize）后必须调用。
function ensureAttachments() {
    if (!device || !canvas)
        return;
    const w = canvas.width || 1;
    const h = canvas.height || 1;
    if (msaaTexture) {
        msaaTexture.destroy?.();
        msaaTexture = null;
    }
    if (sampleCount > 1) {
        msaaTexture = device.createTexture({
            size: [w, h],
            sampleCount,
            format,
            usage: 0x10, // GPUTextureUsage.RENDER_ATTACHMENT
        });
    }
    if (depthTexture) {
        depthTexture.destroy?.();
        depthTexture = null;
    }
    depthTexture = device.createTexture({
        size: [w, h],
        format: 'depth24plus',
        usage: 0x10, // GPUTextureUsage.RENDER_ATTACHMENT
    });
}
/** 把画布尺寸设为 width×height 并重建附件（WebGPU 上下文会随画布尺寸自动调整后备缓冲）。 */
export function resize(width, height) {
    if (!canvas)
        return;
    canvas.width = Math.max(1, width | 0);
    canvas.height = Math.max(1, height | 0);
    ensureAttachments();
}
// ---------- 纯状态 / 查询 ----------
export function setAntialias(enabled) {
    antialiasEnabled = !!enabled;
    sampleCount = antialiasEnabled ? 4 : 1;
}
export function getAntialias() { return antialiasEnabled; }
export function getCanvasElement() { return canvas; }
export function isContextLost() { return !device; }
export function getPreferredFormat() { return format; }
/** WebGPU 无全局错误码，恒返回 0（错误走 device.lost / uncapturederror 异步事件）。 */
export function getError() { return 0; }
export function getParameterInt(pname) {
    const lim = adapter?.limits;
    if (pname === 0x0D33)
        return lim?.maxTextureDimension2D ?? 0; // MAX_TEXTURE_SIZE
    if (pname === 0x8D57)
        return lim?.maxSampleCount ?? 0; // MAX_SAMPLES
    return 0;
}
export function getParameterString(pname) {
    if (pname === 0x1F02)
        return 'WebGPU'; // VERSION
    if (pname === 0x1F01)
        return adapter?.info?.description ?? 'WebGPU Device'; // RENDERER
    return 'WebGPU';
}
// ---------- 缓冲 ----------
/** 创建 GPUBuffer；usage 为 GPUBufferUsage 位标志的组合。返回整数句柄（0 表示失败）。 */
export function createBuffer(size, usage) {
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const id = allocId();
    buffers.set(id, device.createBuffer({ size: Math.max(0, size | 0), usage: usage | 0 }));
    return id;
}
/** 上传字节到缓冲（queue.writeBuffer）。data 可为 TypedArray 或 .NET MemoryView。 */
export function writeBuffer(id, offset, data) {
    const buf = buffers.get(id);
    const bytes = toUint8Array(data);
    if (!buf || !bytes || !device)
        return;
    device.queue.writeBuffer(buf, offset | 0, bytes);
}
export function destroyBuffer(id) {
    const buf = buffers.get(id);
    if (buf) {
        buf.destroy?.();
        buffers.delete(id);
    }
}
// ---------- 着色器（WGSL）----------
/** 由 WGSL 源码创建 GPUShaderModule，返回整数句柄。 */
export function createShaderModule(code) {
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const id = allocId();
    const mod = device.createShaderModule({ code });
    mod.getCompilationInfo?.().then((info) => {
        for (const m of (info?.messages ?? [])) {
            if (m.type === 'error')
                console.error('[webgpu] shader 编译错误 L' + m.lineNum + ': ' + m.message);
        }
    });
    shaders.set(id, mod);
    return id;
}
export function destroyShaderModule(id) {
    shaders.delete(id);
}
// ---------- 管线 ----------
// 把混合因子（WebGPU 字符串或由 WebGL 枚举兼容传入）映射为 GPUBlendFactor。
function blendFactor(v) {
    if (typeof v === 'string')
        return v;
    switch (v) {
        case 0x0: return 'zero';
        case 0x1: return 'one';
        case 0x0300: return 'src';
        case 0x0301: return 'one-minus-src';
        case 0x0302: return 'src-alpha';
        case 0x0303: return 'one-minus-src-alpha';
        case 0x0304: return 'dst-alpha';
        case 0x0305: return 'one-minus-dst-alpha';
        case 0x0306: return 'dst';
        case 0x0307: return 'one-minus-dst';
        default: return 'one';
    }
}
function blendOp(v) {
    if (typeof v === 'string')
        return v;
    switch (v) {
        case 0x8006: return 'add';
        case 0x800A: return 'subtract';
        case 0x800B: return 'reverse-subtract';
        case 0x8007: return 'min';
        case 0x8008: return 'max';
        default: return 'add';
    }
}
function buildBlend(b) {
    return {
        color: {
            srcFactor: blendFactor(b?.color?.srcFactor ?? 'one'),
            dstFactor: blendFactor(b?.color?.dstFactor ?? 'zero'),
            operation: blendOp(b?.color?.operation ?? 'add'),
        },
        alpha: {
            srcFactor: blendFactor(b?.alpha?.srcFactor ?? 'one'),
            dstFactor: blendFactor(b?.alpha?.dstFactor ?? 'zero'),
            operation: blendOp(b?.alpha?.operation ?? 'add'),
        },
    };
}
/**
 * 创建 GPURenderPipeline。
 * descriptor 结构（字段均为 WebGPU 原生值）：
 * {
 *   vertexShader: number, fragmentShader: number,
 *   vertex?: { entryPoint?, buffers?: [{ arrayStride, stepMode?, attributes: [{shaderLocation, offset, format}] }] },
 *   fragment?: { entryPoint? },
 *   primitive?: { topology?, cullMode?, frontFace? },          // 默认 triangle-list / none / ccw
 *   blend?: { color:{srcFactor,dstFactor,operation}, alpha:{...} } | null,
 *   depthStencil?: { format?, depthWriteEnabled?, depthCompare? } | null
 * }
 * 返回整数句柄（0 表示失败）。
 */
export function createPipeline(descriptorJson) {
    const desc = parseJson(descriptorJson);
    if (!desc) {
        console.error('[webgpu] createPipeline: 描述为空或 JSON 解析失败');
        return 0;
    }
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const vs = shaders.get(desc?.vertexShader);
    const fs = shaders.get(desc?.fragmentShader);
    if (!vs || !fs) {
        console.error('[webgpu] createPipeline: 着色器缺失');
        return 0;
    }
    const target = { format };
    if (desc?.blend)
        target.blend = buildBlend(desc.blend);
    const pipelineDesc = {
        layout: 'auto',
        vertex: {
            module: vs,
            entryPoint: desc?.vertex?.entryPoint ?? 'vs_main',
            buffers: desc?.vertex?.buffers ?? [],
        },
        fragment: {
            module: fs,
            entryPoint: desc?.fragment?.entryPoint ?? 'fs_main',
            targets: [target],
        },
        primitive: {
            topology: desc?.primitive?.topology ?? 'triangle-list',
            cullMode: desc?.primitive?.cullMode ?? 'none',
            frontFace: desc?.primitive?.frontFace ?? 'ccw',
        },
        multisample: { count: sampleCount },
    };
    if (desc?.depthStencil) {
        pipelineDesc.depthStencil = {
            format: desc.depthStencil.format ?? 'depth24plus',
            depthWriteEnabled: desc.depthStencil.depthWriteEnabled ?? true,
            depthCompare: desc.depthStencil.depthCompare ?? 'less',
        };
    }
    const id = allocId();
    try {
        pipelines.set(id, device.createRenderPipeline(pipelineDesc));
    }
    catch (e) {
        console.error('[webgpu] createRenderPipeline 失败:', e);
        return 0;
    }
    return id;
}
export function destroyPipeline(id) { pipelines.delete(id); }
// ---------- 绑定组 ----------
/**
 * 用管线（layout:'auto' 推导的绑定组布局）创建 GPUBindGroup。
 * entries: [{ binding, type:'uniform'|'texture'|'sampler', id, offset?, size? }]
 *   - uniform: 取 buffers[id]（offset/size 默认 0 / 整段）
 *   - texture: 取 textures[id].createView()
 *   - sampler: 取 samplers[id]
 */
export function createBindGroup(pipelineId, groupIndex, entriesJson) {
    const entries = parseJson(entriesJson);
    const pipe = pipelines.get(pipelineId);
    if (!pipe || !device) {
        console.error('[webgpu] createBindGroup: pipeline 缺失');
        return 0;
    }
    let layout;
    try {
        layout = pipe.getBindGroupLayout(groupIndex | 0);
    }
    catch (e) {
        console.error('[webgpu] getBindGroupLayout 失败:', e);
        return 0;
    }
    const gpuEntries = (entries ?? []).map((e) => {
        if (e.type === 'texture') {
            const tex = textures.get(e.id);
            return { binding: e.binding | 0, resource: tex?.createView() };
        }
        if (e.type === 'sampler') {
            return { binding: e.binding | 0, resource: samplers.get(e.id) };
        }
        const buf = buffers.get(e.id);
        return { binding: e.binding | 0, resource: { buffer: buf, offset: e.offset ?? 0, size: e.size ?? buf?.size } };
    });
    const id = allocId();
    try {
        bindGroups.set(id, device.createBindGroup({ layout, entries: gpuEntries }));
    }
    catch (e) {
        console.error('[webgpu] createBindGroup 失败:', e);
        return 0;
    }
    return id;
}
export function destroyBindGroup(id) { bindGroups.delete(id); }
// ---------- 纹理 / 采样器 ----------
/** 创建空 GPUTexture（默认 usage 含 TEXTURE_BINDING | RENDER_ATTACHMENT | COPY_DST）。 */
export function createTexture(width, height, formatStr) {
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const id = allocId();
    textures.set(id, device.createTexture({
        size: [width | 0, height | 0],
        format: formatStr || 'rgba8unorm',
        usage: 0x04 | 0x10 | 0x02, // TEXTURE_BINDING | RENDER_ATTACHMENT | COPY_DST
    }));
    return id;
}
/** 上传 RGBA8 像素到纹理（queue.writeTexture，按 rgba8unorm 假设 bytesPerRow = width*4）。 */
export function uploadTexture(id, data, width, height, formatStr) {
    const tex = textures.get(id);
    const bytes = toUint8Array(data);
    if (!tex || !bytes || !device)
        return;
    const w = width | 0;
    const h = height | 0;
    // 当前仅实现 rgba8unorm / bgra8unorm（bytesPerRow = width*4）；其余格式暂按 4 字节/像素处理并提示。
    const fmt = formatStr || 'rgba8unorm';
    if (fmt !== 'rgba8unorm' && fmt !== 'bgra8unorm') {
        console.warn('[webgpu] uploadTexture 当前按 rgba8 假设处理，收到格式: ' + fmt);
    }
    device.queue.writeTexture({ texture: tex, mipLevel: 0, origin: { x: 0, y: 0, z: 0 } }, bytes, { offset: 0, bytesPerRow: w * 4, rowsPerImage: h }, { width: w, height: h, depthOrArrayLayers: 1 });
}
export function destroyTexture(id) {
    const t = textures.get(id);
    if (t) {
        t.destroy?.();
        textures.delete(id);
    }
}
/** 创建 GPUSampler。descriptor: { magFilter?, minFilter?, addressU?, addressV?, mipmapFilter? }。 */
export function createSampler(descriptorJson) {
    const desc = parseJson(descriptorJson);
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const d = {
        magFilter: desc?.magFilter ?? 'linear',
        minFilter: desc?.minFilter ?? 'linear',
        addressModeU: desc?.addressU ?? 'clamp-to-edge',
        addressModeV: desc?.addressV ?? 'clamp-to-edge',
        mipmapFilter: desc?.mipmapFilter ?? 'nearest',
    };
    const id = allocId();
    samplers.set(id, device.createSampler(d));
    return id;
}
export function destroySampler(id) { samplers.delete(id); }
// ---------- 帧绘制（即时模式外壳）----------
/**
 * 开帧：建立命令编码器并 begin 一个渲染通道到画布。
 * depthClear >= 0 时附带深度附件（clear 到该值）；此时所用管线必须带匹配的 depthStencil。
 */
export function beginFrame(r, g, b, a, depthClear) {
    if (!device || !context) {
        console.error('[webgpu] 未初始化');
        return;
    }
    encoder = device.createCommandEncoder();
    const canvasView = context.getCurrentTexture().createView();
    const colorAttachment = {
        view: sampleCount > 1 ? msaaTexture.createView() : canvasView,
        clearValue: { r, g, b, a },
        loadOp: 'clear',
        storeOp: 'store',
    };
    if (sampleCount > 1)
        colorAttachment.resolveTarget = canvasView;
    const passDesc = { colorAttachments: [colorAttachment] };
    if (depthClear >= 0 && depthTexture) {
        passDesc.depthStencilAttachment = {
            view: depthTexture.createView(),
            depthClearValue: depthClear,
            depthLoadOp: 'clear',
            depthStoreOp: 'store',
        };
    }
    pass = encoder.beginRenderPass(passDesc);
    curPipeline = null;
}
export function setPipeline(id) {
    curPipeline = pipelines.get(id) ?? null;
    if (curPipeline && pass)
        pass.setPipeline(curPipeline);
}
export function setVertexBuffer(slot, id) {
    const buf = buffers.get(id);
    if (buf && pass)
        pass.setVertexBuffer(slot | 0, buf);
}
export function setIndexBuffer(id, type) {
    const buf = buffers.get(id);
    if (!buf || !pass)
        return;
    pass.setIndexBuffer(buf, type === 'uint32' ? 'uint32' : 'uint16');
}
export function setBindGroup(group, id) {
    const bg = bindGroups.get(id);
    if (bg && pass)
        pass.setBindGroup(group | 0, bg);
}
export function draw(vertexCount, instanceCount) {
    if (!pass || !curPipeline)
        return;
    pass.draw(vertexCount | 0, instanceCount | 0);
}
/**
 * 按索引缓冲绘制。
 * firstIndex / baseVertex 是批处理的关键：同一帧内多次 queue.writeBuffer 到同一个缓冲时，
 * 后排队的操作会先于本帧任何 submit 生效，因此顶点必须【累加分区】写入，靠这两个参数把
 * 相对索引偏移到本次写入区间的起点（firstIndex 恒为 0，因为静态索引是每个四边形内部编号）。
 */
export function drawIndexed(indexCount, instanceCount, firstIndex, baseVertex) {
    if (!pass || !curPipeline)
        return;
    pass.drawIndexed(indexCount | 0, instanceCount | 0, firstIndex | 0, baseVertex | 0);
}
/** 收帧：结束渲染通道并提交命令缓冲区到队列。 */
export function endFrame() {
    if (!pass || !encoder || !device)
        return;
    pass.end();
    device.queue.submit([encoder.finish()]);
    pass = null;
    encoder = null;
    curPipeline = null;
}
