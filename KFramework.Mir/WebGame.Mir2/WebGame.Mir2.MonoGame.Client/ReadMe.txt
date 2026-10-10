====================================================================
WebGame.Mir2 (MonoGame.Client) —— 基于 KFramework.MonoGame 重构的传奇客户端
            （内含 UI 编写指南：原 UI_ReadMe.txt 已合并至第八节）
====================================================================

★ 原版在哪里（务必先看这里）
--------------------------------------------------------------------
原版传奇客户端源码在本地：

    D:\OpenSource\Crystal        （客户端在 D:\OpenSource\Crystal\Client）

它是一个 WinForms + SlimDX/Direct3D9 的桌面客户端，是【本项目一切行为的
标准答案】。凡涉及“某功能本来该怎么表现 / 某结构该怎么组织 / 某资源怎么
解析”的问题，一律【先到 D:\OpenSource\Crystal 里 grep / 读源码】，确认原版
怎么做，再考虑 Web（渲染分层 / 输入桥 / 资源懒加载 / WASM 运行时）这一层
需要怎样最小适配。

心法：不要凭记忆重写功能——先对照原版，照着它的结构搬，只在 Web 特有约束
处做最小改动，并写注释说明为何偏离。


一、项目定位
--------------------------------------------------------------------
本项目（WebGame.Mir2.MonoGame.Client）是经典网游《传奇》（Mir2 / 热血传奇）的
浏览器化客户端。它最重要的工程意义在于：

    >>> 用 KFramework.MonoGame 彻底重构了底层基础设施 <<<
    >>> 整个项目也可以理解为 KFramework.MonoGame 框架的一个商业案例 <<<

- 它证明了 KFramework.MonoGame（一个基于 MonoGame 的通用游戏基础设施框架）能够承接
  真实、体量庞大、逻辑复杂的商业级游戏客户端；
- 同时，它也在实践中反向驱动了 KFramework.MonoGame 的能力演进
  （内容异步加载、图集、键鼠输入 / 音频桥接、浏览器 WASM 运行时等）。

换言之：本项目是 KFramework.MonoGame 的“商业化落地样例（showcase）”，
而 KFramework.MonoGame 是本项目得以在浏览器中运行的底层底座。


二、为什么重构底层
--------------------------------------------------------------------
原版客户端位于 D:\OpenSource\Crystal\Client，底层强依赖：
  - System.Windows.Forms / System.Drawing  （窗体、GDI+ 绘图）
  - SlimDX（Direct3D9）/ NAudio            （原生 / 半原生依赖）

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
                                  ├─→ KFramework.MonoGame（图形 / 输入 / 音频 / 资源底层）
    浏览器桥接层 TSEngine ────────┘        ▲
            │                              │ ProjectReference
            ▼                              │
       wwwroot/jsengine (dist 产物)    WebGame.Mir2.MonoGame.Client

核心组件：

  - CMain.cs
        游戏主机（纯 C#，无任何 [JSExport]）。承载原 WinForms CMain 的窗体 / 输入语义
        （单例 CMain.Form、输入事件桥接、帧循环 Loop()）。Init() 由 MirGame.LoadContentAsync
        在 C# 内直接调用；Loop() 由 MirGame.Draw（经框架 Game.TickFrame）调用。
        原 Program 入口的 JS 导出入口 Init/Frame/Step 已全部移除——本项目不得向 JS 暴露任何入口。

  - MirGame.cs
        游戏引导（MonoGame 化的 Game 子类）；LoadContentAsync 中调用 CMain.Init()。

  - Mir2/Host/CMain.cs
        历史目录；引导 / 主机类已上移到项目根 CMain.cs（见上）。同样禁止 [JSExport]。

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


六、与参考源的关系（以原版传奇 Crystal 为准则）
--------------------------------------------------------------------
- 原版传奇客户端：D:\OpenSource\Crystal（标准答案，任何时候都先对照它）；
- 本工程业务代码（Mir2/ + Shared/）与各参考源同源，保持一致；
- 原生依赖（SlimDX / NAudio / System.Windows.Forms / System.Drawing）【不再引用】，
  全部由本工程 Shims/ 自提供；
- 底层图形 / 输入 / 音频 / 资源【统一走 KFramework.MonoGame】。


七、运行约束：底层统一走 KFramework.MonoGame，且本项目禁止 [JSExport] 和 [JSImport]
--------------------------------------------------------------------
1) 引擎层：本项目浏览器运行时（渲染 / 输入 / 音频 / 资源 / 网络 / 帧循环）
   【完全由 KFramework.MonoGame 提供】。wwwroot/jsengine/ 是 KFramework.TSEngine
   （KFramework.MonoGame 的 JS 引擎层）的编译产物。

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


