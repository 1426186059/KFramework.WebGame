// 【共享模块】画布 / 窗口级事件的统一监听处：尺寸变化、聚焦 / 失焦、页面可见性。
// 本模块只负责"监听并把事件攒起来"，不跨界、不产生 JS 文件里的导出入口 ——
// 每帧由 game_frame_take_js_data 调 {@link drainWindowEvents} 取走，随本帧事件流一起送进 C#。
//
// 为什么单列一个模块：这些事件的来源分散（ResizeObserver / window resize / canvas focus /
// document visibilitychange），以前散落在 game_frame_take_js_data 与 input_mouse 里各绑一份，
// 既重复又容易漏。集中到这里，谁要监听一目了然。
//
// 为什么攒进 Map 而不是直接发：见下面的 pending 说明。
//
// 【失焦的唯一出口】本模块还是全部"失焦 / 交互被中断"的<b>汇合点</b>，来源共三种，全部落成同一条 SysFocusLost：
//   1. 画布 blur                        （本模块监听）
//   2. 每帧 document.hasFocus() 兜底    （pollFocus，切走窗口时规范保证会变 false）
//   3. input_mouse 转交的 pointercancel （手势被系统接管 —— 它知道当前按住了哪些键）
// 键盘原先的 KeyBlur、鼠标原先的 PointerCancel 都已取消 ——
// takeFrameData 只要看到这条，就【只上报它一条】、本帧其余数据一律作废（见 {@link focusLostType}）。
//
// 右键菜单（contextmenu）【不算】失焦：它只拦掉浏览器菜单（见 onContextMenu）。
// 拦掉之后 mouseup 正常送达，不必用失焦来兜；而判失焦会作废整帧，把右键自己的 mousedown 一起丢掉。
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
// 本模块<b>没有 enabled 开关</b>，也不提供 unbind —— 与键盘 / 鼠标 / 触摸那三个装置不同。
//
// 理由：它不是"可开关的输入装置"，只是系统监听的挂载点。尺寸 / 焦点 / 可见性在游戏的
// 整个生命周期里<b>始终要上报</b>（画布尺寸一旦漏同步，渲染分辨率就一直错下去），
// 既没有"停用"的场合，也没有解绑出口（C# 侧只有 BindFrameEvents、没有对应的 Unbind）。
// 因此 bindWindowEvents 不带重入守卫（调用方 C# 侧有自己的 _bound），drainWindowEvents 也不加判断。
//
// 曾试图留一个 <c>const enabled = true</c> 把"始终启用"写成常量，但 tsc 开了 noUnusedLocals：
// 恒为 true 又没有任何读取点的声明编译不过 —— 既然没有读取点，就说明这个状态本来不存在。
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
/**
 * 右键菜单（含触摸板的右键手势 / 长按菜单）：只拦掉浏览器菜单，<b>不判失焦</b>。
 *
 * 【为什么必须拦】菜单一弹出，浏览器就<b>吞掉后续的 mouseup</b> ——
 * 拦不掉的话，右键会永远卡在"按住"，表现为松开后 UI 仍显示 Right 按住不放。
 * 这正是之前"右键一直按住"的直接原因。
 *
 * 【为什么不判失焦】菜单被拦掉之后，mouseup 是正常送达的，本来就不需要失焦来兜；
 * 而判失焦的代价太大 —— 它会作废整帧，把右键自己的 mousedown 一起丢掉（右键变成完全没反应），
 * 还要清空键盘状态、触发 FocusChanged(false)。一次普通右键点击不该有这种后果。
 *
 * 【为什么不放在画布上】见 bindWindowEvents 的注释：本模块的 canvas 是自己解析的，
 * 解析不到就是 null、监听一条都挂不上（同样的原因，mouseup 早就挂在 window 上了）。
 */
function onContextMenu(e) {
    e.preventDefault(); // 阻止浏览器默认菜单
}
/**
 * 由其它输入模块调用：上报一次"<b>失焦 / 交互被中断</b>"。
 *
 * 取代了原先两条各说各话的事件：
 *   * 键盘失焦 → KeyBlur（只清键盘）
 *   * 指针被系统接管（pointercancel）→ PointerCancel（只通知那一个键）
 * 现在两者共用 SysFocusLost，上层只认"这一帧的交互断了"，不必分辨是哪种断法；
 * C# 侧收到后一律 ReleaseAll + ResetAll，比原来各自清一半更不容易漏。
 *
 * 右键菜单（contextmenu）【不走这里】—— 它只拦菜单（见 onContextMenu），不是一次交互中断。
 */
