// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Mouse 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 维护"当前状态/变化"。状态与边沿在 C# 侧（Input_Mouse）实现。
import { getInputCanvas, setCanvasId } from './input_common.js';
import { canvasPoint, ModuleId } from './input_common.js';
const MaxButtons = 8;
const EvMousePos_ByteCount = 5; // EvMousePos：type + x(short) + y(short)
const EvMouseButton_ByteCount = 2; // 其余事件：type + payload
const EvWheel_ByteCount = 2; // 其余事件：type + payload
const SIZE = 1 + EvMouseButton_ByteCount * MaxButtons + EvMousePos_ByteCount + EvWheel_ByteCount;
const EvMousePos = 0;
const EvMouseButton = 1; // 按键变化：payload = button(低7位) | 按下(0x80)
const EvWheel = 2;
const EvPointerCancel = 3; // 指针被系统/浏览器接管或失焦：payload = button（不带按下位）
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
const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);
/**
 * 采集本帧鼠标事件到本地 scratch，返回已用字节数（scratch[0] 已填好条数）。
 * 由 pollMouse 与 writeMouseEvents 共用，避免两份采集逻辑日后走偏。
 */
function collectMouseEvents() {
    let off = 1;
    let nEvents = 0;
    let bSetPos = false;
    if (moved) {
        bSetPos = true;
        moved = false;
    }
    // 【指针被接管 / 失焦】照键盘 EvBlur 的套路：先上报 EvPointerCancel，并丢弃此前残留的
    // 按键变化。手势被接管时浏览器不会再派发 mouseup，少了这一步那个键就永远卡在"按住"。
    // 上报的是【独立的取消事件】而不是伪装成 mouseup —— 用户松手与被系统中断是两回事，
    // 冒充抬起会让上层把一次被中断的交互当成正常松手确认掉（拖动被误判为完成、点击被误触发）。
    if (canceled.length > 0) {
        for (const button of canceled) {
            if (off + EvMouseButton_ByteCount > SIZE)
                break;
            scratch[off] = EvPointerCancel;
            scratch[off + 1] = button & 0x7f;
            off += EvMouseButton_ByteCount;
            nEvents++;
        }
        canceled.length = 0;
        buttons.clear();
        bSetPos = true;
    }
    // 1) 按键变化：每项 2 字节（EvMouseButton + 位打包：低7位=button，最高位=是否按下）
    for (const [button, down] of buttons) {
        if (off + EvMouseButton_ByteCount > SIZE)
            break;
        scratch[off] = EvMouseButton;
        scratch[off + 1] = (button & 0x7f) | (down ? 0x80 : 0);
        off += EvMouseButton_ByteCount;
        nEvents++;
        bSetPos = true;
    }
    buttons.clear();
    // 2) 滚轮：累计增量上报一次；夹紧到 sbyte 范围再按 byte 写入，C# 侧按 sbyte 解读
    if (wheelDelta !== 0 && off + EvWheel_ByteCount <= SIZE) {
        let d = wheelDelta;
        if (d > 127)
            d = 127;
        else if (d < -128)
            d = -128;
        scratch[off] = EvWheel;
        scratch[off + 1] = d; // Uint8Array 自动按 0xff 取模，-1 → 255，C# 读回 -1
        off += EvWheel_ByteCount;
        nEvents++;
        wheelDelta = 0;
        bSetPos = true;
    }
    //只要 鼠标移动/点击/滚轮转动 都要设置位置
    if (bSetPos && off + EvMousePos_ByteCount <= SIZE) {
        scratch[off] = EvMousePos;
        view.setInt16(off + 1, posX, true);
        view.setInt16(off + 3, posY, true);
        off += EvMousePos_ByteCount;
        nEvents++;
        moved = false;
    }
    // 玩家无任何输入 → nEvents=0，scratch 只有一个 0 字节，什么都不填
    scratch[0] = nEvents & 0xff;
    return off;
}
/**
 * 把本帧的鼠标事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用）。
 * 本模块依旧独立：监听、状态、payload 格式全部保持原样，只是不再自己跨界回传 ——
 * payload 就是 collectMouseEvents 的产出（scratch[0] = 条数），故 C# 侧解析无需改动。
 */
export function writeMouseEvents(w) {
    const off = collectMouseEvents();
    if (scratch[0] > 0)
        w.put(ModuleId.Mouse, scratch, off);
}
