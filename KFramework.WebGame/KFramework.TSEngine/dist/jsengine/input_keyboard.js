// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Keyboard 经 [JSImport(module: "input_keyboard")] 调用；产物 input_keyboard.js 由 SyncJsEngine 复制。
import { copyOut } from './input_common.js';
import { getCanvas } from './html_canvas.js';
const CODE_TO_INDEX = {
    'KeyW': 0,
    'KeyA': 1,
    'KeyS': 2,
    'KeyD': 3,
    'ArrowUp': 4, 'ArrowDown': 5, 'ArrowLeft': 6, 'ArrowRight': 7,
    'Space': 8, 'ShiftLeft': 9, 'Escape': 10,
    // ... 按需扩充
};
const MAX_EVENTS = 64;
const STRIDE = 2;
const SIZE = 1 + MAX_EVENTS * STRIDE;
let m_Canvas = null;
const pending = new Map();
const scratch = new Uint8Array(SIZE);
function Process_KeyDown(e) {
    pending.set(e.code, 1);
}
function Process_KeyUp(e) {
    pending.set(e.code, 2);
}
export function bindKeyboard(mCanvasId) {
    m_Canvas = getCanvas(mCanvasId);
    if (m_Canvas) {
        m_Canvas.addEventListener('keydown', Process_KeyDown);
        m_Canvas.addEventListener('keyup', Process_KeyUp);
    }
    else {
        window.addEventListener('keydown', Process_KeyDown);
        window.addEventListener('keyup', Process_KeyUp);
    }
}
export function unbindKeyboard() {
    if (m_Canvas) {
        m_Canvas.removeEventListener('keydown', Process_KeyDown);
        m_Canvas.removeEventListener('keyup', Process_KeyUp);
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
    scratch[0] = pending.size;
    let nIndex = 0;
    for (const [key, flag] of pending) {
        if (nByteCount >= SIZE) {
            break;
        }
        const dst = 1 + nIndex * STRIDE;
        scratch[dst] = CODE_TO_INDEX[key] ?? 0;
        scratch[dst + 1] = flag;
        nIndex++;
        nByteCount = dst + STRIDE;
    }
    pending.clear();
    copyOut(target, scratch.subarray(0, 1 + nIndex * STRIDE));
}
