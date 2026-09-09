import { dotnet } from './_framework/dotnet.js'
import * as gl from './gl.js'
import * as platform from './platform.js'
import * as audio from './audio.js'
import * as text from './text.js'

const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

// 注册 C# [JSImport] 使用的模块
setModuleImports('gl', gl);
setModuleImports('platform', platform);
setModuleImports('audio', audio);
setModuleImports('text', text);

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// KFramework.GameHost.Frame 由 requestAnimationFrame 每帧调用
let host = exports.KFramework?.GameHost ?? exports.GameHost;

// 兜底：导出结构可能因命名空间层级不同而变化，按方法特征查找
if (!host) {
    host = Object.values(exports).find((value) => value && typeof value.Frame === 'function');
}

if (!host) {
    console.error('[main] 找不到 KFramework.GameHost 导出，可用导出：', Object.keys(exports));
} else {
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
