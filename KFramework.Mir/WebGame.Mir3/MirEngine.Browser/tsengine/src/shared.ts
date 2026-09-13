// mirengine/shared.ts
// 跨模块共享的 DOM 引用、可变状态与工具函数。
// 各模块（core/*、render/*）从这里导入，确保 textures / offscreens / audio 等状态全局唯一。
// 本文件是 TypeScript 源码，编译产出 dist\shared.js（见 tsconfig.json：rootDir=src / outDir=dist）。

// ---------------- 共享类型 ----------------

/** 一张已上传的 GL 精灵纹理（已预乘 alpha）。 */
export interface GfxTexture {
    tex: WebGLTexture;
    w: number;
    h: number;
}

/**
 * 绘制目标：主画布（fbo 为 null，尺寸取 canvas）或离屏 FBO / RenderTarget。
 * tex 仅离屏目标具有。
 */
export interface GfxTarget {
    fbo: WebGLFramebuffer | null;
    tex?: WebGLTexture;
    w: number;
    h: number;
}

/** drawTexQuad 系列接受的绘制源（精灵纹理或离屏，均已预乘 alpha）。 */
export type GfxSource = GfxTexture;

/** 一条正在播放的音效实例。 */
export interface AudioEntry {
    src: AudioBufferSourceNode | null;
    gain: GainNode | null;
    canceled: boolean;
    /** WebAudio 增益 0..1（由游戏侧 0..100 音量换算而来）。 */
    volume: number;
}

/** C# 侧 MirEngine.BrowserKeyboard 的 [JSExport] 入口。 */
export interface BrowserKeyboardExports {
    OnKeyDown(key: string): void;
    OnKeyUp(key: string): void;
    OnKeyPress(key: string): void;
}

/** C# 侧 MirEngine.BrowserMouse 的 [JSExport] 入口。 */
export interface BrowserMouseExports {
    OnMouseDown(button: number, x: number, y: number): void;
    OnMouseMove(x: number, y: number): void;
    OnMouseUp(button: number, x: number, y: number): void;
    OnMouseWheel(delta: number, x: number, y: number): void;
}

/** C# 侧 MirEngine.BrowserInputOverlay 的 [JSExport] 入口。 */
export interface BrowserInputOverlayExports {
    OnInput(value: string): void;
    OnEnter(): void;
    OnBlur(): void;
}

/** MirEngine 命名空间下的托管导出（各 Bind 类的静态 [JSExport]）。 */
export interface MirEngineExports {
    BrowserKeyboard: BrowserKeyboardExports;
    BrowserMouse: BrowserMouseExports;
    BrowserInputOverlay: BrowserInputOverlayExports;
    [type: string]: any;
}

/**
 * getAssemblyExports 返回的托管导出树（键为命名空间 / 完整类型名）。
 * 见 main.ts：主程序集之外还会深合并引擎程序集的导出。
 */
export interface DotNetExports {
    MirEngine: MirEngineExports;
    [ns: string]: any;
}

/** 关键 DOM 引用（index.html 提供）+ 渲染上下文，模块加载时一次性建好。 */
export interface DomRefs {
    canvas: HTMLCanvasElement;
    panel: HTMLElement;
    loading: HTMLElement;
    /** WebGL 2.0 上屏上下文。 */
    gl: WebGL2RenderingContext;
    /** 文字度量用的隐藏 2D 上下文。 */
    measureCtx: CanvasRenderingContext2D;
    /** 文字 / 标签栅格化用的离屏 2D 画布。 */
    scratch: HTMLCanvasElement;
    scratchCtx: CanvasRenderingContext2D;
}

// ---------------- 共享状态 ----------------

const _canvas = document.getElementById('screen') as HTMLCanvasElement;
const _measureCanvas = document.createElement('canvas');
const _scratchCanvas = document.createElement('canvas');

export const dom: DomRefs = {
    canvas: _canvas,
    panel: document.getElementById('panel') as HTMLElement,
    loading: document.getElementById('loading') as HTMLElement,
    // WebGL 2.0 上屏上下文（统一渲染后端；仅 WebGL2，无 WebGL1 回退）。
    // alpha:false 让画布不透明；antialias:false 配合 CSS image-rendering:pixelated 保持像素风。
    gl: _canvas.getContext('webgl2', {
        alpha: false,
        antialias: false,
        depth: false,
        stencil: false,
        premultipliedAlpha: false,
        preserveDrawingBuffer: false,
    }) as WebGL2RenderingContext,
    // 文字度量用的隐藏 2D 画布（仅用于 measureText，不参与上屏）。
    measureCtx: _measureCanvas.getContext('2d') as CanvasRenderingContext2D,
    scratch: _scratchCanvas,
    scratchCtx: _scratchCanvas.getContext('2d') as CanvasRenderingContext2D,
};
if (!dom.gl) console.error('[gl] 当前环境不支持 WebGL 2.0');

