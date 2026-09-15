// 输入【薄绑定层】。
//
// 设计原则：这一层只做两件事——注册/注销监听、把事件原样入队。
// 任何语义都不在这里处理：不做键码映射、不维护按键电平、不算边沿、不做触点差分、不识别手势。
// 全部逻辑都在 C# 侧（KFramework.MonoGame.Input）实现，与 gl.ts 的"纯转发"风格保持一致。
//
// 事件经 pollInput 一次性交回 C#（每帧一次跨界调用），避免 mousemove / touchmove 这类
// 高频事件逐个回调 C# 造成抖动。
import { getCanvasElement } from './gl.js';
// 每条事件 5 个 i32（type / a / b / c / d）= 20 字节
export const MAX_EVENTS = 128;
export const EVENT_STRIDE = 20;
export const INPUT_BUFFER_SIZE = 4 + MAX_EVENTS * EVENT_STRIDE;
const queue = new Int32Array(MAX_EVENTS * 5);
let queueCount = 0;
function push(type, a, b, c, d) {
    if (queueCount >= MAX_EVENTS)
        return; // 队列满则丢弃，宁可丢事件也不越界
    const o = queueCount * 5;
    queue[o] = type;
    queue[o + 1] = a;
    queue[o + 2] = b;
    queue[o + 3] = c;
    queue[o + 4] = d;
    queueCount++;
}
const registrations = [];
let bound = false;
function on(target, name, handler, options) {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}
/** 画布内的 CSS 像素坐标（只减 rect 偏移，不做任何 dpr 换算——dpr 由 C# 决定）。 */
function canvasPoint(clientX, clientY) {
    const canvas = getCanvasElement();
    if (!canvas)
        return [0, 0];
    const rect = canvas.getBoundingClientRect();
    return [Math.round(clientX - rect.left), Math.round(clientY - rect.top)];
}
export function bindInput() {
    if (bound)
        return;
    // ---- 键盘 ----
    on(window, 'keydown', (e) => {
        const ev = e;
        push(1 /* InputEventType.KeyDown */, ev.keyCode, 0, 0, 0);
        // 阻止空格 / 方向键滚动页面（默认行为只能在 JS 侧拦）
        if (ev.keyCode === 32 || (ev.keyCode >= 37 && ev.keyCode <= 40))
            ev.preventDefault();
    });
    on(window, 'keyup', (e) => {
        push(2 /* InputEventType.KeyUp */, e.keyCode, 0, 0, 0);
    });
    // 失焦：交给 C# 清空状态，避免按键卡住
    on(window, 'blur', () => {
        push(10 /* InputEventType.Blur */, 0, 0, 0, 0);
    });
    // ---- 鼠标 / 触摸（都挂在画布上，画布就绪后才有意义） ----
    const canvas = getCanvasElement();
    if (canvas) {
        on(canvas, 'mousemove', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(5 /* InputEventType.MouseMove */, 0, x, y, 0);
        });
        on(canvas, 'mousedown', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(3 /* InputEventType.MouseDown */, ev.button, x, y, 0);
            ev.preventDefault();
        });
        // 抬起挂 window：在画布外松手也能收到
        on(window, 'mouseup', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(4 /* InputEventType.MouseUp */, ev.button, x, y, 0);
        });
        on(canvas, 'wheel', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            push(6 /* InputEventType.Wheel */, 0, x, y, Math.sign(ev.deltaY));
            ev.preventDefault();
        }, { passive: false });
        on(canvas, 'contextmenu', (e) => e.preventDefault());
        // ---- 触摸：用 changedTouches，只报本帧发生变化的触点 ----
        const pushTouches = (e, type) => {
            const list = e.changedTouches;
            for (let i = 0; i < list.length && i < 8; i++) {
                const t = list[i];
                const [x, y] = canvasPoint(t.clientX, t.clientY);
                push(type, t.identifier, x, y, 0);
            }
            e.preventDefault();
        };
        const touchOptions = { passive: false };
        on(canvas, 'touchstart', (e) => pushTouches(e, 7 /* InputEventType.TouchStart */), touchOptions);
        on(canvas, 'touchmove', (e) => pushTouches(e, 8 /* InputEventType.TouchMove */), touchOptions);
        on(canvas, 'touchend', (e) => pushTouches(e, 9 /* InputEventType.TouchEnd */), touchOptions);
        on(canvas, 'touchcancel', (e) => pushTouches(e, 9 /* InputEventType.TouchEnd */), touchOptions);
    }
    bound = true;
}
/** 解绑全部监听并清空队列 —— 切场景 / 销毁时必须调用，否则监听器会泄漏。 */
export function unbindInput() {
    for (const r of registrations) {
        r.target.removeEventListener(r.name, r.handler);
    }
    registrations.length = 0;
    queueCount = 0;
    bound = false;
}
// ---------- 回传：一次调用把整队事件交给 C# ----------
const scratch = new Uint8Array(INPUT_BUFFER_SIZE);
const scratchView = new DataView(scratch.buffer);
export function pollInput(target) {
    if (!bound)
        bindInput();
    scratchView.setInt32(0, queueCount, true);
    for (let i = 0; i < queueCount; i++) {
        const src = i * 5;
        const dst = 4 + i * EVENT_STRIDE;
        scratchView.setInt32(dst, queue[src], true);
        scratchView.setInt32(dst + 4, queue[src + 1], true);
        scratchView.setInt32(dst + 8, queue[src + 2], true);
        scratchView.setInt32(dst + 12, queue[src + 3], true);
        scratchView.setInt32(dst + 16, queue[src + 4], true);
    }
    queueCount = 0;
    if (target instanceof Uint8Array) {
        target.set(scratch);
        return;
    }
    if (typeof target.set === 'function') {
        target.set(scratch, 0);
        return;
    }
    const fallback = target;
    for (let i = 0; i < INPUT_BUFFER_SIZE; i++)
        fallback[i] = scratch[i];
}
