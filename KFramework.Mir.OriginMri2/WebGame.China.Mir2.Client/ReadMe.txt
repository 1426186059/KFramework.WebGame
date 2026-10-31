====================================================================
WebGame.China.Mir2.Client —— 基于 KFramework.MonoGame 重构的传奇客户端
            （内含 UI 编写指南：原 UI_ReadMe.txt 已合并至第八节）
====================================================================

★ 参考对象（务必先看这里）
--------------------------------------------------------------------
本项目一切行为的标准答案是：

    Web 页游《原始传奇》—— Egret 引擎，构建 fugu_20201203，
    主包 main10.25341.1.min.js

它是一个跑在浏览器里的传奇页游。凡涉及“某功能本来该怎么表现 / 某资源怎么
组织 / 地图怎么切分 / 数据怎么解析”的问题，一律【先查《原始传奇》页游的实际
表现与逆向记录】：

    Tools\原始传奇-资源提取\README.md              逆向出的资源 / URL / 地图规则
    Tools\原始传奇-资源提取\原始传奇-资源参考\      页游原始 JS 与配置存档
    Tools\原始传奇-资源提取\Mir2ReverseWeb\         提取结果的 Web 预览

然后再考虑 Web（渲染分层 / 输入桥 / 资源懒加载 / WASM 运行时）这一层
需要怎样最小适配。

心法：不要凭记忆重写功能——先对照页游，照着它的结构搬，只在 Web 特有约束
处做最小改动，并写注释说明为何偏离。

⚠ 本工程【不再参考 D:\OpenSource\Crystal】。那个 WinForms + SlimDX/Direct3D9
桌面客户端只是本工程的历史来源，不再作为标准答案；遇到“原版怎么做”的问题
时不要再去那里 grep，一律以《原始传奇》页游为准。


一、项目定位
--------------------------------------------------------------------
本项目（WebGame.China.Mir2.Client）是经典网游《传奇》（Mir2 / 热血传奇）的
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
传奇客户端的传统桌面实现，底层强依赖：
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
       wwwroot/jsengine (dist 产物)    WebGame.China.Mir2.Client

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
        引擎的浏览器 JS 层（jsengine/）；由 npm run build / build:release 生成
        dist/<Debug|Release>/jsengine，再由 csproj 的 SyncJsEngine 目标按配置整目录复制到
        wwwroot/jsengine（Debug 未压、Release 已压缩并剥离 console.*）。


四、目录结构（节选）
--------------------------------------------------------------------
WebGame.China.Mir2.Client/
├── CMain.cs                    # 游戏主机 + WASM 入口（Main）
├── MirGame.cs                  # 游戏引导（MonoGame 化 Game 子类）
├── WebGame.China.Mir2.Client.csproj
├── Mir2/
│   ├── MirControls/            # MirControl 基类、MirScene 场景基类、各类 UI 控件
│   ├── MirScenes/              # 登录 / 选角 / 游戏场景（GameScene、MapControl）
│   ├── MirGraphics/            # DXManager、MLibrary、纹理库
│   ├── MirNetwork/             # Network（引用 CMain.Form）
│   ├── MirSounds/              # 音频
│   └── ...                     # 地图 / 物品 / 技能等业务逻辑
├── 2026New/                    # 《原始传奇》原版资源加载层（地图 dat / 图集 / 配置分片）
│   ├── Mir2OriginResource.cs   # CDN 资源拉取（合并全图碰撞 dat 等）
│   ├── Mir2MapSource.cs        # 地图加载：从原版 .dat 解码碰撞 / 遮挡 / 安全区
│   └── Mir2MapDecoder.cs       # 原版 .dat 解码（ExtractMapBlock / Decode），与服务端一致
├── Shared/                     # 与原版共享的纯逻辑
├── Shims/                      # 浏览器垫片（Forms / Drawing / SlimDX / NAudio 高仿）
└── wwwroot/
    └── jsengine/               # KFramework.TSEngine 编译产物


