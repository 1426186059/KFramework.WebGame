// 触摸模块（手机 / 平板）：只注册监听 + 事件入队。
// 触点表与 Began/Moved/Ended 阶段在 C# 侧（Input_Touch）实现。
//
// 事件格式：每条 4 个 i32（type, id, x, y）= 16 字节。
// type: 7=TouchStart  8=TouchMove  9=TouchEnd

import { getCanvasElement } from './gl.js';
import { canvasPoint, copyOut } from './input_common.js';

const MAX_EVENTS = 64;
const STRIDE = 16;
const SIZE = 4 + MAX_EVENTS * STRIDE;

const queue = new Int32Array(MAX_EVENTS * 4);
let count = 0;

interface Registration {
    target: EventTarget;
    name: string;
    handler: EventListener;
}

const registrations: Registration[] = [];
let bound = false;

function on(target: EventTarget, name: string, handler: EventListener,
            options?: AddEventListenerOptions): void {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}

function push(type: number, id: number, x: number, y: number): void {
    if (count >= MAX_EVENTS) return;
    const o = count * 4;
    queue[o] = type;
    queue[o + 1] = id;
    queue[o + 2] = x;
    queue[o + 3] = y;
    count++;
}

export function bindTouch(): void {
    if (bound) return;

    const canvas = getCanvasElement();
    if (!canvas) return;

    // 用 changedTouches：只报本帧发生变化的触点，抬手与按下同帧也不会丢
    const pushTouches = (e: TouchEvent, type: number): void => {
        const list = e.changedTouches;
        for (let i = 0; i < list.length && i < 8; i++) {
            const t = list[i];
            const [x, y] = canvasPoint(t.clientX, t.clientY);
            push(type, t.identifier, x, y);
        }
        e.preventDefault();
    };

    const touchOptions: AddEventListenerOptions = { passive: false };
    on(canvas, 'touchstart', (e: Event) => pushTouches(e as TouchEvent, 7), touchOptions);
    on(canvas, 'touchmove', (e: Event) => pushTouches(e as TouchEvent, 8), touchOptions);
    on(canvas, 'touchend', (e: Event) => pushTouches(e as TouchEvent, 9), touchOptions);
    on(canvas, 'touchcancel', (e: Event) => pushTouches(e as TouchEvent, 9), touchOptions);

    bound = true;
}

/** 解绑触摸监听并清空队列。 */
export function unbindTouch(): void {
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    count = 0;
    bound = false;
}

const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);

export function pollTouch(target: MemoryView | Uint8Array): void {
    if (!bound) bindTouch();

    view.setInt32(0, count, true);
    for (let i = 0; i < count; i++) {
        const src = i * 4;
        const dst = 4 + i * STRIDE;
        view.setInt32(dst, queue[src], true);
        view.setInt32(dst + 4, queue[src + 1], true);
        view.setInt32(dst + 8, queue[src + 2], true);
        view.setInt32(dst + 12, queue[src + 3], true);
    }
    count = 0;

    copyOut(target, scratch);
}
