// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Mouse 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 维护"当前状态/变化"。状态与边沿在 C# 侧（Input_Mouse）实现。
import { getInputCanvas, setCanvasId } from './input_common.js';
import { canvasPoint } from './input_common.js';
import { E_HTML_Event_Type } from './html_event_type.js';
const registrations = [];
let bound = false;
const buttons = new Map(); // button → 1(按下) / 0(抬起)，上次 poll 以来最新值
// 当前按住的键。buttons 每次 poll 后就被清空，而 C# 侧是"收到变化才更新状态"，
// 所以必须另留一份"当前按住"的记录：手势被接管 / 窗口失焦时收不到 mouseup，
// 要靠它补发抬起 —— 否则那一次抬起永远丢在 JS 侧，C# 侧就一直显示按住不放。
const held = new Set();
// 待上报的"指针取消"键（pointercancel / 失焦）。照键盘 m_Blurred 的做法：
// handler 只入队，真正上报延到 pollMouse，保证与一次 poll 的时序对齐。
const canceled = [];
let posX = 0;
let posY = 0;
let moved = false; // 上次 poll 以来是否发生过移动
let wheelDelta = 0; // 上次 poll 以来累计滚轮增量
function on(target, name, handler, options) {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}
/** 失焦兜底：把当前按住的键记为"指针取消"，随下一次 poll 上报（照键盘 Process_Blur 只置标记）。 */
function releaseAll() {
    for (const button of held)
        canceled.push(button);
    held.clear();
}
export function bindMouse(canvasId) {
    if (bound)
        return;
    // 记下画布 id，后续 canvasPoint 的坐标换算才能用同一块画布。
    if (canvasId)
        setCanvasId(canvasId);
    const canvas = getInputCanvas();
    if (canvas) {
        // 防止画布在按住拖动时被浏览器当作可拖拽元素/可选文本，进而提前结束“按下”。
        const c = canvas;
        c.setAttribute('draggable', 'false');
        c.style.userSelect = 'none';
        c.style.touchAction = 'none';
        c.style.webkitUserSelect = 'none';
        on(canvas, 'dragstart', (e) => e.preventDefault());
        on(canvas, 'mousemove', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x;
            posY = y;
            moved = true;
            // 阻止按住拖动时的原生文本选择/元素拖拽，避免浏览器在首次 mousemove 时
            // 中断“按下”状态并隐式发出 mouseup，导致 UI 面板无法拖动（单击正常）。
            ev.preventDefault();
        });
        on(canvas, 'mousedown', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x;
            posY = y;
            buttons.set(ev.button, 1);
            held.add(ev.button);
            ev.preventDefault();
        });
        on(canvas, 'wheel', (e) => {
            const ev = e;
            const [x, y] = canvasPoint(ev.clientX, ev.clientY);
            posX = x;
            posY = y;
            wheelDelta += Math.sign(ev.deltaY);
            ev.preventDefault();
        }, { passive: false });
        on(canvas, 'contextmenu', (e) => e.preventDefault());
    }
    // 抬起挂 window：在画布外松手也能收到
    on(window, 'mouseup', (e) => {
        const ev = e;
        const [x, y] = canvasPoint(ev.clientX, ev.clientY);
        posX = x;
        posY = y;
        buttons.set(ev.button, 0);
        held.delete(ev.button);
    });
    // 【手势被接管】浏览器/系统接管指针（右键手势、拖拽、长按菜单等）时【不会】再派发 mouseup，
    // 只给 pointercancel —— 少了这一步，被接管的那个键就永远卡在"按住"，
    // 表现为松开后 UI 仍显示 Left/Right 按住不放。
    on(window, 'pointercancel', (e) => {
        const ev = e;
        // 只入队，真正上报延到 pollMouse（照键盘 Process_Blur 只置标记）。
        if (held.delete(ev.button))
            canceled.push(ev.button);
    });
    // 【失焦兜底】切走窗口（Alt+Tab）或切到别的标签页同样收不到抬起，补发一次全量抬起。
    on(window, 'blur', releaseAll);
    on(document, 'visibilitychange', () => { if (document.hidden)
        releaseAll(); });
    bound = true;
}
/** 解绑鼠标监听并清空状态。 */
export function unbindMouse() {
    for (const r of registrations)
        r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    buttons.clear();
    held.clear();
    canceled.length = 0;
    moved = false;
    wheelDelta = 0;
    bound = false;
}
/** 单条事件的 data：最长的是 MouseMove（x i16 + y i16 = 4 字节）。 */
const scratch = new Uint8Array(4);
const view = new DataView(scratch.buffer);
/**
 * 把本帧鼠标事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用），逐条 put。
 */
export function writeMouseEvents(w) {
    let bSetPos = moved;
    moved = false;
    // 【指针被接管 / 失焦】手势被接管时浏览器不会再派发 mouseup，只给 pointercancel ——
    // 上报【独立的取消事件】而不是伪装成 mouseup：用户松手与被系统中断是两回事，
    // 冒充抬起会让上层把一次被中断的交互当成正常松手确认掉（拖动被误判为完成、点击被误触发）。
    if (canceled.length > 0) {
        for (const button of canceled) {
            scratch[0] = button & 0x7f;
            w.put(E_HTML_Event_Type.PointerCancel, scratch, 1);
        }
        canceled.length = 0;
        buttons.clear(); // 被接管的键不再上报变化，免得"松手后又跳回按下"
        bSetPos = true;
    }
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
        if (d > 127)
            d = 127;
        else if (d < -128)
            d = -128;
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
