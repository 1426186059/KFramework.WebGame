// 【依赖 C#】本文件编译出的 main.js 每帧回调 KFramework.MonoGame.JSBind_GameUpdate.Frame（类名/命名空间被其硬编码查找）。
// 引擎的浏览器启动器。
//
// 它是 TypeScript 源码，编译产物由 tsconfig 直接输出到 MirGame/wwwroot/jsengine/，
// 因此 _framework 需要用 ../ 回退一级。

import { dotnet } from '../_framework/dotnet.js';
import * as gl from './render_webgl20.js';
import * as webgpu from './render_webgpu.js';
import * as html5Canvas from './html_canvas.js';
import * as htmlWindow from './html_window.js';
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
import * as gameUpdate from './game_update.js';
import * as gameFrameData from './game_frame_take_js_data.js';

interface GameHost {
    // 只推帧、<b>不带</b>事件数据：事件由 C# 在本帧的 Update 里调 takeFrameData 回头取（一推一拉）。
    // 取的那个方向是 C#→JS，能走 MemoryView 零拷贝，每帧零分配；
    // 而随帧一并送来是 JS→C# 的 byte[]，每帧都要新建一个托管数组。
    Frame(timestampMs: number): void;
}

interface NetExport {
    OnOpen: (handle: number) => void;
    OnBinaryMessage: (handle: number, data: Uint8Array) => void;
    OnClose: (handle: number, code: number) => void;
    OnError: (handle: number, message: string) => void;
}

// 网络回调的转发目标：存成模块级变量，好让下面的回调写成具名函数（不产生闭包）
let netNs: NetExport | null = null;

function onNetOpen(handle: number): void {
    if (netNs) netNs.OnOpen(handle);
}

function onNetBinaryMessage(handle: number, data: Uint8Array): void {
    if (netNs) netNs.OnBinaryMessage(handle, data);
}

function onNetClose(handle: number, code: number): void {
    if (netNs) netNs.OnClose(handle, code);
}

function onNetError(handle: number, message: string): void {
    if (netNs) netNs.OnError(handle, message);
}

// KFramework.MonoGame 程序集导出的 JSBind_* 绑定集合（网络层等）。
interface MonoGameExports {
    KFramework?: {
        MonoGame?: {
            JSBind_Net_WebSocket?: NetExport;
        };
    };
}

interface AssemblyExports {
    [key: string]: unknown;
}

function findHost(exports: AssemblyExports | null): GameHost | undefined {
    if (!exports) return undefined;

    // 引擎的 JS 绑定统一放在 KFramework.MonoGame 命名空间，类名以 JSBind_ 开头。
    // 帧回调的完整路径是 KFramework.MonoGame.JSBind_GameUpdate。
    const byNamespace = (
        exports as { KFramework?: { MonoGame?: { JSBind_GameUpdate?: GameHost } } }
    ).KFramework?.MonoGame?.JSBind_GameUpdate;
    if (byNamespace) return byNamespace;

    const direct = exports as { JSBind_GameUpdate?: GameHost };
    if (direct.JSBind_GameUpdate) return direct.JSBind_GameUpdate;

    // 兜底：递归下钻整棵导出树找带 Frame 的类型，避免 C# 侧改名后这里静默失效。
    const stack: unknown[] = [exports];
    while (stack.length > 0) {
        const node = stack.pop();
        if (!node || typeof node !== 'object') continue;
        if (typeof (node as GameHost).Frame === 'function') return node as GameHost;
        stack.push(...Object.values(node));
    }
    return undefined;
}

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

// 注册 C# [JSImport] 使用的模块。模块名必须与 C# 中 [JSImport("函数名", "模块名")] 一致，
// 且函数名不带点号（.NET 会把点号当嵌套路径解析）。
setModuleImports('render_webgl20', gl);
setModuleImports('render_webgpu', webgpu);
setModuleImports('canvas', html5Canvas);
setModuleImports('window', htmlWindow);
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
setModuleImports('game_update', gameUpdate);
// 每帧统一事件入口：C# 每帧只调它一次，取回键盘/鼠标/触摸/系统事件（取代三者各自 poll）。
setModuleImports('game_frame_take_js_data', gameFrameData);

const config = getConfig();

