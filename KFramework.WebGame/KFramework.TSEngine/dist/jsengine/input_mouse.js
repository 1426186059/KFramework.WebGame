// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 事件入队。状态与边沿在 C# 侧（Input_Mouse）实现。
//
// 事件格式：每条 5 个 i32（type, button, x, y, wheel）= 20 字节。
// type: 3=MouseDown  4=MouseUp  5=MouseMove  6=Wheel
import { getCanvasElement } from './gl.js';
import { canvasPoint, copyOut } from './input_common.js';
const MAX_EVENTS = 64;
const STRIDE = 20;
const SIZE = 4 + MAX_EVENTS * STRIDE;
const queue = new Int32Array(MAX_EVENTS * 5);
let count = 0;
const registrations = [];
let bound = false;
function on(target, name, handler, options) {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}
function push(type, button, x, y, wheel) {
    if (count >= MAX_EVENTS)
        return;
    const o = count * 5;
    queue[o] = type;
    queue[o + 1] = button;
    queue[o + 2] = x;
    queue[o + 3] = y;
    queue[o + 4] = wheel;
    count++;
}
export function bindMouse() {
    if (bound)
        return;
    const canvas = getCanvasElement();
    if (canvas) {
        // 防止画布在按住拖动时被浏览器当作可拖拽元素/可选文本，进而提前结束“按下”。
        canvas.draggable = false;
        canvas.style.userSelect = 'none';
        canvas.style.webkitUserSelect = 'none';
        canvas.style.touchAction = 'none';
        on(canvas, 'dragstart', (e) => e.preventDefault());
        on(canvas, 'mousemove', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(5, 0, x, y, 0);
            // 阻止按住拖动时的原生文本选择/元素拖拽，避免浏览器在首次 mousemove 时
            // 中断“按下”状态并隐式发出 mouseup，导致 UI 面板无法拖动（单击正常）。
            ev.preventDefault();
        });
        on(canvas, 'mousedown', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(3, ev.button, x, y, 0);
            ev.preventDefault();
        });
        on(canvas, 'wheel', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(6, 0, x, y, Math.sign(ev.deltaY));
            ev.preventDefault();
        }, { passive: false });
        on(canvas, 'contextmenu', (e) => e.preventDefault());
    }
    // 抬起挂 window：在画布外松手也能收到
    on(window, 'mouseup', (e) => {
        const ev = e;
        const [x, y] = canvasPoint(ev.clientX, ev.clientY);
        push(4, ev.button, x, y, 0);
    });
    bound = true;
}
/** 解绑鼠标监听并清空队列。 */
export function unbindMouse() {
    for (const r of registrations)
        r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    count = 0;
    bound = false;
}
const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);
export function pollMouse(target) {
    if (!bound)
        bindMouse();
    view.setInt32(0, count, true);
    for (let i = 0; i < count; i++) {
        const src = i * 5;
        const dst = 4 + i * STRIDE;
        view.setInt32(dst, queue[src], true);
        view.setInt32(dst + 4, queue[src + 1], true);
        view.setInt32(dst + 8, queue[src + 2], true);
        view.setInt32(dst + 12, queue[src + 3], true);
        view.setInt32(dst + 16, queue[src + 4], true);
    }
    count = 0;
    copyOut(target, scratch);
}
