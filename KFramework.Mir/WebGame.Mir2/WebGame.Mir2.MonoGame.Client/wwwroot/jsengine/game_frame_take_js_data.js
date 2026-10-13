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
import { bindWindowEvents, drainWindowEvents, hadFocusLost } from './input_window_event.js';
import { discardKeyboardEvents, writeKeyboardEvents } from './input_keyboard.js';
import { discardMouseEvents, writeMouseEvents } from './input_mouse.js';
import { discardTouchEvents, writeTouchEvents } from './input_touch.js';
let count = 0;
let off = 0;
// 复用的 1 字节临时缓冲：MemoryView 没有 [] 索引器（types.d.ts:15），单个 type 字节只能经 .set() 写入。
const _typeByte = new Uint8Array(1);
// 本帧的目标缓冲（C# 传进来的 MemoryView_Span）。sink 直接写进它，
// 避免先攒到 scratch 再整体 .set 一遍 —— 那一次 memcpy 正是当初"零拷贝"设计想消灭的。
let _target = null;
const sink = {
    put(type, data, len) {
        // 布局：[count(1)][ [type(1)][data] × count ]。data 长度按 type 定长（evDataBytes），
        // 所以解析端查表推进即可 —— 流里无需再带长度，也不会解析错位。
        const n = len > 0 ? len : evDataBytes(type);
        if (count >= MAX_EVENTS_PER_FRAME || off + 1 + n > MAX_FRAME_BYTES)
            return; // 放不下就整条丢弃，绝不写半截
        const t = _target;
        _typeByte[0] = type;
        t.set(_typeByte, off); // 单字节 type 直写 MemoryView（无索引器，只能 .set）
        off += 1;
        t.set(data.subarray(0, n), off); // data 已是 Uint8Array，直接 .set 进目标（源在事件对象里，这次拷贝不可避免）
        off += n;
        count++;
    },
};
// ---------- 画布 / 窗口级事件（尺寸变化、聚焦、可见性）----------
// 全部交给 input_window_event 统一监听，它攒进一个 Map，本文件每帧 drain 一次取走。
// 这里只剩"转发"，不再自己挂任何监听。
//
// 没有 unbindFrameEvents：系统监听在游戏生命周期内始终需要（画布尺寸一旦漏同步，
// 渲染分辨率就一直错下去），C# 侧也只有 BindFrameEvents、没有对应的 Unbind ——
// 留一个没人调用的解绑出口，只会让人以为"解绑后还能正常收尺寸"。
export function bindFrameEvents() {
    bindWindowEvents();
}
export function takeFrameData(target) {
    _target = target;
    count = 0;
    off = 1;
    const lost = hadFocusLost();
    drainWindowEvents(sink); //窗口系统事件，必须保留
    if (lost) {
        discardKeyboardEvents();
        discardTouchEvents();
        discardMouseEvents();
    }
    else {
        writeKeyboardEvents(sink);
        writeMouseEvents(sink);
        writeTouchEvents(sink);
    }
    _typeByte[0] = count;
    target.set(_typeByte, 0); // count 写回 0 号位（MemoryView，无索引器）
    _target = null;
    return off;
}
