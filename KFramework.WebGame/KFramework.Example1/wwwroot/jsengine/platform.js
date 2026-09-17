// 【依赖 C#】由 KFramework.MonoGame.JSBind_Platform 经 [JSImport(module: "platform")] 调用；产物 platform.js 由 SyncJsEngine 复制。
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
// 复用的整数缓冲，避免每帧 getCanvasSize 都 new Int32Array（减少 GC 抖动）。
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