// 渲染共享状态（精灵纹理 + 离屏画布 / RenderTarget）
// textures:  key -> { tex, w, h }（GL 纹理，已预乘 alpha）
// offscreens: id  -> { fbo, tex, w, h }（DXControl 离屏 / RenderTarget）
const _mainTarget: GfxTarget = { fbo: null, w: dom.canvas.width, h: dom.canvas.height };
export const gfx = {
    textures: new Map<number, GfxTexture>(),
    offscreens: new Map<number, GfxTarget>(),
    nextOffId: 1000,
    mainTarget: _mainTarget,
    cur: _mainTarget,                      // 当前绘制目标
    blendOp: 'source-over',                // 当前混合模式（由 SetBlend 推送）
    blendRate: 1,                          // 当前混合速率（HIGHLIGHT 等用于缩放 alpha）
};

// 资源 / 主机共享状态
export const host = {
    assets: new Map<string, Uint8Array>(),  // url -> Uint8Array
    // 由引导入口 main.ts 在 getAssemblyExports 之后写成真实导出；在此之前是空对象，
    // 事件回调里用可选链访问（如 input.ts 的 overlay()），不会因时序问题报错。
    exports: {} as DotNetExports,
};

// 音频共享状态
export const audio = {
    ctx: null as AudioContext | null,
    nextId: 1,
    active: new Map<number, AudioEntry>(),   // id -> { src, gain, canceled, volume }
    buffers: new Map<string, AudioBuffer>(), // url -> AudioBuffer（解码结果缓存）
};

// ---------------- 工具函数 ----------------

/**
 * 32 位 ARGB 颜色（0xAARRGGBB，C# int）转 CSS rgba() 字符串。
 * @param argb ARGB 颜色
 * @returns 形如 "rgba(r,g,b,a)" 的 CSS 颜色串
 */
export const toCss = (argb: number): string => {
    const a = (argb >>> 24) & 0xFF;
    const r = (argb >>> 16) & 0xFF;
    const g = (argb >>> 8) & 0xFF;
    const b = argb & 0xFF;
    return `rgba(${r},${g},${b},${(a / 255).toFixed(3)})`;
};

// 同步按需拉取 URL 字节（XMLHttpRequest 同步模式），带缓存。
// 注意：同步 XHR 不允许 responseType='arraybuffer'（会抛 InvalidAccessError），
// 故改用 x-user-defined 文本编码逐字节还原二进制，这是同步取二进制的可靠方式。
/**
 * 同步拉取 URL 字节并写入 host.assets 缓存。
 * @param url 资源地址（空格自动转义为 %20）
 * @returns 字节内容；网络失败或非 200 返回 null
 */
export function fetchBytes(url: string): Uint8Array | null {
    const cached = host.assets.get(url);
    if (cached) return cached;
    try {
        const xhr = new XMLHttpRequest();
        xhr.open('GET', url.replace(/ /g, '%20'), false);
        xhr.overrideMimeType('text/plain; charset=x-user-defined');
        xhr.send();
        if (xhr.status !== 200) return null;
        const text = xhr.responseText;
        const len = text.length;
        const data = new Uint8Array(len);
        for (let i = 0; i < len; i++)
            data[i] = text.charCodeAt(i) & 0xFF;
        host.assets.set(url, data);
        return data;
    } catch (e) { return null; }
}

// 异步预取一组 URL 到资源缓存（不阻塞主线程）。
// 预取完成后，同步 getBytes 命中缓存即瞬时返回，避免加载 DB / 资源时因同步 XHR 冻结主线程导致心跳超时。
/**
 * 异步预取一批资源到 host.assets 缓存。
 * @param urls 资源地址列表
 */
export async function preloadAssets(urls: string[]): Promise<void> {
    const tasks: Promise<void>[] = [];
    for (const raw of urls) {
        if (!raw || host.assets.has(raw)) continue;
        const fetchUrl = raw.replace(/ /g, '%20');
        tasks.push(
            fetch(fetchUrl)
                .then(r => (r.ok ? r.arrayBuffer() : null))
                .then(buf => { if (buf) host.assets.set(raw, new Uint8Array(buf)); })
                .catch(() => { /* 预取失败则回退同步 XHR，不影响流程 */ })
        );
    }
    await Promise.all(tasks);
}

/**
 * 取（必要时创建并 resume）全局 AudioContext。
 * @returns AudioContext；浏览器不支持时返回 null
 */
export function ensureAudio(): AudioContext | null {
    let ctx = audio.ctx;
    if (!ctx) {
        const AC = window.AudioContext || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
        if (!AC) return null;
        ctx = new AC();
        audio.ctx = ctx;
    }
    if (ctx.state === 'suspended') ctx.resume();
    return ctx;
}

// 把 DOM 客户端坐标换算成画布内部像素坐标
/**
 * DOM 客户端坐标 -> 画布内部像素坐标。
 * @param e 带 clientX / clientY 的鼠标事件
 * @returns [x, y] 画布像素坐标
 */
export function canvasToClient(e: { clientX: number; clientY: number }): [number, number] {
    const r = dom.canvas.getBoundingClientRect();
    return [
        Math.round((e.clientX - r.left) * dom.canvas.width / r.width),
        Math.round((e.clientY - r.top) * dom.canvas.height / r.height),
    ];
}
