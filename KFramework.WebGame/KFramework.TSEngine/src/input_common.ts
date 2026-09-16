// 【依赖 C#】本文件是 JSBind_Input 三个输入模块（input_keyboard / input_mouse / input_touch）的公共工具，自身不被 [JSImport] 直接调用。
// 输入模块的公共工具：坐标换算 + 缓冲写回。
// 键盘 / 鼠标 / 触摸三个模块共用，本文件自身不注册任何监听、不持有状态。

import { getCanvasElement } from './gl.js';

/**
 * 浏览器客户端坐标 → 画布 CSS 像素坐标。
 * 只减去 rect 偏移，不做 dpr 换算 —— dpr 怎么处理由 C# 决定。
 */
export function canvasPoint(clientX: number, clientY: number): [number, number] {
    const canvas = getCanvasElement();
    if (!canvas) return [0, 0];
    const rect = canvas.getBoundingClientRect();
    return [Math.round(clientX - rect.left), Math.round(clientY - rect.top)];
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
