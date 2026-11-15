# KCBWG 游戏引擎

KCBWG 是 KFramework.CSharp.Browser.WebAssembly.Game 的缩写。

一句话定位：**用纯 C# 编写浏览器 2D 游戏的一套引擎 / 框架**——把 .NET 编译为 WebAssembly，在浏览器里跑 C# 游戏逻辑，底层图形 / 输入 / 音频 / 内容管线由 `KFramework.MonoGame` 提供，浏览器能力通过 `KFramework.TSEngine`（TypeScript → JS）薄桥暴露。

本仓库是 KCBWG 引擎本体与示例 / 测试 / 工具集；基于它复刻《原始传奇》页游的实际游戏工程在同级目录 `KFramework.Mir.OriginMri2/`。

> ### ⚠️ 适用规模（选型前务必先看）
>
> **现阶段，KCBWG 非常适合小微型游戏的开发**；**中大型游戏不太合适**——开发效率与运行性能都会出问题，不建议采用。
> 下文第三节列出了具体原因（非 AOT 下解释器性能、JSON 解析、反射约束等）。

---

## 一、技术栈与分层

    ┌──────────────────────────────────────────────┐
    │  游戏业务（纯 C#，net10.0）                    │  ← 你写的代码
    │  例：KFramework.Mir.OriginMri2 的 Mir2/ 业务   │
    └───────────────────┬──────────────────────────┘
                        │ 项目引用
    ┌───────────────────▼──────────────────────────┐
    │  KFramework.MonoGame / MonoGameExtend         │  图形/输入/音频/内容/协程/字体
    │  JSBind_* 互操作门面（[JSImport]/[JSExport]）  │  C# 侧只写纯 C#
    └───────────────────┬──────────────────────────┘
                        │ 源生成互操作
    ┌───────────────────▼──────────────────────────┐
    │  KFramework.TSEngine（TS 源码 → dist/jsengine）│  WebGL2 / WebGPU / Canvas2D 后端
    └───────────────────┬──────────────────────────┘
                        │ 浏览器 API
    ┌───────────────────▼──────────────────────────┐
    │  浏览器：requestAnimationFrame / Canvas / WebGL │
    └──────────────────────────────────────────────┘

帧驱动链路（单链路，必须经由框架上屏）：

    index.html → ./jsengine/main.js（TSEngine 启动器）
      → 每帧 requestAnimationFrame 回调 JSBind_GameHost.Frame
      → Game.TickFrame（KFramework.MonoGame）
      → 游戏 Update / Draw
      → 框架在本帧末上屏（present）

**硬约束**：
- 业务工程内**禁止出现任何 `[JSExport]` / `[JSImport]`**；JS 互操作基础设施统一由 `KFramework.MonoGame` 的 `JSBind_*` 提供，业务只写纯 C#。
- **运行时禁止反射**（见第五节）：反射在 wasm 解释 / AOT 下极慢且发布裁剪后易崩，一律用 Roslyn 源生成器 / 静态注册替代。
- JS 是 TS 的**自动生成产物**：改渲染逻辑只改 `KFramework.TSEngine/src/*.ts`，再 `tsc` 生成 `dist/<Debug|Release>/jsengine`，由 `SyncJsEngine` 拷入各 `wwwroot/jsengine`；不要手改生成的 `.js`。

---

## 二、引擎特点（优势）

1. **用 C# 写浏览器游戏，复用既有 C# 资产**
   游戏逻辑、数据结构、服务端协议可直接在浏览器端复用，无需用 JS/TS 重写一遍；对已有 C# 游戏代码库（如经典 Mir2）的移植尤其省事。

2. **Unity 风格的内容管线**
   `ContentManager` 只负责异步拉 `AssetBundle`（`LoadBundleAsync`，可上报进度），`AssetBundle` 负责同步取资源（`LoadAsset` / `LoadText` / `LoadJson` / `LoadTexture`），语义与 Unity AssetBundle 一致——加载集中、运行时取资源零 await。资源由 `KFramework.Content.Cli`（`kfc`）从 `raw/` 打包，图集经 `SpriteSheetLoader` 统一加载为可批处理的 `KSpriteInfo`。

3. **分层清晰的渲染后端**
   图形 / 输入 / 音频抽象在 `KFramework.MonoGame`，浏览器侧渲染由 `KFramework.TSEngine` 提供 WebGL2 / WebGPU / Canvas2D 多后端，业务代码不直接碰浏览器 API。

