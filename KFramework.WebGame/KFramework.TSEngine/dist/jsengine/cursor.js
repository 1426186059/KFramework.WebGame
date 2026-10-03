// 【依赖 C#】由 KFramework.MonoGame.JSBind_Cursor 经 [JSImport(module: "cursor")] 调用；
// 产物 cursor.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
//
// 设置画布的 CSS 光标（把原桌面端 .CUR 资源切换改为切换 canvas 的 style.cursor）。
// 接受任意合法 CSS cursor 值：default / crosshair / pointer / wait / text / move /
// not-allowed / grab / zoom-in / none，以及 url(...) 形式。
import { getCanvas } from "./html_canvas.js";
function target() {
    return getCanvas();
}
/** 设置光标；name 为空或 "default" 时复位为默认箭头。 */
export function setCursor(name) {
    const el = target();
    if (!el)
        return;
    el.style.cursor = (name && name.length > 0) ? name : 'default';
}
/** 复位为默认光标。 */
export function resetCursor() {
    const el = target();
    if (!el)
        return;
    el.style.cursor = 'default';
}
