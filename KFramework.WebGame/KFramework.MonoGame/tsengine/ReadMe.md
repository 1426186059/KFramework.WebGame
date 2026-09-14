# 浏览器层（jsengine）维护说明

本目录是引擎的 TypeScript 源码，编译后输出到各示例的 `wwwroot/jsengine/` 供浏览器加载。

## 唯一真源：这里（tsengine/src/*.ts）

**改 TS 只改 `KFramework.MonoGame/tsengine/src/` 下的 `.ts` 文件**（例如 `main.ts`）。
构建时 `tsc` 会把它编译成 `Example1/wwwroot/jsengine/main.js`，再经复制链铺到其他位置。

## 不要手动改的副本

以下 `main.js` 都是**自动生成 / 自动复制**的，手动改会被下次构建覆盖，**请勿直接编辑**：

| 文件 | 来源 | 是否自动 |
| --- | --- | --- |
| `KFramework.Example1/wwwroot/jsengine/*.js` | `tsc` 直接产物（`tsconfig.json` 的 `outDir` 指向它） | 是（tsc 生成） |
| `MirGame/wwwroot/jsengine/*.js` | 暂存副本（MirGame 无 csproj，靠提交/手工同步） | 是（需随 Example1 同步） |
| `KFramework.Example2/wwwroot/jsengine/*.js` | 由 `Example2.csproj` 的 `SyncJsEngine` 目标从 `MirGame/wwwroot/jsengine` 自动复制 | 是（构建复制） |

复制链：`tsengine/src` -(tsc)-> `Example1/wwwroot/jsengine` →(同步) `MirGame/wwwroot/jsengine` →(SyncJsEngine) `Example2/wwwroot/jsengine`。

> 注：`MirGame` 与 `Example1` 之间目前没有构建目标兜底，只改 `.ts` 后务必跑 `tsc`（或确认 `MirGame` 那份也被同步），否则 `Example2` 仍会拿到旧的 `MirGame` 副本。

## 引擎改名时务必同步（main.ts）

`main.ts` 通过 `getAssemblyExports(程序集名)` 查找 C# 侧 `[JSExport]` 的 `JSBind_GameHost`，
并把导出按命名空间下钻。以下两处硬编码了引擎的程序集名与命名空间，**改名时必须同步修改**：

- `resolveGameHost()` 的候选列表：`'KFramework.MonoGame'` / `'KFramework.MonoGame.dll'`
  （与 `KFramework.MonoGame.csproj` 的 `<AssemblyName>` 一致）。
- `findHost()` 的命名空间路径：`exports.KFramework?.MonoGame?.JSBind_GameHost`
  （与 `JSBind_GameHost.cs` 的 `namespace KFramework.MonoGame` 一致）。

若程序集名/命名空间改了但 `main.ts` 没同步，`getAssemblyExports` 会在空导出表上越界
（"memory access out of bounds"），帧循环挂不上，画面不会刷新。
