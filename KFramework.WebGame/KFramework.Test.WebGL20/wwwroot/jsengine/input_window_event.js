// 【共享模块】画布 / 窗口级事件的统一监听处：尺寸变化、聚焦 / 失焦、页面可见性。
// 本模块只负责"监听并把事件攒起来"，不跨界、不产生 JS 文件里的导出入口 ——
// 每帧由 game_frame_take_js_data 调 {@link drainWindowEvents} 取走，随本帧事件流一起送进 C#。
//
// 为什么单列一个模块：这些事件的来源分散（ResizeObserver / window resize / canvas focus /
// document visibilitychange），以前散落在 game_frame_take_js_data 与 input_mouse 里各绑一份，
// 既重复又容易漏。集中到这里，谁要监听一目了然。
//
// 为什么攒进 Map 而不是直接发：见下面的 pending 说明。
import { E_HTML_Event_Type } from './html_event_type.js';
import { getInputCanvas, setCanvasId } from './input_common.js';
import { focusCanvas } from './html_canvas.js';
/**
 * 待上报事件：<c>类型 → data</c>。
 *
 * 用 Map 而不是队列，是为了<b>同类型去重</b>：一帧内连续 resize 会触发多次回调，
 * 但只有<b>最终尺寸</b>有意义，中间那些上报过去只是白占带宽、还让 C# 侧多做几次同步。
 * 同理，一帧内"失焦又聚焦"也只剩最后那一次。
 */
const pending = new Map();
/** 尺寸事件的 data（5 × i16 = 10 字节），复用同一块缓冲。 */
const sizeData = new Uint8Array(10);
const sizeView = new DataView(sizeData.buffer);
/** 无 data 的事件（失焦 / 可见性）共用一个空数组，避免为每次上报新建数组。 */
const noData = new Uint8Array(0);
let bound = false;
let canvas = null;
// ResizeObserver 观察的是元素的 CSS 尺寸（布局盒），比 window resize 更准 ——
// CSS 改了、父容器变了、flex 重排了都会触发，而这些 window resize 都不触发。
let observer = null;
let wasFocused = true;
// ---------- 事件来源 ----------
/**
 * 夹到 int16 范围：画布尺寸远小于 32767，理论上不会超；
 * 真超了也要给出边界值，而不是让 setInt16 静默溢出成负数（那样 C# 侧会拿到一个荒谬的宽高）。
 */
function clampI16(v) {
    return v > 32767 ? 32767 : v < -32768 ? -32768 : v | 0;
}
/** 画布尺寸变了：读取当前完整尺寸写进 Map（与 platform.getCanvasSize 的 out 布局同序）。 */
function reportCanvasSize() {
    const c = canvas;
    if (!c)
        return;
    const rect = c.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    // CSS 尺寸 × DPR 即绘制缓冲尺寸 —— 与 platform.js 的 getCanvasSize 同一套算法，
    // 两边必须一致，否则事件里报的尺寸会和"直接查"的结果对不上。
    const bw = Math.max(1, Math.round((rect.width || 1) * dpr));
    const bh = Math.max(1, Math.round((rect.height || 1) * dpr));
    sizeView.setInt16(0, clampI16(Math.round(rect.width || 1)), true);
    sizeView.setInt16(2, clampI16(Math.round(rect.height || 1)), true);
    sizeView.setInt16(4, clampI16(bw), true);
    sizeView.setInt16(6, clampI16(bh), true);
    sizeView.setInt16(8, clampI16(Math.round(dpr * 1000)), true);
    pending.set(E_HTML_Event_Type.CanvasResized, sizeData);
}
function onResize() {
    reportCanvasSize();
}
function onFocus() {
    pending.set(E_HTML_Event_Type.SysFocusGained, noData);
}
function onBlur() {
    pending.set(E_HTML_Event_Type.SysFocusLost, noData);
}
function onVisibility() {
    pending.set(document.hidden ? E_HTML_Event_Type.SysPageHidden : E_HTML_Event_Type.SysPageVisible, noData);
}
// ---------- 绑定 / 解绑 ----------
/**
 * 挂上全部监听（调用一次即可，重复调用无副作用）。
 *
 * 焦点一律绑在<b>画布</b>上（游戏关心的是"用户在不在游戏里"，窗口本身是否失焦不是重点），
 * 但需要 {@link pollFocus} 兜底：切走窗口时 <c>document.hasFocus()</c> 必然变 false（规范保证），
 * 而"焦点元素会不会收到 blur"没有保证 —— 漏掉就是按住的键收不到 keyup 而卡死。
 */
export function bindWindowEvents(canvasId) {
    if (bound)
        return;
    if (canvasId)
        setCanvasId(canvasId);
    canvas = getInputCanvas();
    if (canvas) {
        canvas.addEventListener('focus', onFocus);
        canvas.addEventListener('blur', onBlur);
        // 画布必须先可获焦（tabIndex + focus），否则整条焦点链路都是断的
        focusCanvas(canvasId ?? null);
        if (typeof ResizeObserver !== 'undefined') {
            observer = new ResizeObserver(onResize);
            observer.observe(canvas);
        }
    }
    // window resize 仍需监听：浏览器缩放 / DPR 变化会走这里，
    // 而那时画布的 CSS 尺寸可能没变，ResizeObserver 不会触发。
    window.addEventListener('resize', onResize);
    document.addEventListener('visibilitychange', onVisibility);
    bound = true;
    wasFocused = document.hasFocus();
    // 首帧先报一次尺寸：初始化时 C# 需要知道起始尺寸，不能等到第一次 resize。
    reportCanvasSize();
}
export function unbindWindowEvents() {
    if (!bound)
        return;
    if (canvas) {
        canvas.removeEventListener('focus', onFocus);
        canvas.removeEventListener('blur', onBlur);
    }
    observer?.disconnect();
    observer = null;
    window.removeEventListener('resize', onResize);
    document.removeEventListener('visibilitychange', onVisibility);
    bound = false;
    canvas = null;
    pending.clear();
}
/** 每帧兜一次焦点（替代 window 的 focus / blur 监听）。 */
export function pollFocus() {
    const now = document.hasFocus();
    if (wasFocused && !now)
        pending.set(E_HTML_Event_Type.SysFocusLost, noData);
    else if (!wasFocused && now)
        pending.set(E_HTML_Event_Type.SysFocusGained, noData);
    wasFocused = now;
}
/**
 * 把攒下的事件写进本帧事件流，写完清空（由 game_frame_take_js_data 每帧调用一次）。
 * 顺序上先于各输入模块 —— 尺寸与失焦都要早于本帧的输入事件生效。
 */
export function drainWindowEvents(w) {
    for (const [type, data] of pending) {
        w.put(type, data, data.length);
    }
    pending.clear();
}
