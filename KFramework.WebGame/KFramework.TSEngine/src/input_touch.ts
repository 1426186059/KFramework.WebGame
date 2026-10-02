// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Touch 经 [JSImport(module: "input_touch")] 调用；产物 input_touch.js 由 SyncJsEngine 复制。
// 触摸模块（手机 / 平板）：只注册监听 + 维护“当前触点表 / 变化”。状态与阶段(Began/Moved/Ended)在 C# 侧（Input_Touch）实现。
//
// 与键盘/鼠标一致改为“Map + 变化上报”：不再把每个 DOM 事件 push 进队列，
// 而是用 Map<id, 触点> 维护当前状态，poll 时只上报自上次 poll 以来的变化
// （刚按下=Began、移动过=Moved、抬起=Ended），静止的触点不上报，省掉大量冗余 move。
//
// 事件编码统一到 html_event_type：每条 = E_HTML_Event_Type.Touch*(1) + id(1) + x i16 + y i16 = 6 字节。
// 原先是 (type,id,x,y) 四个 i32 共 16 字节 —— 屏幕坐标是整数、画布上限 16384，i16 装得下，
// 每条省 10 字节；多指同按时（最多 64 条）一帧能省下 640 字节。

import { getInputCanvas } from './input_common.js';
import { canvasPoint } from './input_common.js';
import { E_HTML_Event_Type,FrameDataStream} from './html_event_type.js';

const MAX_EVENTS = 64;

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

    const canvas = getInputCanvas();
    if (!canvas) return;

    // 用 changedTouches：只报本帧发生变化的触点，抬手与按下同帧也不会丢
    const handle = (e: TouchEvent, type: number): void => {
        const list = e.changedTouches;
        for (let i = 0; i < list.length; i++) {
            const t = list[i];
            const [x, y] = canvasPoint(t.clientX, t.clientY);
            const id = t.identifier;

            if (type === E_HTML_Event_Type.TouchBegin) {
                active.set(id, { x, y, dirty: E_HTML_Event_Type.TouchBegin });
                ended.delete(id);
            } else if (type === E_HTML_Event_Type.TouchMove) {
                const s = active.get(id);
                if (s) {
                    s.x = x; s.y = y;
                    // 本帧已刚按下则保持 Begin（首帧只报一次按下）
                    if (s.dirty !== E_HTML_Event_Type.TouchBegin) s.dirty = E_HTML_Event_Type.TouchMove;
                } else {
                    active.set(id, { x, y, dirty: E_HTML_Event_Type.TouchBegin });   // 缺失的按下（防御）
                }
            } else { // End / Cancel
                active.delete(id);
                ended.set(id, { x, y });
            }
        }
        e.preventDefault();
    };

    const touchOptions: AddEventListenerOptions = { passive: false };
    on(canvas, 'touchstart', (e: Event) => handle(e as TouchEvent, E_HTML_Event_Type.TouchBegin), touchOptions);
    on(canvas, 'touchmove', (e: Event) => handle(e as TouchEvent, E_HTML_Event_Type.TouchMove), touchOptions);
    on(canvas, 'touchend', (e: Event) => handle(e as TouchEvent, E_HTML_Event_Type.TouchEnd), touchOptions);
    on(canvas, 'touchcancel', (e: Event) => handle(e as TouchEvent, E_HTML_Event_Type.TouchCancel), touchOptions);

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

/** 单条事件的 data：id(1) + x i16 + y i16 = 5 字节。 */
const scratch = new Uint8Array(5);
const view = new DataView(scratch.buffer);

/**
 * 把本帧触摸事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用），逐条 put。
 * 每条 = E_HTML_Event_Type.Touch* + data(id, x i16, y i16)，共 6 字节。
 */
export function writeTouchEvents(w: FrameDataStream): void {
    let n = 0;

    // 1) 活跃触点：只上报本帧有变化的（Begin / Move），静止的触点不上报
    for (const [id, t] of active) {
        if (t.dirty === 0) continue;
        if (n >= MAX_EVENTS) break;
        scratch[0] = id & 0xff;
        view.setInt16(1, t.x, true);
        view.setInt16(3, t.y, true);
        w.put(t.dirty === E_HTML_Event_Type.TouchMove ? E_HTML_Event_Type.TouchMove : E_HTML_Event_Type.TouchBegin, scratch, 5);
        n++;
        t.dirty = 0;   // 已上报，清空变化标记
    }

    // 2) 抬起的触点：上报一次 End 后清空（同帧内 按下→抬起 合并为仅 End）
    for (const [id, t] of ended) {
        if (n >= MAX_EVENTS) break;
        scratch[0] = id & 0xff;
        view.setInt16(1, t.x, true);
        view.setInt16(3, t.y, true);
        w.put(E_HTML_Event_Type.TouchEnd, scratch, 5);
        n++;
    }
    ended.clear();
}
