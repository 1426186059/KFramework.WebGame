// 【依赖 C#】由 KFramework.MonoGame.JSBind_InputOverlay 经 [JSImport(module: "input_overlay")] 调用；
// 产物 input_overlay.js 由各示例 / 游戏工程的 SyncJsEngine 从 KFramework.TSEngine/dist/jsengine 复制到 wwwroot/jsengine。
//
// 浏览器原生文本输入覆盖层：在 canvas 之上叠加一个 DOM <input>/<textarea>，承接键盘 / IME /
// 密码掩码，并把输入结果经 setHandlers 注册的 C# 回调（[JSExport]）回传，驱动 MirTextBox。
//
// 重要分工：本 DOM 元素【只做输入捕获代理】，可见的文字与光标由引擎在 canvas 上绘制
// （见 MirTextBox.CreateTexture → BrowserCanvas.DrawTextBox）。因此本元素的文字颜色与光标均设为
// 透明（见 place()），仅借其获得浏览器原生的 IME 组字 / 候选窗 / 软键盘能力——这是 WebGL canvas
// 拿不到输入合成事件时的唯一可行路径。引擎绘制的好处：文字/光标归对话框绘制层级管，
// 弹窗出现时正常遮挡，不再有“浮层盖不住 / 焦点切换光标跳到顶”的问题。
//
// 为什么光标 / 输入必须走这个 DOM 覆盖层方案（而不是在 canvas 上自绘）？
//   canvas / WebGL 是“位图画布”，画布上的一切——文字、光标、选区——都必须由游戏每帧手绘。
//   而浏览器原生的文本编辑能力是“活”的、由操作系统 / 浏览器增量维护的，canvas 无法等价复刻：
//     1. 光标（caret）：闪烁、粗细、平台风格、点击定位（point-to-caret）全靠浏览器；自绘要做到一致极难。
//     2. 选区：双击选词、拖拽选段、跨行选择高亮，自绘等于重写一个文本编辑器。
//     3. IME（中文 / 日文等输入法）：组字过程（preedit）、候选词窗、上屏提交由输入法驱动，
//        canvas 里几乎无法正确实现（热血传奇是中文游戏，IME 是刚需）。
//     4. 密码掩码、复制 / 粘贴、Undo、键盘可达性（a11y）等，也都是浏览器白送的能力。
//   因此标准做法（Unity WebGL / Phaser / PixiJS / 本项目 Mir3 的 core/input.ts 皆如此）是：
//   把一个透明的 <input>/<textarea> 浮在画布对应控件位置上，让浏览器原生处理光标 / 选区 / IME，
//   仅把“值变化 / 回车 / 失焦”三类事件经 setHandlers 回传 C#，由 MirTextBox 把文本同步回游戏侧 TextBox。
//   这样光标体验与系统一致，且无需在 canvas 上重造一个文本编辑内核。
//
// 坐标换算：游戏侧传入的 cx/cy/cw/ch 是画布“后备缓冲（绘制）像素”——SpriteBatch 视口与
// 控件 DisplayLocation 都基于它；canvas 以 CSS 尺寸显示，且 backing = CSS × dpr，
// 故 CSS 像素 = backing / dpr，再叠加 canvas 在视口中的 rect 偏移即为 DOM 定位。
// 本模块用 position:fixed（与 getBoundingClientRect 的视口坐标天然对齐，页面滚动也不漂移），
// 并在 resize / scroll 时按最近一次 show 参数重新定位当前可见输入框。
import { getCanvasElement } from './gl.js';
// 模块级设置：DOM 输入覆盖层是否透明（true=文字/光标由引擎自绘；false=由 DOM 直接显示）。show() 可逐框覆盖。
let _transparentInput = true;
export function setTransparentInput(v) { _transparentInput = v; }
let handlers = null;
// 复用两个 DOM 元素：单行用 <input>，多行用 <textarea>（两者互斥显示，避免类型切换异常）。
let inputEl = null;
let areaEl = null;
let last = null;
function canvasMetrics() {
    const canvas = getCanvasElement();
    if (!canvas)
        return { left: 0, top: 0, dpr: 1 };
    const rect = canvas.getBoundingClientRect();
    const dpr = rect.width > 0 ? canvas.width / rect.width : 1; // backing/CSS 比，与 input_common.canvasPoint 同一算法
    return { left: rect.left, top: rect.top, dpr: dpr > 0 ? dpr : 1 };
}
function attach(el) {
    if (el.parentNode)
        return;
    el.style.position = 'fixed'; // 视口相对定位，与 getBoundingClientRect 对齐，滚动不漂移
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
        const ke = e;
        if (ke.key === 'Enter') {
            if (!last || !last.multiline || !ke.shiftKey) {
                ke.preventDefault();
                handlers && handlers.onEnter();
            }
        }
        else if (ke.key === 'Escape') {
            ke.preventDefault();
            handlers && handlers.onBlur();
        }
    });
    el.addEventListener('keyup', stop);
    el.addEventListener('input', () => handlers && handlers.onValueChanged(el.value));
    el.addEventListener('blur', () => handlers && handlers.onBlur());
}
function activeEl() {
    if (!last)
        return null;
    const el = last.multiline ? areaEl : inputEl;
    return el && el.style.display !== 'none' ? el : null;
}
function ensureEl(multiline) {
    const el = multiline
        ? (areaEl ||= document.createElement('textarea'))
        : (inputEl ||= document.createElement('input'));
    // 隐藏另一种元素，避免两个输入框同时可见。
    if (inputEl && inputEl !== el)
        inputEl.style.display = 'none';
    if (areaEl && areaEl !== el)
        areaEl.style.display = 'none';
    attach(el);
    return el;
}
// 把完整 CSS 字体串(含字重/族，如 "bold 14px 'Tahoma'")中的 px 按 dpr 缩放到 CSS 像素后整体套用，
// 使 DOM 输入框字形与画布 SpriteFont 完全一致（字重/族/字号均对齐）。
function scaleFontPx(css, dpr) {
    const c = (css || '').trim();
    if (!c) return (10 / dpr) + 'px sans-serif';
    const m = c.match(/([\d.]+)\s*px/);
    if (!m) return c;
    const px = parseFloat(m[1]) / dpr;
    return c.replace(/([\d.]+)\s*px/, px.toFixed(2) + 'px');
}

