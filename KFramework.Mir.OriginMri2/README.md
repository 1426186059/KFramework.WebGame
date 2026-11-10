# KFramework.Mir.原始传奇

一个**参考 Web 页游《原始传奇》**的 Web 游戏工程：以 `KFramework.MonoGame` 为底层底座，
把传奇（Mir2）的客户端 + 服务端整套搬进浏览器（.NET 10 / WASM）。

## 项目现状

作者本人已转向 **机器学习 / 大模型** 方向，**本仓库暂不更新**，不再持续跟进新特性。
但作为一份把 Mir2 客户端 + 服务端整套搬进浏览器的完整工程，它对于学习
**游戏引擎** 和 **传奇类游戏** 仍然是个不错的仓库，代码与资料都保留在仓库里可读可查：

- **学游戏引擎**：帧驱动链路、渲染分层与批处理、资源按需异步加载、浏览器输入桥、
  WASM 运行时适配，以及 `KFramework.MonoGame` 这一层底座，都有可运行的完整实现；
- **学传奇游戏**：地图与瓦片、配置表体系、物品 / 技能 / 掉落 / 任务 / NPC、
  服务端战斗与存档逻辑，连同 `Tools/原始传奇-资源提取/` 下的页游逆向资料，都能对照着看。

另外，本工程的游戏逻辑跑在 **.NET WASM** 上而不是 JS：JS 是明文源码，打开开发者工具
即可直接阅读、调试、打补丁；WASM 是编译后的二进制字节码，逆向和篡改的门槛高得多，
**程序极难被破解，安全性更高**。

---

## 一、参考对象（务必先看这里）

本工程“界面长什么样 / 资源怎么组织 / 地图怎么切分 / 数据怎么解析”的标准答案是：

    Web 页游《原始传奇》—— Egret 引擎，构建 fugu_20201203，主包 main10.25341.1.min.js

- 逆向出的资源规则（manifest 图集索引、CDN URL 拼装、zipmap 地图瓦片、地图逻辑数据 `.bin`）
  记录在 [`Tools/原始传奇-资源提取/README.md`](Tools/原始传奇-资源提取/README.md)；
- 页游原始 JS / 配置存档在 `Tools/原始传奇-资源提取/原始传奇-资源参考/`；
- 提取 / 转码工具在 `Tools/` 下。

> ### 📌 两个“传奇”务必区分（最容易搞混，先背下来）
>
> | 称呼 | 路径 | 是什么 | 在本工程的角色 |
> | --- | --- | --- | --- |
> | **原始传奇**（页游） | 资源提取根：`Tools/原始传奇-资源提取/`<br>原始 JS / 配置存档：`Tools/原始传奇-资源提取/原始传奇-资源参考/` | Egret 引擎的 **Web 页游**（构建 `fugu_20201203`，主包 `main10.25341.1.min.js`）—— **本工程要复刻的目标游戏** | ✅ **权威参考 / 行为标准**：界面、资源组织、地图切分、数据解析，一律以它为准 |
> | **原版传奇**（经典 Mir2） | `D:\OpenSource\Crystal` | 经典 Mir2 服务端 + 桌面客户端（WinForms / SlimDX / D3D9），含 `Envir/Quests/*.txt`、`Server.MirDB` 二进制等 | ⚠️ **历史来源，不再作为行为标准**：仅供偶尔对照旧实现，**不要把它当成“原始传奇”去查** |
>
> 简记：
> - **原始传奇 = 我们要做的 Web 页游** → 看 `Tools/原始传奇-资源提取/`（配置分片 `0config~5config`、`main*.min.js`、`map*.dat` 等）。
> - **原版传奇 = 经典 Mir2 老工程** → 看 `D:\OpenSource\Crystal`（任务脚本是 `Envir/Quests/*.txt`，不是我们这套 JSON 表）。
>
> 遇到“本来怎么做”的问题，**只信前者（原始传奇页游）**；后者（原版传奇 / Crystal）仅作历史对照，且两者是**不同游戏**，不能混为一谈。

---

## 二、解决方案构成

解决方案文件：`WebGame.China.Mir2.slnx`