====================================================================
八、UI 编写指南（合并自原 UI_ReadMe.txt）
====================================================================
适用代码：WebGame.Mir2.MonoGame.Client
最后核对：2026-09-21（基于当前 2026New 分层渲染 + 恒等变换）

--------------------------------------------------------------------------------
8.0 一句话结论
--------------------------------------------------------------------------------
UI 是「场景(MirScene) → 两个层(WorldLayerControl / UILayerControl) → 控件树」
的三层结构。所有界面控件挂在 UILayer 下，按画布原生分辨率 1:1 布局，窗口
尺寸变化由「锚点重排」负责，不做任何 x/y 拉伸缩放。

--------------------------------------------------------------------------------
8.1 三层结构
--------------------------------------------------------------------------------
- MirScene（场景基类）：只有两个直接子 —— WorldLayerControl、UILayerControl。
    （MirScene.cs:21、38 处 UILayer/WorldLayer 字段初始化并 Parent=this）
- 世界层 WorldLayerControl：地图、角色、物品等【世界坐标】内容。
    恒等变换（1:1），世界像素不缩放，窗口变大只是"看到更多世界"。
- UI 层 UILayerControl：对话框 / HUD / 按钮等界面控件。
    恒等变换（1:1），UI 以画布像素布局，1:1 上屏。
    （两个层都覆写 GetLayerTransform 返回 CreateScaleTranslation(1,1,0,0)，
     见 UILayerControl.cs:21-24、WorldLayerControl.cs:10-11）

注意：基类 MirControl.GetLayerTransform 原本按高度缩放（h/768），但当前两个
层都已覆写为恒等，所以「逻辑坐标 == 画布像素」成立。

--------------------------------------------------------------------------------
8.2 怎么挂一个控件（最关键的一步）
--------------------------------------------------------------------------------
(1) 在 GameScene 里：
        new XxxDialog { Parent = this };          // 走 MirScene.AddControl 路由
    MirScene.AddControl 会把控件路由进 UILayer（UI）/ WorldLayer（地图）。
    ★ 现在路由用的是 Insert 而非 Add：Insert 会同步 control._parent = UILayer，
      否则置顶/排序逻辑会把它写回场景根导致「画不出、点不动」。
      （MirScene.cs:48-56，AddControl 内 UILayer.Insert(末尾)）

(2) 在 LoginScene / SelectScene 里：
        Parent = this.UILayer;                    // 直接挂 UI 层
    （LoginScene.cs:48、SelectScene.cs:38/47 已是这种写法）

(3) 千万不要：
    - GameScene.Controls.Add(ctrl)        // 绕过路由，控件落在场景根、永不被烘焙
    - 直接操作 Parent.Controls（Remove/Add）// TrySort / OnVisibleChanged /
      BringToFront 内部就是这么写的，一旦 Parent 与真实容器不一致就会把控件
      踢回场景根（这一坑已通过 (1) 的 Insert 修掉）

--------------------------------------------------------------------------------
8.3 常用控件
--------------------------------------------------------------------------------
- MirImageControl：图片控件（Library + Index 画图）。属性：
    Library / Index / AutoSize / DrawImage / UseOffSet / ForeColour / Opacity /
    Blending / GrayScale
- 交互控件：MirButton、MirLabel、MirTextBox、MirItemCell、MirComboBox、
    MirScrollingBar、MirCheckBox、MirImageBox 等。
- 通用属性（MirControl）：Location、Size、Visible、Enabled、Movable、Sort、
    Modal、NotControl、DrawControlTexture、BackColour、Border、Opacity。
- 事件：Click、MouseEnter、MouseLeave、MouseDown、MouseUp、BeforeDraw、
    SizeChanged、VisibleChanged。

--------------------------------------------------------------------------------
8.4 绘制原理（为什么画图片"不用 Size"）
--------------------------------------------------------------------------------
每帧链路：
    CMain.Loop → ActiveScene.Draw → MirScene.DrawControl
      → WorldLayer.Bake()  +  UILayer.Bake()          (MirScene.cs:102-103)
      → 两层 RT 各 PresentToScreen 上屏（先世界后 UI 叠加）

层 Bake（LayerControl.CreateTexture）：
    为层创建 Size = DXManager.FullScreenSize 的 RT（不是 Size！），
    再 DrawChildControls 递归画子控件。

