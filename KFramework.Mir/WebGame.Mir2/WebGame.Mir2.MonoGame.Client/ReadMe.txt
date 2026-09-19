WebGame.Mir2 (MonoGame.Client) — 基于 KFramework.MonoGame 重构的传奇客户端
====================================================================

一、项目定位
--------------------------------------------------------------------
本项目（WebGame.Mir2.MonoGame.Client）是经典网游《传奇》（Mir2 / 热血传奇）的
浏览器化客户端。它最重要的工程意义在于：

    >>> 用 KFramework.MonoGame 彻底重构了底层基础设施 <<<
    >>> 整个项目也可以理解为 KFramework.MonoGame 框架的一个商业案例 <<<

- 它证明了 KFramework.MonoGame（一个基于 MonoGame 的通用游戏基础设施框架）能够承接
  真实、体量庞大、逻辑复杂的商业级游戏客户端；
- 同时，它也在实践中反向驱动了 KFramework.MonoGame 的能力演进
  （内容异步加载、图集、键鼠输入/音频桥接、浏览器 WASM 运行时等）。

换言之：本项目是 KFramework.MonoGame 的“商业化落地样例（showcase）”，
而 KFramework.MonoGame 是本项目得以在浏览器中运行的底层底座。


二、为什么重构底层
--------------------------------------------------------------------
原版客户端位于同仓库的 Web_Mir2.Client/，底层强依赖：

  - System.Windows.Forms / System.Drawing  （窗体、GDI+ 绘图）
  - SlimDX                                （DirectX 图形）
  - NAudio                                （音频）
  - 自研的 Web_Mir2.Engine 浏览器化垫片

这些原生 / 半原生依赖在浏览器 WASM 场景下要么不可用、要么需要大量修补，
工程上难以维护，也不利于把“游戏引擎能力”沉淀成可复用的框架。

本项目把「图形 / 输入 / 音频 / 资源」四大底层基础设施【整体替换为 KFramework.MonoGame】：

  - 渲染、纹理、精灵、图集  →  KFramework.MonoGame 的
                              DXManager / ContentManager / AssetBundle / SpriteSheetLoader
  - 输入（键鼠）            →  KFramework.MonoGame 的 Input 层
                              （Input_KeyBoard / Input_Mouse）
  - 音频                    →  KFramework.MonoGame 统一音频后端
  - 资源异步加载            →  KFramework.MonoGame.Content
                              （用法详见 KFramework.WebGame/README.md）

游戏业务逻辑（Mir2/ 下的场景、控件、网络、地图、物品等）几乎原样保留，
仅通过一层“浏览器垫片（Shims）”继续以 MirEngine / SlimDX / System.Windows.Forms
等熟悉的命名空间编写 —— 业务代码无需改写即可运行在 KFramework.MonoGame 之上。


三、架构总览
--------------------------------------------------------------------
    Mir2 业务代码 ──(Shims 垫片)──┐
                                  ├─→ KFramework.MonoGame（图形/输入/音频/资源底层）
    浏览器桥接层 TSEngine ────────┘        ▲
            │                              │ ProjectReference
            ▼                              │
       wwwroot/jsengine (dist 产物)    WebGame.Mir2.MonoGame.Client

核心组件：

  - CMain.cs
        游戏主机（纯 C#，无任何 [JSExport]）。承载原 WinForms CMain 的窗体/输入语义
        （单例 CMain.Form、输入事件桥接、帧循环 Loop()）。Init() 由 MirGame.LoadContentAsync
        在 C# 内直接调用；Loop() 由 MirGame.Draw（经框架 Game.TickFrame）调用。
        原 Program 入口的 JS 导出入口 Init/Frame/Step 已全部移除——本项目不得向 JS 暴露任何入口。

  - MirGame.cs
        游戏引导（MonoGame 化的 Game 子类）；LoadContentAsync 中调用 CMain.Init()。

  - Mir2/Host/CMain.cs
        历史目录；引导/主机类已上移到项目根 CMain.cs（见上）。同样禁止 [JSExport]。

  - Shims/
        浏览器垫片，提供高仿的 System.Windows.Forms / System.Drawing / SlimDX /
        NAudio 类型，有意覆盖框架同名类型以去掉原生依赖
        （编译期 CS0436 警告已统一抑制，见 csproj）。

  - KFramework.TSEngine（tsengine）
        引擎的浏览器 JS 层（jsengine/）；由 npm run build 生成 dist/jsengine，
        再由 csproj 的 SyncJsEngine 目标整目录复制到 wwwroot/jsengine。


