// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input 经 [JSImport(module: "input_keyboard")] 调用；产物 input_keyboard.js 由 SyncJsEngine 复制。
// 键盘模块：只注册监听 + 事件入队；键码映射、按下/抬起状态、边沿全在 C# 侧（KFramework.MonoGame.Input_KeyBoard）实现。
//
// 事件格式（与 Input_KeyBoard.cs 严格一致）：布局为字节流，offset 0 = int32 count，
// 之后每条 8 字节 = (int32 type, int32 keyCode)；type: 1=KeyDown 2=KeyUp 10=Blur（失焦，C# 据此清空状态）。
import { copyOut } from './input_common';
const MAX_EVENTS = 64;
const STRIDE = 8; // 每条 2 个 i32 = 8 字节
const SIZE = 4 + MAX_EVENTS * STRIDE; // count(4) + 64*8
// 本帧待上报的键盘事件，按 keyCode 归并到字典：同一键一帧内的多次按下/抬起合并为最终边沿，
// 避免定长队列在事件过多（尤其长按 auto-repeat 连发）时丢事件导致“卡键”或边沿错乱。
// 值 = 边沿标志位：bit0=KeyDown(1)  bit1=KeyUp(2)（二者皆置表示该键本帧内按下又抬起，需先后上报）。
const pending = new Map();
// 失焦事件单独标记（C# 据此 Reset 全部状态）。
let blurPending = false;
const registrations = [];
let bound = false;
function on(target, name, handler) {
    target.addEventListener(name, handler);
    registrations.push({ target, name, handler });
}
function push(type, keyCode) {
    if (type === 10) {
        blurPending = true;
        return;
    } // Blur：失焦清空
    if (type !== 1 && type !== 2)
        return;
    const prev = pending.get(keyCode) ?? 0;
    pending.set(keyCode, prev | (type === 1 ? 1 : 2));
}
export function bindKeyboard() {
    if (bound)
        return;
    on(window, 'keydown', (e) => push(1, e.keyCode));
    on(window, 'keyup', (e) => push(2, e.keyCode));
    on(window, 'blur', () => push(10, 0));
    bound = true;
}
/** 解绑键盘监听并清空队列（切场景 / 销毁时调用）。 */
export function unbindKeyboard() {
    for (const r of registrations)
        r.target.removeEventListener(r.name, r.handler);
    registrations.length = 0;
    pending.clear();
    blurPending = false;
    bound = false;
}
// 先在本地的 scratch（字节数组）上按 C# 期望的布局拼好整段（count 头 + 成对事件），
// 再经由 MemoryView.set 一次性写回 C# 缓冲。
// 注意：MemoryView_Span 不是 Uint8Array、没有 [] 索引器，不能直接 target[i]=x，故用 DataView 写 scratch，
// 且 DataView.setInt32 显式小端，与 C# BinaryPrimitives.ReadInt32LittleEndian 对齐。
// 直接在一次遍历里把事件写进 scratch，不另开 events 中间数组 —— 省掉那一道多余的拷贝。
const scratch = new Uint8Array(SIZE);
const view = new DataView(scratch.buffer);
export function pollKeyboard(target) {
    if (!bound)
        bindKeyboard();
    let n = 0;
    if (blurPending) {
        view.setInt32(4 + n * STRIDE, 10, true);
        view.setInt32(4 + n * STRIDE + 4, 0, true);
        n++;
        blurPending = false;
    }
    for (const [code, flags] of pending) {
        if (n >= MAX_EVENTS)
            break; // 超出 C# 缓冲容量则截断（极端情况，正常远达不到）
        const dst = 4 + n * STRIDE;
        if ((flags & 1) !== 0) {
            view.setInt32(dst, 1, true);
            view.setInt32(dst + 4, code, true);
            n++;
        }
        if ((flags & 2) !== 0) {
            view.setInt32(dst, 2, true);
            view.setInt32(dst + 4, code, true);
            n++;
        }
    }
    pending.clear();
    view.setInt32(0, n, true);
    copyOut(target, scratch.subarray(0, 4 + n * STRIDE));
}
