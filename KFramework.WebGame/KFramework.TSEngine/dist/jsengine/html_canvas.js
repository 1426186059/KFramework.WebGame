// 【依赖 C#】由 KFramework.MonoGame.JSBind_HTML_Canvas 经 [JSImport(module: "canvas")] 调用；产物 html_canvas.js 由 SyncJsEngine 复制。
// 画布元素管理层（HTML5 <canvas>）：创建、设置位置与尺寸、删除，以及「页面里没有画布时自动建一块全屏默认画布」。
//
// 与 gl.ts 的分工：
//   · 本文件只管 **DOM 元素**（canvas 有没有、插在哪、CSS 多大、删没删）；
//   · gl.ts 只管 **WebGL2 上下文**（从本文件给出的元素上 getContext('webgl2')）。
// 因此 gl.ts 不再自己 querySelector，避免出现两套查找 / 创建规则。
//
// 尺寸链路（改动这里之前请先读 platform.js 的 getCanvasSize 注释）：
//   CSS 尺寸（本文件写的 style.width / style.height）
//     → platform.getCanvasSize 每帧读 rect × DPR 写进 canvas.width / canvas.height（backing）
//     → C# 侧 GraphicsDevice.SyncCanvasSize 更新 PresentationParameters 与 Viewport，并触发 GameWindow.SizeChanged。
// 所以本文件只改 CSS，backing / 窗口 / 输入坐标（input_common 按 rect 换算）会在下一帧自动跟上。
/** id → 画布元素。由本模块统一持有，删除时同步摘除。 */
const canvases = new Map();
/** 处于「居中模式」的画布：窗口变化时自动重新居中（浏览器窗口缩放）。 */
const centered = new Map();
/** 未指定画布时的默认 DOM id（与 C# 侧 Game 的默认选择器 "#game" 对齐）。 */
export const DEFAULT_CANVAS_ID = 'game';
/**
 * 把 "#game" 这类选择器归一化成 DOM id；空串回落到默认 id。
 * C# 侧传进来的永远是选择器或 id 字符串，这里做一次统一处理。
 */