function place(el, p) {
    const { left, top, dpr } = canvasMetrics();
    el.style.left = left + p.cx / dpr + 'px';
    el.style.top = top + p.cy / dpr + 'px';
    el.style.width = p.cw / dpr + 'px';
    el.style.height = p.ch / dpr + 'px';
    el.style.font = scaleFontPx(p.fontFamily, dpr);
    // transparent=true：文字与光标由引擎在 canvas 自绘，本 DOM 元素仅作 IME / 键盘捕获代理，颜色与光标均透明；
    // transparent=false：由 DOM 直接显示文字与光标；密码掩码由上面的 type=password 处理。
    // 注意：透明不影响 IME——候选词窗是浏览器 UI，仍按本元素光标位置弹出；input 事件照常回传合成文本。
    if (p.transparent) {
        el.style.color = 'transparent';
        el.style.caretColor = 'transparent';
    }
    else {
        const r = (p.color >> 16) & 0xff, g = (p.color >> 8) & 0xff, b = p.color & 0xff;
        const css = `rgb(${r},${g},${b})`;
        el.style.color = css;
        el.style.caretColor = css;
    }
}
/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h) {
    handlers = h;
}
export function show(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline, fontFamily, transparent = _transparentInput) {
    const el = ensureEl(multiline);
    last = { cx, cy, cw, ch, fontPx, color, password, maxLength, multiline, fontFamily, transparent };
    place(el, last);
    el.style.display = 'block';
    el.value = value ?? '';
    if (multiline) {
        el.style.resize = 'none';
        el.style.whiteSpace = 'pre-wrap';
        el.style.overflow = 'hidden';
    }
    else {
        el.type = password ? 'password' : 'text';
    }
    if (maxLength > 0 && maxLength < 100000)
        el.maxLength = maxLength;
    else
        el.removeAttribute('maxlength');
    // 延迟聚焦：确保样式/定位已生效后再聚焦，浏览器才会弹出软键盘 / IME。
    setTimeout(() => {
        try {
            el.focus();
            const len = el.value.length;
            if (typeof el.setSelectionRange === 'function')
                el.setSelectionRange(len, len);
        }
        catch { /* ignore */ }
    }, 0);
}
export function hide() {
    last = null;
    if (inputEl)
        inputEl.style.display = 'none';
    if (areaEl)
        areaEl.style.display = 'none';
}
export function reposition(cx, cy, cw, ch) {
    if (!last)
        return;
    last.cx = cx;
    last.cy = cy;
    last.cw = cw;
    last.ch = ch;
    const el = activeEl();
    if (el)
        place(el, last);
}
export function setValue(v) {
    const el = activeEl();
    if (el)
        el.value = v ?? '';
}
export function getValue() {
    const el = activeEl();
    return el ? el.value : '';
}
// 窗口缩放 / 页面滚动时，画布 rect 会变，重新按最近一次 show 参数定位当前可见输入框。
function reflow() {
    const el = activeEl();
    if (el && last)
        place(el, last);
}
window.addEventListener('resize', reflow);
window.addEventListener('scroll', reflow, true);
