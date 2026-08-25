# KFramework.WebGame

创作 Web 游戏的三种方法：

| # | 方法 | 代表技术 | 取舍 |
|---|------|----------|------|
| **1** | **原生 JS 开发** | PixiJS、Three.js、Phaser 等 | 直接用 JavaScript/TypeScript + 现成渲染库（如 PixiJS 做 2D、Three.js 做 3D）。生态最成熟、部署最简单，但逻辑层仍是 JS，类型与工程化靠 TS 弥补。**最快上手。** |
| **2** | **C# + WebAssembly 开发** | .NET 10 WASM（Blazor / WasmBrowserApp） | 游戏逻辑全部用 C# 编写，编译为 WASM 跑在浏览器；浏览器底层能力（渲染 / 输入 / 音效 / 存储）通过一层极薄的 JS 桥（`[JSImport]` / `[JSExport]`）暴露给 C#。**本仓库采用的就是这条路线**，在「C# 工程化 + 可接受性能」之间最均衡。 |
| **3** | **C# + Emscripten 开发** | Emscripten 把 C/C++/C#(IL2CPP 等) 编成 WASM | 借助 Emscripten 工具链将 C#（通常经由 IL2CPP 或 NativeAOT-LLVM 等路径）直接编译为 WASM，绕开托管运行时、贴近原生性能，二进制体积更小；但工具链重、互操作与调试成本高（Unity WebGL、Godot Web 导出即属此类）。**性能上限最高。** |

> 一句话取舍：**方法 1 最快上手，方法 3 性能上限最高，方法 2 最均衡**——本引擎按方法 2 实现。