子控件绘制：
    Ctrl.Draw → DrawControl：
      a) base.DrawControl：若 DrawControlTexture=true，按 Size 建自身
         ControlTexture，再 DrawOpaque(ControlTexture, rect(0,0,Size), DisplayLocation)
         贴回父层 RT —— 这是"底"。
      b) MirImageControl 额外调用：
         Library.Draw(Index, DisplayLocation, ForeColour, ...)   (MirImageControl.cs:177-189)
         ★ 这个调用只用「图索引 + 位置」，根本不传 Size；
           图片尺寸 = Library.GetTrueSize(Index)（资源本身），不按 Size 缩放/裁剪。

结论：
    Size 只管「控件这个框多大 / 命中矩形多大 / 底图 RT 多大」；
    图片本体永远跟随资源，不跟随 Size。

超屏剔除守卫：
    MirControl.Draw：Size.Width > Settings.ScreenWidth || Size.Height > Settings.ScreenHeight
    时直接 return 不画（MirControl.cs:773-776）。全屏层必须绕开 Size，用 FullScreenSize。

--------------------------------------------------------------------------------
8.5 尺寸与坐标（重点坑）
--------------------------------------------------------------------------------
三套口径：
    - Size          ：控件自身逻辑尺寸（资源图/代码常量），用于框、命中、底图 RT
    - FullScreenSize：画布后备缓冲真实像素（如 1461x799），所有层 RT 按它建
    - GetLayerTransform：逻辑坐标→RT 像素的映射（当前 UI/世界层都是恒等 1:1）

坐标系要点：
    - DisplayLocation = Parent.DisplayLocation + Location，一路累加到场景根
      （MirControl.cs:13）。这是逻辑坐标。
    - 当前恒等变换下逻辑坐标 == 画布像素，UI 按真实像素布局。
    - Settings.ScreenWidth = DXManager.GDevice.Viewport.Width。浏览器宿主下
      Viewport 可能读到 0 或逻辑尺寸，DXManager.Initialize 已强行校正成
      BackBuffer 尺寸（DXManager.cs:83-96），但仍不要在构造期依赖它做硬编码定位。
    - 命中测试 IsMouseOver 用 DisplayRectangle = (DisplayLocation, Size)，
      鼠标已在输入入口转过一次（KCamera.ScreenToWorldPos 只减视口原点、不逆缩放，
      因为层是恒等变换）。渲染口径与命中口径必须一致，否则「画得出点不动」。

常见副作用：手动把 Size 设得比图小 → 图溢出框照画（Library.Draw 不裁剪到 Size），
看得见但点击区只有框那么大。

--------------------------------------------------------------------------------
8.6 自适应布局（窗口变化）
--------------------------------------------------------------------------------
- 锚点机制：Anchor + AnchorPos（MirControl.ApplyAnchor）。
    基准取自活的 Settings.ScreenWidth/Height，窗口变化后重排即得新位置。
    九宫格表达不了的布局可覆写 ApplyAnchor（如"右边距固定 170px"）。
- 窗口大小变化回调：
    MirGame.Window.SizeChanged → CMain.OnWindowSizeChanged
      → 1) 释放地板/光照离屏纹理（按新尺寸重建）
         2) RelayoutAll：按锚点重排 UI 顶层控件，子控件随父移动
         3) Refresh：令当前场景重新烘焙         (CMain.cs:180-186)
- 不要硬编码右下角坐标，改用 Anchor.Right / Anchor.Bottom / Anchor.Center。

--------------------------------------------------------------------------------
8.7 常见坑清单
--------------------------------------------------------------------------------
[1] 控件消失 / 点不动
     → 多半掉出 UILayer（被踢回场景根）。挂控件用 Parent=this（GameScene）/
       Parent=this.UILayer（登录/选人），别直接 Controls.Add。
[2] UI 只铺左上角 + 四周洋红底
     → 层 RT 尺寸用了 Size 而非 FullScreenSize；上屏是 1:1，RT 必须按画布建。
[3] 图溢出框（画得出、点不准）
     → 手动 Size 小于图；设 AutoSize=true 让框等于图，或别改 Size。
[4] 画得出点不动
     → 渲染与命中坐标口径不一致。检查 DisplayLocation / IsMouseOver 用的是否
       都是逻辑坐标，鼠标入口是否已转过一次。
[5] UI 变形（圆变椭圆、字体压扁）
     → 层变换被改成非等比缩放；当前两层都是恒等，改回 1:1。
[6] 整层不画
     → Size > Settings.ScreenWidth 守卫触发；全屏层必须绕开 Size、用 FullScreenSize。
[7] 浏览器下对话框跑到负坐标
     → 构造期依赖 Settings.ScreenWidth 读到 0；DXManager.Initialize 已校正，
       但定位优先用锚点而非构造期硬编码。
