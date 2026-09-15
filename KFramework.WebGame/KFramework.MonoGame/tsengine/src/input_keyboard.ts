// 键盘模块：只注册监听 + 事件入队。
// 键码映射、按下/抬起状态、边沿全在 C# 侧（KFramework.MonoGame.Input_KeyBoard）实现。
//
// 事件格式：每条 2 个 i32（type, keyCode）= 8 字节。
// type: 1=KeyDown  2=KeyUp  10=Blur（失焦，C# 据此清空状态）

import { copyOut } from './input_common.js';

const MAX_EVENTS = 64;
const STRIDE = 8;
const SIZE = 4 + MAX_EVENTS * STRIDE;

const queue = new Int32Array(MAX_EVENTS * 2);
let count = 0;

interface Registration {
    target: EventTarget;
    name: string;
    handler: EventListener;
}

const registrations: Registration[] = [];
let bound = false;

function on(target: EventTarget, name: string, handler: EventListener): void {
    target.addEventListener(name, handler);
    registrations.push({ target, name, handler });
}

function push(type: number, keyCode: number): void {
    if (count >= MAX_EVENTS) return;   // 队列满则丢弃
    const o = count * 2;
    queue[o] = type;
    queue[o + 1] = keyCode;
    count++;
}

export function bindKeyboard(): void {
    if (bound) return;

    on(window, 'keydown', (e: Event) => {
        const ev = e as KeyboardEvent;
        push(1, ev.keyCode);
        // 阻止空格 / 方向键滚动页面（默认行为只能在 JS 侧拦）
        if (ev.keyCode === 32 || (ev.keyCode >= 37 && ev.keyCode <= 40)) ev.preventDefault();
    });

    on(window, 'keyup', (e: Event) => push(2, (e as KeyboardEvent).keyCode));

    on(window, 'blur', () => push(10, 0));

    bound = true;
}

/** 解绑键盘监听并清空队列（切场景 / 销毁时调用）。 */
export function unbindKeyboard(): void {
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    count = 0;
    bound = false;
}

const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);

export function pollKeyboard(target: MemoryView | Uint8Array): void {
    if (!bound) bindKeyboard();

    view.setInt32(0, count, true);
    for (let i = 0; i < count; i++) {
        const dst = 4 + i * STRIDE;
        view.setInt32(dst, queue[i * 2], true);
        view.setInt32(dst + 4, queue[i * 2 + 1], true);
    }
    count = 0;

    copyOut(target, scratch);
}