五、构建与运行
--------------------------------------------------------------------
前置（依据 csproj 注释与仓库约定）：

  1. 引擎 JS 层由 csproj 的 BuildTypeScript 自动构建（Debug 走 npm run build、
     Release 走 npm run build:release —— 后者压缩并剥离 console.*），产物落在
     dist/<Debug|Release>/jsengine；只需先在该目录做过一次 npm install。
     手工构建时同样：
       cd <repo>/KFramework.WebGame/KFramework.TSEngine
       npm install && npm run build          # 或 npm run build:release
     SyncJsEngine 目标随后按当前配置把对应目录复制到 wwwroot/jsengine。

  2. 资源基址默认 http://127.0.0.1:5080/
     （见 CMain.Init 中的 BrowserResource.Configure，指向本地 HTTP 资源服务，
      提供客户端贴图库 / 地图库等）。

  3. 以 .NET WASM（Microsoft.NET.Sdk.WebAssembly，net10.0）发布 / 调试。


六、与参考源的关系（以 Web 页游《原始传奇》为准则）
--------------------------------------------------------------------
- 参考对象：Web 页游《原始传奇》（Egret 引擎，构建 fugu_20201203），任何时候都先对照它；
  逆向记录见 Tools\原始传奇-资源提取\README.md；
- 本工程业务代码（Mir2/ + Shared/）与传奇业务保持一致；
- 【不再参考】D:\OpenSource\Crystal（历史来源，非标准答案）；
- 原生依赖（SlimDX / NAudio / System.Windows.Forms / System.Drawing）【不再引用】，
  全部由本工程 Shims/ 自提供；
- 底层图形 / 输入 / 音频 / 资源【统一走 KFramework.MonoGame】。
- 地图资源已切换为《原始传奇》原版 .dat：经 2026New/Mir2MapSource + Mir2MapDecoder
  解码（CDN 合并全图碰撞数据 map10.25487.1.dat），与服务端 OriginalMapDecoder
  同一套算法（先跳 8 字节条目头、再跳 4 字节块头、高 nibble 在前的 4-bit 格标志），
  不再使用旧版内置地图格式。


七、运行约束：底层统一走 KFramework.MonoGame，且本项目禁止 [JSExport] 和 [JSImport]
--------------------------------------------------------------------
1) 引擎层：本项目浏览器运行时（渲染 / 输入 / 音频 / 资源 / 网络 / 帧循环）
   【完全由 KFramework.MonoGame 提供】。wwwroot/jsengine/ 是 KFramework.TSEngine
   （KFramework.MonoGame 的 JS 引擎层）的编译产物。

2) 禁止 [JSExport]：WebGame.China.Mir2.Client 内【不允许出现任何 [JSExport]】。
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
适用代码：WebGame.China.Mir2.Client
最后核对：2026-10-31（基于当前「无分层」结构：场景本身即 UI 容器，地图由 GameScene
下的 MapControl 子控件直接绘制；MapControl 按全屏 / 窗口原生分辨率 1:1 渲染）

--------------------------------------------------------------------------------
8.0 一句话结论
--------------------------------------------------------------------------------
UI 是「场景(MirScene) → 控件树」的两层结构：MirScene 自身即是 UI 容器，
地图由挂在场景下的 MapControl 子控件直接绘制（世界坐标）。所有界面控件直接
挂到场景或其子容器，按画布原生分辨率 1:1 布局，窗口尺寸变化由「锚点重排」
负责，不做任何 x/y 拉伸缩放。

注意：本项目【已移除】原 WorldLayerControl / UILayerControl 两个分层容器控件。
取而代之的是渲染合成层面的「地图 RT + UI RT」双缓冲：地图先由 MapControl 画到
它的离屏纹理，UI 再由场景(MirScene)本身画到另一张离屏纹理，两张 RT 靠
「透明洋红键(alpha=0)」合成上屏（UI 区域外的洋红区透出地图）—— 拓扑等价于
原版 Crystal 的「地图 RT + UI RT、洋红=透明键」，但控件树只有一层，没有容器层。

--------------------------------------------------------------------------------
8.1 当前结构（无分层容器）
--------------------------------------------------------------------------------
- MirScene（场景基类，位于 Mir2/MirControls/MirScene.cs）：本身即 UI 容器。
    其 ControlTexture 就是 UI 离屏纹理（底色透明洋红 alpha=0），只装 UI、
    不装地图；窗口/画布尺寸变化时按锚点重排整棵控件树。
- MapControl（位于 Mir2/MirScenes/MapControl.cs）：挂在 GameScene 下的子控件，
    负责绘制【世界坐标】内容（地图/地板/角色/物品，瓦片固定 48x32 世界单位）。
    它在 GameScene.DrawControl 中先于场景 UI 绘制，UI RT 叠加其上、靠洋红键透出地图。
