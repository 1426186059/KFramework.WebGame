// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Mouse 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 维护"当前状态/变化"。状态与边沿在 C# 侧（Input_Mouse）实现。
import { canvasPoint} from './input_common.js';
import { getCanvas, focusCanvas } from './html_canvas.js';
import { E_HTML_Event_Type,FrameDataStream } from './html_event_type.js';
import { reportPointerCancel } from './input_window_event.js';


interface Registration {
    target: EventTarget;
    name: string;
    handler: EventListener;
}

const registrations: Registration[] = [];

/**
 * 装置是否<b>激活</b>（= 是否绑着监听在采集）：由 bindMouse / unbindMouse 维护，
 * 与 C# 侧 <c>Input_Mouse.Active</c> 一一对应。
 *
 * 只有这一个开关：C# 的 Activate / Deactivate 与这里的
 * bind / unbind 成对调用，"绑着监听"就是"在采集"，两个标志表达同一件事，
 * 迟早会有一处忘了同步。它同时兼作重复绑定的守卫。
 */
let enabled = false;
let m_Canvas: HTMLCanvasElement | null = null;

const buttons = new Map<number, number>();   // button → 1(按下) / 0(抬起)，上次 poll 以来最新值
// 当前按住的键。buttons 每次 poll 后就被清空，而 C# 侧是"收到变化才更新状态"，
// 所以必须另留一份"当前按住"的记录：手势被接管 / 窗口失焦时收不到 mouseup，
// 要靠它补发抬起 —— 否则那一次抬起永远丢在 JS 侧，C# 侧就一直显示按住不放。
const held = new Set<number>();
let posX = 0;
let posY = 0;
let moved = false;                            // 上次 poll 以来是否发生过移动
let wheelDelta = 0;                            // 上次 poll 以来累计滚轮增量

function on(target: EventTarget, name: string, handler: EventListener,
            options?: AddEventListenerOptions): void {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}

// 失焦一律交给 input_window_event 的 SysFocusLost（画布 blur + hasFocus 兜底）：
// C# 侧分发该事件时先调 Input_Mouse.ReleaseAll() 逐个键上报"被中断"，再 ResetAll 清空 ——
// 所以本模块既不用监听 window 的 blur，也不用再为"手势被接管"单列一个事件。

export function bindMouse(bUseCanvasListener: boolean): void {
    if (enabled) return;

    const target: EventTarget = bUseCanvasListener ? (m_Canvas = getCanvas())! : window;
    if (bUseCanvasListener && !m_Canvas) {
        console.error("bindMouse canvas Find Error");
        return;
    }
    if (bUseCanvasListener) focusCanvas(true);

    on(target, 'mousemove', onMouseMove);
    on(target, 'mousedown', onMouseDown);
    on(target, 'mouseup', onMouseUp);
    on(target, 'wheel', onMouseWheel, { passive: false });
    on(target, 'pointercancel', onPointerCancel);

    enabled = true;                 // 监听全部挂上了才算激活
}

function onMouseMove(e: Event): void {
    const ev = e as MouseEvent;
    const p = canvasPoint(ev.clientX, ev.clientY);
    posX = p.x; posY = p.y; moved = true;
    ev.preventDefault();
}

function onMouseDown(e: Event): void {
    const ev = e as MouseEvent;
    const p = canvasPoint(ev.clientX, ev.clientY);
    posX = p.x; posY = p.y;
    buttons.set(ev.button, 1);
    held.add(ev.button);
    ev.preventDefault();
}

function onMouseWheel(e: Event): void {
    const ev = e as WheelEvent;
    const p = canvasPoint(ev.clientX, ev.clientY);
    posX = p.x; posY = p.y;
    wheelDelta += Math.sign(ev.deltaY);
    ev.preventDefault();
}

function onMouseUp(e: Event): void {
    const ev = e as MouseEvent;
    const p = canvasPoint(ev.clientX, ev.clientY);
    posX = p.x; posY = p.y;
    buttons.set(ev.button, 0);
    held.delete(ev.button);
}

function onPointerCancel(e: Event): void {
    const ev = e as PointerEvent;
    if (!held.delete(ev.button)) return;
    held.clear();                  // C# 侧会 ReleaseAll 全部键，本侧不必再逐个记
    reportPointerCancel();
}

export function unbindMouse(): void {
    enabled = false;                // 先置未激活：此后攒下的数据一律不再上报
    for (const r of registrations) r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    if (m_Canvas) {
        focusCanvas(false);
        m_Canvas = null;
    }
    buttons.clear();
    held.clear();
    moved = false;
    wheelDelta = 0;
}

/** 单条事件的 data：最长的是 MouseMove（x i16 + y i16 = 4 字节）。 */
const scratch = new Uint8Array(4);
const view = new DataView(scratch.buffer);

/**
 * 把本帧鼠标事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用），逐条 put。
 */
export function writeMouseEvents(w: FrameDataStream): void {
    if (!enabled)
    {
        discardMouseEvents();       // 未激活：不采集，也不留陈旧数据（理由见 enabled 的注释）
        return;
    }

    let bSetPos = moved;
    moved = false;

    // 【指针被接管 / 失焦】不再在这里单发一条取消事件 ——
    // 指针被接管走 input_window_event 的 SysPointerCancel；失焦则<b>不影响这里</b>：
    // mousedown / mouseup 是可靠送达的，失焦帧照样写（丢了才会卡住按键）。

    // 按键变化：data 1 字节 = 低 7 位键号 | 最高位是否按下
    for (const [button, down] of buttons) {
        scratch[0] = (button & 0x7f) | (down ? 0x80 : 0);
        w.put(E_HTML_Event_Type.MouseButton, scratch, 1);
        bSetPos = true;
    }
    buttons.clear();

    // 滚轮：累计增量上报一次；夹到 i8 范围后按 byte 写入，C# 侧按 sbyte 解读（-1 → 255 → -1）
    if (wheelDelta !== 0) {
        let d = wheelDelta;
        if (d > 127) d = 127; else if (d < -128) d = -128;
        scratch[0] = d;
        w.put(E_HTML_Event_Type.MouseWheel, scratch, 1);
        wheelDelta = 0;
        bSetPos = true;
    }

    // 移动 / 点击 / 滚轮都会带一次坐标：渲染侧要的是本帧的最终位置
    if (bSetPos) {
        view.setInt16(0, posX, true);
        view.setInt16(2, posY, true);
        w.put(E_HTML_Event_Type.MouseMove, scratch, 4);
    }
}


export function discardMouseEvents(): void {
    buttons.clear();
    held.clear();
    moved = false;
    wheelDelta = 0;
}