四、目录结构（节选）
--------------------------------------------------------------------
WebGame.Mir2.MonoGame.Client/
├── CMain.cs                    # 游戏主机 + WASM 入口（Main）
├── MirGame.cs                  # 游戏引导（MonoGame 化 Game 子类）
├── WebGame.Mir2.MonoGame.Client.csproj
├── Mir2/
│   ├── Host/                   # （历史目录；引导/主机类已上移到项目根 CMain.cs / MirGame.cs）
│   ├── MirScenes/              # 登录 / 选角 / 游戏场景
│   ├── MirControls/            # UI 控件
│   ├── MirGraphics/            # DXManager、MLibrary、纹理库
│   ├── MirNetwork/             # Network（引用 CMain.Form）
│   ├── MirSounds/              # 音频
│   └── ...                     # 地图 / 物品 / 技能等业务逻辑
├── Shared/                     # 与原版共享的纯逻辑
├── Shims/                      # 浏览器垫片（Forms / Drawing / SlimDX / NAudio 高仿）
└── wwwroot/
    └── jsengine/               # KFramework.TSEngine 编译产物


五、构建与运行
--------------------------------------------------------------------
前置（依据 csproj 注释与仓库约定）：

  1. 先构建引擎 JS 层：
       cd <repo>/KFramework.WebGame/KFramework.TSEngine
       npm install && npm run build
     生成 dist/jsengine 后，csproj 的 SyncJsEngine 目标会自动复制到 wwwroot/jsengine。

  2. 资源基址默认 http://127.0.0.1:5080/
     （见 CMain.Init 中的 BrowserResource.Configure，指向本地 HTTP 资源服务，
      提供客户端贴图库 / 地图库等）。

  3. 以 .NET WASM（Microsoft.NET.Sdk.WebAssembly，net10.0）发布 / 调试。


六、与原版（Web_Mir2.Client）的关系
--------------------------------------------------------------------
- 本工程从 Web_Mir2.Client 迁移而来，业务代码（Mir2/ + Shared/）保持一致；
- 引擎层 Web_Mir2.Engine 以及 SlimDX / NAudio 等原生依赖【不再引用】，
  全部由本工程 Shims/ 自提供；
- 底层图形 / 输入 / 音频 / 资源【统一走 KFramework.MonoGame】。

七、运行约束：彻底不用 Web_Mir2.Engine，且本项目禁止 [JSExport]
--------------------------------------------------------------------
1) 引擎层：本项目【彻底抛弃 Web_Mir2.Engine】，浏览器运行时（渲染 / 输入 / 音频 /
   资源 / 网络 / 帧循环）【完全由 KFramework.MonoGame 提供】。wwwroot/jsengine/ 是
   KFramework.TSEngine（KFramework.MonoGame 的 JS 引擎层）的编译产物，并非 Web_Mir2.Engine。
   原 Web_Mir2.Engine/tsengine 的启动器与本工程无关，不要在此引用或混用。

2) 禁止 [JSExport]：WebGame.Mir2.MonoGame.Client 内【不允许出现任何 [JSExport]】。
   所有 JS 互操作基础设施都由 KFramework.MonoGame 以 JSBind_* 形式提供
   （如 JSBind_GameHost、JSBind_Net_WebSocket 等）。业务工程只写纯 C#，
   通过框架注入的 GraphicsDevice / SpriteBatch / Input / Audio 等能力工作，
   不得自行向 JS 暴露函数入口。

3) 帧驱动路径（单链路，必须经由框架上屏）：
       index.html → ./jsengine/main.js（KFramework.TSEngine 启动器）
         → 每帧 requestAnimationFrame 回调 JSBind_GameHost.Frame
         → Game.TickFrame（KFramework.MonoGame 框架）
         → MirGame.Update / MirGame.Draw
         → CMain.Loop()（清屏 + 场景 Process/Draw + 纹理回收）
         → 框架在本帧末执行上屏（present）。
   注意：引擎启动器会在多个程序集里查找帧宿主，优先按命名空间
   KFramework.MonoGame.JSBind_GameHost 精确匹配；游戏程序集内的普通类型
   （如 CMain）绝不能因为带了 [JSExport] 的 Frame 而被“递归兜底”误判为帧宿主，
   否则帧回调会绕过 Game.TickFrame / 上屏，表现为“闪几下就黑屏”。
   因此 CMain 不得导出任何 Frame / Step 之类入口（已移除）。

一句话总结：
  原版用 WinForms/SlimDX/NAudio 把传奇搬到浏览器，本项目则用 KFramework.MonoGame
  把同一套传奇业务“重做底座”，并以此作为 KFramework.MonoGame 的商业化验证案例。
