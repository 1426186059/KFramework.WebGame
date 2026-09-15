// 平台层：画布尺寸自适应、requestAnimationFrame 主循环、浏览器环境查询。
//
// 输入已独立成 ./input.ts（薄绑定层），由 C# 侧 JSBind_Input 单独对接模块名 "input"。
import { getCanvasElement } from './gl.js';
let frameCallback = null;
let running = false;
export function setFrameCallback(callback) {
    frameCallback = callback;
}
export function startRenderLoop() {
    if (running)
        return;
    running = true;
    const tick = (timestamp) => {
        if (!running)
            return;
        try {
            frameCallback?.(timestamp);
        }
        catch (error) {
            console.error('[platform] 帧回调异常:', error);
        }
        requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
}
export function stopRenderLoop() {
    running = false;
}
// ---------- 画布尺寸 ----------
function writeInts(view, values) {
    const array = new Int32Array(values);
    if (view instanceof Int32Array) {
        view.set(array);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(array, 0);
        return;
    }
    const fallback = view;
    for (let i = 0; i < values.length; i++)
        fallback[i] = values[i];
}
export function getCanvasSize(view) {
    const canvas = getCanvasElement();
    if (!canvas) {
        writeInts(view, [1, 1, 1, 1, 1000]);
        return;
    }
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = canvas.getBoundingClientRect();
    const cssWidth = Math.max(1, Math.round(rect.width || canvas.clientWidth || 1));
    const cssHeight = Math.max(1, Math.round(rect.height || canvas.clientHeight || 1));
    const drawWidth = Math.max(1, Math.round(cssWidth * dpr));
    const drawHeight = Math.max(1, Math.round(cssHeight * dpr));
    if (canvas.width !== drawWidth || canvas.height !== drawHeight) {
        canvas.width = drawWidth;
        canvas.height = drawHeight;
    }
    writeInts(view, [cssWidth, cssHeight, drawWidth, drawHeight, Math.round(dpr * 1000)]);
}
// ---------- 浏览器环境 ----------
export function setTitle(title) {
    document.title = title;
}
export function getQueryParameter(name) {
    return new URLSearchParams(window.location.search).get(name) ?? '';
}
export function isMobile() {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
}
export function getBaseUri() {
    return document.baseURI || window.location.href;
}
