// 【依赖 C#】本文件是 JSBind_Input 三个输入模块（input_keyboard / input_mouse / input_touch）的公共工具，自身不被 [JSImport] 直接调用。
// 输入模块的公共工具：坐标换算 + 缓冲写回。
// 键盘 / 鼠标 / 触摸三个模块共用，本文件自身不注册任何监听、不持有状态。

import { getCanvasElement } from './gl.js';

/**
 * 浏览器客户端坐标（CSS 像素）→ 画布后备缓冲（backing）像素坐标。
 *
 * 一个 HTML <canvas> 同时有两个尺寸，浏览器会在二者之间自动缩放（画面始终铺满，视觉无错位）：
 *   1. CSS 尺寸（显示尺寸）：页面布局给你的框，即 rect.width / rect.height。
 *      —— 鼠标 / 触摸事件（clientX - rect.left）就落在这个坐标系里，单位是 CSS 像素。
 *   2. Backing 尺寸（绘制缓冲）：canvas.width / canvas.height，即 GPU 真正光栅化的像素。
 *      —— platform.js 设为 Math.round(CSS × dpr)，且 GraphicsDevice 的 Viewport / BackBufferWidth
 *         用的就是它，所以【渲染坐标系用的也是 backing 像素】。
 *
 * 两者的比例就是 DPR（高 DPI 下 backing 是 CSS 的 DPR 倍，典型 1.5 / 2 / 3）。
 * 若直接把 CSS 坐标交给渲染侧，会落在左上角 1/DPR 区域（偏左上），所以这里乘上比例换算过去；
 * DPR=1 时比例就是 1，行为与不加这层完全一致。
 */
export function canvasPoint(clientX: number, clientY: number): [number, number] {
    const canvas = getCanvasElement();
    if (!canvas) return [0, 0];
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    return [Math.round((clientX - rect.left) * scaleX), Math.round((clientY - rect.top) * scaleY)];
}

/** 把本地缓冲写回 C# 传来的目标（MemoryView 或 Uint8Array）。 */
export function copyOut(target: MemoryView | Uint8Array, src: Uint8Array): void {
    if (target instanceof Uint8Array) {
        target.set(src);
        return;
    }
    if (typeof target.set === 'function') {
        target.set(src, 0);
        return;
    }
    const fallback = target as unknown as Record<number, number>;
    for (let i = 0; i < src.length; i++) fallback[i] = src[i];
}
