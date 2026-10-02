// 【依赖 C#】由 KFramework.MonoGame.JSBind_GameFrameData 经 [JSImport(module: "game_frame_take_js_data")] 调用；
// 产物 game_frame_take_js_data.js 由 SyncJsEngine 复制。
//
// 每帧统一事件入口：各输入模块按 html_event_type 的 E_HTML_Event_Type 把本帧事件<b>逐条</b>写进这里的 sink，
// 汇总成一条扁平字节流后，由 C# 每帧取走【一次】（取代原先"键盘 / 鼠标 / 触摸各 poll 一次"）。
//
// 各输入模块【依旧独立】：各自注册监听、各自维护状态，只是把"写事件"这一步交给这里的 sink，
// 且事件格式统一到 html_event_type —— 合并的是传输通道与编码，不是各模块本身。
//
// 注：IME 走 [JSExport] 由 JS 主动推给 C#（OnDomValue / OnKeyDown），本来就不占这条拉取流。
import { FrameDataStream, E_HTML_Event_Type, MAX_EVENTS_PER_FRAME, MAX_FRAME_BYTES, evDataBytes } from './html_event_type.js';
import { writeKeyboardEvents } from './input_keyboard.js';
import { writeMouseEvents } from './input_mouse.js';
import { writeTouchEvents } from './input_touch.js';

const scratch = new Uint8Array(MAX_FRAME_BYTES);
let count = 0;
let off = 0;

const sink: FrameDataStream = {
    put(type: number, data: Uint8Array, len: number): void {
        // 布局：[count(1)][ [type(1)][data] × count ]。data 长度按 type 定长（evDataBytes），
        // 所以解析端查表推进即可 —— 流里无需再带长度，也不会解析错位。
        const n = len > 0 ? len : evDataBytes(type);
        if (count >= MAX_EVENTS_PER_FRAME || off + 1 + n > MAX_FRAME_BYTES) return;   // 放不下就整条丢弃，绝不写半截
        scratch[off++] = type;
        scratch.set(data.subarray(0, n), off);
        off += n;
        count++;
    },
};

// ---------- 系统事件：失焦 / 获得焦点 / 页面可见性 ----------
// 所有输入模块都依赖"失焦"这件事（按键会在窗外松手后卡住、指针会被系统手势接管），
// 与其每个模块各自去监听 window.blur，不如集中在这里上报一次，由 C# 分发给全部模块。
let pendingSystem = -1;                 // 待上报的 EvType；-1 = 无
let bound = false;

function onFocus(): void { pendingSystem = E_HTML_Event_Type.SysFocusGained; }
function onBlur(): void { pendingSystem = E_HTML_Event_Type.SysFocusLost; }
function onVisibility(): void { pendingSystem = document.hidden ? E_HTML_Event_Type.SysPageHidden : E_HTML_Event_Type.SysPageVisible; }

/** 绑定系统级监听（窗口焦点 / 页面可见性），调用一次即可，重复调用无副作用。 */
export function bindFrameEvents(): void {
    if (bound) return;
    window.addEventListener('focus', onFocus);
    window.addEventListener('blur', onBlur);
    document.addEventListener('visibilitychange', onVisibility);
    bound = true;
}

export function unbindFrameEvents(): void {
    if (!bound) return;
    window.removeEventListener('focus', onFocus);
    window.removeEventListener('blur', onBlur);
    document.removeEventListener('visibilitychange', onVisibility);
    bound = false;
    pendingSystem = -1;
}

const empty = new Uint8Array(0);

function writeSystemEvents(w: FrameDataStream): void {
    if (pendingSystem < 0) return;
    w.put(pendingSystem, empty, 0);      // 系统事件只靠 type，没有 data
    pendingSystem = -1;
}

/**
 * 汇总本帧全部输入模块的事件流，供 C# 每帧取走一次。
 *
 * 布局：<c>[count(1)][ [type(1)][data] × count ]</c>，类型与字节数见 html_event_type。
 * 返回的是 scratch 的<b>视图</b>（subarray），不分配新数组 —— 每帧零分配。
 */
export function takeFrameData(): Uint8Array {
    count = 0;
    off = 1;                    // 第 0 字节留给条数

    writeSystemEvents(sink);
    writeKeyboardEvents(sink);
    writeMouseEvents(sink);
    writeTouchEvents(sink);

    scratch[0] = count;
    return scratch.subarray(0, off);
}
