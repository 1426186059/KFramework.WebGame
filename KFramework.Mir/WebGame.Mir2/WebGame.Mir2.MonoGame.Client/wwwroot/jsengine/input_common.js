// 【依赖 C#】本文件是 JSBind_Input 三个输入模块（input_keyboard / input_mouse / input_touch）的公共工具，自身不被 [JSImport] 直接调用。
// 输入模块的公共工具：坐标换算 + 缓冲写回。
// 键盘 / 鼠标 / 触摸三个模块共用，本文件自身不注册任何监听、不持有状态。
import { getCanvasElement } from './gl.js';
/**
 * 浏览器客户端坐标 → 画布后备缓冲（绘制）像素坐标。
 *
 * 关键：必须乘 DPR。platform.getCanvasSize 把 canvas.width/height 设成
 * cssWidth*dpr / cssHeight*dpr（高 DPI 下 backing 大于 CSS），而游戏渲染、
 * 控件坐标（MirScene 以 1:1 画进 backing 视口）全部基于后备缓冲像素。若只做
 * clientX - rect.left（CSS 像素），鼠标坐标会整体偏小 DPR 倍，导致命中测试
 * IsMouseOver(CMain.MPoint) 全部落空（按钮点不动）且光标画在偏移处（看似不显示）。
 */
export function canvasPoint(clientX, clientY) {
    const canvas = getCanvasElement();
    if (!canvas)
        return [0, 0];
    const rect = canvas.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0)
        return [0, 0];
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return [Math.round((clientX - rect.left) * scaleX), Math.round((clientY - rect.top) * scaleY)];
}
/** 把本地缓冲写回 C# 传来的目标（MemoryView 或 Uint8Array）。 */
export function copyOut(target, src) {
    if (target instanceof Uint8Array) {
        target.set(src);
        return;
    }
    if (typeof target.set === 'function') {
        target.set(src, 0);
        return;
    }
    const fallback = target;
    for (let i = 0; i < src.length; i++)
        fallback[i] = src[i];
}
