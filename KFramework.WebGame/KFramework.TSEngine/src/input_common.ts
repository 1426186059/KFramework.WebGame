// 【依赖 C#】本文件是 JSBind_Input_Keyboard / JSBind_Input_Mouse / JSBind_Input_Touch 三个输入模块（input_keyboard / input_mouse / input_touch）的公共工具，自身不被 [JSImport] 直接调用。
// 输入模块的公共工具：坐标换算 + 缓冲写回。
// 键盘 / 鼠标 / 触摸三个模块共用，本文件自身不注册任何监听、不持有状态。

// 画布的权威来源是 html_canvas（它持有 id → 元素的注册表，且会回退到 document.getElementById）。
// 早先这里从 render_webgl20 取画布 —— 那意味着【所有输入模块都依赖 WebGL 上下文已初始化】：
// 纯 WebGPU 的应用从不建 WebGL 上下文，getCanvasElement() 恒为 null，
// 于是 bindMouse 绑不上事件（点击全失效），canvasPoint 也换算不出坐标。
import { getCanvas } from './html_canvas.js';

/**
 * 当前输入画布的 id（由 bindMouse / bindTouch 在绑定时写入，空串表示用默认 id）。
 * 之所以要单独记：C# 侧 GraphicsDevice.CanvasId 才是权威值，而鼠标 / 触摸绑定需要知道绑到哪块画布。
 */
let currentCanvasId = '';

/** 记录当前输入画布 id（空串回落到默认 id）。 */
export function setCanvasId(id: string): void {
    currentCanvasId = id;
}

/** 取当前输入画布（供各输入模块与坐标换算共用）。 */
export function getInputCanvas(): HTMLCanvasElement | null {
    return getCanvas(currentCanvasId);
}

/**
 * 浏览器客户端坐标（CSS 像素）→ 画布后备缓冲（backing）像素坐标。
 *
 * 一个 HTML <canvas> 同时有两个尺寸，浏览器会在二者之间自动缩放（画面始终铺满，视觉无错位）：
 *   1. CSS 尺寸（显示尺寸）：页面布局给你的框，即 rect.width / rect.height。
 *      —— 鼠标 / 触摸事件（clientX - rect.left）就落在这个坐标系里，单位是 CSS 像素。
 *   2. Backing 尺寸（绘制缓冲）：canvas.width / canvas.height，即 GPU 真正光栅化的像素。
 *      —— platform.js 的 getCanvasSize 把 backing 尺寸算作 Math.round(CSS尺寸 × dpr)（dpr 取系统
 *         window.devicePixelRatio，仅被 Math.min 掐到最多 2 这个硬上限；比值本身来自系统，程序设不了），
 *         写进 size[2]/size[3]（绘制缓冲宽/高，见 JSBind_Platform.GetCanvasSize 的 out 布局注释）；
 *         之后 GraphicsDevice.SyncCanvasSize 把这两个值直接赋给 PresentationParameters.BackBufferWidth/Height
 *         和 Viewport(0,0,width,height)，所以【渲染坐标系的原点和范围就是 0..canvas.width × 0..canvas.height，
 *         单位全是 backing 像素】。
 *
 * 两者的比例就是 DPR（高 DPI 下 backing 是 CSS 的 DPR 倍，典型 1.5 / 2 / 3）。
 * 若直接把 CSS 坐标交给渲染侧，会落在左上角 1/DPR 区域（偏左上），所以这里乘上比例换算过去；
 * DPR=1 时比例就是 1，行为与不加这层完全一致。
 */
export function canvasPoint(clientX: number, clientY: number): [number, number] {
    const canvas = getInputCanvas();
    if (!canvas) return [0, 0];
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    return [Math.round((clientX - rect.left) * scaleX), Math.round((clientY - rect.top) * scaleY)];
}