| 目录 | 说明 |
| --- | --- |
| `WebGame.Mir2.MonoGame.Client/` | **浏览器客户端**。net10.0 + `Microsoft.NET.Sdk.WebAssembly`；游戏业务在 `Mir2/`，主机与引导在 `CMain.cs` / `MirGame.cs`，浏览器垫片在 `Shims/`，Web 部署配置在 `2026New/RemoteWebSetting.cs`。完整说明见该目录下 `ReadMe.txt` |
| `Web_Mir2.Server/` | 游戏服务端（网络、地图、怪物、物品、技能、数据库） |
| `Web_Mir2.Server.WinForms/` | 服务端 WinForms 管理 / 监控界面 |
| `../KFramework.WebGame/KFramework.MonoGame/` | 底层框架底座：图形 / 输入 / 音频 / 资源异步加载 / WASM 运行时 |
| `../KFramework.WebGame/KFramework.TSEngine/` | 引擎的浏览器 JS 层（TS 源码，`npm run build` 产出 `dist/<配置>/jsengine`） |
| `Tools/` | 资源与逆向工具链 |

---

## 三、架构总览

    Mir2 业务代码 ──(Shims 垫片)──┐
                                  ├─→ KFramework.MonoGame（图形 / 输入 / 音频 / 资源底层）
    浏览器桥接层 TSEngine ────────┘        ▲
            │                              │ ProjectReference
            ▼                              │
       wwwroot/jsengine (dist 产物)   WebGame.China.Mir2.Client

帧驱动链路（单链路，必须经由框架上屏）：

    index.html → ./jsengine/main.js（KFramework.TSEngine 启动器）
      → 每帧 requestAnimationFrame 回调 JSBind_GameHost.Frame
      → Game.TickFrame（KFramework.MonoGame）
      → MirGame.Update / MirGame.Draw
      → CMain.Loop()（清屏 + 场景 Process/Draw + 纹理回收）
      → 框架在本帧末上屏（present）

**约束**：客户端工程内**禁止出现任何 `[JSExport]` / `[JSImport]`**。
所有 JS 互操作基础设施由 `KFramework.MonoGame` 以 `JSBind_*` 形式提供，业务工程只写纯 C#。

---

## 四、构建与运行

前置：

1. **引擎 JS 层**：由 csproj 的 `BuildTypeScript` 目标自动构建
   （Debug → `npm run build`，Release → `npm run build:release`，后者压缩并剥离 `console.*`），
   产物落在 `dist/<Debug|Release>/jsengine`，再由 `SyncJsEngine` 目标按配置整目录复制到
   `wwwroot/jsengine`。首次需先执行一次：

   ```powershell
   cd <repo>/KFramework.WebGame/KFramework.WebGame/KFramework.TSEngine
   npm install
   ```

2. **本地资源服务**：`Tools/资源服务器/start-assets.bat` 启动两个静态 HTTP 服务
   （`:5080` = 客户端贴图库根目录，`:5081` = Mir2Res http 根，提供地图 Lib / 热更包）。
   地址统一写在 `WebGame.Mir2.MonoGame.Client/2026New/RemoteWebSetting.cs`
   （Web 端没有“本地”概念，部署时只改这一个文件）。

3. **游戏网关**：`RemoteWebSetting.GameServerUrl`（默认 `ws://127.0.0.1:8000`）。

构建：

```powershell
dotnet build WebGame.China.Mir2.slnx
# 或只构建客户端
dotnet build WebGame.Mir2.MonoGame.Client/WebGame.China.Mir2.Client.csproj
```

以 .NET WASM（net10.0）发布后，`wwwroot/index.html` 即为游戏入口。

---

## 五、资源与工具

资源两条来源：

- **《原始传奇》页游 CDN**（`https://cdn.yscq-wy.yscq.com/fugu_20201203/...`）
  - 图集 / UI / 配置：由 `allmanifest*.json` 索引，实际 URL 按 `{v 前 2 位}/{v}_{s}.{ext}` 拼；
  - 地图视觉：`assets/resource/zipmap/{mapId}/{index}.jpg?ver=1.0.2` 瓦片（512×256，行列优先 1-based）；
  - 地图逻辑数据：`{js_gameVars.configUrl}{mapId}.bin`（含可走性、门 / 传送 / 内嵌对象等）。
- **本地提取产物**：由 `Tools/` 下的提取器产出（WebP 化贴图、按地图蒸馏的 Lib、AssetBundle 热更包），
  经本地 HTTP 资源服务提供给浏览器端按需异步加载。