- 控件树只有一层：UI 对话框、HUD、按钮等全部直接挂在场景（或其子容器控件）之下，
    不再有 WorldLayer / UILayer 路由。逻辑坐标 == 画布像素（1:1 上屏）。

--------------------------------------------------------------------------------
8.2 怎么挂一个控件（最关键的一步）
--------------------------------------------------------------------------------
控件通过 Parent 直接挂到目标控件（场景或其子容器）的 Controls 列表，不再经过
UILayer / WorldLayer 路由（这两个容器控件已移除）。设置 Parent 会触发
MirControl.AddControl → Controls.Add，并同步 _parent。

(1) 在任意场景里：
        new XxxDialog { Parent = this };          // 直接挂到场景（UI 容器）
    GameScene 下的 MapControl 也是用同样方式作为子控件挂上的；UI 对话框与地图
    同处一棵控件树，靠渲染合成（洋红键）分层显示。

(2) 在 LoginScene / SelectScene 里，写法完全一致：
        Parent = this;                            // 直接挂到场景

(3) 千万不要：
    - Scene.Controls.Add(ctrl)                    // 绕过 Parent 直接 Add，_parent
      不会被设置，命中链/绘制链断裂 → 画不出、点不动
    - 直接操作 Parent.Controls（Remove/Add）      // TrySort / OnVisibleChanged /
      BringToFront 内部就是这么写的，一旦 Parent 与真实容器不一致就会把控件
      踢回场景根

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
    CMain.Loop → ActiveScene.Draw → MirScene.Draw
      → GameScene 先 MapControl.DrawControl（地图 RT：世界坐标内容）
      → 再 base.DrawControl（场景 UI RT：递归 DrawChildControls 画 UI 控件）
      → 两张 RT 靠「透明洋红键(alpha=0)」合成上屏（先地图后 UI 叠加；
        UI 区域外的洋红区透出地图）

场景/控件 RT（MirScene.CreateTexture）：
    为场景创建 Size = (Settings.ScreenWidth, Settings.ScreenHeight) 的 UI RT，
    再 DrawChildControls 递归画子控件（不含地图，地图由 MapControl 单独画）。

子控件绘制：
    Ctrl.Draw → DrawControl：
      a) base.DrawControl：若 DrawControlTexture=true，按 Size 建自身
         ControlTexture，再 DrawOpaque(ControlTexture, rect(0,0,Size), DisplayLocation)
         贴回父 RT —— 这是"底"。
      b) MirImageControl 额外调用：
         Library.Draw(Index, DisplayLocation, ForeColour, ...)   (MirImageControl.cs:177-189)
         ★ 这个调用只用「图索引 + 位置」，根本不传 Size；
           图片尺寸 = Library.GetTrueSize(Index)（资源本身），不按 Size 缩放/裁剪。

结论：
    Size 只管「控件这个框多大 / 命中矩形多大 / 底图 RT 多大」；
    图片本体永远跟随资源，不跟随 Size。

超屏剔除守卫：
    MirControl.Draw：Size.Width > Settings.ScreenWidth || Size.Height > Settings.ScreenHeight
    时直接 return 不画。全屏控件必须绕开 Size，用 FullScreenSize（地图 MapControl
    即把 ControlTexture 设为窗口原生分辨率，由 DrawControl 1:1 合成，避免触发该守卫）。

--------------------------------------------------------------------------------
8.5 尺寸与坐标（重点坑）
--------------------------------------------------------------------------------
三套口径：
    - Size          ：控件自身逻辑尺寸（资源图/代码常量），用于框、命中、底图 RT
    - FullScreenSize：画布后备缓冲真实像素（如 1461x799），全屏控件/地图 RT 按它建
    - 逻辑坐标→RT 像素：当前为恒等 1:1（无分层缩放变换）

坐标系要点：
    - DisplayLocation = Parent.DisplayLocation + Location，一路累加到场景根
      （MirControl.cs:13）。这是逻辑坐标。
    - 当前恒等映射下逻辑坐标 == 画布像素，UI 按真实像素布局。
    - Settings.ScreenWidth = DXManager.GDevice.Viewport.Width。浏览器宿主下
      Viewport 可能读到 0 或逻辑尺寸，DXManager.Initialize 已强行校正成
      BackBuffer 尺寸（DXManager.cs:83-96），但仍不要在构造期依赖它做硬编码定位。
    - 命中测试 IsMouseOver 用 DisplayRectangle = (DisplayLocation, Size)，
      鼠标已在输入入口转过一次（屏幕坐标即逻辑坐标，因为映射是恒等 1:1）。
      渲染口径与命中口径必须一致，否则「画得出点不动」。

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
     → 多半掉出场景(被踢回场景根)。挂控件用 Parent=this（GameScene）/
       Parent=this（登录/选人），别直接 Controls.Add。
