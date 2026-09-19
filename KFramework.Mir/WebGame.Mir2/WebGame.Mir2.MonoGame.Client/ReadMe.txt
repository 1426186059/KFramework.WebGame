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


八、可参考的同源 / 兄弟工程（定位与取舍参考）
--------------------------------------------------------------------
本项目在重构与排障时，可对照以下三处源码：

  1. D:\OpenSource\Mir2_Unity_2027\Mir2_Unity_2027_1
     Unity 重制版。涉及“分辨率 / 全屏缩放 / 相机正交尺寸”等处理方式时优先参考：
       - Assets\Client\Settings.cs  （Resolution = 1024，按所选分辨率原生渲染，
         而非把固定缓冲放大铺满窗口）
       - Assets\Client\Resolution\DisplayResolutions.cs / eSupportedResolution.cs
         （多分辨率支持枚举与探测）
     其思路：游戏按“原生分辨率”渲染、UI 随分辨率自适应，从根上避免“低分辨率缓冲放大
     导致的模糊”。本项目若要做到真正清晰的全屏，应借鉴此思路（见第九节）。

  2. D:\OpenSource\Crystal\Client
     Crystal 客户端（同源美术/资源）。涉及贴图库、地图库、音效索引等资源结构与命名
     （如音效索引 index → 文件名的 `index/10 - index%10` 规则）时参考。

  3. D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Web_Mir2.Client
     原版 Web 客户端（WinForms/SlimDX 底座）。业务逻辑与本工程同源，涉及场景/控件/
     网络/地图等具体实现细节时直接对照，是最贴近本工程的参考源。


九、已知体验取舍：全屏拉伸 vs 清晰度
--------------------------------------------------------------------
当前为“固定逻辑分辨率(1024x768)烘焙 → 拉伸铺满画布(Viewport)”实现全屏。
代价：把 1024 时代素材放大到更大画布，必然出现像素化/柔化（无法凭空变清晰）。
若后续要求“全屏且清晰”，正确做法是借鉴 Mir2_Unity 的思路——按画布原生分辨率渲染
（ControlTexture 取画布尺寸 + 全局缩放变换），而非放大固定缓冲。该改动较大，
需评估后再实施。


十、世界层/相机原则：等比铺满会放大世界坐标（重要）
--------------------------------------------------------------------
世界层(WorldLayer)承载的是【世界坐标】内容（地图/角色/物品，瓦片固定 48x32 世界单位）。
其投影变换 MirScene.WorldLayer.LayerTransform 严禁用 aspect-fill（等比铺满 / Math.Max）去
“铺满”窗口——那会把整个世界放大（1280 窗口下放大 1.25×），既违背世界坐标的语义，
也会与鼠标→世界命中（以 48x32 世界像素计、与屏幕 1:1）错位。

正确做法（参照本仓库兄弟工程 Mir2_Unity_2027 的重制版：
Assets/KFramework/Rumtime/Tools/SafeAreaFit.cs 的“改相机渲染区域(rect)而非缩放”，
以及 GameTools.cs 的 Camera.ScreenToWorldPoint/WorldToScreenPoint 同一投影命中）：

  - 世界以【固定比例】映射到屏幕（Unity 正交相机 orthographicSize，不缩放世界内容本身）；
  - 窗口更大时“渲染更大的世界区域 / 扩展相机视口”以显示更多世界，而非放大世界；
  - 渲染与命中共用同一投影（KCamera 的 Window↔World 换算）。

当前实现（MapControl 按全屏 / 窗口原生分辨率渲染）：
  - WorldLayer = 1:1 单位变换（见 MirScene.WorldLayer.LayerTransform），不缩放、不 aspect-fill；
  - MapControl 按【全屏/窗口原生分辨率】烘焙世界，地板 1:1 铺满、无黑边、无放大；
  - 窗口更大只是“看到更多世界”（以 48x32 世界像素为单位的视口更大），而非放大世界坐标；
  - 命中：MapControl 内鼠标即世界像素（与屏幕 1:1），与 OffSetX/Y、ViewRangeX/Y 一致；
    KCamera.ScreenToWorldPos 的 s=h/768 只服务于【UI 层逻辑坐标】，与世界层无关。
  - MapControl.Size 保持 1024x768（受 MirControl.Draw 的 Size>ScreenWidth 裁剪守卫约束），
    只把 ControlTexture 设为窗口原生分辨率，由 DrawControl 1:1 合成，避免触发该守卫。
  - 关键结论：等比铺满(scale-fill)会放大世界坐标，而世界坐标不应被放大——这是错的。

