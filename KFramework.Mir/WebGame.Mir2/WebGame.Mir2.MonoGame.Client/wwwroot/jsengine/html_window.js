// 【依赖 C#】由 KFramework.MongoGame.JSBind_HTML_Window 经 [JSImport(module: "window")] 调用；
// 产物 html_window.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 浏览器窗口层（<window> / <document>）：页面标题、地址栏参数、环境查询、外链打开，
// 以及 HTML 页面尺寸。与 html_canvas.ts 的分工：本文件管「窗口」，html_canvas.ts 管「画布元素」。
/** 写回整数缓冲（与 html_canvas 同一套 MemoryView_Span 处理）。 */
let _int32Scratch = new Int32Array(8);
function writeInts(view, values) {
    if (_int32Scratch.length < values.length)
        _int32Scratch = new Int32Array(values.length);
    _int32Scratch.set(values);
    const slice = _int32Scratch.subarray(0, values.length);
    if (view instanceof Int32Array) {
        view.set(slice);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(slice, 0);
        return;
    }
    const fallback = view;
    for (let i = 0; i < values.length; i++)
        fallback[i] = values[i];
}
/** 设置页面标题（浏览器标签页文字）。 */
export function setTitle(title) {
    document.title = title;
}
/** 读取地址栏查询参数（?key=value 中的 value）。 */
export function getQueryParameter(name) {
    return new URLSearchParams(window.location.search).get(name) ?? '';
}
/** 当前是否移动端（触屏优先设备）。 */
export function isMobile() {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
}
/** 页面基址（document.baseURI），用于把内容包的相对路径拼成绝对 URL。 */
export function getBaseUri() {
    return document.baseURI || window.location.href;
}
/**
 * 在浏览器中打开一个 URL（新标签）。用于跳转支付页、官网等外部链接。
 * 注意：浏览器通常只在「用户手势」（如点击）内允许 window.open，否则会被弹窗拦截。
 */
export function openUrl(url) {
    window.open(url, "_blank");
}
/** 读 HTML 页面尺寸（window.innerWidth / innerHeight），常用于画布居中。写入 [宽, 高]。 */
export function getPageSize(view) {
    writeInts(view, [Math.round(window.innerWidth), Math.round(window.innerHeight)]);
}
