// 浏览器原生文本输入覆盖层（input_html_ime 模块）。
//
// 作用：在 canvas 之上叠加一个透明的 DOM <input>/<textarea>，承接键盘 / IME 捕获。
// 文字一律由引擎在 canvas 自绘（见 TextBoxRenderer / TextCaret），DOM 元素只作输入代理。
//
import { getInputCanvas } from './input_common.js';
// 引擎在 main.ts 解析出程序集导出树后，通过 init() 把该对象注入本模块。
// 覆盖层需要在 JS 侧把原生编辑结果 / 控制键回调给 C# 的 [JSExport]（合并于 JSBind_Input_IME）。
// 注意：host 并非全局变量，必须显式注入，否则 host.exports... 会抛 ReferenceError，导致 input 事件回传失效。
let exportRoot = null;
export function init(root) { exportRoot = root; }
// 在导出树里查找 JSBind_Input_IME（[JSExport] 类型，含 OnDomValue / OnKeyDown）。
// 优先走合并后的直接路径，失败再递归兜底，避免不同 .NET 版本导出结构差异导致找不到。
function findIme(node) {
    if (!node || typeof node !== 'object')
        return null;
    if (typeof node.OnDomValue === 'function' && typeof node.OnKeyDown === 'function')
        return node;
    for (const v of Object.values(node)) {
        const r = findIme(v);
        if (r)
            return r;
    }
    return null;
}
function ime() {
    if (!exportRoot)
        return null;
    const direct = exportRoot?.KFramework?.MonoGame?.JSBind_Input_IME;
    if (direct && typeof direct.OnDomValue === 'function' && typeof direct.OnKeyDown === 'function')
        return direct;
    return findIme(exportRoot);
}
let inputEl = null;
let areaEl = null;
let last = null;
let composing = false; // IME 组字中（compositionstart..compositionend），用于拦截选词用的 Enter
function canvasMetrics() {
    const canvas = getInputCanvas();
    if (!canvas)
        return { left: 0, top: 0, dpr: 1 };
    const rect = canvas.getBoundingClientRect();
    const dpr = rect.width > 0 ? canvas.width / rect.width : 1; // backing/CSS 比
    return { left: rect.left, top: rect.top, dpr: dpr > 0 ? dpr : 1 };
}
function activeEl() {
    if (!last)
        return null;
    const el = last.multiline ? areaEl : inputEl;
    return el && el.style.display !== 'none' ? el : null;
}
// 复用两个 DOM 元素：单行 <input> / 多行 <textarea>（互斥显示，避免类型切换异常）。
function ensureEl(multiline) {
    let el = null;
    if (multiline) {
        if (areaEl == null) {
            areaEl = document.createElement('textarea');
        }
        el = areaEl;
    }
    else {
        if (inputEl == null) {
            inputEl = document.createElement('input');
        }
        el = inputEl;
    }
    if (inputEl && inputEl !== el) {
        inputEl.style.display = 'none';
    }
    if (areaEl && areaEl !== el) {
        areaEl.style.display = 'none';
    }
    if (!el.parentNode) {
        attach(el);
    }
    return el;
}
function attach(el) {
    el.style.position = 'fixed'; // 视口相对定位，与 getBoundingClientRect 对齐，滚动不漂移
    el.style.margin = '0';
    el.style.padding = '0';
    el.style.border = 'none';
    el.style.outline = 'none';
    el.style.background = 'transparent';
    el.style.boxSizing = 'border-box';
    el.style.zIndex = '10';
    el.style.pointerEvents = 'none'; // 点击穿透到 canvas，由引擎按坐标自算光标（对齐 UGUI InputField 自行处理指针）
    el.style.display = 'none';
    // 覆盖层永远不暴露为密码框：本游戏自行渲染/持久化凭据，不需要浏览器密码管理器介入。
    // autocomplete=off 仅作兜底；真正“不触发自动填充/保存提示”靠 show() 里始终 type=text（无 password 框）。
    el.setAttribute('autocomplete', 'off');
    el.setAttribute('spellcheck', 'false');
    document.body.appendChild(el);
    el.addEventListener('keydown', (e) => {
        const ke = e;
        const ctrl = ke.ctrlKey || ke.metaKey, shift = ke.shiftKey, alt = ke.altKey;
        const k = ke.key;
        if (composing && k === 'Enter') {
            ke.preventDefault();
            ke.stopPropagation();
            return;
        }
        const controlKeys = ['Backspace', 'Delete', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'Enter', 'Escape', 'Tab'];
        if (ctrl && (k === 'a' || k === 'A')) {
            ime()?.OnKeyDown(k, ctrl, shift, alt);
            ke.preventDefault();
            ke.stopPropagation();
            return;
        }
        if (controlKeys.indexOf(k) !== -1) {
            ime()?.OnKeyDown(k, ctrl, shift, alt);
            ke.preventDefault();
            ke.stopPropagation();
            return;
        }
        ke.stopPropagation();
    });
    el.addEventListener('input', (e) => {
        const el2 = activeEl();
        if (!el2)
            return;
        const ke = e;
        // 带上 DOM 真实光标位置，让引擎按此镜像 text / 光标，保证光标停在末尾而非开头。
        ime()?.OnDomValue(el2.value, el2.selectionStart, el2.selectionEnd, ke.isComposing === true);
    });
    // 跟踪 IME 组字状态（比单纯依赖 keydown.isComposing 更稳，覆盖部分浏览器边缘）。
    el.addEventListener('compositionupdate', () => {
        const el2 = activeEl();
        if (el2)
            ime()?.OnDomValue(el2.value, el2.selectionStart, el2.selectionEnd, true);
    });
    el.addEventListener('compositionstart', () => { composing = true; });
    el.addEventListener('compositionend', () => {
        composing = false;
        // 组字结束：把最终文本作为「已提交」回传引擎（composing=false），
        // 否则在启用输入法（即使处于英文模式，每次按键也走 composition）时，引擎侧组字状态会卡死，
        // 导致退格 / 回车永远被当作组字中而失效。
        const el2 = activeEl();
        if (el2)
            ime()?.OnDomValue(el2.value, el2.selectionStart, el2.selectionEnd, false);
    });
    el.addEventListener('blur', () => {
        if (last && el.style.display !== 'none') {
            setTimeout(() => {
                if (last && el.style.display !== 'none' && document.activeElement !== el) {
                    try {
                        el.focus();
                    }
                    catch { /* ignore */ }
                }
            }, 0);
        }
    });
}
// 把完整 CSS 字体串中的 px 按 dpr 缩放到 CSS 像素后整体套用，使 DOM 字形与画布 SpriteFont 一致。
function scaleFontPx(css, dpr) {
    const c = (css || '').trim();
    if (!c)
        return (10 / dpr) + 'px sans-serif';
    const m = c.match(/([\d.]+)\s*px/);
    if (!m)
        return c;
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
    // transparent=true：文字与光标由引擎在 canvas 自绘，本 DOM 元素仅作捕获代理，颜色/光标透明避免重影。
    // 透明不影响 IME——候选窗是浏览器 UI，仍按本元素光标位置弹出。
    if (p.transparent) {
        el.style.color = 'transparent';
        el.style.caretColor = 'transparent';
    }
    else {
        const r = (p.color >> 16) & 0xff, g = (p.color >> 8) & 0xff, b = p.color & 0xff;
        el.style.color = `rgb(${r},${g},${b})`;
        el.style.caretColor = `rgb(${r},${g},${b})`;
    }
}
/** C# 主动激活覆盖层：在画布上以 cx/cy/cw/ch（后备缓冲像素）显示原生输入框并聚焦。 */
export function show(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline, fontFamily, transparent = true) {
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
    }
    else {
        // 始终用 text：密码掩码由引擎在 canvas 自绘（DOM 输入框透明），无需 type=password。
        // 一旦存在 password 框，浏览器会把登录/聊天等任意文本框当成凭据框，触发“保存密码”提示与用户名自动填充。
        el.type = 'text';
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
/** C# 主动关闭覆盖层：隐藏并移除输入框（失焦），清空最近一次 show 参数。 */
export function hide() {
    last = null;
    composing = false;
    if (inputEl)
        inputEl.style.display = 'none';
    if (areaEl)
        areaEl.style.display = 'none';
    // 覆盖层隐藏后，原本被其抢走的画布焦点没有自动归还：浏览器会把焦点退到 body，
    // 导致画布 keydown 监听收不到键（表现为“关掉聊天后再按 Enter/打字没反应，点一下屏幕又好了”）。
    // 这里把焦点还给画布，恢复键盘输入。复用本模块已导入的 getCanvasElement 取画布，无需依赖画布 id。
    const c = getInputCanvas();
    if (c) {
        c.tabIndex = 0;
        if (document.activeElement !== c) {
            try {
                c.focus();
            }
            catch { /* ignore */ }
        }
    }
}
/** C# 每帧拉取：返回当前输入框文本（含 IME 组字内容）；未激活时返回空串。 */
export function getValue() {
    const el = activeEl();
    return el ? el.value : '';
}
/** C# 把引擎 text + IME 预览写回 DOM，使镜像与引擎保持一致。 */
export function setValue(v) {
    const el = activeEl();
    if (!el)
        return;
    if (el.value !== (v ?? ''))
        el.value = v ?? '';
}
/** C# 把引擎光标区间写回 DOM（对齐 IME 候选窗位置）。 */
export function setSelectionRange(start, end) {
    const el = activeEl();
    if (!el)
        return;
    try {
        if (typeof el.setSelectionRange === 'function')
            el.setSelectionRange(start, end);
    }
    catch { /* ignore */ }
}
/** 重新定位当前可见输入框（窗口缩放 / 页面滚动时由 reflow 自动调用，也可由 C# 显式调用）。 */
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
// 窗口缩放 / 页面滚动时，画布 rect 会变，重新按最近一次 show 参数定位当前可见输入框。
function reflow() {
    const el = activeEl();
    if (el && last)
        place(el, last);
}
window.addEventListener('resize', reflow);
window.addEventListener('scroll', reflow, true);
