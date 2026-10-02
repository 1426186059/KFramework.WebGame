// 【共享模块】画布 / 窗口级事件的统一监听处：尺寸变化、聚焦 / 失焦、页面可见性。
// 本模块只负责"监听并把事件攒起来"，不跨界、不产生 JS 文件里的导出入口 ——
// 每帧由 game_frame_take_js_data 调 {@link drainWindowEvents} 取走，随本帧事件流一起送进 C#。
//
// 为什么单列一个模块：这些事件的来源分散（ResizeObserver / window resize / 页面可见性 /
// 每帧焦点兜底），以前散落在 game_frame_take_js_data 与 input_mouse 里各绑一份，
// 既重复又容易漏。集中到这里，谁要监听一目了然。
//
// 为什么攒进 Map 而不是直接发：见下面的 pending 说明。
//
// 【失焦的唯一出口】本模块还是全部"失焦 / 交互被中断"的<b>汇合点</b>，来源共两种，全部落成同一条 SysFocusLost：
//   1. 每帧 document.hasFocus() 兜底    （pollFocus —— <b>唯一</b>的焦点来源，见下方说明）
//   2. input_mouse 转交的 pointercancel （手势被系统接管 —— 它知道当前按住了哪些键）
//
// 【为什么不监听画布 / 窗口的 focus、blur】元素级焦点与游戏要的"窗口失焦"不是一回事，
// 右键按住会让画布瞬时 blur，据此判失焦会把正在进行的拖拽取消掉（实测复现，见 onContextMenu 附近那段）。
// document.hasFocus() 是窗口级的、规范保证切走窗口时为 false，由 pollFocus 每帧采样即可，
// 最多晚一帧发现，且不会误报。
// 键盘原先的 KeyBlur、鼠标原先的 PointerCancel 都已取消 ——
// 右键菜单（contextmenu）【不算】失焦：它只拦掉浏览器菜单（见 onContextMenu）——
// 拦不住的话浏览器会吞掉 mouseup，那只键会一直卡在"按住"；拦住之后 mouseup 正常送达，无需兜底。
// takeFrameData 看到这条（见 {@link hadFocusLost}）就作废键盘 / 鼠标 / 触摸本帧攒下的全部数据，
// 系统事件本身照发（见 {@link drainWindowEvents}）。

import { E_HTML_Event_Type, FrameDataStream } from './html_event_type.js';
import { getInputCanvas, setCanvasId } from './input_common.js';
import { focusCanvas } from './html_canvas.js';

/**
 * 待上报事件：<c>类型 → data</c>。
 *
 * 用 Map 而不是队列，是为了<b>同类型去重</b>：一帧内连续 resize 会触发多次回调，
 * 但只有<b>最终尺寸</b>有意义，中间那些上报过去只是白占带宽、还让 C# 侧多做几次同步。
 * 同理，一帧内"失焦又聚焦"也只剩最后那一次。
 */
const pending = new Map<number, Uint8Array>();

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
let canvas: HTMLCanvasElement | null = null;

// ResizeObserver 观察的是元素的 CSS 尺寸（布局盒），比 window resize 更准 ——
// CSS 改了、父容器变了、flex 重排了都会触发，而这些 window resize 都不触发。
let observer: ResizeObserver | null = null;

let wasFocused = true;

// ---------- 事件来源 ----------

/**
 * 夹到 int16 范围：画布尺寸远小于 32767，理论上不会超；
 * 真超了也要给出边界值，而不是让 setInt16 静默溢出成负数（那样 C# 侧会拿到一个荒谬的宽高）。
 */
function clampI16(v: number): number {
    return v > 32767 ? 32767 : v < -32768 ? -32768 : v | 0;
}

