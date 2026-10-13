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
// 重要：.NET 侧 Span<T> 在 JS 侧是 MemoryView（不是 TypedArray），必须经 ByteCache 转换后才能交给 WebGPU。
import { getCanvas } from './html_canvas.js';
import { ByteCache } from './custom_data_byte_cache.js';
// ---- 全局 WebGPU 状态 ----
let device = null; // GPUDevice
let adapter = null; // GPUAdapter
let context = null; // GPUCanvasContext
let canvas = null;
let format = 'bgra8unorm'; // 画布首选格式（GPUTextureFormat）
let antialiasEnabled = false;
let sampleCount = 1;
// 深度 / 多重采样 / 屏幕缓冲附件（随画布尺寸变化，由 ensureAttachments 维护）
let depthTexture = null;
let msaaTexture = null;
let screenTexture = null;
// 当前通道是不是"画向画布"的（endFrame 时据此决定要不要拷回画布纹理）
let passIsCanvas = false;
// ---- GPU 对象句柄表（整数 id → GPU 对象）----
const shaders = new Map();
const pipelines = new Map();
const buffers = new Map();
const textures = new Map();
const samplers = new Map();
const bindGroups = new Map();
let nextId = 1;
function allocId() { return nextId++; }
// ---- 复用的字节缓冲（见 custom_data_byte_cache.ts）----
// .NET 的 MemoryView 没有 .buffer，只能 copyTo / slice 出副本交给 WebGPU：拷贝免不掉，
// 但"每次调用都 new Uint8Array"的分配可以免。按用途分开 —— 两者量级差太远，不该共用。
// 上限默认 65535（ushort 最大值）对这两类都不够，需显式放宽：顶点单批约 320KB，
// 纹理 1024² RGBA 就是 4MB。
const _cacheBuffer = new ByteCache(64 * 1024, 1024 * 1024); // queue.writeBuffer：每帧高频
const _cacheTexels = new ByteCache(2048, 32 * 1024 * 1024); // queue.writeTexture：块大但低频
// ---- 当前帧状态（命令编码器 / 渲染通道）----
let encoder = null;
let pass = null;
let curPipeline = null;
// MemoryView → Uint8Array 的转换统一走 ByteCache（见 custom_data_byte_cache.ts），
// 按用途分流到 _cacheBuffer / _cacheTexels。
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
export async function init(antialias) {
    const element = getCanvas();
    if (!element) {
        console.error('[webgpu] 无法创建或找到画布');
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
        // 按需启用压缩纹理特性：仅当 adapter 支持时才请求（否则 requestDevice 会抛错），
        // 启用后 device.features 才含这些特性，Ktx2TranscodeSelector 才能在 WebGPU 下选压缩格式。
        const FEATURES = ['texture-compression-bc', 'texture-compression-etc2', 'texture-compression-astc'];
        const requiredFeatures = [];
        for (const f of FEATURES) {
            if (adapter.features.has(f))
                requiredFeatures.push(f);
        }
        device = await adapter.requestDevice({ requiredFeatures });
        if (!device) {
            console.error('[webgpu] requestDevice 返回 null');
            return false;
        }
        device.lost.then((info) => {
            console.error('[webgpu] WebGPU 设备已丢失（上下文丢失），渲染将暂停（刷新页面可恢复）。reason:', info?.reason, 'message:', info?.message);
            device = null;
            context = null;
        });
        context = canvas.getContext('webgpu');
        if (!context) {
            console.error('[webgpu] getContext("webgpu") 返回 null');
            return false;
        }
        // 画布格式【不用】getPreferredCanvasFormat()（多数平台是 bgra8unorm），固定用 rgba8unorm：
        // 实测 bgra8unorm 的画布/屏幕缓冲在"切走再切回、用 loadOp:'load' 续画"时保不住内容
        //（同样的顺序画在 rgba8unorm 的离屏上就正常 —— 见测试页「实际(离屏) vs 实际(画布)」）。
        // 统一成 rgba8unorm 后，屏幕缓冲与所有离屏 RT 同格式，拷回画布的 copyTextureToTexture 也仍然合法。
        format = 'rgba8unorm';
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
    // usage 必须带上 COPY_DST：画布通道画在自建的屏幕缓冲里，通道结束要【拷贝】回画布纹理。
    // GPUCanvasConfiguration.usage 默认只有 RENDER_ATTACHMENT(0x10)，不显式加 COPY_DST 就拷不进去。
    // 注意别用 0x08 —— 那是 STORAGE_BINDING，且 bgra8unorm 不支持 storage binding，
    // 配上去会让交换链纹理直接变 invalid（Dawn 报 ValidateTextureUsageConstraints）。
    context.configure({
        device,
        format,
        alphaMode: 'premultiplied',
        usage: 0x10 | 0x02, // RENDER_ATTACHMENT | COPY_DST
    });
    ensureAttachments();
}
// 依当前画布尺寸重建屏幕缓冲 / 深度 / 多重采样附件。画布尺寸变化（resize）后必须调用。
function ensureAttachments() {
    if (!device || !canvas)
        return;
    const w = canvas.width || 1;
    const h = canvas.height || 1;
    // 【屏幕缓冲】画布的内容先画在这里，通道结束再拷回真正的画布纹理 —— 详见 beginFrame 的注释。
    // 用途标志要和普通离屏纹理【完全一致】（见 createTexture 的 0x04|0x10|0x02），
    // 额外加 COPY_SRC 供拷回画布用。若少了 TEXTURE_BINDING / COPY_DST，
    // 这张纹理与离屏纹理在"切走再切回、用 load 续画"时的表现就可能不一致。
    if (screenTexture) {
        screenTexture.destroy?.();
        screenTexture = null;
    }
    screenTexture = device.createTexture({
        size: [w, h],
        format,
        usage: 0x04 | 0x10 | 0x02 | 0x01, // TEXTURE_BINDING | RENDER_ATTACHMENT | COPY_DST | COPY_SRC
    });
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
/**
 * 【关键】尺寸没变就必须直接返回，一步都不要做：
 *   - `canvas.width = ...` 赋值本身就会重置绘制缓冲区；
 *   - `ensureAttachments()` 更会【销毁并重建】屏幕缓冲 screenTexture，内容全部归零。
 * 而本函数会在【每次切换渲染目标】时被间接调用 —— GraphicsDevice.Viewport 的 setter 会下发
 * SetViewport，后者转调本函数；切回画布（colorTarget=0）时正好放行。
 * 于是一帧内每切回一次画布，就把画布上已画好的内容抹掉一次，只剩最后一次绘制
 * （表现：来回切换渲染目标做合成时内容丢失，且 loadOp:'load' 明明发对了也无效）。
 */
export function resize(width, height) {
    if (!canvas)
        return;
    const w = Math.max(1, width | 0);
    const h = Math.max(1, height | 0);
    // 判断依据是【屏幕缓冲自身】的尺寸，而不是 canvas.width —— 真正决定渲染区域的是
    // screenTexture，两者可能不同步（例如 canvas 已被外部改成目标尺寸，而缓冲仍是旧尺寸），
    // 若拿 canvas.width 去比对就会误判为"无需重建"，画面便只渲染到那一小块（缩在左上角）。
    if (screenTexture && screenTexture.width === w && screenTexture.height === h)
        return;
    // 确实要改尺寸：canvas.width 赋值本身就会重置绘制缓冲区，故仅在必要时才做。
    if (canvas.width !== w)
        canvas.width = w;
    if (canvas.height !== h)
        canvas.height = h;
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
/** WebGPU 设备是否已就绪（已成功 requestDevice）。用于判断当前后端是否为 WebGPU（KTX2 格式选择用）。 */
export function isActive() { return device !== null; }
/** 查询 WebGPU 设备是否已启用某特性（如 'texture-compression-bc'）。用于 KTX2 压缩格式选择。 */
export function hasFeature(flag) {
    return !!device && device.features.has(flag);
}
// 压缩纹理格式 → { 每块字节数 bpp, 块宽 bx, 块高 by }。WebGPU 的 GPUTextureFormat 名与块参数。
// 仅列入本引擎实际会用到的格式（KTX2 转码产物为 4x4 / 16 字节的 BC3/BC7/ETC2/ASTC4x4）。
const COMPRESSED_INFO = {
    'bc1-rgba-unorm': { bpp: 8, bx: 4, by: 4 },
    'bc2-rgba-unorm': { bpp: 16, bx: 4, by: 4 },
    'bc3-rgba-unorm': { bpp: 16, bx: 4, by: 4 },
    'bc7-rgba-unorm': { bpp: 16, bx: 4, by: 4 },
    'etc2-rgb8unorm': { bpp: 8, bx: 4, by: 4 },
    'etc2-rgba8unorm': { bpp: 16, bx: 4, by: 4 },
    'astc-4x4-rgba-unorm': { bpp: 16, bx: 4, by: 4 },
    'astc-5x5-rgba-unorm': { bpp: 16, bx: 5, by: 5 },
    'astc-6x6-rgba-unorm': { bpp: 16, bx: 6, by: 6 },
    'astc-8x8-rgba-unorm': { bpp: 16, bx: 8, by: 8 },
    'astc-10x10-rgba-unorm': { bpp: 16, bx: 10, by: 10 },
    'astc-12x12-rgba-unorm': { bpp: 16, bx: 12, by: 12 },
};
function isCompressedFormat(f) { return Object.prototype.hasOwnProperty.call(COMPRESSED_INFO, f); }
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
    const bytes = _cacheBuffer.copyFrom(data);
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
// 混合因子 / 混合运算：C# 侧的中立枚举（Blend）已由 WebGpuBackend 映射为
// WebGPU 原生字符串，这里只做非法值的兜底（混合状态不带 GL 常量，无需再兼容数字）。
function blendFactor(v) {
    return typeof v === 'string' ? v : 'one';
}
function blendOp(v) {
    return typeof v === 'string' ? v : 'add';
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
    // 管线必须与通道的颜色附件【格式 + 采样数】完全一致，否则 WebGPU 报错。
    // 画布格式（bgra8unorm）与离屏 RT（rgba8unorm）可能不同，故格式要能按目标指定。
    const targetFormat = desc?.colorFormat ?? format;
    const target = { format: targetFormat };
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
        multisample: { count: desc?.sampleCount ?? sampleCount },
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
/**
 * 创建纹理。
 * sampleCount > 1 时创建的是【多重采样附件】：只用于渲染（RENDER_ATTACHMENT），
 * 不能被 shader 采样、也不能由 CPU 上传 —— 内容靠通道的 resolveTarget 解析进单采样纹理。
 */
export function createTexture(width, height, formatStr, sampleCount, extraUsage = 0) {
    if (!device) {
        console.error('[webgpu] device 未就绪');
        return 0;
    }
    const fmt = formatStr || 'rgba8unorm';
    const comp = isCompressedFormat(fmt);
    // 压缩纹理不能做渲染目标（无 RENDER_ATTACHMENT），也不可多重采样（sampleCount 强制 1）；
    // usage 只需 TEXTURE_BINDING | COPY_DST（上传 + 采样）。
    const samples = comp ? 1 : Math.max(1, sampleCount | 0);
    const usage = comp
        ? (0x04 | 0x02 | (extraUsage & 0x01)) // TEXTURE_BINDING | COPY_DST | (RenderTarget 才有的 COPY_SRC)
        : (samples > 1
            ? 0x10 // RENDER_ATTACHMENT（多重采样附件）
            : (0x04 | 0x10 | 0x02 | (extraUsage & 0x01))); // TEXTURE_BINDING | RENDER_ATTACHMENT | COPY_DST | COPY_SRC
    const id = allocId();
    textures.set(id, device.createTexture({
        size: [width | 0, height | 0],
        sampleCount: samples,
        format: fmt,
        usage,
    }));
    return id;
}
/** 上传 RGBA8 像素到纹理（queue.writeTexture，按 rgba8unorm 假设 bytesPerRow = width*4）。 */
/**
 * 上传 RGBA8 像素到纹理（queue.writeTexture）。
 * x / y 为写入原点的左上角（用于 SpriteFont 字形图集这类「逐块局部更新」的场景）：
 * 数据只含本块的 w×h×4 字节，故 bytesPerRow 按块宽计算，配合 origin 即可精确写进子区域。
 */
export function uploadTexture(id, data, x, y, width, height, formatStr) {
    const tex = textures.get(id);
    const bytes = _cacheTexels.copyFrom(data);
    if (!tex || !bytes || !device)
        return;
    const w = width | 0;
    const h = height | 0;
    // 当前仅实现 rgba8unorm / bgra8unorm（bytesPerRow = width*4）；其余格式暂按 4 字节/像素处理并提示。
    const fmt = formatStr || 'rgba8unorm';
    // 压缩纹理：按格式查块参数，整张贴图一次 writeTexture（origin 0,0）。KTX2 走此分支。
    if (isCompressedFormat(fmt)) {
        const info = COMPRESSED_INFO[fmt];
        const wb = Math.ceil(w / info.bx);
        const hb = Math.ceil(h / info.by);
        const rowBytes = wb * info.bpp;
        // WebGPU 硬规定：多行 writeTexture 的 bytesPerRow 必须 256 字节对齐，否则整次写入静默失败。
        const bytesPerRow = Math.ceil(rowBytes / 256) * 256;
        if (bytesPerRow === rowBytes) {
            device.queue.writeTexture({ texture: tex, mipLevel: 0, origin: { x: x | 0, y: y | 0, z: 0 } }, bytes, { offset: 0, bytesPerRow, rowsPerImage: hb }, { width: w, height: h, depthOrArrayLayers: 1 });
        }
        else {
            // 行字节需 256 对齐：补一帧带 padding 的中间缓冲（逐行拷贝），再整张写入。
            const padded = new Uint8Array(bytesPerRow * hb);
            for (let r = 0; r < hb; r++) {
                padded.set(bytes.subarray(r * rowBytes, (r + 1) * rowBytes), r * bytesPerRow);
            }
            device.queue.writeTexture({ texture: tex, mipLevel: 0, origin: { x: x | 0, y: y | 0, z: 0 } }, padded, { offset: 0, bytesPerRow, rowsPerImage: hb }, { width: w, height: h, depthOrArrayLayers: 1 });
        }
        return;
    }
    if (fmt !== 'rgba8unorm' && fmt !== 'bgra8unorm') {
        console.warn('[webgpu] uploadTexture 收到未知格式，按 rgba8 假设处理: ' + fmt);
    }
    const bytesPerRow = w * 4;
    // WebGPU 硬规定：多行 writeTexture 的 bytesPerRow 必须 256 字节对齐，否则整次写入静默失败。
    // 字体图集的字形细胞宽度仅几十像素（bytesPerRow 远小于 256），直接写会整块丢失 → 文字全透明（黑屏）。
    // 故非对齐的多行情况改逐行写入：单行 writeTexture 不受 256 限制，origin.y 逐行推进即可精确填进子区域。
    if (h <= 1 || bytesPerRow % 256 === 0) {
        device.queue.writeTexture({ texture: tex, mipLevel: 0, origin: { x: x | 0, y: y | 0, z: 0 } }, bytes, { offset: 0, bytesPerRow, rowsPerImage: h }, { width: w, height: h, depthOrArrayLayers: 1 });
        return;
    }
    for (let r = 0; r < h; r++) {
        device.queue.writeTexture({ texture: tex, mipLevel: 0, origin: { x: x | 0, y: (y + r) | 0, z: 0 } }, bytes.subarray(r * bytesPerRow, (r + 1) * bytesPerRow), { offset: 0, bytesPerRow, rowsPerImage: 1 }, { width: w, height: 1, depthOrArrayLayers: 1 });
    }
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
/**
 * 开帧（开始一个渲染通道）。
 *
 * 句柄语义：colorTarget = 0 表示画布交换链；非 0 用离屏纹理（需先由 createTexture 创建）。
 *
 * MSAA 解析采用【Vulkan 的 pResolveAttachments 语义】：渲染进 colorTarget（多重采样纹理），
 * resolveTarget 指向单采样纹理，通道结束时由实现自动解析。
 * 注意：WebGPU 的 resolveTarget 必须在【通道创建时】指定，不能像 GL 的 blitFramebuffer 那样事后解析 ——
 * 这正是它与 WebGL 后端最大的建模差异。
 *
 * @param colorTarget   颜色附件句柄（0 = 画布）
 * @param resolveTarget 解析目标句柄（0 = 不解析）
 * @param depthTarget   深度附件句柄（0 = 用画布自带的深度纹理，或不用）
 */
/**
 * 开帧。
 * @param loadMode 通道的载入方式：0 = clear（用 clearValue 清屏），1 = load（沿用目标里已有的内容）。
 *   —— 切换渲染目标后继续绘制时必须用 1：目标切换会结束当前通道，若新通道再走 clear，
 *      就会擦掉已经画好的部分。传奇那种「画布 ↔ 多个离屏 RT 来回切换做合成」的模式正是靠 load 保住内容。
 */
export function beginFrame(r, g, b, a, depthClear, colorTarget, resolveTarget, depthTarget, loadMode) {
    if (!device || !context) {
        console.error('[webgpu] 未初始化');
        return;
    }
    encoder = device.createCommandEncoder();
    const toCanvas = colorTarget === 0;
    // 【画布不直接画】画向画布时，一律先画进我们自建的屏幕缓冲 screenTexture，通道结束再拷回画布纹理。
    // 原因：交换链纹理（getCurrentTexture）在每次提交后不保证保留内容，
    // 于是"切去离屏再切回画布继续画"时用 loadOp:'load' 取不到先前画的东西 ——
    // 表现就是切换前画的内容凭空消失（传奇那种一帧内来回切很多次的用法会整屏发黑）。
    // 换成自己的缓冲后，load / store 完全由我们掌控，跨通道续画才可靠。
    let view;
    let resolved = null;
    if (toCanvas) {
        if (!screenTexture) {
            console.error('[webgpu] 屏幕缓冲未就绪');
            return;
        }
        const screenView = screenTexture.createView();
        if (sampleCount > 1 && msaaTexture) {
            view = msaaTexture.createView(); // 多重采样：渲染进 MSAA 纹理
            resolved = screenView; // 解析进屏幕缓冲
        }
        else {
            view = screenView;
        }
    }
    else {
        view = textures.get(colorTarget)?.createView();
        if (resolveTarget !== 0)
            resolved = textures.get(resolveTarget)?.createView();
    }
    if (!view) {
        console.error('[webgpu] beginFrame: 颜色目标句柄无效: ' + colorTarget);
        return;
    }
    const load = loadMode === 1;
    const colorAttachment = {
        view,
        clearValue: { r, g, b, a },
        loadOp: load ? 'load' : 'clear',
        // 必须 store，不能因为有解析目标就 discard：
        // 多重采样附件的解析结果写进了 resolveTarget，但【附件自身】仍是下一次 load 的来源 ——
        // 一旦 discard，后续以 load 方式开帧就会载入到已被丢弃的内容（表现为整屏变黑）。
        // 这里用 store 换正确性：切换渲染目标后要接着画，靠的就是附件里还留着上一通道的内容。
        // 代价是少了 "resolve 后丢弃 MSAA 附件" 这点带宽优化。
        storeOp: 'store',
    };
    if (resolved)
        colorAttachment.resolveTarget = resolved;
    const passDesc = { colorAttachments: [colorAttachment] };
    let depthView = null;
    if (depthTarget !== 0)
        depthView = textures.get(depthTarget)?.createView();
    else if (depthClear >= 0 && depthTexture)
        depthView = depthTexture.createView();
    if (depthView) {
        passDesc.depthStencilAttachment = {
            view: depthView,
            depthClearValue: depthClear >= 0 ? depthClear : 1,
            depthLoadOp: load ? 'load' : 'clear',
            depthStoreOp: 'store',
        };
    }
    pass = encoder.beginRenderPass(passDesc);
    passIsCanvas = toCanvas;
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
    // 画向画布的通道：把屏幕缓冲拷回真正的画布纹理，这一帧画的东西才会呈现出来。
    // 每次画布通道结束都拷一次，故一帧内多次来回切也没问题 —— 最后一次拷的就是最终结果。
    if (passIsCanvas && screenTexture && context) {
        const dst = context.getCurrentTexture();
        encoder.copyTextureToTexture({ texture: screenTexture }, { texture: dst }, [screenTexture.width, screenTexture.height]);
    }
    device.queue.submit([encoder.finish()]);
    pass = null;
    encoder = null;
    passIsCanvas = false;
    curPipeline = null;
}
let pendingReadback = null;
/**
 * 读取纹理区域像素（x/y 为纹理坐标，原点在左上；WebGPU 纹理原点即左上，无需翻转）。
 * 走 copyTextureToBuffer + mapAsync（异步），结果存到模块级 pendingReadback，待 readPixelsGet 同步拷出。
 * textureId 为 createTexture 返回的整数句柄；该纹理须带 COPY_SRC 用途（离屏 RT 已具备）。
 */
export async function readPixels(textureId, x, y, w, h) {
    if (!device)
        return;
    const tex = textures.get(textureId);
    if (!tex)
        return;
    const wi = w | 0, hi = h | 0;
    if (wi <= 0 || hi <= 0)
        return;
    const bytesPerRow = Math.ceil((wi * 4) / 256) * 256; // WebGPU 要求 256 字节对齐
    const buffer = device.createBuffer({
        size: bytesPerRow * hi,
        usage: 0x0001 | 0x0008, // GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST
    });
    const enc = device.createCommandEncoder();
    enc.copyTextureToBuffer({ texture: tex, mipLevel: 0, origin: { x: x | 0, y: y | 0, z: 0 } }, { buffer, bytesPerRow, rowsPerImage: hi }, { width: wi, height: hi, depthOrArrayLayers: 1 });
    device.queue.submit([enc.finish()]);
    await buffer.mapAsync(0x0001); // GPUMapMode.READ
    const mapped = new Uint8Array(buffer.getMappedRange());
    const out = new Uint8Array(wi * hi * 4);
    for (let r = 0; r < hi; r++) {
        out.set(mapped.subarray(r * bytesPerRow, r * bytesPerRow + wi * 4), r * wi * 4);
    }
    buffer.unmap();
    buffer.destroy();
    pendingReadback = out;
}
/** 把上一次 readPixels 异步读回的像素（RGBA8 自上而下）同步拷进 out（MemoryView，长度需 w*h*4）。 */
export function readPixelsGet(out) {
    if (pendingReadback) {
        out.set(pendingReadback);
        pendingReadback = null;
    }
}