[2] UI 只铺左上角 + 四周洋红底
     → 场景 UI RT 尺寸用了 Size 而非全屏；上屏是 1:1，RT 必须按画布(全屏)建。
[3] 图溢出框（画得出、点不准）
     → 手动 Size 小于图；设 AutoSize=true 让框等于图，或别改 Size。
[4] 画得出点不动
     → 渲染与命中坐标口径不一致。检查 DisplayLocation / IsMouseOver 用的是否
       都是逻辑坐标，鼠标入口是否已转过一次。
[5] UI 变形（圆变椭圆、字体压扁）
     → 给场景/控件加了非等比缩放变换；当前为恒等 1:1，去掉缩放变换。
[6] 整层不画
     → Size > Settings.ScreenWidth 守卫触发；全屏控件(含 MapControl)必须绕开 Size、用 FullScreenSize。
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
         Scene→_background→LoginDialog→按钮 全程通过 → 按钮可点；
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
// GameScene 里创建对话框（直接挂到场景，无分层路由）
CharacterDialog = new CharacterDialog(MirGridType.Equipment, User)
{
    Parent  = this,                          // 直接挂到场景（UI 容器）
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
- 谁决定最终像素：FullScreenSize（RT 尺寸）+ 恒等 1:1 映射 + 洋红键合成上屏
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


十、地图/世界坐标原则：等比铺满会放大世界坐标（重要）
--------------------------------------------------------------------
地图由 GameScene 下的 MapControl 子控件直接渲染，承载【世界坐标】内容
（地图/角色/物品，瓦片固定 48x32 世界单位）。地图的投影严禁用 aspect-fill
（等比铺满 / Math.Max）去“铺满”窗口——那会把整个世界放大（1280 窗口下放大 1.25×），
既违背世界坐标的语义，也会与鼠标→世界命中（以 48x32 世界像素计、与屏幕 1:1）错位。

正确做法（参照本仓库兄弟工程 Mir2_Unity_2027 的重制版：
Assets/KFramework/Rumtime/Tools/SafeAreaFit.cs 的“改相机渲染区域(rect)而非缩放”，
以及 GameTools.cs 的 Camera.ScreenToWorldPoint/WorldToScreenPoint 同一投影命中）：

  - 世界以【固定比例】映射到屏幕（Unity 正交相机 orthographicSize，不缩放世界内容本身）；
  - 窗口更大时“渲染更大的世界区域 / 扩展相机视口”以显示更多世界，而非放大世界；
  - 渲染与命中共用同一投影（KCamera 的 Window↔World 换算）。

当前实现（MapControl 按全屏 / 窗口原生分辨率渲染）：
  - MapControl 世界坐标为 1:1 单位映射（不缩放、不 aspect-fill）；
  - MapControl 按【全屏/窗口原生分辨率】烘焙世界，地板 1:1 铺满、无黑边、无放大；
  - 窗口更大只是“看到更多世界”（以 48x32 世界像素为单位的视口更大），而非放大世界坐标；
  - 命中：MapControl 内鼠标即世界像素（与屏幕 1:1），与 OffSetX/Y、ViewRangeX/Y 一致；
    KCamera.ScreenToWorldPos 的 s=h/768 只服务于【UI 逻辑坐标】，与世界坐标无关。
  - MapControl.Size 保持 1024x768（受 MirControl.Draw 的 Size>ScreenWidth 裁剪守卫约束），
    只把 ControlTexture 设为窗口原生分辨率，由 DrawControl 1:1 合成，避免触发该守卫。
  - 关键结论：等比铺满(scale-fill)会放大世界坐标，而世界坐标不应被放大——这是错的。


十一、与参考源的对照工作法（本项目通用准则）
--------------------------------------------------------------------
1. 先查参考源：以 Web 页游《原始传奇》的实际表现与 Tools\原始传奇-资源提取\
   下的逆向记录为准，确认“本来怎么做”（字段名、调用点、事件签名、绘制时机）。
   ⚠ 不要再去 D:\OpenSource\Crystal grep——本工程已不再以它为标准答案。
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