function toId(idOrSelector) {
    const trimmed = (idOrSelector ?? '').trim().replace(/^#/, '');
    return trimmed.length > 0 ? trimmed : DEFAULT_CANVAS_ID;
}
/** 所有画布共用的样式（全屏与矩形定位只有 left / top / width / height 不同）。 */
function applyCommonStyle(element) {
    element.style.position = 'fixed';
    element.style.display = 'block';
    element.style.margin = '0';
    element.style.padding = '0';
    element.style.outline = 'none';
    element.style.touchAction = 'none';
    element.style.zIndex = '0';
}
/** 填满整个 HTML 页面（100% / inset 效果由 left/top=0 + width/height=100% 实现）。 */
function applyFullscreenStyle(element) {
    applyCommonStyle(element);
    element.style.left = '0px';
    element.style.top = '0px';
    element.style.width = '100%';
    element.style.height = '100%';
}
/** 绝对定位到 (x, y)，CSS 尺寸为 width × height（单位：CSS 像素，最小 1px）。 */
function applyRectStyle(element, x, y, width, height) {
    applyCommonStyle(element);
    element.style.left = `${Math.round(x)}px`;
    element.style.top = `${Math.round(y)}px`;
    element.style.width = `${Math.max(1, Math.round(width))}px`;
    element.style.height = `${Math.max(1, Math.round(height))}px`;
}
/** 把画布摆到窗口正中（按 CSS 尺寸），位置取自 window.innerWidth / innerHeight —— 不是画布自己的尺寸。 */
function applyCentered(element, width, height) {
    const w = Math.max(1, Math.round(width));
    const h = Math.max(1, Math.round(height));
    element.style.width = `${w}px`;
    element.style.height = `${h}px`;
    element.style.left = `${Math.max(0, Math.round((window.innerWidth - w) / 2))}px`;
    element.style.top = `${Math.max(0, Math.round((window.innerHeight - h) / 2))}px`;
}
// 窗口变了就把居中画布重新居中：否则浏览器一缩放，原本居中的画布就偏了。
window.addEventListener('resize', () => {
    centered.forEach((size, id) => {
        const element = canvases.get(id);
        if (element?.isConnected)
            applyCentered(element, size.width, size.height);
        else
            centered.delete(id);
    });
});
/** 写回整数缓冲（与 platform.js 同一套 MemoryView 处理）。 */
let _int32Scratch = new Int32Array(8);
function writeInts(view, values) {
    if (_int32Scratch.length < values.length)
        _int32Scratch = new Int32Array(values.length);
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
    const fallback = view;
    for (let i = 0; i < values.length; i++)
        fallback[i] = values[i];
}
function lookup(id) {
    const known = canvases.get(id);
    if (known)
        return known;
    const byDom = document.getElementById(id);
    if (byDom instanceof HTMLCanvasElement) {
        canvases.set(id, byDom);
        return byDom;
    }
    return null;
}
/**
 * 把布局方式应用到画布元素上（新建或直接套用共享同一套逻辑）。
 * @param mode 0 Rect / 1 Size / 2 Centered / 3 Fullscreen。
 */
function applyLayoutStyle(element, mode, x, y, width, height, id) {
    switch (mode) {
        case 1 /* LayoutMode.Size */:
            // 只改尺寸：位置保持不动
            centered.delete(id);
            element.style.width = `${Math.max(1, Math.round(width))}px`;
            element.style.height = `${Math.max(1, Math.round(height))}px`;
            break;
        case 2 /* LayoutMode.Centered */:
            applyCommonStyle(element);
            applyCentered(element, width, height);
            centered.set(id, { width: Math.max(1, Math.round(width)), height: Math.max(1, Math.round(height)) });
            break;
        case 3 /* LayoutMode.Fullscreen */:
            centered.delete(id);
            applyFullscreenStyle(element);
            break;
        default:
            // LayoutMode.Rect：手动摆位后不再跟随窗口居中
            centered.delete(id);
            applyRectStyle(element, x, y, width, height);
            break;
    }
}
/**
 * 创建一块画布并插入 <body>。
 * @param idOrSelector 画布 id（可写 "#id" 形式）；已存在同名画布时返回 false。
 * @param mode 布局方式（LayoutMode）：Rect 按 x/y/w/h 摆位；Centered 窗口居中；Fullscreen 填满整个 HTML 页面；Size 仅设尺寸（新建时回落到 0,0）。
 * @param x 左上角 X（CSS 像素，相对窗口），Rect 模式使用。
 * @param y 左上角 Y（CSS 像素，相对窗口），Rect 模式使用。
 * @param width CSS 宽度（像素）。
 * @param height CSS 高度（像素）。
 */
export function create(idOrSelector, mode, x, y, width, height) {
    const id = toId(idOrSelector);
    if (lookup(id))
        return false;
    const element = document.createElement('canvas');
    element.id = id;
    applyCommonStyle(element);
    applyLayoutStyle(element, mode, x, y, width, height, id);
    document.body.appendChild(element);
    canvases.set(id, element);
    return true;
}
/**
 * 【通用布局入口】一次函数表达 Rect / Size / Centered / Fullscreen 四种布局。
 * 上层（C# 的 HTML_Canvas.SetLayout）只需这一个 channel 就能表达全部摆位需求。
 */
export function applyLayout(idOrSelector, mode, x, y, width, height) {
    const id = toId(idOrSelector);
    const element = lookup(id);
    if (!element)
        return false;
    applyLayoutStyle(element, mode, x, y, width, height, id);
    return true;
}

/**
 * 撤销本模块写在画布上的行内样式，让页面自己的 CSS（例如 <canvas> 的 width:100%）重新生效。
 * 用于「恢复启动时的默认布局」，同时解除居中模式。
 */
export function restoreLayout(idOrSelector) {
    const id = toId(idOrSelector);
    const element = lookup(id);
    if (!element)
        return false;
    centered.delete(id);
    for (const property of ['position', 'left', 'top', 'width', 'height', 'display', 'margin', 'padding', 'outline', 'touch-action', 'z-index']) {
        element.style.removeProperty(property);
    }
    return true;
}
/** 读 HTML 页面尺寸（window.innerWidth / innerHeight），常用于自己算居中位置。写入 [宽, 高]。 */
export function getHTMLPageSize(view) {
    writeInts(view, [Math.round(window.innerWidth), Math.round(window.innerHeight)]);
}
/**
 * 读画布当前的实际矩形（不含边框）：写入 [left, top, width, height]，单位 CSS 像素，坐标相对窗口左上角。
 * 画布不存在时写入全 0 —— 调用方可以据此判断「还没这块画布」。
 */
export function getRect(idOrSelector, view) {
    const element = lookup(toId(idOrSelector));
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
/** 删除画布：从 DOM 移除并注销。没有这块画布时返回 false。 */
export function destroy(idOrSelector) {
    const id = toId(idOrSelector);
    const element = lookup(id);
    if (!element)
        return false;
    element.remove();
    canvases.delete(id);
    centered.delete(id);
    return true;
}
/** 该画布是否存在（页面上已有、或由本模块创建过）。 */
export function exists(idOrSelector) {
    return lookup(toId(idOrSelector)) !== null;
}
/** 取画布元素本身（给 gl.ts 初始化上下文用，不由 C# 直接调用）。 */
export function getCanvas(idOrSelector) {
    return lookup(toId(idOrSelector));
}
/**
 * 解析「游戏要用哪块画布」：页面里已有同 id 的元素就直接用；
 * 没有则由引擎自建一块全屏默认画布——这就是「游戏启动时自动创建默认全屏画布」的入口。
 * @param idOrSelector 画布 id 或选择器（空串 / 缺省 → 默认 id）。
 * @returns 画布元素；无法创建时为 null。
 */
export function getOrCreateCanvasElement(idOrSelector) {
    const id = toId(idOrSelector);
    const existing = lookup(id);
    if (existing)
        return existing;
    if (!create(id, 3 /* LayoutMode.Fullscreen */, 0, 0, 0, 0))
        return null;
    return canvases.get(id) ?? null;
}