工具一览：

| 工具 | 用途 |
| --- | --- |
| `Tools/原始传奇-资源提取/` | 页游资源逆向：配置读取、瓦片下载、大图拼接、地图数据拉取（见其 README） |
| `Tools/原始传奇-资源提取/Mir2ReverseWeb/` | 资源提取结果的 Web 预览服务 |
| `Tools/Mir2Tool/MirLib/` | Mir2 `.Lib` 素材库读写 |
| `Tools/Mir2Tool/LibraryExtract/` | 素材库批量提取 |
| `Tools/Mir2Tool/MapExtract/`、`MapExtract2/`、`MapExtractVerify/` | 地图 `.map` 提取与校验 |
| `Tools/Mir2Tool/TextureToWebP/` | 贴图转 WebP（减小 Web 传输体积） |
| `Tools/资源服务器/` | 本地静态资源 HTTP 服务（`:5080` / `:5081`） |
| `Tools/compare-wwwroot-size.mjs` | 发布产物体积对比 |

> 注：部分历史工具脚本 / 配置（如 `Tools/资源服务器/start-assets.bat`、`Tools/Mir2Tool/*.config.json`）
> 里的**素材源目录**仍指向旧的 Crystal Build 目录——那只是资源提取来源，不是行为标准；
> 随资源全面切到《原始传奇》CDN 后会一并替换。

---

## 六、开发准则

1. **先查参考源**：以《原始传奇》页游的实际表现与逆向出的资源规则为准，确认“本来怎么做”
   （字段名、调用点、事件签名、绘制时机）；
2. **判定差异类别**：属于哪一类 Web 适配——
   (a) 渲染分层烘焙 / (b) 浏览器输入桥 / (c) 资源按需异步加载 / (d) WASM 运行时
   （内存 / 无 `System.Drawing` / IME）——并列出会让原写法失效的点；
3. **最小忠实改动**：照参考结构搬，只在 Web 约束处适配；不重写、不“顺手优化”；
4. **编译验证**：`dotnet build` 客户端工程确认 0 错误；留意 CS0104 类型歧义
   （`MirEngine.Color` 与 `KFramework.MonoGame` 同名类型要用全限定）；
5. **检查副作用**：改输入查“左键 vs 右键 / 按住 vs 单击”；改渲染查“是否重复绘制 / 尺寸不更新”；
   改资源查“冷库首帧空白”；
6. **写注释说明偏离**：凡与页游写法不同处（不挂 Parent、显式 DisposeTexture、预热库、
   批次缓存、F12 被浏览器吃掉等）都加注释写清“为什么 Web 要这样”。

更多客户端细节（三层结构、UI 编写指南、世界层 / 相机原则、已知体验取舍）
见 [`WebGame.Mir2.MonoGame.Client/ReadMe.txt`](WebGame.Mir2.MonoGame.Client/ReadMe.txt)。

---

## 七、服务端配置：安全区发光边框（SafeZoneBorder）

安全区“四边发光”的效果**完全是服务端开关驱动**的，不是客户端自定义绘制。

### 工作原理

- 安全区“四边发光”由服务端 `SafeZoneBorder` 开关控制（`Settings.SafeZoneBorder == true`）。
- 该效果**与 `TrapHexagon` spell 无关**；边框的渲染由客户端既有逻辑负责（与服务端 / 桌面端 Crystal 表现对齐），
  本工程不在客户端自定义绘制。

### ⚠️ 配置位置陷阱（最容易踩的坑）

`SafeZoneBorder` 写在 `Setup.ini`，但工程里**有两份**必须同时改、保持同步：

| 文件 | 角色 |
| --- | --- |
| `Web_Mir2.Server/Configs/Setup.ini` | 源码模板（提交进仓库的“标准值”） |
| `Web_Mir2.Server.Version/Configs/Setup.ini` | **实际部署 / 运行**时服务端读取的配置副本 |

只改其中一份（尤其只改了源码模板、没改运行的 `Version` 那份）就会“明明设了 `True` 却不生效”。
两份都要设成：

```ini
SafeZoneBorder=True
```

改完需**重启服务端并重新加载地图**（`SafeZoneBorder` 相关逻辑在地图加载时执行，已加载进内存的地图不会即时补边框）。
