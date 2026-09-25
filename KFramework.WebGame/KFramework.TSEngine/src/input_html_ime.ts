// 浏览器原生文本输入覆盖层（input_html_ime 模块）。
//
// 作用：在 canvas 之上叠加一个透明的 DOM <input>/<textarea>，承接键盘 / IME 捕获。
//       文字一律由引擎在 canvas 自绘（见 TextBoxRenderer / TextCaret），DOM 元素只作输入代理。
//
// 数据流向（纯 Pull，无回调）：
//   C# 主动调用 show / hide 控制覆盖层；每帧调用 getValue 拉取当前文本（含 IME 组字内容，
//   浏览器在组字过程中已把值写入 <input>.value，无需 composition 事件）。
//   DOM 不向 C# 推送任何事件。回车 / Esc 仅对普通字符 stopPropagation（避免触发游戏全局快捷键）；
//   非组字态的回车 / Esc 放行冒泡，由全局键盘（input_keyboard）捕获，供 C# 侧判断确认 / 取消；
//   IME 组字中的回车（选词）通过 isComposing 在 Web 端直接吞掉，不会冒泡误触发确认。
import { getCanvasElement } from './gl.js';

type InputEl = HTMLInputElement | HTMLTextAreaElement;

interface ShowParams {
    cx: number; cy: number; cw: number; ch: number;
    fontPx: number; color: number;
    password: boolean; maxLength: number; multiline: boolean;
    fontFamily: string;
    transparent: boolean;
}

let inputEl: HTMLInputElement | null = null;
let areaEl: HTMLTextAreaElement | null = null;
let last: ShowParams | null = null;
let composing = false; // IME 组字中（compositionstart..compositionend），用于拦截选词用的 Enter

function canvasMetrics(): { left: number; top: number; dpr: number } {
    const canvas = getCanvasElement();
    if (!canvas) return { left: 0, top: 0, dpr: 1 };
    const rect = canvas.getBoundingClientRect();
    const dpr = rect.width > 0 ? canvas.width / rect.width : 1; // backing/CSS 比
    return { left: rect.left, top: rect.top, dpr: dpr > 0 ? dpr : 1 };
}

function activeEl(): InputEl | null {
    if (!last) return null;
    const el = last.multiline ? areaEl : inputEl;
    return el && el.style.display !== 'none' ? el : null;
}

// 复用两个 DOM 元素：单行 <input> / 多行 <textarea>（互斥显示，避免类型切换异常）。
function ensureEl(multiline: boolean): InputEl {
    const el = multiline
        ? (areaEl ||= document.createElement('textarea'))
        : (inputEl ||= document.createElement('input'));
    if (inputEl && inputEl !== el) inputEl.style.display = 'none';
    if (areaEl && areaEl !== el) areaEl.style.display = 'none';
    if (!el.parentNode) attach(el);
    return el;
}

function attach(el: InputEl): void {
    el.style.position = 'fixed';          // 视口相对定位，与 getBoundingClientRect 对齐，滚动不漂移
    el.style.margin = '0';
    el.style.padding = '0';
    el.style.border = 'none';
    el.style.outline = 'none';
    el.style.background = 'transparent';
    el.style.boxSizing = 'border-box';
    el.style.zIndex = '10';
    el.style.pointerEvents = 'none'; // 点击穿透到 canvas，由引擎按坐标自算光标（对齐 UGUI InputField 自行处理指针）
    el.style.display = 'none';
    el.setAttribute('autocomplete', 'off');
    el.setAttribute('spellcheck', 'false');
    document.body.appendChild(el);

    // 普通字符 stopPropagation，避免冒泡到 window 被 input_keyboard 再次消费
    // （例如密码里敲字母触发游戏全局快捷键）。回车 / Esc 的放行策略见下方 keydown。
    el.addEventListener('keydown', (e: Event) => {
        const ke = e as KeyboardEvent;
        const ctrl = ke.ctrlKey || ke.metaKey, shift = ke.shiftKey, alt = ke.altKey;
        const k = ke.key;
        const controlKeys = ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'Enter', 'Escape', 'Tab'];
        if (ctrl && (k === 'a' || k === 'A')) {
            host.exports.MirEngine.BrowserInputIme.OnKeyDown(k, ctrl, shift, alt);
            ke.preventDefault();
            return;
        }
        if (controlKeys.indexOf(k) !== -1) {
            // 控制键转发给引擎，由引擎自行维护光标 / 文本（引擎是唯一真相源）
            host.exports.MirEngine.BrowserInputIme.OnKeyDown(k, ctrl, shift, alt);
            ke.preventDefault();
            return;
        }
        e.stopPropagation();
    });
    // 跟踪 IME 组字状态（比单纯依赖 keydown.isComposing 更稳，覆盖部分浏览器边界）。
    // 原生编辑结果回传：以引擎自身光标为锚点合并进 text（引擎是唯一真相源）
    el.addEventListener('input', (e: Event) => {
        const el2 = activeEl();
        if (!el2) return;
        const ke = e as InputEvent;
        host.exports.MirEngine.BrowserInputIme.OnDomValue(el2.value, ke.isComposing === true);
    });
    // 跟踪 IME 组字状态（比单纯依赖 keydown.isComposing 更稳，覆盖部分浏览器边缘）。
    el.addEventListener('compositionupdate', () => {
        const el2 = activeEl();
        if (el2) host.exports.MirEngine.BrowserInputIme.OnDomValue(el2.value, true);
    });
    el.addEventListener('compositionstart', () => { composing = true; });
    el.addEventListener('compositionend', () => { composing = false; });

}

