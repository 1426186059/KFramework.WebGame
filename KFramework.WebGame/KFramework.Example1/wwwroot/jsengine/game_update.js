// 帧循环：requestAnimationFrame 主循环、呈现间隔（限帧）、帧回调注册。
//
// 这些符号由 KFramework.MonoGame.JSBind_Platform 经 [JSImport(module: "platform")] 调用；
// 本文件导出的符号经 ./platform.ts 的 `export * from` 转发，因此对外模块名仍为 "platform"，C# 侧无需改动。
//
// 输入 / 画布 / 环境查询等其余平台绑定见 ./platform.ts。
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
function renderLoopTick(timestamp) {
    if (!running)
        return;
    // 跳过的帧仍然继续排队 rAF：只是这一帧不进游戏循环（逻辑时间按 timestamp 差值照常推进）
    frameCounter++;
    if (frameCounter % frameInterval === 0)
        frameCallback?.(timestamp);
    requestAnimationFrame(renderLoopTick);
}
export function startRenderLoop() {
    if (running)
        return;
    running = true;
    requestAnimationFrame(renderLoopTick);
}
export function stopRenderLoop() {
    running = false;
}
