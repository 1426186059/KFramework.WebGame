// mirengine/core/keyboard.ts
// 键盘输入。对应 JSBind/BrowserKeyboard.cs（mir.keyboardAttach / keyboardDetach）。
// 仅做 DOM 事件 -> BrowserKeyboard 导出方法的转发；键名 -> Keys 的语义翻译在 C# 端完成。
import { host } from '../shared.js';

let attached = false;

// 原生文本输入覆盖层（<input>/<textarea>）聚焦时，按键交给浏览器自己处理，
// 不再转发给游戏，避免游戏侧重复插入字符或抢走输入焦点。
/** 当前焦点是否落在原生输入框上（此时按键应交给浏览器）。 */
function isTyping(): boolean {
    const a = document.activeElement;
    return !!a && (a.tagName === 'INPUT' || a.tagName === 'TEXTAREA');
}

/**
 * DOM keydown -> BrowserKeyboard.OnKeyDown / OnKeyPress。
 * @param e 键盘事件
 */
function onKeyDown(e: KeyboardEvent): void {
    if (isTyping()) return;
    const k = host.exports.MirEngine.BrowserKeyboard;
    k.OnKeyDown(e.key);
    if (e.key.length === 1) k.OnKeyPress(e.key);
}

/**
 * DOM keyup -> BrowserKeyboard.OnKeyUp。
 * @param e 键盘事件
 */
function onKeyUp(e: KeyboardEvent): void {
    if (isTyping()) return;
    const k = host.exports.MirEngine.BrowserKeyboard;
    k.OnKeyUp(e.key);
}

/** 注册全局键盘监听（mir.keyboardAttach）。 */
export const keyboardAttach = (): void => {
    if (attached) return;
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    attached = true;
};

/** 注销全局键盘监听（mir.keyboardDetach）。 */
export const keyboardDetach = (): void => {
    if (!attached) return;
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    attached = false;
};