4. **AOT + IL 裁剪的发布形态**
   Release 走 `npm run build:release`（压缩并剥离 `console.*`）+ NativeAOT + IL trim，产物为纯 wasm，配合预压缩体积可控（见第七节实测）。

5. **互操作面向源生成，AOT 友好**
   `JSBind_*` 门面只持有缓存名（string）、不持有 JS 对象句柄，避免句柄泄漏且能走 `[JSImport]` 源生成 / AOT；TS 函数名 == C# `[JSImport]` 字符串 == `JSBind_*` 静态方法名，三者一致，便于维护。

6. **WASM 安全性相对 JS 更高**
   WASM 是编译后的二进制字节码，逆向与篡改门槛远高于明文 JS（详见第六节）。

---

## 三、缺点与限制（务必先读）

> **现阶段结论（同开头）：KCBWG 非常适合小微型游戏的开发；中大型游戏不太合适——开发效率与运行性能都会出问题，不建议采用。**

1. **非 AOT（解释器模式）下性能巨慢——最需要警惕**
   - .NET Browser-WASM 有两种执行形态：**AOT**（编译期把 IL 编成 wasm 原生码）与 **Mono 解释器**（运行时逐条解释 IL）。
   - **开发 / Debug 态默认不走 AOT**，跑的是解释器；其性能比原生 JIT **慢一到数个数量级**。反射路径（元数据查找、IL 解释、动态分发）更是重灾区。
   - 后果：同一个游戏在解释器下可能卡到几乎不可玩，**只有 Release + AOT 发布才接近可用性能**。因此务必用 AOT 形态做性能评估，不要拿解释器下的帧率下结论；也不要指望"开发环境凑合、发布自然流畅"——发布若漏配 AOT 反而更慢。

2. **JSON 解析巨慢——中大型游戏加载大量配置表可卡几十秒**
   - JSON 是中大型游戏配置的主要载体：成百上千张配置表、地图数据、存档、网络协议几乎都是 JSON。JSON 解析是**计算密集**操作——海量字符扫描、字符串比较、对象分配、状态机调用；在非 AOT 下这些 IL 全部由 **Mono 解释器逐条解释执行**，而非编译成 wasm 原生码，因此慢得离谱。**根子是解释器本身执行慢，不是反射**：即便用零反射的 `JsonDocument`，解释器下照样慢。
   - **典型现象**：中大型游戏一次性加载几十 / 上百张配置表，非 AOT 下**直接卡住几十秒**，页面假死；同一份逻辑换成 AOT 发布后，耗时可降到可接受范围。差距不是几倍，而是一到数个数量级。
   - **正确做法**：① **务必用 AOT 发布**再评估解析性能，不要在解释器下测；② 改用 `JsonDocument` / `Utf8JsonReader` 这类读取器 API（纯顺序解析、零反射、AOT 安全），或 source-generated `JsonSerializerContext`（`[JsonSerializable]` + `JsonSerializer.Deserialize(json, ctx.XXX)`）避免反射式反序列化的额外开销；③ 配置表尽量在构建期转成二进制 / 紧凑格式，从源头减少运行时解析量。
   - **附带约束**：若用默认反射式反序列化，在 IL 裁剪后还会直接抛 `TypeLoadException` / `NullReferenceException`（开发期正常、发布期崩），与上面的性能问题叠加。

3. **禁止运行时反射，开发约束多**
   `Type.GetType` / `Activator.CreateInstance` / `PropertyInfo.GetValue` / `Expression.Compile` / 依赖反射的反序列化（默认 `JsonSerializer` 对未知 / 多态类型）一律不可用。任何"按名字建对象 / 通用属性读写 / 多态分发 / 序列化"都必须改成显式 `switch`、静态字典工厂或 Roslyn 源生成器。大量现成 .NET 库因此不能直接拿来用，开发效率显著低于原生 C# 桌面 / 服务端。

4. **AOT + IL 裁剪的"开发期正常、发布期崩"陷阱**
   发布默认开启 IL 裁剪，未被静态引用到的类型 / 成员会被移除；若靠字符串反射取类型，开发期一切正常，发布版直接抛 `NullReferenceException` / `TypeLoadException`，极难排查。AOT 无 JIT，任何依赖运行时代码发射（Emit / Expression.Compile）的方案都会直接失败。

5. **启动速度与体积不及原生 JS**
   与 PixiJS / Phaser 等原生 JS 方案相比，WASM 需先下载、实例化运行时再启动，首屏和启动速度都不占优；即便 AOT 后 `.wasm` 仍是大头（见第六节）。