export function reportFocusLost() {
    pending.set(E_HTML_Event_Type.SysFocusLost, noData);
}
// ---------- 绑定 / 解绑 ----------
/**
 * 挂上全部监听 —— <b>只调用一次</b>，且没有对应的解绑（见 <c>enabled</c> 的注释）。
 *
 * 调用方是 C# 的 <c>Input_GameFrameData.Update</c>：它在首次取数据前懒绑定一次，
 * 并有自己的 <c>_bound</c> 守卫，所以这里不再重复设一道重入判断。
 *
 * 焦点一律绑在<b>画布</b>上（游戏关心的是"用户在不在游戏里"，窗口本身是否失焦不是重点），
 * 但需要 {@link pollFocus} 兜底：切走窗口时 <c>document.hasFocus()</c> 必然变 false（规范保证），
 * 而"焦点元素会不会收到 blur"没有保证 —— 漏掉就是按住的键收不到 keyup 而卡死。
 */
export function bindWindowEvents(canvasId) {
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
    // 【contextmenu 挂 window 而不是画布】右击的目标不一定是本模块解析到的那块画布：
    // 本模块的 canvas 由 getInputCanvas() 自己解析（bindFrameEvents 从 C# 侧拿不到 id），
    // 解析不到就是 null —— 挂在上面的监听一条都不会生效，等于没拦住菜单。
    // 挂在 window 上则无论右击落在哪个元素都拦得到（与 mouseup 挂 window 同一理由）。
    window.addEventListener('contextmenu', onContextMenu);
    wasFocused = document.hasFocus();
    // 首帧先报一次尺寸：初始化时 C# 需要知道起始尺寸，不能等到第一次 resize。
    reportCanvasSize();
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
 * 本帧是否"失焦过"；非 null 时返回<b>应当单独上报的那一条</b>事件类型。
 *
 * 判定只看 pending 里有没有 SysFocusLost / SysPageHidden ——
 * 所有失焦来源（画布 blur、hasFocus 兜底、{@link reportFocusLost}）都汇到这两个键上，
 * 所以不需要另开一个标志位去和 pending 同步（多一份状态就多一处会不同步的地方）。
 * 两条同时存在时取 SysFocusLost：C# 侧对这两者的处理本就完全相同，
 * 而"窗口失焦"是更具体的那条，报它更贴近实情。
 */
export function focusLostType() {
    if (pending.has(E_HTML_Event_Type.SysFocusLost))
        return E_HTML_Event_Type.SysFocusLost;
    if (pending.has(E_HTML_Event_Type.SysPageHidden))
        return E_HTML_Event_Type.SysPageHidden;
    return null;
}
/**
 * 【失焦帧的专属出口】只写一条失焦事件，本帧其余数据<b>全部作废</b>。
 *
 * 作废的理由：失焦那一刻起，之前攒下的都是<b>陈旧状态</b> ——
 * 窗外松手收不到 keyup、被接管的指针收不到 mouseup，把它们照常发出去，
 * 上层只会看到"一直按住"。C# 侧收到这条会 ReleaseAll + ResetAll，
 * 而 JS 侧各模块的待发数据由它们自己 discard（见 game_frame_take_js_data）。
 *
 * 唯一不作废的是 <c>CanvasResized</c>：尺寸只在<b>真变化</b>时才上报一次，
 * 跟着本帧一起丢掉就再也补不回来了（要等下一次 resize），分辨率会一直错下去。
 * 故把它留在 pending 里，下一帧（没失焦）随正常流程发出。
 */
export function drainFocusLostOnly(w, lostType) {
    const size = pending.get(E_HTML_Event_Type.CanvasResized);
    pending.clear();
    if (size)
        pending.set(E_HTML_Event_Type.CanvasResized, size);
    w.put(lostType, noData, 0);
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
