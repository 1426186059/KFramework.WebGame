# KFramework.WebGame

创作 Web 游戏的三种方法：

| # | 方法 | 代表技术 | 取舍 |
|---|------|----------|------|
| **1** | **原生 JS 开发** | PixiJS、Three.js、Phaser 等 | 直接用 JavaScript/TypeScript + 现成渲染库（如 PixiJS 做 2D、Three.js 做 3D）。**无论性能还是启动速度都是第一**，无需编译、无运行时开销，生态最成熟、部署最简单。 |
| **2** | **C# + WebAssembly 开发** | .NET 10 WASM（Blazor / WasmBrowserApp） | 游戏逻辑全部用 C# 编写，编译为 WASM 跑在浏览器；浏览器底层能力（渲染 / 输入 / 音效 / 存储）通过一层极薄的 JS 桥（`[JSImport]` / `[JSExport]`）暴露给 C#。**主要用于跨平台**复用 C# 逻辑。 |
| **3** | **C# + Emscripten 开发** | Emscripten 把 C/C++/C#(IL2CPP 等) 编成 WASM | 借助 Emscripten 工具链将 C#（通常经由 IL2CPP 或 NativeAOT-LLVM 等路径）编译为 WASM，绕开托管运行时贴近原生。**同样主要用于跨平台**移植原生引擎（Unity WebGL、Godot Web 导出即属此类）；但工具链重、互操作与调试成本高。 |

> 一句话取舍：**原生 JS 在性能与启动速度上都是第一；WebAssembly / Emscripten 的核心价值是跨平台**。本代码库同时涵盖上述三种方法。