6. **构建 / 调试链偏重**
   TS 层（C# 渲染桥）与 C# 层双构建；改渲染要动 TS 并重生成 JS，业务改 C# 要重编 wasm；互操作边界的排错成本高于纯 JS 或纯 C# 工程。

7. **无 `System.Drawing` / 有内存约束**
   WASM 运行环境没有桌面 API，纹理 / 音频 / 字体都要走框架提供的加载与解码通道，Canvas / 大图处理受浏览器内存与 GC 约束。

---

## 四、与其它 Web 游戏方案的对比

创作 Web 游戏的三种方法：

| # | 方法 | 代表技术 | 取舍 |
|---|------|----------|------|
| **1** | **原生 JS 开发** | PixiJS、Three.js、Phaser 等 | 直接用 JavaScript/TypeScript + 现成渲染库（如 PixiJS 做 2D、Three.js 做 3D）。**无论性能还是启动速度都是第一**，无需编译、无运行时开销，生态最成熟、部署最简单。 |
| **2** | **C# + WebAssembly 开发（KCBWG 属此类）** | .NET 10 WASM（Blazor / WasmBrowserApp） | 把 **.NET 运行时编译为 WASM**，得到一个跑在 Web 平台的 .NET 运行时，再由它**加载并运行 IL 代码**（游戏逻辑即 C# 编译出的 IL）。浏览器底层能力（渲染 / 输入 / 音效 / 存储）通过极薄的 JS 桥（`[JSImport]` / `[JSExport]`）暴露给 C#。**核心价值是跨平台**复用 C# 逻辑。 |
| **3** | **C# + Emscripten 开发** | Emscripten 把 C/C++/C#(IL2CPP 等) 编成 WASM | 借助 Emscripten 将 C#（通常经 IL2CPP 或 NativeAOT-LLVM 路径）编译为 WASM，绕开托管运行时贴近原生；**同样主要用于跨平台**移植原生引擎（Unity WebGL、Godot Web 导出即属此类）；工具链重、互操作与调试成本高。 |

> 一句话取舍：**原生 JS 在性能与启动速度上都是第一；WebAssembly / Emscripten 的核心价值是跨平台**。本代码库同时涵盖上述三种方法（同级目录 `PixiJS.Engine/`、`KNI.Engine/`、`WebAssemblyBrowserApp/` 等）。KCBWG 是其中"方法 2"的引擎化封装。

---

## 五、安全性：WASM 相对 JS

JS 是明文源码，打开浏览器开发者工具即可直接阅读、调试、打补丁；WASM 是编译后的二进制字节码，逆向与篡改门槛高得多，**程序极难被破解，安全性更高**。

---

## 六、发布体积（实测：方法 2 / AOT）

AOT 发布后，`publish/wwwroot` 里每个文件会并存三套形态（原始 / `.gz` / `.br`，.NET 自动生成）。它们混在一起，看目录属性得到的是「三套之和」，**那不是客户端要下载的量**。按形态分开看：

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

---

## 七、仓库结构导览

| 目录 | 说明 |
|---|---|
| `KFramework.WebGame/KFramework.MonoGame/` | 引擎底座：图形 / 输入 / 音频 / 内容管线 / 协程 / 字体 / JSBind 互操作门面 |
| `KFramework.WebGame/KFramework.MonoGameExtend/` | 引擎扩展 |
| `KFramework.WebGame/KFramework.TSEngine/` | 浏览器 JS 层（TS 源码，`npm run build` 产出 `dist/<配置>/jsengine`） |
| `KFramework.WebGame/KFramework.Content.Cli/` | `kfc` 内容打包 CLI（raw → `.web.lib` AssetBundle） |
| `KFramework.WebGame/KFramework.Game.Template/` | 游戏工程模板（新项目起点） |
| `KFramework.WebGame/KFramework.Example.Mario/`、`KFramework.Example.Tank/` | 示例游戏 |
| `KFramework.WebGame/KFramework.Test.*/` | Canvas2D / WebGL2 / WebGPU / Common 等渲染测试 |
| `KFramework.WebGame/KFramework.Editor/` | 编辑器 |
| `KFramework.WebGame/Need_DLL/`、`TestTool/` | 依赖 DLL 与测试工具 |
| `KFramework.Mir.OriginMri2/` | 基于 KCBWG 复刻《原始传奇》页游的实际游戏工程（含服务端） |
| `KFramework.Mir.Crystal/`、`KNI.Engine/`、`PixiJS.Engine/`、`WebAssemblyBrowserApp*/` | 其它技术路线 / 历史工程对照 |
