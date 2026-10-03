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

/**
 * 装置是否<b>激活</b>（= 是否绑着监听在采集）：由 bindTouch / unbindTouch 维护，
 * 与 C# 侧 <c>Input_Touch.Active</c> 一一对应。
 *
 * 写入前必须先看它 —— 未激活时不仅不写，还要把攒下的清空：
 * 停用期间残留的触点到重新激活时<b>早已陈旧</b>（抬手收不到 touchend），
 * 留着就会在激活后的第一帧一次性涌出，表现为"刚启用就有个触点一直按着"。
 *
 * 只有这一个开关：C# 的 Activate / Deactivate 与这里的
 * bind / unbind 成对调用，"绑着监听"就是"在采集"，两个标志表达同一件事，迟早有一处忘了同步。
 * 它同时兼作重复绑定的守卫。
 *
 * 不叫 active 是因为本模块已有同名的触点表（<c>active: Map&lt;id, TouchState&gt;</c>）。
 */
let enabled = false;

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
    if (enabled) return;
    
    const canvas = getInputCanvas();
    if (!canvas) return;

    // 四种 touch 事件共用一个【具名】处理函数：相位由事件自身的 type 推断 ——
    // 既免掉为每种事件各建一个闭包（本目录禁止匿名函数，见 ReadMe），也省掉一层包装。
    const touchOptions: AddEventListenerOptions = { passive: false };
    on(window, 'touchstart', onTouch, touchOptions);
    on(window, 'touchmove', onTouch, touchOptions);
    on(window, 'touchend', onTouch, touchOptions);
    on(window, 'touchcancel', onTouch, touchOptions);
    enabled = true;                 // 监听全部挂上了才算激活（画布找不到时保持未激活）
}

/**
 * 触摸事件处理：用 changedTouches 只报发生变化的触点，抬手与按下同帧也不会丢。
 * 相位从 <c>ev.type</c> 推断，故四个事件共用一个函数。
 */
function onTouch(e: Event): void {
    const ev = e as TouchEvent;
    const type = ev.type === 'touchstart' ? E_HTML_Event_Type.TouchBegin
        : ev.type === 'touchmove' ? E_HTML_Event_Type.TouchMove
            : ev.type === 'touchend' ? E_HTML_Event_Type.TouchEnd
                : E_HTML_Event_Type.TouchCancel;      // touchcancel：手势被系统接管

    const list = ev.changedTouches;
    for (let i = 0; i < list.length; i++) {
        const t = list[i];
        const p = canvasPoint(t.clientX, t.clientY);   // 复用对象，立即取值
        const x = p.x, y = p.y;
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
    ev.preventDefault();
}

/** 解绑触摸监听并清空状态。 */
export function unbindTouch(): void {
    enabled = false;                // 先置未激活：此后攒下的触点一律不再上报
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    active.clear();
    ended.clear();
}

/** 单条事件的 data：id(1) + x i16 + y i16 = 5 字节。 */
const scratch = new Uint8Array(5);
const view = new DataView(scratch.buffer);

/**
 * 把本帧触摸事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用），逐条 put。
 * 每条 = E_HTML_Event_Type.Touch* + data(id, x i16, y i16)，共 6 字节。
 */
export function writeTouchEvents(w: FrameDataStream): void {
    if (!enabled)
    {
        discardTouchEvents();       // 未激活：不采集，也不留陈旧触点（理由见 enabled 的注释）
        return;
    }

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

/**
 * 作废本帧攒下的触摸事件（失焦帧由 game_frame_take_js_data 调用）。
 *
 * 监听仍挂着，只丢数据。必须丢：窗口失焦 / 页面切后台时，抬手收不到 touchend，
 * 把"按下"留到下一帧发出去，C# 侧就会一直显示按住。
 */
export function discardTouchEvents(): void {
    active.clear();
    ended.clear();
}
