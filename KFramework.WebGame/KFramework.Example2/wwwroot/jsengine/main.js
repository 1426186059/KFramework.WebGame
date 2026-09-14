// 引擎的浏览器启动器。
//
// 它是 TypeScript 源码，编译产物由 tsconfig 直接输出到 MirGame/wwwroot/jsengine/，
// 因此 _framework 需要用 ../ 回退一级。
import { dotnet } from '../_framework/dotnet.js';
import * as gl from './gl.js';
import * as platform from './platform.js';
import * as audio from './audio.js';
import * as text from './text.js';
function findHost(exports) {
    if (!exports)
        return undefined;
    const byNamespace = exports.KFramework?.GameHost;
    if (byNamespace)
        return byNamespace;
    const direct = exports;
    if (direct.GameHost)
        return direct.GameHost;
    return Object.values(exports).find((value) => !!value && typeof value.Frame === 'function');
}
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();
// 注册 C# [JSImport] 使用的模块。模块名必须与 C# 中 [JSImport("函数名", "模块名")] 一致，
// 且函数名不带点号（.NET 会把点号当嵌套路径解析）。
setModuleImports('gl', gl);
setModuleImports('platform', platform);
setModuleImports('audio', audio);
setModuleImports('text', text);
const config = getConfig();
/**
 * 帧回调 KFramework.GameHost.Frame 定义在 KFramework 程序集里，
 * 而 config.mainAssemblyName 是游戏程序集，因此需要在多个程序集中查找。
 */
async function resolveGameHost() {
    const candidates = [config.mainAssemblyName, 'KFramework', 'KFramework.dll'].filter(Boolean);
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
    console.error('[main] 找不到 KFramework.GameHost 导出，画面不会刷新');
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
