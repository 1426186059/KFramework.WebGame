// 【依赖 C#】由 KFramework.MonoGame.JSBind_Cursor 经 [JSImport(module: "cursor")] 调用；
// 产物 cursor.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 设置画布的 CSS 光标（把原桌面端 .CUR 资源切换改为切换 canvas 的 style.cursor）。
// 接受任意合法 CSS cursor 值：default / crosshair / pointer / wait / text / move /
// not-allowed / grab / zoom-in / none，以及 url(...) 形式。
/** 默认画布 id（与 html_canvas.ts 的 DEFAULT_CANVAS_ID 对齐）。 */
const DEFAULT_CANVAS_ID = 'game';
function toId(idOrSelector) {
    const trimmed = (idOrSelector ?? '').trim().replace(/^#/, '');
    return trimmed.length > 0 ? trimmed : DEFAULT_CANVAS_ID;
}
function target(idOrSelector) {
    const el = document.getElementById(toId(idOrSelector));
    return el instanceof HTMLCanvasElement ? el : null;
}
/** 设置光标；name 为空或 "default" 时复位为默认箭头。 */
export function setCursor(idOrSelector, name) {
    const el = target(idOrSelector);
    if (!el)
        return;
    el.style.cursor = (name && name.length > 0) ? name : 'default';
}
/** 复位为默认光标。 */
export function resetCursor(idOrSelector) {
    const el = target(idOrSelector);
    if (!el)
        return;
    el.style.cursor = 'default';
}