[8] 登录按钮「看得见点不动」，MouseControl 停在 LoginScene（场景根）
     → 根因在背景图 _background 的命中矩形没随窗口变大而变大。
       _background 是 MirImageControl，Anchor = MiddleCenter；
       ApplyAnchor 的 MiddleCenter 只设 Location = 屏幕中心、不改 Size
       （见 MirControl.cs:335-355）。它的 Size 停在构造时图片固定尺寸
       （ChrSel 首图 1024x768），不随 resize 变大。
       - 窗口 ≤ 1024x768 时：_background 居中后整块盖住窗口，命中链
         Scene→UILayer→_background→LoginDialog→按钮 全程通过 → 按钮可点；
       - 最大化（>1024x768）时：_background 只占屏幕中间一块，LoginDialog/
         按钮按屏幕中心布局探出它的命中矩形（如按钮在 x≈1489 处），
         OnMouseMove 子控件循环里 _background.IsMouseOver=false 直接跳过
         整棵子树 → MouseControl 停在 LoginScene → 点登录无反应。
       修复：让背景（或承载 UI 的容器）在 resize 时铺满全屏——把 _background.Size
       设为 Settings.ScreenWidth×Settings.ScreenHeight（或在 ApplyAnchors 里
       随窗口重设），命中即可穿透到按钮。注意 MirImageControl 绘制走 Library.Draw，
       不按 Size 缩放，所以把 Size 设大不会拉伸背景图，只是扩大命中/底图矩形。
       排障线索：浏览器控制台里 [Mir][Down] MC=LoginScene、MP 命中按钮位置、
       且只有 Down 没有 Click，基本就是命中链断在背景层。

--------------------------------------------------------------------------------
8.8 最小示例
--------------------------------------------------------------------------------
// GameScene 里创建对话框（自动路由进 UILayer）
CharacterDialog = new CharacterDialog(MirGridType.Equipment, User)
{
    Parent  = this,                          // 路由进 UILayer（Insert 同步 _parent）
    Index   = 504,
    Library = Libraries.Title,
    Location = new Point(Settings.ScreenWidth - 264, 0),
    Movable = true,
    Sort    = true,                          // 置顶靠 Sort，不要手动 Controls.Add
};

// 对话框内加一个子按钮（Parent 指向对话框，不是场景）
CloseButton = new MirButton
{
    Parent    = CharacterDialog,             // 子控件挂到对话框
    Index     = 120,
    Library   = Libraries.Title,
    Location  = new Point(CharacterDialog.Size.Width - 24, 4),
    Hint      = "关闭",
    ClickAction = (_) => CharacterDialog.Hide(),
};

// 自适应：想让它贴右边，用锚点而不是改 Location
CloseButton.Anchor    = MirAnchor.Top | MirAnchor.Right;
CloseButton.AnchorPos = new Point(4, 4);

--------------------------------------------------------------------------------
8.9 排查口诀
--------------------------------------------------------------------------------
- 谁决定最终像素：FullScreenSize（RT 尺寸）+ GetLayerTransform（映射）+ PresentToScreen（1:1 铺）
- Size 只决定：自身纹理大小、命中矩形、底图 RT
- 看到「只占一角 / 突然不画 / 能画不能点」先查三件事：
    RT 是不是按 FullScreenSize 建的？
    是不是撞上 Size > Settings.ScreenWidth 的守卫？
    命中坐标是不是多转/少转了一次？


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


十一、与原版的对照工作法（本项目通用准则）
--------------------------------------------------------------------
1. 先读原版：grep / 读 D:\OpenSource\Crystal 对应文件，确认原版“本来怎么做”
   （字段名、调用点、事件签名、绘制时机）。
2. 判定差异类别：属于哪一类 Web 适配——(a) 渲染分层烘焙 / (b) 浏览器输入桥 /
   (c) 资源按需异步加载 / (d) WASM 运行时（内存 / 无 System.Drawing / IME）——并列出
   会让原版写法失效的点。
3. 最小忠实改动：照原版结构搬，只在 Web 约束处适配；不重写、不“顺手优化”原版逻辑。
4. 编译验证：dotnet build 客户端工程确认 0 错误；留意 CS0104 类型歧义
   （MirEngine.Color vs KFramework.MonoGame 同名类型要用全限定）。
5. 检查副作用：改输入查“左键 vs 右键 / 按住 vs 单击”；改渲染查“是否重复绘制 /
   尺寸不更新”；改资源查“冷库首帧空白”。
6. 写注释说明偏离：凡与原版不同处（不挂 Parent、显式 DisposeTexture、预热库、
   批次缓存、F12 被浏览器吃掉等）都加注释写清“为什么 Web 要这样”，方便后人对照。
