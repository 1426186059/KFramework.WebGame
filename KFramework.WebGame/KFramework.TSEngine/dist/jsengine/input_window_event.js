import { E_HTML_Event_Type } from './html_event_type.js';
import { getCanvas } from './html_canvas.js';
const pending = new Map();
const sizeData = new Uint8Array(10);
const sizeView = new DataView(sizeData.buffer);
const noData = new Uint8Array(0);
let canvas = null;
let wasFocused = true;
let _cacheCanvasSize = { x: -1, y: -1 };
function clampI16(v) {
    return v > 32767 ? 32767 : v < -32768 ? -32768 : v | 0;
}
function onDocumentVisibility() {
    pending.set(document.hidden ? E_HTML_Event_Type.SysPageHidden : E_HTML_Event_Type.SysPageVisible, noData);
}
function onWindowFocus() {
    console.log('窗口 聚焦');
}
function onWindowBlur() {
    console.log('窗口 失焦');
}
function onWindowContextMenu(e) {
    e.preventDefault();
    console.log("onWindow ContextMenu");
}
export function bindWindowEvents() {
    canvas = getCanvas();
    console.assert(canvas != null, "bindWindowEvents Error");
    document.addEventListener('visibilitychange', onDocumentVisibility);
    window.addEventListener('contextmenu', onWindowContextMenu);
    window.addEventListener('blur', onWindowBlur);
    window.addEventListener('focus', onWindowFocus);
    wasFocused = document.hasFocus();
}
export function reportWindowFocus() {
    console.assert(canvas != null, "reportCanvasFocus error");
    const now = document.hasFocus();
    if (wasFocused != now) {
        wasFocused = now;
        if (now) {
            pending.set(E_HTML_Event_Type.SysFocusGained, noData);
        }
        else {
            pending.set(E_HTML_Event_Type.SysFocusLost, noData);
        }
    }
}
function reportCanvasSize() {
    const c = canvas;
    //游戏中 禁止使用 canvas.getBoundingClientRect();
    if (c.clientWidth != _cacheCanvasSize.x || c.clientHeight != _cacheCanvasSize.y) {
        _cacheCanvasSize = { x: c.clientWidth, y: c.clientHeight };
        const devicePixelRatio = window.devicePixelRatio || 1;
        const cssWidth = Math.max(1, Math.round(c.clientWidth || 1));
        const cssHeight = Math.max(1, Math.round(c.clientHeight || 1));
        const drawWidth = Math.max(1, Math.round(cssWidth * devicePixelRatio));
        const drawHeight = Math.max(1, Math.round(cssHeight * devicePixelRatio));
        if (c.width !== drawWidth || c.height !== drawHeight) {
            c.width = drawWidth;
            c.height = drawHeight;
        }
        sizeView.setInt16(0, clampI16(cssWidth), true);
        sizeView.setInt16(2, clampI16(cssHeight), true);
        sizeView.setInt16(4, clampI16(drawWidth), true);
        sizeView.setInt16(6, clampI16(drawHeight), true);
        sizeView.setInt16(8, clampI16(Math.round(devicePixelRatio * 1000)), true);
        pending.set(E_HTML_Event_Type.CanvasResized, sizeData);
    }
}
export function reportPointerCancel() {
    pending.set(E_HTML_Event_Type.SysPointerCancel, noData);
}
export function hadFocusLost() {
    return !document.hasFocus();
}
export function drainWindowEvents(w) {
    reportCanvasSize();
    reportWindowFocus();
    for (const [type, data] of pending) {
        w.put(type, data, data.length);
    }
    pending.clear();
}