// 把完整 CSS 字体串中的 px 按 dpr 缩放到 CSS 像素后整体套用，使 DOM 字形与画布 SpriteFont 一致。
function scaleFontPx(css: string | undefined, dpr: number): string {
    const c = (css || '').trim();
    if (!c) return (10 / dpr) + 'px sans-serif';
    const m = c.match(/([\d.]+)\s*px/);
    if (!m) return c;
    const px = parseFloat(m[1]) / dpr;
    return c.replace(/([\d.]+)\s*px/, px.toFixed(2) + 'px');
}

function place(el: HTMLElement, p: ShowParams): void {
    const { left, top, dpr } = canvasMetrics();
    el.style.left = left + p.cx / dpr + 'px';
    el.style.top = top + p.cy / dpr + 'px';
    el.style.width = p.cw / dpr + 'px';
    el.style.height = p.ch / dpr + 'px';
    el.style.font = scaleFontPx(p.fontFamily, dpr);
    // transparent=true：文字与光标由引擎在 canvas 自绘，本 DOM 元素仅作捕获代理，颜色/光标透明避免重影。
    // 透明不影响 IME——候选窗是浏览器 UI，仍按本元素光标位置弹出。
    if (p.transparent) {
        el.style.color = 'transparent';
        el.style.caretColor = 'transparent';
    } else {
        const r = (p.color >> 16) & 0xff, g = (p.color >> 8) & 0xff, b = p.color & 0xff;
        el.style.color = `rgb(${r},${g},${b})`;
        el.style.caretColor = `rgb(${r},${g},${b})`;
    }
}

/** C# 主动激活覆盖层：在画布上以 cx/cy/cw/ch（后备缓冲像素）显示原生输入框并聚焦。 */
export function show(cx: number, cy: number, cw: number, ch: number, fontPx: number, color: number, value: string, password: boolean, maxLength: number, multiline: boolean, fontFamily: string, transparent: boolean = true): void {
    composing = false;
    const el = ensureEl(multiline);
    last = { cx, cy, cw, ch, fontPx, color, password, maxLength, multiline, fontFamily, transparent };
    place(el, last);
    el.style.display = 'block';
    el.value = value ?? '';
    if (multiline) {
        el.style.resize = 'none';
        el.style.whiteSpace = 'pre-wrap';
        el.style.overflow = 'hidden';
    } else {
        (el as HTMLInputElement).type = password ? 'password' : 'text';
    }
    if (maxLength > 0 && maxLength < 100000) el.maxLength = maxLength;
    else el.removeAttribute('maxlength');
    // 延迟聚焦：确保样式/定位已生效后再聚焦，浏览器才会弹出软键盘 / IME。
    setTimeout(() => {
        try {
            el.focus();
            const len = el.value.length;
            if (typeof el.setSelectionRange === 'function') el.setSelectionRange(len, len);
        } catch { /* ignore */ }
    }, 0);
}

/** C# 主动关闭覆盖层：隐藏并移除输入框（失焦），清空最近一次 show 参数。 */
export function hide(): void {
    last = null;
    composing = false;
    if (inputEl) inputEl.style.display = 'none';
    if (areaEl) areaEl.style.display = 'none';
}

/** C# 每帧拉取：返回当前输入框文本（含 IME 组字内容）；未激活时返回空串。 */
export function getValue(): string {
    const el = activeEl();
    return el ? el.value : '';
}

/** C# 把引擎 text + IME 预览写回 DOM，使镜像与引擎保持一致。 */
export function setValue(v: string): void {
    const el = activeEl();
    if (!el) return;
    if (el.value !== (v ?? '')) el.value = v ?? '';
}

/** C# 把引擎光标区间写回 DOM（对齐 IME 候选窗位置）。 */
export function setSelectionRange(start: number, end: number): void {
    const el = activeEl();
    if (!el) return;
    try { if (typeof el.setSelectionRange === 'function') el.setSelectionRange(start, end); } catch { /* ignore */ }
}

/** 重新定位当前可见输入框（窗口缩放 / 页面滚动时由 reflow 自动调用，也可由 C# 显式调用）。 */
export function reposition(cx: number, cy: number, cw: number, ch: number): void {
    if (!last) return;
    last.cx = cx; last.cy = cy; last.cw = cw; last.ch = ch;
    const el = activeEl();
    if (el) place(el, last);
}

// 窗口缩放 / 页面滚动时，画布 rect 会变，重新按最近一次 show 参数定位当前可见输入框。
function reflow(): void {
    const el = activeEl();
    if (el && last) place(el, last);
}
window.addEventListener('resize', reflow);
window.addEventListener('scroll', reflow, true);
