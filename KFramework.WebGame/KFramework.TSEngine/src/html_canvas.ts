// 【依赖 C#】由 KFramework.MonoGame.JSBind_HTML_Canvas 经 [JSImport(module: "canvas")] 调用；产物 html_canvas.js 由 SyncJsEngine 复制。
// 画布元素管理层（HTML5 <canvas>）：创建、设置位置与尺寸、删除，以及「页面里没有画布时自动建一块全屏默认画布」。
//
// 与 gl.ts 的分工：
//   · 本文件只管 **DOM 元素**（canvas 有没有、插在哪、CSS 多大、删没删）；
//   · gl.ts 只管 **WebGL2 上下文**（从本文件给出的元素上 getContext('webgl2')）。
// 因此 gl.ts 不再自己 querySelector，避免出现两套查找 / 创建规则。
//
// 尺寸链路（改动这里之前请先读 platform.js 的 SyncJSCanvasInfo 注释）：
//   CSS 尺寸（本文件写的 style.width / style.height）
//     → SyncJSCanvasInfo 每帧读 rect × DPR 写进 canvas.width / canvas.height（backing）
//     → C# 侧 GraphicsDevice.SyncCanvasSize 更新 PresentationParameters 与 Viewport，并触发 GameWindow.SizeChanged。
// 所以本文件只改 CSS，backing / 窗口 / 输入坐标（input_common 按 rect 换算）会在下一帧自动跟上。

/** 唯一的画布元素（单画布模型）。由本模块统一持有，删除时置空。 */
let canvas: HTMLCanvasElement | null = null;

/** 处于「居中模式」时的画布尺寸：窗口变化时自动重新居中（浏览器窗口缩放）。 */
let centered: { width: number; height: number } | null = null;

/** 未指定画布时的默认 DOM id（与 C# 侧 Game 的默认选择器 "#game" 对齐）。 */
export const DEFAULT_CANVAS_ID = 'game';

/**
 * 把 "#game" 这类选择器归一化成 DOM id；空串回落到默认 id。
 * C# 侧传进来的永远是选择器或 id 字符串，这里做一次统一处理。
 */
