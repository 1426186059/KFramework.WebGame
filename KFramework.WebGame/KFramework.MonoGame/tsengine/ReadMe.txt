KFramework.MonoGame/tsengine 说明与性能优化建议
=============================================

一、浏览器层（jsengine）维护说明
-------------------------------

本目录是引擎的 TypeScript 源码，编译后输出到各示例的 `wwwroot/jsengine/` 供浏览器加载。

1. 唯一真源：这里（tsengine/src/*.ts）
   改 TS 只改 `KFramework.MonoGame/tsengine/src/` 下的 `.ts` 文件（例如 `main.ts`）。
   构建时 `tsc` 会把它编译成 `Example1/wwwroot/jsengine/main.js`，再经复制链铺到其他位置。

2. 不要手动改的副本
   以下 `main.js`/`audio.js` 等都是**自动生成 / 自动复制**的，手动改会被下次构建覆盖，请勿直接编辑：

   | 文件 | 来源 | 是否自动 |
   | --- | --- | --- |
   | `KFramework.Example1/wwwroot/jsengine/*.js` | `tsc` 直接产物（`tsconfig.json` 的 `outDir` 指向它） | 是（tsc 生成） |
   | `MirGame/wwwroot/jsengine/*.js` | 暂存副本（MirGame 无 csproj，靠提交/手工同步） | 是（需随 Example1 同步） |
   | `KFramework.Example2/wwwroot/jsengine/*.js` | 由 `Example2.csproj` 的 `SyncJsEngine` 目标从 `MirGame/wwwroot/jsengine` 自动复制 | 是（构建复制） |

   复制链：`tsengine/src` -(tsc)-> `Example1/wwwroot/jsengine` →(同步) `MirGame/wwwroot/jsengine` →(SyncJsEngine) `Example2/wwwroot/jsengine`。

   > 注：`MirGame` 与 `Example1` 之间目前没有构建目标兜底，只改 `.ts` 后务必跑 `tsc`
   > （或确认 `MirGame` 那份也被同步），否则 `Example2` 仍会拿到旧的 `MirGame` 副本。

3. 引擎改名时务必同步（main.ts）
   `main.ts` 通过 `getAssemblyExports(程序集名)` 查找 C# 侧 `[JSExport]` 的 `JSBind_GameHost`，
   并把导出按命名空间下钻。以下两处硬编码了引擎的程序集名与命名空间，**改名时必须同步修改**：

   - `resolveGameHost()` 的候选列表：`'KFramework.MonoGame'` / `'KFramework.MonoGame.dll'`
     （与 `KFramework.MonoGame.csproj` 的 `<AssemblyName>` 一致）。
   - `findHost()` 的命名空间路径：`exports.KFramework?.MonoGame?.JSBind_GameHost`
     （与 `JSBind_GameHost.cs` 的 `namespace KFramework.MonoGame` 一致）。

   若程序集名/命名空间改了但 `main.ts` 没同步，`getAssemblyExports` 会在空导出表上越界
   （"memory access out of bounds"），帧循环挂不上，画面不会刷新。

二、性能优化建议
---------------

适用范围：本引擎 JS 层，被各 Example 的 wasm 构建引用。
核心结论：本引擎已在多处做了正确优化，以下列出"应继续保持的项"与"可进一步打磨的项"。
严禁在没有实测的情况下随意加转换层（见第 4 节反面教材）。

1. 音频（jsengine/audio.js）
   - 预解码优先：decodeAudioData 是异步的，应在资源加载阶段（如 Example3 的
     AssetBundle 预载、或 LoadAsync 等待就绪）完成，避免运行时首次触发导致卡顿
     或延迟出声。不要在播放热路径里做同步重解码。
   - 噪声缓冲复用：getNoiseBuffer() 已做缓存（只生成一次 0.6s 噪声），避免每帧
     用 Math.random 重建。保持该写法，这是正确示范。
   - 实例复用：对同一音效的重复播放，复用已 createInstance 得到的实例
     （stop 后重新 start），不要在每个播放点都 new 一套 gain/panner 节点。
   - master gain（setMasterVolume / setMuted）是一次性的，开销可忽略，无需优化。
   - 自动解锁：attachAutoUnlock 在首次 pointerdown/keydown/touchstart 时 resume，
     避免 AudioContext 停留在 suspended 状态空跑。保持。

2. WebGL（src/gl.ts）
   - 上下文配置已性能友好，保持现状：
     * preserveDrawingBuffer: false（避免每帧强制回读）
     * antialias: false
     * powerPreference: 'high-performance'
   - 数据上传的拷贝开销：toBytes / toFloats 在每次 bufferData / texImage2D 调用时
     会 new 一个 Uint8Array 再做 copyTo。对"每帧更新"的动态数据（如动态纹理、
     每帧变化的顶点缓冲），建议缓存一个固定尺寸的 scratch Uint8Array 复用，
     减少 GC 抖动；静态数据一次性上传即可，不要每帧重传。
   - 渲染循环（在主工程侧实现）应合批绘制、减少 shader / bind 切换、避免每帧
     重复创建 GL 对象（buffer / texture / program）。

3. 内存与 GC
   - 类型转换（gl.ts 的 toBytes / toFloats，text.ts 的 writeInts / writeBytes）
     会产生临时 TypedArray 分配。高频路径上尽量复用缓冲、避免在闭包内分配大对象。
   - 流式写回（text.ts）已优先使用 view.set 快路径，只在无 set 时才走逐元素回退。
     保持该判定顺序。

4. 反面教材（不要做的事）
   - 不要"为了防 MemoryView"给音频的 decodeAudioData 套 toBytes 转换层。
     MemoryView.slice() 返回的是 ArrayBufferView，原版 data.slice().buffer 已可直接
     交给 decodeAudioData（已实测：原版 audio.js 与重置后的 audio.ts 均正常播放）。
     多余的转换只增加一次内存拷贝和分支判断，属于无依据的代码复杂化。
   - 区别对待两种 API：
     * WebGL（bufferData / texImage2D）：只认 TypedArray → MemoryView 必须 toBytes 转。
     * Audio（decodeAudioData）：吃 ArrayBuffer，MemoryView.slice() 已满足 → 不要转。
     二者不可混为一谈，这是最容易踩的坑。

5. 验证清单
   - 改完音频/图形相关代码后，至少确认：
     * 声音能正常播放（控制台无 decodeAudioData 失败日志）；
     * 画面能正常绘制（readPixel 自检点能读到非全 0 像素）；
     * 长时间运行无内存持续增长（DevTools Performance / Memory 面板抽样）。
