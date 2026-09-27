// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Touch 经 [JSImport(module: "input_touch")] 调用；产物 input_touch.js 由 SyncJsEngine 复制。
// 触摸模块（手机 / 平板）：只注册监听 + 维护“当前触点表 / 变化”。状态与阶段(Began/Moved/Ended)在 C# 侧（Input_Touch）实现。
//
// 与键盘/鼠标一致改为“Map + 变化上报”：不再把每个 DOM 事件 push 进队列，
// 而是用 Map<id, 触点> 维护当前状态，poll 时只上报自上次 poll 以来的变化
// （刚按下=Began、移动过=Moved、抬起=Ended），静止的触点不上报，省掉大量冗余 move。
// 线协议保持兼容：每条仍是 (type,id,x,y) 四个 i32，type 7/8/9 = Start/Move/End。

import { getCanvasElement } from './gl.js';
import { canvasPoint, copyOut } from './input_common.js';

const MAX_EVENTS = 64;
const STRIDE = 16;
const SIZE = 4 + MAX_EVENTS * STRIDE;

const EvStart = 7;
const EvMove = 8;
const EvEnd = 9;

interface Registration {
    target: EventTarget;
    name: string;
    handler: EventListener;
}

const registrations: Registration[] = [];
let bound = false;

// 当前活跃触点：id -> 最新位置 + 自上次 poll 以来是否变化
// dirty: 0=无变化 / EvStart=本区间刚按下 / EvMove=本区间移动过
interface TouchState {
    x: number;
    y: number;
    dirty: number;
}
const active = new Map<number, TouchState>();

// 本 poll 区间内抬起的触点（上报一次 Ended 后清空）：id -> 最后位置
const ended = new Map<number, { x: number; y: number }>();

function on(target: EventTarget, name: string, handler: EventListener,
            options?: AddEventListenerOptions): void {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}

export function bindTouch(): void {
    if (bound) return;

    const canvas = getCanvasElement();
    if (!canvas) return;

    // 用 changedTouches：只报本帧发生变化的触点，抬手与按下同帧也不会丢
    const handle = (e: TouchEvent, type: number): void => {
        const list = e.changedTouches;
        for (let i = 0; i < list.length; i++) {
            const t = list[i];
            const [x, y] = canvasPoint(t.clientX, t.clientY);
            const id = t.identifier;

            if (type === EvStart) {
                active.set(id, { x, y, dirty: EvStart });
                ended.delete(id);
            } else if (type === EvMove) {
                const s = active.get(id);
                if (s) {
                    s.x = x; s.y = y;
                    // 本帧已刚按下则保持 Began（首帧只报一次按下）
                    if (s.dirty !== EvStart) s.dirty = EvMove;
                } else {
                    active.set(id, { x, y, dirty: EvStart });   // 缺失的按下（防御）
                }
            } else { // EvEnd / Cancel
                active.delete(id);
                ended.set(id, { x, y });
            }
        }
        e.preventDefault();
    };

    const touchOptions: AddEventListenerOptions = { passive: false };
    on(canvas, 'touchstart', (e: Event) => handle(e as TouchEvent, EvStart), touchOptions);
    on(canvas, 'touchmove', (e: Event) => handle(e as TouchEvent, EvMove), touchOptions);
    on(canvas, 'touchend', (e: Event) => handle(e as TouchEvent, EvEnd), touchOptions);
    on(canvas, 'touchcancel', (e: Event) => handle(e as TouchEvent, EvEnd), touchOptions);

    bound = true;
}

/** 解绑触摸监听并清空状态。 */
export function unbindTouch(): void {
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    active.clear();
    ended.clear();
    bound = false;
}

const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);

export function pollTouch(target: MemoryView_Span): void {
    let off = 4;       // 跳过 count
    let n = 0;

    // 1) 活跃触点：只上报本 poll 区间内有变化的（Began / Moved）
    for (const [id, t] of active) {
        if (t.dirty === 0) continue;
        if (n >= MAX_EVENTS) break;
        view.setInt32(off, t.dirty, true); off += 4;
        view.setInt32(off, id, true); off += 4;
        view.setInt32(off, t.x, true); off += 4;
        view.setInt32(off, t.y, true); off += 4;
        n++;
        t.dirty = 0;   // 已上报，清空变化标记
    }

    // 2) 抬起的触点：上报一次 Ended 后清空（同帧内 按下→抬起 会合并为仅 Ended）
    for (const [id, t] of ended) {
        if (n >= MAX_EVENTS) break;
        view.setInt32(off, EvEnd, true); off += 4;
        view.setInt32(off, id, true); off += 4;
        view.setInt32(off, t.x, true); off += 4;
        view.setInt32(off, t.y, true); off += 4;
        n++;
    }
    ended.clear();

    view.setInt32(0, n, true);
    copyOut(target, scratch.subarray(0, off));   // 只发送用到的字节
}
