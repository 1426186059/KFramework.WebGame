// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Mouse 经 [JSImport(module: "input_mouse")] 调用；产物 input_mouse.js 由 SyncJsEngine 复制。
// 鼠标模块：只注册监听 + 维护"当前状态/变化"。状态与边沿在 C# 侧（Input_Mouse）实现。
import { getCanvasElement } from './gl.js';
import { canvasPoint, copyOut } from './input_common.js';
const MaxButtons = 8;
const EvMousePos_ByteCount = 5; // EvMousePos：type + x(short) + y(short)
const EvMouseButton_ByteCount = 2; // 其余事件：type + payload
const EvWheel_ByteCount = 2; // 其余事件：type + payload
const SIZE = EvMouseButton_ByteCount * MaxButtons + EvMousePos_ByteCount + EvWheel_ByteCount;
const EvMousePos = 0;
const EvMouseButton = 1; // 按键变化：payload = button(低7位) | 按下(0x80)
const EvWheel = 2;
const registrations = [];
let bound = false;
const buttons = new Map(); // button → 1(按下) / 0(抬起)，上次 poll 以来最新值
let posX = 0;
let posY = 0;
let moved = false; // 上次 poll 以来是否发生过移动
let wheelDelta = 0; // 上次 poll 以来累计滚轮增量
function on(target, name, handler, options) {
    target.addEventListener(name, handler, options);
    registrations.push({ target, name, handler });
}
export function bindMouse() {
    if (bound)
        return;
    const canvas = getCanvasElement();
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
    });
    bound = true;
}
/** 解绑鼠标监听并清空状态。 */
export function unbindMouse() {
    for (const r of registrations)
        r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    buttons.clear();
    moved = false;
    wheelDelta = 0;
    bound = false;
}
const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);
export function pollMouse(target) {
    let off = 1;
    let nEvents = 0;
    let bSetPos = false;
    if (moved) {
        bSetPos = true;
        moved = false;
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
    copyOut(target, scratch.subarray(0, off));
}