function toId(idOrSelector?: string | null): string {
    const trimmed = (idOrSelector ?? '').trim().replace(/^#/, '');
    return trimmed.length > 0 ? trimmed : DEFAULT_CANVAS_ID;
}

/** 所有画布共用的样式（全屏与矩形定位只有 left / top / width / height 不同）。 */
function applyCommonStyle(): void {
    let element = canvas!
    element.style.position = 'fixed';
    element.style.display = 'block';
    element.style.margin = '0';
    element.style.padding = '0';
    element.style.outline = 'none';
    element.style.touchAction = 'none';
    element.style.zIndex = '0';
}

/** 填满整个 HTML 页面（100% / inset 效果由 left/top=0 + width/height=100% 实现）。 */
function applyFullscreenStyle(): void {
    let element = canvas!
    applyCommonStyle();
    element.style.left = '0px';
    element.style.top = '0px';
    element.style.width = '100%';
    element.style.height = '100%';
}

/** 绝对定位到 (x, y)，CSS 尺寸为 width × height（单位：CSS 像素，最小 1px）。 */
function applyRectStyle(x: number, y: number, width: number, height: number): void 
{
    let element = canvas!
    applyCommonStyle();
    element.style.left = `${Math.round(x)}px`;
    element.style.top = `${Math.round(y)}px`;
    element.style.width = `${Math.max(1, Math.round(width))}px`;
    element.style.height = `${Math.max(1, Math.round(height))}px`;
}

/** 把画布摆到窗口正中（按 CSS 尺寸），位置取自 window.innerWidth / innerHeight —— 不是画布自己的尺寸。 */
function applyCentered(width: number, height: number): void 
{
    let element = canvas!
    const w = Math.max(1, Math.round(width));
    const h = Math.max(1, Math.round(height));

    element.style.width = `${w}px`;
    element.style.height = `${h}px`;
    element.style.left = `${Math.max(0, Math.round((window.innerWidth - w) / 2))}px`;
    element.style.top = `${Math.max(0, Math.round((window.innerHeight - h) / 2))}px`;
}

// 窗口变了就把居中画布重新居中：否则浏览器一缩放，原本居中的画布就偏了。
window.addEventListener('resize', () => 
{
    if (centered && canvas?.isConnected) applyCentered(canvas, centered.width, centered.height);
    else centered = null;
});

let _int32Scratch = new Int32Array(8);
function writeInts(view: MemoryView_Span | Int32Array, values: number[]): void 
{
    if (_int32Scratch.length < values.length) _int32Scratch = new Int32Array(values.length);
    _int32Scratch.set(values);
    const slice = _int32Scratch.subarray(0, values.length);
    if (view instanceof Int32Array) {
        view.set(slice);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(slice, 0);
        return;
    }
    const fallback = view as unknown as Record<number, number>;
    for (let i = 0; i < values.length; i++) fallback[i] = values[i];
}

function lookup(id: string): HTMLCanvasElement | null 
{
    if (canvas) return canvas;
    const byDom = document.getElementById(id);
    if (byDom instanceof HTMLCanvasElement) {
        canvas = byDom;
        return byDom;
    }
    return null;
}

export function applyLayout(
    mode: number,
    x: number,
    y: number,
    width: number,
    height: number
): void 
{
    let element = canvas!;
    switch (mode) {
        case LayoutMode.Size:
            centered = null;
            element.style.width = `${Math.max(1, Math.round(width))}px`;
            element.style.height = `${Math.max(1, Math.round(height))}px`;
            break;

        case LayoutMode.Centered:
            applyCommonStyle();
            applyCentered(width, height);
            centered = { width: Math.max(1, Math.round(width)), height: Math.max(1, Math.round(height)) };
            break;

        case LayoutMode.Fullscreen:
            centered = null;
            applyFullscreenStyle();
            break;

        default:
            centered = null;
            applyRectStyle(x, y, width, height);
            break;
    }
}

export function create(idOrSelector: string, mode: number, x: number, y: number, width: number, height: number): boolean {
    const id = toId(idOrSelector);
    canvas = lookup(id)
    if(canvas == null)
    {
        const element = document.createElement('canvas');
        element.id = id;
        document.body.appendChild(element);
        canvas = element;
    }
    applyCommonStyle();
    applyLayout(mode, x, y, width, height);
    return true;
}


const enum LayoutMode {
    Rect = 0,
    Size = 1,
    Centered = 2,
    Fullscreen = 3,
}

/**
 * 撤销本模块写在画布上的行内样式，让页面自己的 CSS（例如 <canvas> 的 width:100%）重新生效。
 * 用于「恢复启动时的默认布局」，同时解除居中模式。
 */
export function restoreLayout(): boolean {
    const element = canvas!;
    if (!element) return false;
    centered = null;
    for (const property of ['position', 'left', 'top', 'width', 'height', 'display', 'margin', 'padding', 'outline', 'touch-action', 'z-index']) {
        element.style.removeProperty(property);
    }
    return true;
}

export function getRect(view: MemoryView_Span | Int32Array): void 
{
    const element = canvas!;
    if (!element) {
        writeInts(view, [0, 0, 0, 0]);
        return;
    }

    const rect = element.getBoundingClientRect();
    writeInts(view, [
        Math.round(rect.left),
        Math.round(rect.top),
        Math.round(rect.width),
        Math.round(rect.height),
    ]);
}

export function getCanvas(): HTMLCanvasElement | null 
{
    return canvas;
}

/**
 * 让画布可获焦 / 取消获焦：绑在 <canvas> 上的 keydown/keyup 只有在画布获焦时才会触发，
 * 而 <canvas> 默认 tabindex 为 -1（不可获焦），所以监听键盘前要 focusCanvas(id, true)。
 * @param idOrSelector 画布 id（空 / 缺省 / null → 默认 id）。
 * @param focus true（默认）：设 tabindex=0 并聚焦（键盘事件派发到画布）；false：失焦并移除 tabindex。
 */
export function focusCanvas(idOrSelector?: string | null, focus: boolean = true): void
{
    const c = getCanvas();
    if (!c) return;
    if (focus)
    {
        c.tabIndex = 0;
        c.focus();
    }
    else
    {
        c.blur();
        c.removeAttribute('tabindex');
    }
}

export function IsFocus(idOrSelector?: string | null) 
{
    const c = getCanvas();
    console.assert(c != null, "canvas == null")
    return document.activeElement === c
}

export function SyncJSCanvasInfo(view: MemoryView_Span | Int32Array): void {
    const canvas = getCanvas();
    if (!canvas) {
        writeInts(view, [1, 1, 1, 1, 1000]);
        return;
    }

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const cssWidth = Math.max(1, Math.round(canvas.clientWidth || 1));
    const cssHeight = Math.max(1, Math.round(canvas.clientHeight || 1));
    const drawWidth = Math.max(1, Math.round(cssWidth * dpr));
    const drawHeight = Math.max(1, Math.round(cssHeight * dpr));

    if (canvas.width !== drawWidth || canvas.height !== drawHeight) {
        canvas.width = drawWidth;
        canvas.height = drawHeight;
    }
    
    writeInts(view, [cssWidth, cssHeight, drawWidth, drawHeight, Math.round(dpr * 1000)]);
}
