// mirengine/core/input.ts
// 浏览器原生文本输入覆盖层。对应 JSBind/BrowserInputOverlay.cs（mir.inputShow / inputHide / inputSetValue / inputReposition）。
// 思路：放弃模拟 WinForms 的 TextBox 按键逻辑，直接在 canvas 之上叠加一个真实的 <input>/<textarea>，
// 由其原生处理光标、选区与 IME；仅在值变化/回车/失焦时回调 C#，把结果同步回游戏侧 shim TextBox。
import { dom, host, toCss, BrowserInputOverlayExports } from '../shared.js';

/** 最近一次 inputShow 的参数，用于窗口缩放时重新定位。 */
interface ShowParams {
    cx: number;
    cy: number;
    cw: number;
    ch: number;
    fontPx: number;
    color: number;
    password: boolean;
    maxLength: number;
    multiline: boolean;
}

/** 原生输入框元素（单行 <input> 或多行 <textarea>）。 */
type InputEl = HTMLInputElement | HTMLTextAreaElement;

let inputEl: HTMLInputElement | null = null;
let areaEl: HTMLTextAreaElement | null = null;
let last: ShowParams | null = null; // 最近一次显示的参数，用于窗口缩放时重新定位

/**
 * 覆盖层通用样式：绝对定位、透明无边框、置于最上层。
 * @param el 原生输入元素
 */
function styleCommon(el: HTMLElement): void {
    el.style.position = 'fixed';
    el.style.margin = '0';
    el.style.padding = '0 4px';
    el.style.border = 'none';
    el.style.outline = 'none';
    el.style.background = 'transparent';
    el.style.boxSizing = 'border-box';
    el.style.display = 'none';
    el.style.color = '#fff';
    el.style.zIndex = '9999';
    el.style.fontFamily = 'Tahoma, Arial, "Microsoft YaHei", sans-serif';
    el.style.whiteSpace = 'nowrap';
}

/** 取托管侧的事件接收对象；模块尚未初始化（host.exports 还是空对象）时返回 undefined。 */
function overlay(): BrowserInputOverlayExports | undefined {
    return host.exports?.MirEngine?.BrowserInputOverlay;
}

/**
 * 取（必要时惰性创建）对应类型的原生输入元素。
 * @param multiline true 用 <textarea>，false 用 <input>
 * @returns 已 wire 过事件的元素实例
 */
function getEl(multiline: boolean): InputEl {
    if (multiline) {
        if (!areaEl) {
            areaEl = document.createElement('textarea');
            styleCommon(areaEl);
            areaEl.style.resize = 'none';
            areaEl.style.overflow = 'hidden';
            areaEl.style.whiteSpace = 'pre';
            document.body.appendChild(areaEl);
            wire(areaEl, true);
        }
        if (inputEl) inputEl.style.display = 'none';
        return areaEl;
    }
    if (!inputEl) {
        inputEl = document.createElement('input');
        styleCommon(inputEl);
        document.body.appendChild(inputEl);
        wire(inputEl, false);
    }
    if (areaEl) areaEl.style.display = 'none';
    return inputEl;
}

/**
 * 绑定 input / blur / keydown 回调到托管侧 BrowserInputOverlay。
 * @param el 原生输入元素
 * @param multiline 是否多行（回车键的 preventDefault 只在单行下生效）
 */
function wire(el: InputEl, multiline: boolean): void {
    el.addEventListener('input', () => {
        const o = overlay();
        if (o && o.OnInput) o.OnInput(el.value);
    });
    el.addEventListener('blur', () => {
        const o = overlay();
        if (o && o.OnBlur) o.OnBlur();
    });
    el.addEventListener('keydown', (ev: Event) => {
        const e = ev as KeyboardEvent;
        if (e.key === 'Enter') {
            const o = overlay();
            if (o && o.OnEnter) o.OnEnter();
            if (!multiline) e.preventDefault();
        } else if (e.key === 'Escape') {
            e.preventDefault();
            const o = overlay();
            if (o && o.OnBlur) o.OnBlur();
            el.blur();
        }
    });
}

