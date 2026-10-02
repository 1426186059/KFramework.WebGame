// 【依赖 C#】由 KFramework.MonoGame.JSBind_GameFrameData 经 [JSImport(module: "game_frame_take_js_data")] 调用；
// 产物 game_frame_take_js_data.js 由 SyncJsEngine 复制。
//
// 每帧统一事件入口：C# 每帧只调 takeFrameData【一次】，取回所有输入模块本帧的事件，
// 取代原先"键盘 / 鼠标 / 触摸各 poll 一次"（一帧 3 次跨界）。
//
// 各输入模块【依旧独立】：各自注册监听、各自维护状态、各自定义 payload 格式，
// 只是把"写入事件"这一步交给这里的 sink —— 合并的是传输通道，不是各模块本身。
// 因此 payload 沿用各模块现有的格式，C# 侧各 Input_* 类的解析逻辑也无需重写。
//
// 注：IME 走 [JSExport] 由 JS 主动推给 C#（OnDomValue / OnKeyDown），本来就不占这条拉取流。
import { ModuleId } from './input_common.js';
import { writeKeyboardEvents } from './input_keyboard.js';
import { writeMouseEvents } from './input_mouse.js';
import { writeTouchEvents } from './input_touch.js';
// 触摸每触点 16B、最多 64 条，单模块就能到 1KB，故缓冲给到 2048B；
// 真放不下就丢弃（输入是低频操作，不会静默截断出半条事件 —— 见 sink.put 的整体判断）。
const MAX_BYTES = 2048;
const scratch = new Uint8Array(MAX_BYTES);
let count = 0;
let off = 0;
const sink = {
    put(module, payload, len) {
        // 头 3 字节：module + len(UInt16 小端)。len 用两个字节是因为
        // 触摸模块每触点 16B、多指同按时 payload 轻松超过单字节能表达的 255B。
        // 放不下就整条丢弃 —— 绝不写入半截事件让 C# 侧解析错位。
        if (len <= 0 || off + 3 + len > MAX_BYTES)
            return;
        scratch[off++] = module;
        scratch[off++] = len & 0xff;
        scratch[off++] = (len >> 8) & 0xff;
        scratch.set(payload.subarray(0, len), off);
        off += len;
        count++;
    },
};
// ---------- 系统事件：失焦 / 获得焦点 / 页面可见性 ----------
// 所有输入模块都依赖"失焦"这件事（按键会在窗外松手后卡住、指针会被系统手势接管），
// 与其每个模块各自去监听 window.blur，不如集中在这里上报一次，由 C# 分发给全部模块。
export const SysFocusLost = 0;
export const SysFocusGained = 1;
export const SysPageHidden = 2;
export const SysPageVisible = 3;
let pendingSystem = -1; // 待上报的系统事件；-1 = 无
const sysPayload = new Uint8Array(1);
let bound = false;
function onFocus() { pendingSystem = SysFocusGained; }
function onBlur() { pendingSystem = SysFocusLost; }
function onVisibility() { pendingSystem = document.hidden ? SysPageHidden : SysPageVisible; }
/** 绑定系统级监听（窗口焦点 / 页面可见性），调用一次即可，重复调用无副作用。 */
export function bindFrameEvents() {
    if (bound)
        return;
    window.addEventListener('focus', onFocus);
    window.addEventListener('blur', onBlur);
    document.addEventListener('visibilitychange', onVisibility);
    bound = true;
}
export function unbindFrameEvents() {
    if (!bound)
        return;
    window.removeEventListener('focus', onFocus);
    window.removeEventListener('blur', onBlur);
    document.removeEventListener('visibilitychange', onVisibility);
    bound = false;
    pendingSystem = -1;
}
function writeSystemEvents(w) {
    if (pendingSystem < 0)
        return;
    sysPayload[0] = pendingSystem;
    w.put(ModuleId.System, sysPayload, 1);
    pendingSystem = -1;
}
/**
 * 生成本帧所有输入模块的事件流，供帧回调一并送往 C#。
 *
 * 布局：<c>scratch[0]</c> = 事件条数；其后每条 = <c>[module(1)][len(2, 小端)][payload(len)]</c>。
 * C# 侧按 module 把 payload 原样转交对应的 Input_* 类解析。
 *
 * 返回的是 scratch 的<b>视图</b>（subarray），不分配新数组 —— 每帧零分配。
 */
export function takeFrameData() {
    count = 0;
    off = 1; // 第 0 字节留给条数
    writeSystemEvents(sink);
    writeKeyboardEvents(sink);
    writeMouseEvents(sink);
    writeTouchEvents(sink);
    scratch[0] = count;
    return scratch.subarray(0, off); // 只带用到的字节
}
