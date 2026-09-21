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
// ---------- 呈现间隔（对应 MonoGame 的 swapInterval） ----------
// 照 MonoGame：PresentationInterval → GL 的 swapInterval（Graphics/GraphicsExtensions.cs 的 GetSwapInterval：
// Immediate=0 / One=1 / Two=2 / Default=-1）。浏览器没有 swapInterval 可调，但 requestAnimationFrame
// 本身就是垂直同步，所以等价实现是「每 N 个 rAF 才回调一帧」：1=每个垂直同步都画，2=隔一个（半帧率）。
let frameInterval = 1;
let frameCounter = 0;
/**
 * 设置呈现间隔：每 N 个 rAF 回调一帧（N ≥ 1）。
 * 由 GraphicsDeviceManager.ApplyChanges 按 PresentationInterval 下发。
 */
export function setFrameInterval(interval) {
    frameInterval = Math.max(1, Math.min(8, Math.round(interval) || 1));
}
/** 当前呈现间隔（1 = 每个垂直同步都画）。 */
export function getFrameInterval() {
    return frameInterval;
}
export function startRenderLoop() {
    if (running)
        return;
    running = true;
    const tick = (timestamp) => {
        if (!running)
            return;
        try {
            // 跳过的帧仍然继续排队 rAF：只是这一帧不进游戏循环（逻辑时间按 timestamp 差值照常推进）
            frameCounter++;
            if (frameCounter % frameInterval === 0)
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

/** 在浏览器中打开一个 URL（新标签）。用于跳转支付页、官网等外部链接。 */
export function openUrl(url) {
    window.open(url, "_blank");
}
