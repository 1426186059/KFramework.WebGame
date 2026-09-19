// 【依赖 C#】由 MirEngine.BrowserInputOverlay 经 [JSImport(module: "input_overlay")] 调用；
// 产物 input_overlay.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 浏览器原生文本输入覆盖层：在 canvas 之上叠加一个 DOM <input>/<textarea>，承接键盘 / IME /
// 密码掩码，并把输入结果经 setHandlers 注册的 C# 回调（[JSExport]）回传，驱动 MirTextBox。
//
// 坐标换算：游戏侧传入的 cx/cy/cw/ch 是画布“后备缓冲（绘制）像素”——SpriteBatch 视口与
// 控件 DisplayLocation 都基于它；canvas 以 CSS 尺寸显示，且 backing = CSS * dpr，
// 故 CSS 像素 = backing / dpr，再叠加 canvas 在页面中的 rect 偏移即为 DOM 绝对定位。
import { getCanvasElement } from './gl.js';

let handlers = null;

// 复用两个 DOM 元素：单行用 <input>，多行用 <textarea>（两者互斥显示，避免类型切换异常）。
let inputEl = null;
let areaEl = null;
let currentMultiline = false;

function canvasMetrics() {
    const canvas = getCanvasElement();
    if (!canvas) return { left: 0, top: 0, dpr: 1 };
    const rect = canvas.getBoundingClientRect();
    const dpr = rect.width > 0 ? canvas.width / rect.width : 1;
    return { left: rect.left, top: rect.top, dpr: dpr > 0 ? dpr : 1 };
}

function colorToCss(color) {
    const r = (color >> 16) & 0xff;
    const g = (color >> 8) & 0xff;
    const b = color & 0xff;
    return '#' + [r, g, b].map((x) => x.toString(16).padStart(2, '0')).join('');
}

function attach(el) {
    if (el.parentNode) return;
    el.style.position = 'absolute';
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
    const stop = (e) => e.stopPropagation();
    el.addEventListener('keydown', (e) => {
        stop(e);
        if (e.key === 'Enter') {
            if (!currentMultiline || !e.shiftKey) {
                e.preventDefault();
                handlers && handlers.onEnter();
            }
        } else if (e.key === 'Escape') {
            e.preventDefault();
            handlers && handlers.onBlur();
        }
    });
    el.addEventListener('keyup', stop);
    el.addEventListener('input', () => handlers && handlers.onValueChanged(el.value));
    el.addEventListener('blur', () => handlers && handlers.onBlur());
}

function activeEl() {
    return currentMultiline ? areaEl : inputEl;
}

function ensureInput(multiline) {
    currentMultiline = multiline;
    const el = multiline
        ? (areaEl ||= document.createElement('textarea'))
        : (inputEl ||= document.createElement('input'));
    // 隐藏另一种元素，避免两个输入框同时可见。
    if (inputEl && inputEl !== el) inputEl.style.display = 'none';
    if (areaEl && areaEl !== el) areaEl.style.display = 'none';
    attach(el);
    return el;
}

/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h) {
    handlers = h;
}

export function show(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline) {
    const el = ensureInput(multiline);
    const { left, top, dpr } = canvasMetrics();
    el.style.display = 'block';
    el.style.left = left + cx / dpr + 'px';
    el.style.top = top + cy / dpr + 'px';
    el.style.width = cw / dpr + 'px';
    el.style.height = ch / dpr + 'px';
    el.style.font = fontPx / dpr + 'px sans-serif';
    const css = colorToCss(color);
    el.style.color = css;
    el.style.caretColor = css;
    el.value = value || '';
    if (multiline) {
        el.style.resize = 'none';
        el.style.whiteSpace = 'pre-wrap';
        el.style.overflow = 'hidden';
    } else {
        el.type = password ? 'password' : 'text';
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

export function hide() {
    if (inputEl) inputEl.style.display = 'none';
    if (areaEl) areaEl.style.display = 'none';
}

export function reposition(cx, cy, cw, ch) {
    const el = activeEl();
    if (!el || el.style.display === 'none') return;
    const { left, top, dpr } = canvasMetrics();
    el.style.left = left + cx / dpr + 'px';
    el.style.top = top + cy / dpr + 'px';
    el.style.width = cw / dpr + 'px';
    el.style.height = ch / dpr + 'px';
}

export function setValue(v) {
    const el = activeEl();
    if (el) el.value = v || '';
}

export function getValue() {
    const el = activeEl();
    return el ? el.value : '';
}