/** 画布当前的 CSS 尺寸与缩放比（CSS 像素 / 画布内部像素）。 */
function scale(): { r: DOMRect; sx: number; sy: number } {
    const r = dom.canvas.getBoundingClientRect();
    return { r, sx: r.width / dom.canvas.width, sy: r.height / dom.canvas.height };
}

/**
 * 按画布坐标摆放到画布之上。
 * @param el 原生输入元素
 * @param a 定位 / 样式参数（画布内部像素）
 */
function place(el: HTMLElement, a: ShowParams): void {
    const { r, sx, sy } = scale();
    el.style.left = (r.left + a.cx * sx) + 'px';
    el.style.top = (r.top + a.cy * sy) + 'px';
    el.style.width = (a.cw * sx) + 'px';
    el.style.height = (a.ch * sy) + 'px';
    el.style.fontSize = (a.fontPx * ((sx + sy) / 2)) + 'px';
    el.style.lineHeight = (a.ch * sy) + 'px';
    el.style.color = toCss(a.color);
}

/** 当前可见的那个原生输入元素；都不可见时返回 null。 */
function activeEl(): InputEl | null {
    if (inputEl && inputEl.style.display !== 'none') return inputEl;
    if (areaEl && areaEl.style.display !== 'none') return areaEl;
    return null;
}

/**
 * 显示原生输入覆盖层（mir.inputShow）。坐标与尺寸均为画布内部像素。
 * @param cx 左边界（画布像素）
 * @param cy 上边界（画布像素）
 * @param cw 宽度（画布像素）
 * @param ch 高度（画布像素）
 * @param fontPx 字号（px）
 * @param color 文字颜色，32 位 ARGB
 * @param value 初始文本
 * @param password 是否为密码框
 * @param maxLength 最大长度（非法值夹取到 1000000）
 * @param multiline 是否多行
 */
export const inputShow = (cx: number, cy: number, cw: number, ch: number, fontPx: number, color: number, value: string, password: boolean, maxLength: number, multiline: boolean): void => {
    const el = getEl(multiline);
    last = { cx, cy, cw, ch, fontPx, color, password, maxLength, multiline };
    place(el, last);
    el.value = value || '';
    if (!multiline) (el as HTMLInputElement).type = password ? 'password' : 'text';
    el.maxLength = (maxLength > 0 && maxLength < 1000000) ? maxLength : 1000000;
    el.style.display = 'block';
    // 聚焦到原生输入框，光标/IME 全部交给浏览器；preventScroll 避免页面滚动。
    el.focus({ preventScroll: true });
    try { el.setSelectionRange(el.value.length, el.value.length); } catch (_) { }
};

/** 隐藏覆盖层（mir.inputHide）。 */
export const inputHide = (): void => {
    last = null;
    if (inputEl) inputEl.style.display = 'none';
    if (areaEl) areaEl.style.display = 'none';
};

// 读取当前可见的原生输入值，供 C# 在切换/失焦时把文本回写到对应 shim TextBox（避免共享单例 <input> 被复用后旧值丢失）。
/** 取当前可见输入框的文本（mir.inputGetValue）。 */
export const inputGetValue = (): string => {
    const el = activeEl();
    return el ? el.value : '';
};

/**
 * 设置当前可见输入框的文本（mir.inputSetValue）。
 * @param v 新文本
 */
export const inputSetValue = (v: string): void => {
    const el = activeEl();
    if (el) el.value = v || '';
};

/**
 * 重新定位覆盖层（mir.inputReposition）。位置参数含义同 inputShow。
 * @param cx 左边界（画布像素）
 * @param cy 上边界（画布像素）
 * @param cw 宽度（画布像素）
 * @param ch 高度（画布像素）
 */
export const inputReposition = (cx: number, cy: number, cw: number, ch: number): void => {
    if (!last) return;
    last.cx = cx; last.cy = cy; last.cw = cw; last.ch = ch;
    const el = activeEl();
    if (el) place(el, last);
};

window.addEventListener('resize', () => {
    const el = activeEl();
    if (el && last) place(el, last);
});
