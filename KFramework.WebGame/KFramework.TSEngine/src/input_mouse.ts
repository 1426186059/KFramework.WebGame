// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Mouse 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 维护"当前状态/变化"。状态与边沿在 C# 侧（Input_Mouse）实现。
//
// 与 input_keyboard 一致，采用"字典 + 变化上报"模型（不再把每个 DOM 事件 push 进队列）：
//   - 按键：Map<button, flag> 记录自上次 poll 以来的最新变化（按下=1 / 抬起=2），按下+抬起合并为抬起；
//   - 位置：只保留最新坐标（posX/posY），移动事件每 poll 最多报一次；
//   - 滚轮：累加增量（wheelDelta），poll 时上报一次后清零。
// 线协议完全不变（与 Input_Mouse.cs 对齐）：count(4B) + N×5×i32，每条为 (type, button, x, y, wheel)。
//
// 事件类型：3=MouseDown  4=MouseUp  5=MouseMove  6=Wheel

import { getCanvasElement } from './gl.js';
import { canvasPoint, copyOut } from './input_common.js';

const MAX_EVENTS = 64;
const STRIDE = 20;
const SIZE = 4 + MAX_EVENTS * STRIDE;

const EvMouseDown = 3;
const EvMouseUp = 4;
const EvMouseMove = 5;
const EvWheel = 6;

interface Registration {
    target: EventTarget;
    name: string;
    handler: EventListener;
}

const registrations: Registration[] = [];
let bound = false;

// ===== 状态（字典 + 当前值）=====
// 按键变化：button → 1(按下) / 2(抬起)，仅记上次 poll 以来的最新值（按下+抬起合并为抬起，与键盘同语义）。
const buttons = new Map<number, number>();
let posX = 0;
let posY = 0;
let moved = false;        // 自上次 poll 以来是否发生过移动
let wheelDelta = 0;      // 自上次 poll 以来累计的滚轮增量

function on(target: EventTarget, name: string, handler: EventListener,
            options?: AddEventListenerOptions): void {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}

export function bindMouse(): void {
    if (bound) return;

    const canvas = getCanvasElement();
    if (canvas) {
        // 防止画布在按住拖动时被浏览器当作可拖拽元素/可选文本，进而提前结束“按下”。
        const c = canvas as HTMLElement;
        c.setAttribute('draggable', 'false');
        c.style.userSelect = 'none';
        c.style.touchAction = 'none';
        (c.style as any).webkitUserSelect = 'none';
        on(canvas, 'dragstart', (e: Event) => e.preventDefault());
        on(canvas, 'mousemove', (e: Event) => {
            const ev = e as MouseEvent;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x; posY = y; moved = true;
            // 阻止按住拖动时的原生文本选择/元素拖拽，避免浏览器在首次 mousemove 时
            // 中断“按下”状态并隐式发出 mouseup，导致 UI 面板无法拖动（单击正常）。
            ev.preventDefault();
        });

        on(canvas, 'mousedown', (e: Event) => {
            const ev = e as MouseEvent;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x; posY = y;
            buttons.set(ev.button, 1);
            ev.preventDefault();
        });

        on(canvas, 'wheel', (e: Event) => {
            const ev = e as WheelEvent;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x; posY = y;
            wheelDelta += Math.sign(ev.deltaY);
            ev.preventDefault();
        }, { passive: false });

        on(canvas, 'contextmenu', (e: Event) => e.preventDefault());
    }

    // 抬起挂 window：在画布外松手也能收到
    on(window, 'mouseup', (e: Event) => {
        const ev = e as MouseEvent;
        const [x, y] = canvasPoint(ev.clientX, ev.clientY);
        posX = x; posY = y;
        buttons.set(ev.button, 2);
    });

    bound = true;
}

/** 解绑鼠标监听并清空状态。 */
export function unbindMouse(): void {
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    buttons.clear();
    moved = false;
    wheelDelta = 0;
    bound = false;
}

const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);

export function pollMouse(target: MemoryView_Span | Uint8Array): void {
    if (!bound) bindMouse();

    let off = 4;
    let nEvents = 0;

    // 1) 按键变化（字典）：每项一次事件，携带最新坐标。
    for (const [button, flag] of buttons) {
        if (off + STRIDE > SIZE) break;
        view.setInt32(off, flag === 1 ? EvMouseDown : EvMouseUp, true);
        view.setInt32(off + 4, button, true);
        view.setInt32(off + 8, posX, true);
        view.setInt32(off + 12, posY, true);
        view.setInt32(off + 16, 0, true);
        off += STRIDE; nEvents++;
    }
    buttons.clear();

    // 2) 移动：仅报最新位置一次（无移动则不发，C# 侧位置保持不变）。
    if (moved) {
        view.setInt32(off, EvMouseMove, true);
        view.setInt32(off + 4, 0, true);
        view.setInt32(off + 8, posX, true);
        view.setInt32(off + 12, posY, true);
        view.setInt32(off + 16, 0, true);
        off += STRIDE; nEvents++;
        moved = false;
    }

    // 3) 滚轮：累加增量上报一次后清零。
    if (wheelDelta !== 0) {
        view.setInt32(off, EvWheel, true);
        view.setInt32(off + 4, 0, true);
        view.setInt32(off + 8, posX, true);
        view.setInt32(off + 12, posY, true);
        view.setInt32(off + 16, wheelDelta, true);
        off += STRIDE; nEvents++;
        wheelDelta = 0;
    }

    view.setInt32(0, nEvents, true);
    target.set(scratch);
}