/** 画布尺寸变了：读取当前完整尺寸写进 Map（与 platform.getCanvasSize 的 out 布局同序）。 */
function reportCanvasSize(): void {
    const c = canvas;
    if (!c) return;

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

function onResize(): void {
    reportCanvasSize();
}

// 【不再监听画布的 focus / blur】—— 实测这是右键手势被误判成"失焦"的根源：
//
// 右键按住时浏览器会为手势 / 菜单把画布的焦点拿走，于是画布 blur；而<b>在 blur 事件那一刻
// document.hasFocus() 也确实返回 false</b>（日志已确认），所以"加一层 hasFocus 守卫"拦不住。
// 但这个 false 是<b>瞬时的</b>：pollFocus() 每帧采样 hasFocus()，同样的右键操作它一条都没报过 ——
// 说明到帧边界时焦点已经回来。
//
// 根本问题是这两者不是一回事：
//   * 画布 focus / blur = <b>元素</b>级焦点（右键手势、页面内焦点转移都会触发，交互并没断）；
//   * document.hasFocus() = <b>窗口</b>级焦点，规范保证切走窗口时为 false —— 这才是我们要的。
// 后者由 {@link pollFocus} 每帧兜底，最多晚一帧（约 16ms）发现，且不会误报。
// 同理不再监听 window 的 focus / blur：原生右键菜单同样可能让窗口瞬时失焦。

function onVisibility(): void {
    pending.set(document.hidden ? E_HTML_Event_Type.SysPageHidden : E_HTML_Event_Type.SysPageVisible, noData);
}

/**
 * 右键菜单（含触摸板的右键手势 / 长按菜单）：只拦掉浏览器菜单，<b>不判失焦</b>。
 *
 * 【为什么必须拦】菜单一弹出，浏览器就<b>吞掉后续的 mouseup</b> ——
 * 拦不掉的话，右键会永远卡在"按住"，表现为松开后 UI 仍显示 Right 按住不放。
 * 这正是之前"右键一直按住"的直接原因。
 *
 * 【为什么<b>不</b>判失焦】判失焦会作废整帧，把右键自己的 mousedown 一起丢掉，还要清空键盘状态、
 * 触发 FocusChanged(false) —— 一次普通右键点击不该有这种后果，也不能因为"怕收不到 mouseup"
 * 就让每次右键都被当成一次交互中断。菜单被拦掉之后，mouseup 是正常送达的。
 *
 * 【为什么不放在画布上】见 bindWindowEvents 的注释：本模块的 canvas 是自己解析的，
 * 解析不到就是 null、监听一条都挂不上（同样的原因，mouseup 早就挂在 window 上了）。
 * 之前"右键一直按住"就是因为挂画布上没生效、菜单真弹出、mouseup 被吞。
 */
function onContextMenu(e: Event): void {
    e.preventDefault();                 // 阻止浏览器默认菜单

    //经测试 触发手势, 不会触发这个事件
    console.log("onContextMenu 触发手势");
}

function onWindowBlur(): void 
{
    //经测试 触发手势, 不会触发这个事件
    console.log("onWindowBlur onWindowBlur");
}

/**
 * 上报一次<b>失焦</b>（由 {@link pollFocus} 的 <c>document.hasFocus()</c> 驱动）。
 *
 * C# 侧收到后只复位<b>键盘 / 触摸</b>（它们的 keyup / touchend 确实收不到），
 * <b>不动鼠标</b> —— 见 {@link reportPointerCancel} 里说明的理由。
 */
export function reportFocusLost(): void {
    pending.set(E_HTML_Event_Type.SysFocusLost, noData);
}

/**
 * 由 input_mouse 在 <c>pointercancel</c> 时调用：指针被系统 / 浏览器接管。
 *
 * 为什么<b>不</b>复用 SysFocusLost —— 实测证明两者必须分开：
 *   * 右键按住拖动时，浏览器会为手势把焦点拿走，<c>document.hasFocus()</c> 真的变 false，
 *     于是 SysFocusLost 会误报。若据此释放按键，正在进行的右键拖拽当场被取消。
 *   * 而 mouseup <b>是可靠送达的</b>（日志里每次 down 后都跟到了 up），
 *     所以"焦点变了"根本不需要鼠标去兜底 —— 交给 mousedown / mouseup 自己就行。
 *   * 真正需要兜底的是 pointercancel：那时浏览器<b>不会</b>再派发 mouseup，键会一直卡住。
 *
 * 因此 SysFocusLost 只复位键盘 / 触摸（它们的 keyup / touchend 确实收不到），
 * 鼠标按键只在 SysPageHidden（页面切后台）与这条 SysPointerCancel 时才释放。
 */
export function reportPointerCancel(): void {
    pending.set(E_HTML_Event_Type.SysPointerCancel, noData);
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
export function bindWindowEvents(canvasId?: string | null): void 
{
    if (canvasId) setCanvasId(canvasId);
    canvas = getInputCanvas();

    if (canvas) {
        // 画布必须先可获焦（tabIndex + focus），否则键盘监听收不到事件（它是绑在画布上的）。
        // 注意：这里只让它"可获焦并聚焦"，<b>不</b>监听它的 focus / blur —— 见本文件开头那段说明。
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
    window.addEventListener('blur', onWindowBlur);
    wasFocused = document.hasFocus();

    // 首帧先报一次尺寸：初始化时 C# 需要知道起始尺寸，不能等到第一次 resize。
    reportCanvasSize();
}

/**
 * 上一次 {@link pollFocus} 的结论：文档当前是否失焦。
 * 供 input_mouse 的"卡键看门狗"使用 —— 直接读 pollFocus 的结果，不重复调 hasFocus()。
 */
export function isFocusAway(): boolean {
    return !wasFocused;
}

/** 每帧兜一次焦点（替代 window 的 focus / blur 监听）。 */
export function pollFocus(): void {
    const now = document.hasFocus();
    if (wasFocused && !now) pending.set(E_HTML_Event_Type.SysFocusLost, noData);
    else if (!wasFocused && now) pending.set(E_HTML_Event_Type.SysFocusGained, noData);
    wasFocused = now;
}

/**
 * 本帧是否"失焦过"。
 *
 * 判定只看 pending 里有没有 SysFocusLost / SysPageHidden ——
 * 所有失焦来源（画布 blur、hasFocus 兜底、{@link reportFocusLost}、{@link deferFocusLost}）
 * 都汇到这两个键上，所以不需要另开一个标志位去和 pending 同步（多一份状态就多一处会不同步的地方）。
 *
 * <b>返回 boolean，不是 number | null</b>：调用方要用 <c>if (lost)</c>，
 * 写成 <c>if (lost !== null)</c> 会恒为 true —— 那等于每帧都把输入丢光。
 */
export function hadFocusLost(): boolean {
    return pending.has(E_HTML_Event_Type.SysFocusLost)
        || pending.has(E_HTML_Event_Type.SysPageHidden)
        || pending.has(E_HTML_Event_Type.SysPointerCancel);
}

/**
 * 把攒下的事件写进本帧事件流，写完清空（由 game_frame_take_js_data 每帧调用一次）。
 *
 * <b>无条件写</b>，失焦帧也不例外 —— 尺寸 / 焦点 / 可见性必须始终同步：
 * 画布尺寸只在真变化时才上报一次，跟着失焦帧一起丢掉就再也补不回来（要等下次 resize），
 * 渲染分辨率会一直错下去。失焦帧要作废的只是键盘 / 触摸的数据，那是它们自己 discard 的事。
 *
 * 顺序上先于各输入模块 —— 尺寸与失焦都要早于本帧的输入事件生效。
 */
export function drainWindowEvents(w: FrameDataStream): void {
    for (const [type, data] of pending) {
        w.put(type, data, data.length);
    }
    pending.clear();
}
