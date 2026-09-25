// 【依赖 C#】本文件编译出的 main.js 每帧回调 KFramework.MonoGame.JSBind_GameHost.Frame（类名/命名空间被其硬编码查找）。
// 引擎的浏览器启动器。
//
// 它是 TypeScript 源码，编译产物由 tsconfig 直接输出到 MirGame/wwwroot/jsengine/，
// 因此 _framework 需要用 ../ 回退一级。
import { dotnet } from '../_framework/dotnet.js';
import * as gl from './gl.js';
import * as html5Canvas from './html_canvas.js';
import * as platform from './platform.js';
import * as audio from './audio.js';
import * as text from './text.js';
import * as texture from './texture.js';
import * as cachestorage from './storage_cachestorage.js';
import * as httpFunc from './http_func.js';
import * as indexeddb from './storage_indexeddb.js';
import * as inputKeyboard from './input_keyboard.js';
import * as inputMouse from './input_mouse.js';
import * as inputTouch from './input_touch.js';
import * as net from './net_websocket.js';
import * as inputOverlay from './input_html_ime.js';
import * as cursor from './cursor.js';
import * as localstorage from './storage_local.js';
function findHost(exports) {
    if (!exports)
        return undefined;
    // 引擎的 JS 绑定统一放在 KFramework.MonoGame 命名空间，类名以 JSBind_ 开头。
    // 帧回调的完整路径是 KFramework.MonoGame.JSBind_GameHost。
    const byNamespace = exports.KFramework?.MonoGame?.JSBind_GameHost;
    if (byNamespace)
        return byNamespace;
    const direct = exports;
    if (direct.JSBind_GameHost)
        return direct.JSBind_GameHost;
    // 兜底：递归下钻整棵导出树找带 Frame 的类型，避免 C# 侧改名后这里静默失效。
    const stack = [exports];
    while (stack.length > 0) {
        const node = stack.pop();
        if (!node || typeof node !== 'object')
            continue;
        if (typeof node.Frame === 'function')
            return node;
        stack.push(...Object.values(node));
    }
    return undefined;
}
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();
// 注册 C# [JSImport] 使用的模块。模块名必须与 C# 中 [JSImport("函数名", "模块名")] 一致，
// 且函数名不带点号（.NET 会把点号当嵌套路径解析）。
setModuleImports('gl', gl);
setModuleImports('canvas', html5Canvas);
setModuleImports('platform', platform);
setModuleImports('audio', audio);
setModuleImports('text', text);
setModuleImports('texture', texture);
setModuleImports('cachestorage', cachestorage);
setModuleImports('http_func', httpFunc);
setModuleImports('indexeddb', indexeddb);
setModuleImports('input_keyboard', inputKeyboard);
setModuleImports('input_mouse', inputMouse);
setModuleImports('input_touch', inputTouch);
setModuleImports('net_websocket', net);
setModuleImports('input_html_ime', inputOverlay);
setModuleImports('cursor', cursor);
setModuleImports('localstorage', localstorage);
const config = getConfig();
// KFramework.MonoGame 程序集承载所有 JSBind_* 绑定。这里统一取一次导出，再分发给各模块；
// 网络层经 setHandlers 注册的 [JSExport] 回调把 WebSocket 事件推回 C#。
// （文本输入覆盖层 input_html_ime 为纯 Pull：C# 主动 show/hide 并每帧 getValue，无需注册回调。）
const mono = (await getAssemblyExports('KFramework.MonoGame'));
const bind = mono?.KFramework?.MonoGame;
if (!bind) {
    console.warn('[main] KFramework.MonoGame 导出未就绪');
}
else {
    // 网络层：浏览器 WebSocket 事件 → C#（JSBind_Net_WebSocket）
    const ns = bind.JSBind_Net_WebSocket;
    if (ns) {
        net.setHandlers({
            onOpen: (h) => ns.OnOpen(h),
            onBinaryMessage: (h, d) => ns.OnBinaryMessage(h, d),
            onClose: (h, c) => ns.OnClose(h, c),
            onError: (h, m) => ns.OnError(h, m),
        });
    }
    else {
        console.warn('[main] 网络层导出未找到: JSBind_Net_WebSocket');
    }
    // 文本输入覆盖层（input_html_ime）为纯 Pull，无需在此注册回调。
}
/**
 * 帧回调 JSBind_GameHost.Frame 定义在 KFramework.MonoGame 程序集里，
 * 而 config.mainAssemblyName 是游戏程序集，因此需要在多个程序集中查找。
 */
async function resolveGameHost() {
    const candidates = [config.mainAssemblyName, 'KFramework.MonoGame', 'KFramework.MonoGame.dll'].filter(Boolean);
    for (const assemblyName of candidates) {
        try {
            const host = findHost(await getAssemblyExports(assemblyName));
            if (host) {
                console.log('[main] 已定位帧回调:', assemblyName);
                return host;
            }
        }
        catch (error) {
            console.warn('[main] 读取程序集导出失败:', assemblyName, error);
        }
    }
    return undefined;
}
const host = await resolveGameHost();
if (!host) {
    console.error('[main] 找不到 JSBind_GameHost 导出，画面不会刷新');
}
else {
    platform.setFrameCallback((timestamp) => host.Frame(timestamp));
}
// 浏览器要求用户手势后才能启动音频
const unlockAudio = () => {
    audio.unlock();
    window.removeEventListener('pointerdown', unlockAudio);
    window.removeEventListener('keydown', unlockAudio);
};
window.addEventListener('pointerdown', unlockAudio);
window.addEventListener('keydown', unlockAudio);
document.getElementById('boot')?.remove();
await runMain();
