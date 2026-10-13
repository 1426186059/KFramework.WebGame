# KFramework.WebGame

创作 Web 游戏的三种方法：

| # | 方法 | 代表技术 | 取舍 |
|---|------|----------|------|
| **1** | **原生 JS 开发** | PixiJS、Three.js、Phaser 等 | 直接用 JavaScript/TypeScript + 现成渲染库（如 PixiJS 做 2D、Three.js 做 3D）。**无论性能还是启动速度都是第一**，无需编译、无运行时开销，生态最成熟、部署最简单。 |
| **2** | **C# + WebAssembly 开发** | .NET 10 WASM（Blazor / WasmBrowserApp） | 原理：把 **.NET 运行时编译为 WASM**，得到一个跑在 Web 平台上的 .NET 运行时，再由它**加载并运行 IL 代码**（游戏逻辑即 C# 编译出的 IL）。浏览器底层能力（渲染 / 输入 / 音效 / 存储）通过一层极薄的 JS 桥（`[JSImport]` / `[JSExport]`）暴露给 C#。**主要用于跨平台**复用 C# 逻辑。 |
| **3** | **C# + Emscripten 开发** | Emscripten 把 C/C++/C#(IL2CPP 等) 编成 WASM | 借助 Emscripten 工具链将 C#（通常经由 IL2CPP 或 NativeAOT-LLVM 等路径）编译为 WASM，绕开托管运行时贴近原生。**同样主要用于跨平台**移植原生引擎（Unity WebGL、Godot Web 导出即属此类）；但工具链重、互操作与调试成本高。 |

> 一句话取舍：**原生 JS 在性能与启动速度上都是第一；WebAssembly / Emscripten 的核心价值是跨平台**。本代码库同时涵盖上述三种方法。

## 发布体积（实测：方法 2）

AOT 发布后，`publish/wwwroot` 里每个文件会并存三套形态（原始 / `.gz` / `.br`，.NET 自动生成）。
它们混在一起，看目录属性得到的是「三套之和」，**那不是客户端要下载的量**。按形态分开看：

以传奇客户端（Mir2，Release + AOT + Trimmed）为例：

| 形态 | 文件数 | 体积 | 相对原始 |
|---|---|---|---|
| 原始未压 | 56 | **21.41 MB** | — |
| gzip 预压缩 | 56 | **6.80 MB** | 省 68% |
| brotli 预压缩 | 56 | **4.78 MB** | 省 78% |
| 合计 | 168 | 32.98 MB | 即 `wwwroot` 目录本身的大小 |

- `.wasm` 占原始体积的 **86%**（18.33 / 21.41 MB），压缩收益基本全在它身上。
- 服务器若只用一种预压缩，**另一套（6.80 或 4.78 MB）是纯占地方**，可从部署目录删掉。
- 拆分工具（不改文件名，只按形态搬运；三份相加 == 源目录总量，用于核对没漏）：

  ```
  cd KFramework.Mir/WebGame.Mir2/Tools
  node compare-wwwroot-size.mjs "<publish 下的 wwwroot 路径>"
  ```

  生成 `wwwroot-ori` / `wwwroot-gz` / `wwwroot-br` 三个同级目录。

- AOT 发布脚本：`KFramework.Mir/WebGame.Mir2/WebGame.Mir2.MonoGame.Client/publish-aot.bat`（双击即可）。
