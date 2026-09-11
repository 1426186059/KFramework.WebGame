// mirengine/main.ts
// 浏览器客户端统一入口（Web_Mir2 / Web_Mir3 共用同一份）。
// 单一可编辑源位于本文件；先由 tsc 编译成 dist\main.js，再由各自 csproj 的 CopyJsEngine 目标把 dist\ 下的 *.js
// 物理拷贝到本站 wwwroot\jsengine（保持子目录结构），作为本地静态资源随构建/发布输出。
// 注意：编译产物最终落在 wwwroot\jsengine 下，故 import 路径以该目录为基准（./shared.js、./core/...、../_framework/...）。
// 仅使用 WebGL 2.0 渲染后端（见 ./render/webgl/*）。
// @ts-ignore —— dotnet.js 由 WASM 构建产出（wwwroot\_framework 下），源码阶段不存在该模块。
import { dotnet } from '../_framework/dotnet.js';
import { host, dom, preloadAssets } from './shared.js';
// 各输入 / 渲染 / 存储模块（每个对应 JSBind 里的一个 C# 类）。
import * as storage from './core/storage.js';
import * as keyboard from './core/keyboard.js';
import * as input from './core/input.js';
import * as mouse from './core/mouse.js';
import * as audio from './core/audio.js';
import * as resource from './core/resource.js';
import * as websocket from './core/websocket.js';
import * as printTool from './core/PrintTool.js';
import * as webgl2d from './render/webgl/webgl2d.js';
import * as webglEngine from './render/webgl/webgl-engine.js';
import * as cursor from './core/cursor.js';
const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.create();
// 把各模块导出的 mir.* 函数聚合成一个 mir 对象。
// 模块名仍为 "main.js"，与 C# 侧 [JSImport("mir.xxx", "main.js")] 对齐。
const mir = {
    ...storage,
    ...keyboard,
    ...input,
    ...mouse,
    ...audio,
    ...resource,
    ...websocket,
    ...printTool,
    ...webgl2d,
    ...webglEngine,
    ...cursor,
};
setModuleImports('main.js', { mir });
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
host.exports = exports; // 供各输入模块在 DOM 事件回调里调用 BrowserKeyboard/BrowserMouse 的 [JSExport] 入口
// 引擎层（输入/渲染/存储等 JSBind）的 [JSExport] 位于 MirEngine.Browser 程序集，
// 而 getAssemblyExports 只返回主程序集的导出。若不合并，host.exports.MirEngine 缺失，
// 鼠标/键盘事件回调会抛 “Cannot read properties of undefined (reading 'BrowserMouse')”。
// 深合并其余已加载程序集的导出（按命名空间/类型嵌套合并）。
/**
 * 深合并托管导出树（按命名空间 / 类型逐层合并）。
 * @param target 合并到的目标对象
 * @param src 源导出对象
 */
function mergeExports(target, src) {
    for (const k of Object.keys(src)) {
        const v = src[k];
        if (v && typeof v === 'object' && !Array.isArray(v)) {
            if (!target[k] || typeof target[k] !== 'object')
                target[k] = {};
            mergeExports(target[k], v);
        }
        else {
            target[k] = v;
        }
    }
}
for (const name of ['MirEngine.Browser', ...(config.assemblyNames || [])]) {
    if (name === config.mainAssemblyName)
        continue;
    try {
        mergeExports(exports, await getAssemblyExports(name));
    }
    catch (e) {
        // 该装配无 JSExport 或尚未加载，忽略。
    }
}
// [JSExport] 的键是完整类型名（含命名空间）。
// 兼容 Zircon 原版客户端驱动器与 demo 版：多端共用同一份入口。
const game = exports.Client?.MirClientHost
    ?? exports.Client?.Program
    ?? exports.MirClient?.MirGame
    ?? exports.MirGame;
if (!game) {
    console.error('未找到客户端导出（MirClientHost / Client.Program / MirGame），可用导出：', Object.keys(exports));
    dom.loading.innerHTML = '<div style="color:#e06c75">初始化失败：未找到客户端导出（详见控制台）</div>';
    throw new Error('client export not found');
}
// index.html 里固定存在 #loadText，缺失属于页面结构错误，直接断言非空以便访问其属性。
const loadText = document.getElementById('loadText');
try {
    loadText.textContent = '正在初始化客户端（资源按需加载）…';
    game.Init();
    // 启动前异步预热数据库（System.db / Users.db）：避免连接后 LoadDatabase 的同步取字节冻结主线程 → 心跳超时。
    // 预取为异步 fetch，不阻塞主线程；完成后同步 getBytes 命中缓存即瞬时返回。
    if (typeof game.GetPreloadUrls === 'function') {
        try {
            const preload = game.GetPreloadUrls();
            if (preload && preload.length) {
                loadText.textContent = '正在预加载数据库…';
                await preloadAssets(preload);
            }
        }
        catch (e) {
        }
    }
    dom.loading.style.display = 'none';
    requestAnimationFrame(function loop(t) {
        try {
            game.Frame(t);
        }
        catch (err) {
            // 仅捕获托管异常（JSImport 边界的原生异常会以 unreachable 终止实例，无法在此恢复）。
            console.error('[Frame]', err);
        }
        requestAnimationFrame(loop);
    });
}
catch (err) {
    console.error(err);
    // strict 下 catch 变量是 unknown：错误消息取 Error.message，其余退化为 String(err)。
    const msg = err instanceof Error ? err.message : String(err);
    dom.loading.innerHTML = `<div style="color:#e06c75">初始化失败：${msg}</div>`;
}
