// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Keyboard 经 [JSImport(module: "input_keyboard")] 调用；产物 input_keyboard.js 由 SyncJsEngine 复制。
import { getCanvas, focusCanvas } from './html_canvas.js';
// 浏览器 KeyboardEvent.code → KFramework.MonoGame.Keys 枚举数值。
// 必须与 Input/Keys.cs 的枚举值严格一致：字母/数字沿用 ASCII，方向键 37..40，修饰键 16/17/18…。
// 不在表内的键返回 0（= Keys.None），C# 侧 SetKey 会直接忽略（k<=0）。
// 全部键显式列出，不靠规律计算（保持与 C# Keys 枚举一一对应、易对照）。
const CODE_TO_KEYS = {
    // 字母 A..Z
    'KeyA': 65, 'KeyB': 66, 'KeyC': 67, 'KeyD': 68, 'KeyE': 69, 'KeyF': 70,
    'KeyG': 71, 'KeyH': 72, 'KeyI': 73, 'KeyJ': 74, 'KeyK': 75, 'KeyL': 76,
    'KeyM': 77, 'KeyN': 78, 'KeyO': 79, 'KeyP': 80, 'KeyQ': 81, 'KeyR': 82,
    'KeyS': 83, 'KeyT': 84, 'KeyU': 85, 'KeyV': 86, 'KeyW': 87, 'KeyX': 88,
    'KeyY': 89, 'KeyZ': 90,
    // 主键盘数字 0..9
    'Digit0': 48, 'Digit1': 49, 'Digit2': 50, 'Digit3': 51, 'Digit4': 52,
    'Digit5': 53, 'Digit6': 54, 'Digit7': 55, 'Digit8': 56, 'Digit9': 57,
    // 小键盘数字（回落到主键盘数字，Keys 枚举未单独定义 NumPad）
    'Numpad0': 48, 'Numpad1': 49, 'Numpad2': 50, 'Numpad3': 51, 'Numpad4': 52,
    'Numpad5': 53, 'Numpad6': 54, 'Numpad7': 55, 'Numpad8': 56, 'Numpad9': 57,
    // 方向键
    'ArrowLeft': 37, 'ArrowUp': 38, 'ArrowRight': 39, 'ArrowDown': 40,
    // 控制 / 编辑键
    'Backspace': 8, 'Tab': 9, 'Enter': 13, 'Escape': 27, 'Space': 32,
    'Home': 36, 'End': 35, 'Delete': 46,
    // 修饰键（左右别名同值，见 Keys.cs：LeftShift = Shift 等）
    'ShiftLeft': 16, 'ShiftRight': 16,
    'ControlLeft': 17, 'ControlRight': 17,
    'AltLeft': 18, 'AltRight': 18,
};
// KeyboardEvent.code → Keys 数值（仅查表；未列出的键返回 0，C# 侧忽略）。
function codeToKeys(code) {
    return CODE_TO_KEYS[code] ?? 0;
}
const MAX_EVENTS = 32;
const STRIDE = 2;
const SIZE = 1 + MAX_EVENTS * STRIDE;
let m_Canvas = null;
let m_CanvasId = null;
let m_RefocusHandler = null;
const pending = new Map();
const scratch = new Uint8Array(SIZE);
function Process_KeyDown(e) {
    pending.set(e.code, 1);
}
function Process_KeyUp(e) {
    pending.set(e.code, 2);
}
// 键盘监听绑在 canvas 上（依赖画布获焦才会收到 key 事件）。
// <canvas> 默认不可获焦，所以绑监听前必须先 focusCanvas 让它可获焦并聚焦；同时挂一个 pointerdown 重新聚焦，
// 这样切走窗口 / 在输入框打字后点回游戏，键盘依然有效。IME 输入框获焦时不会触发 canvas 的 key 事件，不会误报游戏键。
export function bindKeyboard(canvasId) {
    m_Canvas = getCanvas(canvasId);
    m_CanvasId = canvasId ?? null;
    m_Canvas = null;
    // if (m_Canvas)
    // {
    //     focusCanvas(m_CanvasId);
    //     m_Canvas.addEventListener('keydown', Process_KeyDown);
    //     m_Canvas.addEventListener('keyup', Process_KeyUp);
    //     m_RefocusHandler = () => { m_Canvas?.focus(); };
    //     m_Canvas.addEventListener('pointerdown', m_RefocusHandler);
    // }
    // else
    {
        // 找不到画布（极少见）才回落到 window，保证至少有输入。
        window.addEventListener('keydown', Process_KeyDown);
        window.addEventListener('keyup', Process_KeyUp);
    }
}
export function unbindKeyboard() {
    if (m_Canvas) {
        m_Canvas.removeEventListener('keydown', Process_KeyDown);
        m_Canvas.removeEventListener('keyup', Process_KeyUp);
        if (m_RefocusHandler)
            m_Canvas.removeEventListener('pointerdown', m_RefocusHandler);
        focusCanvas(m_CanvasId, false);
    }
    else {
        window.removeEventListener('keydown', Process_KeyDown);
        window.removeEventListener('keyup', Process_KeyUp);
    }
}
export function pollKeyboard(target) {
    // 先写入真正的 Uint8Array（scratch），再经由 MemoryView.set 写回 C# 缓冲。
    // 注意：MemoryView_Span 不是 Uint8Array、没有 [] 索引器，不能直接 target[i]=x。
    let nByteCount = 0;
    scratch[nByteCount++] = pending.size;
    for (const [key, flag] of pending) {
        if (nByteCount >= SIZE) {
            break;
        }
        scratch[nByteCount++] = codeToKeys(key);
        scratch[nByteCount++] = flag;
    }
    pending.clear();
    target.set(scratch.subarray(0, nByteCount));
}
