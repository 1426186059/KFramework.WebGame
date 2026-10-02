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
import { MAX_EVENTS_PER_FRAME, MAX_FRAME_BYTES, evDataBytes } from './html_event_type.js';
import { bindWindowEvents, drainWindowEvents, pollFocus, unbindWindowEvents } from './input_window_event.js';
import { writeKeyboardEvents } from './input_keyboard.js';
import { writeMouseEvents } from './input_mouse.js';
import { writeTouchEvents } from './input_touch.js';
const scratch = new Uint8Array(MAX_FRAME_BYTES);
let count = 0;
let off = 0;
const sink = {
    put(type, data, len) {
        // 布局：[count(1)][ [type(1)][data] × count ]。data 长度按 type 定长（evDataBytes），
        // 所以解析端查表推进即可 —— 流里无需再带长度，也不会解析错位。
        const n = len > 0 ? len : evDataBytes(type);
        if (count >= MAX_EVENTS_PER_FRAME || off + 1 + n > MAX_FRAME_BYTES)
            return; // 放不下就整条丢弃，绝不写半截
        scratch[off++] = type;
        scratch.set(data.subarray(0, n), off);
        off += n;
        count++;
    },
};
// ---------- 画布 / 窗口级事件（尺寸变化、聚焦、可见性）----------
// 全部交给 input_window_event 统一监听，它攒进一个 Map，本文件每帧 drain 一次取走。
// 这里只剩"转发"，不再自己挂任何监听 —— C# 侧的调用入口保持不变。
export function bindFrameEvents(canvasId) {
    bindWindowEvents(canvasId);
}
export function unbindFrameEvents() {
    unbindWindowEvents();
}
/**
 * 取本帧全部输入模块的事件流：<b>写进 C# 传来的缓冲</b>，返回写入的字节数。
 *
 * 布局：<c>[count(1)][ [type(1)][data] × count ]</c>，类型与字节数见 html_event_type。
 *
 * 【签名必须与 C# 对齐】<c>int TakeFrameData(Span&lt;byte&gt; buffer)</c> ——
 * 返回值是<b>字节数</b>（不是数组）：C# 侧声明的返回类型是 int，若这里交回一个 Uint8Array，
 * 运行时会在 marshal 时断言失败（"Value is not an integer: 0 (object)"）。
 * 写入走 MemoryView 零拷贝，每帧不分配。
 */
export function takeFrameData(target) {
    pollFocus(); // 焦点兜底（替代 window 的 focus / blur 监听）
    count = 0;
    off = 1; // 第 0 字节留给条数
    // 画布 / 窗口事件先写：尺寸与失焦都要早于本帧的输入事件生效
    drainWindowEvents(sink);
    writeKeyboardEvents(sink);
    writeMouseEvents(sink);
    writeTouchEvents(sink);
    scratch[0] = count;
    // 直写 C# 的缓冲：同步调用期间没有 await，Span 的 MemoryView 有效，
    // 且它的 set(源, 偏移) 与 Uint8Array 同签名（见 http_func.ts 的同样写法）。
    target.set(scratch.subarray(0, off), 0);
    return off;
}
