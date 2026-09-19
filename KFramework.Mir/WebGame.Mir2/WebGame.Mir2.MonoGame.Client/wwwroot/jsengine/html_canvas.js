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
// 所以本文件只改 CSS，backing / 视口 / 输入坐标（input_common 按 rect 换算）会在下一帧自动跟上。
/** id → 画布元素。由本模块统一持有，删除时同步摘除。 */
const canvases = new Map();
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
/** 铺满视口（100% / inset 效果由 left/top=0 + width/height=100% 实现）。 */
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
 * 创建一块全屏画布并插入 <body>。
 * @param idOrSelector 画布 id（可写 "#id" 形式）；已存在同名画布时返回 false。
 */
export function createFullscreen(idOrSelector) {
    const id = toId(idOrSelector);
    if (lookup(id))
        return false;
    const element = document.createElement('canvas');
    element.id = id;
    applyFullscreenStyle(element);
    document.body.appendChild(element);
    canvases.set(id, element);
    return true;
}
/**
 * 在指定位置创建一块画布并插入 <body>。
 * @param idOrSelector 画布 id（可写 "#id" 形式）；已存在同名画布时返回 false。
 * @param x 左上角 X（CSS 像素，相对视口）。
 * @param y 左上角 Y（CSS 像素，相对视口）。
 * @param width CSS 宽度（像素）。
 * @param height CSS 高度（像素）。
 */
export function create(idOrSelector, x, y, width, height) {
    const id = toId(idOrSelector);
    if (lookup(id))
        return false;
    const element = document.createElement('canvas');
    element.id = id;
    applyRectStyle(element, x, y, width, height);
    document.body.appendChild(element);
    canvases.set(id, element);
    return true;
}
/** 设置已有画布的位置与 CSS 尺寸。没有这块画布时返回 false。 */
export function setRect(idOrSelector, x, y, width, height) {
    const element = lookup(toId(idOrSelector));
    if (!element)
        return false;
    applyRectStyle(element, x, y, width, height);
    return true;
}
/** 把已有画布恢复为铺满视口。没有这块画布时返回 false。 */
export function setFullscreen(idOrSelector) {
    const element = lookup(toId(idOrSelector));
    if (!element)
        return false;
    applyFullscreenStyle(element);
    return true;
}
/** 删除画布：从 DOM 移除并注销。没有这块画布时返回 false。 */
export function destroy(idOrSelector) {
    const id = toId(idOrSelector);
    const element = lookup(id);
    if (!element)
        return false;
    element.remove();
    canvases.delete(id);
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
export function resolveCanvasElement(idOrSelector) {
    const id = toId(idOrSelector);
    const existing = lookup(id);
    if (existing)
        return existing;
    if (!createFullscreen(id))
        return null;
    return canvases.get(id) ?? null;
}
