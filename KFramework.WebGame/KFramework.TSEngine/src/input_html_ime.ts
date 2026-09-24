// 浏览器原生文本输入覆盖层（input_html_ime 模块）。
//
// 作用：在 canvas 之上叠加一个透明的 DOM <input>/<textarea>，承接键盘 / IME / 密码掩码，
//       把输入结果经 setHandlers 注册的 C# 回调（[JSExport]）推回 JSBind_InputHtmlIme，驱动登录 / 输入框。
//
// 为什么用 DOM 覆盖层而不是在 canvas 上自绘：
//   canvas/WebGL 是位图画布，光标、选区、IME 组字（preedit）、候选窗、软键盘等“活”的输入能力
//   由操作系统/浏览器维护，canvas 无法等价复刻；热血传奇是中文游戏，IME 是刚需。标准做法
//   （Unity WebGL / Phaser / PixiJS 皆如此）是浮一个透明 <input> 在画布对应控件位置，只把
//   “值变化 / 回车 / 失焦 / IME”事件经 [JSExport] 回传 C#，可见文字与光标由引擎在 canvas 自绘。
//
// 坐标：游戏侧传入的 cx/cy/cw/ch 是画布后备缓冲像素；canvas 以 CSS 尺寸显示，backing = CSS × dpr，
//       故 CSS 像素 = backing / dpr，再叠加 canvas 在视口的 rect 偏移即为 DOM 定位（position:fixed）。
import { getCanvasElement } from './gl.js';

/** C# 侧经 setHandlers 注册的回调。 */
export interface OverlayHandlers {
    onValueChanged(value: string): void;
    onEnter(): void;
    onFocus(): void;
    onBlur(): void;
    onImeActivate?(): void;
    onImeUpdate?(text: string): void;
    onImeDeactivate?(text: string): void;
}

/** 最近一次 show 的参数，用于窗口缩放 / 页面滚动时重新定位。 */
interface ShowParams {
    cx: number; cy: number; cw: number; ch: number;
    fontPx: number; color: number;
    password: boolean; maxLength: number; multiline: boolean;
    fontFamily: string; // 完整 CSS 字体串（含字重/族），由 place() 套用（仅缩放 px）
    transparent: boolean; // true=文字/光标由引擎自绘；false=由 DOM 直接显示
}

type InputEl = HTMLInputElement | HTMLTextAreaElement;

let handlers: OverlayHandlers | null = null;
let inputEl: HTMLInputElement | null = null;
let areaEl: HTMLTextAreaElement | null = null;
let last: ShowParams | null = null;
let transparentInput = true; // 模块级默认：DOM 元素仅作 IME/键盘捕获代理

/** 模块级设置：DOM 输入覆盖层是否透明（见 ShowParams.transparent）。 */
export function setTransparentInput(v: boolean): void { transparentInput = v; }

/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h: OverlayHandlers): void { handlers = h; }

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
    el.style.display = 'none';
    el.setAttribute('autocomplete', 'off');
    el.setAttribute('spellcheck', 'false');
    document.body.appendChild(el);

    // input/keydown 等事件会从 DOM 输入框冒泡到 window，被 input_keyboard 再次消费，
    // 导致游戏全局键盘逻辑重复处理字符（如密码里敲字母触发快捷键）。这里 stopPropagation
    // 阻断冒泡；Enter/Escape 还 preventDefault，避免表单提交 / 滚动。
    const stop = (e: Event) => e.stopPropagation();
    el.addEventListener('keydown', (e: Event) => {
        stop(e);
        const ke = e as KeyboardEvent;
        if (ke.key === 'Enter') {
            if (!last || !last.multiline || !ke.shiftKey) {
                ke.preventDefault();
                handlers?.onEnter();
            }
        } else if (ke.key === 'Escape') {
            ke.preventDefault();
            handlers?.onBlur();
        }
    });
    el.addEventListener('keyup', stop);
    el.addEventListener('input', () => handlers?.onValueChanged(el.value));
    el.addEventListener('focus', () => handlers?.onFocus());
    el.addEventListener('blur', () => handlers?.onBlur());
    // IME：组字开始=激活，update=实时候选串，end=停用（含最终上屏文本）。
    el.addEventListener('compositionstart', () => handlers?.onImeActivate?.());
    el.addEventListener('compositionupdate', (e: CompositionEvent) => handlers?.onImeUpdate?.(e.data ?? ''));
    el.addEventListener('compositionend', (e: CompositionEvent) => handlers?.onImeDeactivate?.(e.data ?? ''));
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
    // transparent=false：由 DOM 直接显示；密码掩码由 type=password 处理。
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

export function show(cx: number, cy: number, cw: number, ch: number, fontPx: number, color: number, value: string, password: boolean, maxLength: number, multiline: boolean, fontFamily: string, transparent: boolean = transparentInput): void {
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

export function hide(): void {
    last = null;
    if (inputEl) inputEl.style.display = 'none';
    if (areaEl) areaEl.style.display = 'none';
}

export function reposition(cx: number, cy: number, cw: number, ch: number): void {
    if (!last) return;
    last.cx = cx; last.cy = cy; last.cw = cw; last.ch = ch;
    const el = activeEl();
    if (el) place(el, last);
}

export function setValue(v: string): void {
    const el = activeEl();
    if (el) el.value = v ?? '';
}

export function getValue(): string {
    const el = activeEl();
    return el ? el.value : '';
}

// 窗口缩放 / 页面滚动时，画布 rect 会变，重新按最近一次 show 参数定位当前可见输入框。
function reflow(): void {
    const el = activeEl();
    if (el && last) place(el, last);
}
window.addEventListener('resize', reflow);
window.addEventListener('scroll', reflow, true);