// KFramework.MonoGame 程序集承载所有 JSBind_* 绑定。这里统一取一次导出，再分发给各模块；
// 网络层经 setHandlers 注册的 [JSExport] 回调把 WebSocket 事件推回 C#。
// （文本输入覆盖层 input_html_ime 为纯 Pull：C# 主动 show/hide 并每帧 getValue，无需注册回调。）
const mono = (await getAssemblyExports('KFramework.MonoGame')) as MonoGameExports | null;
const bind = mono?.KFramework?.MonoGame;

// 把程序集导出树注入文本输入覆盖层模块，使其能在 JS 侧回调 C# 的 [JSExport]
//（MirEngine.BrowserInputIme.OnDomValue / OnKeyDown）。host 并非全局变量，必须显式传入，
// 否则 input_html_ime.js 里 host.exports... 会抛 ReferenceError，导致输入回传失效。
inputOverlay.init(mono);

if (!bind) {
    console.warn('[main] KFramework.MonoGame 导出未就绪');
} else {
    // 网络层：浏览器 WebSocket 事件 → C#（JSBind_Net_WebSocket）
    // C# 那侧的对象存进模块级变量，回调才能用具名函数 —— 用具名函数去捕获局部的 ns
    // 就是一个闭包（本目录禁止匿名函数与闭包，见 ReadMe）。
    const ns = bind.JSBind_Net_WebSocket;
    if (ns) {
        netNs = ns;
        net.setHandlers({
            onOpen: onNetOpen,
            onBinaryMessage: onNetBinaryMessage,
            onClose: onNetClose,
            onError: onNetError,
        });
    } else {
        console.warn('[main] 网络层导出未找到: JSBind_Net_WebSocket');
    }

    // 文本输入覆盖层（input_html_ime）为纯 Pull，无需在此注册回调。
}

/**
 * 帧回调 JSBind_GameUpdate.Frame 定义在 KFramework.MonoGame 程序集里，
 * 而 config.mainAssemblyName 是游戏程序集，因此需要在多个程序集中查找。
 */
async function resolveGameHost(): Promise<GameHost | undefined> {
    // 官方类型里 MonoConfig.mainAssemblyName 是可选的（string | undefined），
    // 所以这里必须把空值滤掉 —— 早先手写的宽松声明把它写成必有，才一直没暴露。
    // 注意不能只写 .filter(Boolean)：TS 认不出 Boolean 会滤掉 undefined，类型收窄不了。
    const candidates: string[] = [config.mainAssemblyName, 'KFramework.MonoGame', 'KFramework.MonoGame.dll']
        .filter((name): name is string => typeof name === 'string' && name.length > 0);

    for (const assemblyName of candidates) {
        try {
            const host = findHost(await getAssemblyExports(assemblyName));
            if (host) {
                console.log('[main] 已定位帧回调:', assemblyName);
                return host;
            }
        } catch (error) {
            console.warn('[main] 读取程序集导出失败:', assemblyName, error);
        }
    }
    return undefined;
}

// 帧回调的宿主存成模块级变量：回调必须是【具名函数】，而具名函数不能去捕获局部的 host ——
// 那会构成一个每帧都要重建的闭包（本目录禁止匿名函数与闭包，见 ReadMe）。
let frameHost: GameHost | null = null;

/**
 * 每帧回调：只推帧，不带事件数据。
 * 事件由 C# 在本帧的 Update 里调 takeFrameData 回头取（一推一拉）——
 * 那个方向是 C#→JS，能走 MemoryView 零拷贝，每帧零分配；
 * 若随帧一并送来，JS→C# 的 byte[] 每帧都要新建一个托管数组。
 */
function onFrame(timestampMs: number): void {
    if (frameHost) frameHost.Frame(timestampMs);
}

const host = await resolveGameHost();

if (!host) {
    console.error('[main] 找不到 JSBind_GameUpdate 导出，画面不会刷新');
} else {
    frameHost = host;
    gameUpdate.setFrameCallback(onFrame);
}

// 浏览器要求用户手势后才能启动音频（具名函数：内部自引用解绑，用函数声明即可）
function unlockAudio(): void {
    audio.unlock();
    window.removeEventListener('pointerdown', unlockAudio);
    window.removeEventListener('keydown', unlockAudio);
}
window.addEventListener('pointerdown', unlockAudio);
window.addEventListener('keydown', unlockAudio);

document.getElementById('boot')?.remove();

await runMain();
