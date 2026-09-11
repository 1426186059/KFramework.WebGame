// mirengine/core/mouse.ts
// 鼠标输入。对应 JSBind/BrowserMouse.cs（mir.mouseAttach / mouseDetach）。
// 仅做 DOM 事件 -> BrowserMouse 导出方法的转发；按钮 -> MouseButtons 的语义翻译在 C# 端完成。
import { host, dom, canvasToClient } from '../shared.js';
let attached = false;
/**
 * 画布上的 mousedown -> BrowserMouse.OnMouseDown。
 * @param e 鼠标事件
 */
function onMouseDown(e) {
    // 中键会触发浏览器自动滚动、右键会触发拖拽/选择，这里一并屏蔽；
    // 传奇里右键是走路/攻击，左键是攻击/拾取，都不该触发浏览器默认行为。
    if (e.button === 1 || e.button === 2)
        e.preventDefault();
    const [x, y] = canvasToClient(e);
    host.exports.MirEngine.BrowserMouse.OnMouseDown(e.button, x, y);
}
// 屏蔽浏览器右键菜单（否则在游戏里点右键会弹出浏览器"后退/前进/重新加载"面板）。
// 必须挂在 window 捕获阶段：只挂 canvas 时，右键点在画布外的页面留白上照样弹菜单。
/**
 * 吞掉右键菜单。
 * @param e 事件对象
 * @returns false（浏览器端约定：返回 false 即取消默认行为）
 */
function onContextMenu(e) {
    e.preventDefault();
    return false;
}
// 右键按住拖动会被原生拖拽/划选接走（部分浏览器的"鼠标手势"也由此触发，
// 但浏览器的鼠标手势开关只能在浏览器设置里关，页面层拦不住）。
/**
 * 吞掉原生拖拽。
 * @param e 拖拽事件
 * @returns false
 */
function onDragStart(e) {
    e.preventDefault();
    return false;
}
// 画布之外（页面留白）也吞掉中/右键默认行为：中键自动滚动、右键开始划选/拖拽。
// 挂在 window 捕获阶段，比只挂 canvas 覆盖得全。
/**
 * 画布外区域的中/右键默认行为屏蔽。
 * @param e 鼠标事件
 */
function onWindowMouseDown(e) {
    if (e.button === 1 || e.button === 2)
        e.preventDefault();
}
/** 注册鼠标 / 滚轮监听（mir.mouseAttach）。 */
export const mouseAttach = () => {
    if (attached)
        return;
    dom.canvas.addEventListener('mousedown', onMouseDown);
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    dom.canvas.addEventListener('wheel', onWheel, { passive: true });
    window.addEventListener('contextmenu', onContextMenu, true);
    window.addEventListener('dragstart', onDragStart, true);
    window.addEventListener('mousedown', onWindowMouseDown, true);
    attached = true;
};
/** 注销鼠标 / 滚轮监听（mir.mouseDetach）。 */
export const mouseDetach = () => {
    if (!attached)
        return;
    dom.canvas.removeEventListener('mousedown', onMouseDown);
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('mouseup', onMouseUp);
    dom.canvas.removeEventListener('wheel', onWheel);
    window.removeEventListener('contextmenu', onContextMenu, true);
    window.removeEventListener('dragstart', onDragStart, true);
    window.removeEventListener('mousedown', onWindowMouseDown, true);
    attached = false;
};
/**
 * mousemove -> BrowserMouse.OnMouseMove。
 * @param e 鼠标事件
 */
function onMouseMove(e) {
    const [x, y] = canvasToClient(e);
    host.exports.MirEngine.BrowserMouse.OnMouseMove(x, y);
}
/**
 * mouseup -> BrowserMouse.OnMouseUp。
 * @param e 鼠标事件
 */
function onMouseUp(e) {
    const [x, y] = canvasToClient(e);
    host.exports.MirEngine.BrowserMouse.OnMouseUp(e.button, x, y);
}
/**
 * wheel -> BrowserMouse.OnMouseWheel。
 * @param e 滚轮事件
 */
function onWheel(e) {
    const [x, y] = canvasToClient(e);
    host.exports.MirEngine.BrowserMouse.OnMouseWheel(e.deltaY, x, y);
}
