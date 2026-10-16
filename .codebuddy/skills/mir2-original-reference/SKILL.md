---
name: mir2-original-reference
description: >
  Default working principle for any task on the Mir2 Web project
  (KFramework.WebGame\KFramework.Mir.原始传奇). Load this skill proactively for ANY Mir2 work:
  implementing a feature, fixing a regression, porting a control, or debugging behavior divergence.
  The reference / source of truth is the Web page game 《原始传奇》(Egret, build fugu_20201203),
  whose reverse-engineered rules live in Tools\原始传奇-资源提取\. Before writing code, always ask:
  "How does 《原始传奇》 do this, and what in the Web port would break or need adapting?"
  NOTE: D:\OpenSource\Crystal (WinForms + SlimDX/D3D9 desktop client) is only the historical origin
  of the code and is NO LONGER a reference — do not grep it.
---

# Mir2 Web 工程工作总则：以《原始传奇》页游为锚，思考如何适配 Web

本工程（`D:\OpenSource\KFramework.WebGame\KFramework.Mir.原始传奇`）是
**参考 Web 页游《原始传奇》**（Egret 引擎，构建 `fugu_20201203`，主包 `main10.25341.1.min.js`）
的 Web 游戏工程，通过 KFramework.MonoGame 以 WASM 跑在浏览器里。
《原始传奇》页游的实际表现 + `Tools\原始传奇-资源提取\` 下的逆向记录 = **标准答案**。

> 核心心法：每次动手前，先确认《原始传奇》页游里它"本来怎么做"，再想
> "Web 这一层（渲染 / 输入 / 资源 / 运行时）哪点会让原写法失效，怎么改最小且忠实"。
> 不要凭记忆"重写"一个功能——先查参考源与逆向记录，照着结构搬，只在 Web 特有约束处适配。

⚠️ **不再参考 `D:\OpenSource\Crystal`**。那个 WinForms + SlimDX/D3D9 桌面客户端只是本工程代码的
历史来源，现已不作为行为标准；遇到"原版本来怎么做"的问题一律以《原始传奇》页游为准。

## 1. 先定位参考源，再定位本工程代码

参考源（行为标准）：

| 内容 | 位置 |
|------|------|
| 资源 / URL / 地图规则逆向记录 | `Tools\原始传奇-资源提取\README.md` |
| 页游原始 JS 与配置存档 | `Tools\原始传奇-资源提取\原始传奇-资源参考\`（`main10.25341.1.min.js`、`0config*.json`、`map*.dat`…） |
| 提取结果预览服务 | `Tools\原始传奇-资源提取\Mir2ReverseWeb\` |

本工程代码定位表（Mir2 移植部分）：

| 关注点 | 工程内位置 |
|--------|-----------|
| 程序入口 / 主循环 / 输入派发 | `WebGame.Mir2.MonoGame.Client/CMain.cs` |
| 游戏引导（MonoGame Game 子类） | `WebGame.Mir2.MonoGame.Client/MirGame.cs` |
| Web 部署配置（资源服务器 / 网关地址） | `WebGame.Mir2.MonoGame.Client/2026New/RemoteWebSetting.cs` |
| 地图图片资源热更（URL 直链） | `WebGame.Mir2.MonoGame.Client/2026New/NewResConfig.cs` |
| 场景基类 / Draw 上屏 | `.../Mir2/MirControls/MirScene.cs` |
| 控件基类（Draw/CreateTexture） | `.../Mir2/MirControls/MirControl.cs` |
| 标签 / 文本 | `.../Mir2/MirControls/MirLabel.cs` |
| 文本框 / 光标 / IME | `.../Mir2/MirControls/MirTextBox.cs` |
| 游戏主场景（含 MapControl） | `.../Mir2/MirScenes/GameScene.cs` |
| 资源库（.Lib 加载） | `.../Mir2/MirGraphics/Libraries.cs` + `MLibrary.cs` |
| 引擎级输入（键鼠） | `KFramework.WebGame/KFramework.MonoGame/Input/Input_KeyBoard.cs`、`Input_Mouse.cs` |
| WASM 输入桥 / IME | `.../MonoGame/JSBind/JSBind_Input*.cs` + `wwwroot/jsengine/*.js` |

定位技巧：在移植目录搜 `class XXX`、`Effects.Add`、`OnMouseClick`、`CreateTexture`、`KeyPress` 等关键字。

## 2. Web 适配的思维模型（四类根本差异）

动手前先判定当前改动属于哪一类，每一类都有"传统桌面写法能跑、Web 会炸"的坑：

### (a) 渲染：即时 GDI 绘制 → MonoGame/WebGL 分层烘焙
- 历史来源：控件在 `OnPaint` 里直接 `Draw` 到窗体 DC，浮层挂到场景上即可即时出现。
- 移植：**场景被切成 WorldLayer / UILayer**（见 `2026New/LayerControl.cs`、`UILayerControl.cs`），
  每个 Layer 每帧 `Bake()` 把自己烘焙进一张 RenderTarget，再由 `MirScene.Draw()`（`DXManager.PresentToScreen`）上屏。
- 推论与坑：
  - **浮层控件（调试/提示/特效）不要做场景子控件**（即不要 `Parent = scene`）：它会随 UILayer 烘焙一次，
    又在不该再被 `MirScene.Draw()` 显式 `Draw()` 一次 → 画两遍 / 偏移。正确做法是**无 Parent**，
    只由 `MirScene.Draw()` 在场景上屏后单独 Draw（UI 层是单位变换，屏幕坐标即 UI 坐标）。
  - 文本标签 `AutoSize` 时，底板（父 MirControl）要监听 `SizeChanged` 并 `DisposeTexture()` 后改 `Size`——
    移植的 `MirControl.CreateTexture` 只在 `ControlTexture == null` 时建 RT，尺寸变了不会自动重建，否则底板沿用首次尺寸。
  - 控件可见性/位置由 `Visible`、`Location`、`DisplayRectangle`、`NotControl`、`Opacity` 决定，照搬即可。

### (b) 输入：原生事件 → 浏览器事件桥
- 历史来源：键盘/鼠标是 OS 级事件，`KeyDown`/`KeyPress`/`KeyUp`、`MouseDown`/`MouseClick` 由 WinForms 直接给。
- 移植：浏览器事件经 `wwwroot/jsengine/*.js` → `Input_KeyBoard` / `Input_Mouse`（KFramework.MonoGame）→
  `CMain` 里 `CMain_KeyPress` / `CMain_Mouse*` 转成 `MirEngine` 事件派发给 `MirScene` / 控件。
- 推论与坑：
  - **`KeyPressEventArgs` 现在携带 `Keys`（键码 + 修饰键位，即 `KeyData`），不是 `char`**。比较按键一律用 `e.KeyCode`，
    要区分上档字符（如聊天前缀 `@`/`!`）靠 `e.Shift` / `e.Modifiers`。`CMain.ToKeyPressEventArgs` 必须把 Shift/Ctrl/Alt 并进 `KeyData`（浏览器 `keyCode` 不区分上档）。
  - **F12 会被浏览器当作"打开开发者工具"抢走**，页面收不到 keydown → 游戏内 F12 开关可能"没反应"；
    验证时改用 `Settings.DebugMode` 配置文件写 true，或确认页面焦点在 canvas。
  - 鼠标坐标：`CMain_MouseMove` 用 `KCamera.ScreenToWorldPos` 把原始画布坐标换算成逻辑 UI 坐标（1024×768 系），
    `MapControl.MouseLocation` 等据此计算。
  - 输入派发走 `MirScene.OnMouseClick` → `MouseControl.OnMouseClick`，`MouseControl` 由 `Highlight()` 在鼠标移动时设置；
    若某控件"点击没反应"，先确认 `MouseControl` 是否真的是它（可加日志验证）。

### (c) 资源：启动时全量加载 → 按需异步懒加载
- 历史来源：所有 `.Lib`（Magic、Magic3、Effect、Prguse…）进游戏前就 `Load` 完。
- 移植：`Libraries.LoadLibrariesAsync` **只预加载首屏必需库**（ChrSel/Prguse/Prguse2/Prguse3/UI_32bit/Title），
  其余（含 `Magic3`）是**绘制时按需异步加载**：`MLibrary.CheckImage` 在未就绪时触发 `InitializeAsync()` 并返回马赛克占位图（`MosaicImage`），
  加载完成（一次网络往返，常 > 几百 ms）后才画真实帧。
- 推论与坑：
  - **依赖某个"冷门库"的特效/动画，在它第一次被使用前的那一两帧可能是马赛克或（库加载失败时）完全不可见**。
    典型症状："以前点地面有特效，现在看不到"——可能只是该库尚未加载，或在首次点击的 600ms 窗口内没下完。
  - 若要让某库在首次使用即可见，可在进入场景时**预热**（`_ = Libraries.Xxx.InitializeAsync();`），但代价是进游戏多下一个库；
    移植刻意避免全量预加载以控制 **WASM 堆/显存**压力——这是合理的 Web 约束，除非必要不要"为修一个小特效而全量预加载"。
  - 资源来源两条腿：《原始传奇》页游 CDN（图集 / UI / 配置走 `allmanifest*.json` 索引，
    地图视觉走 `zipmap/{mapId}/{index}.jpg`，地图逻辑数据走 `{configUrl}{mapId}.bin`）
    + 本地提取产物（经 `Tools\资源服务器\` 的静态 HTTP 服务提供）。

### (d) 运行时：桌面进程 → WASM（内存/线程受限）
- 无原生窗体标题栏、无原生 IME、无 System.Drawing；文本/字体走 MonoGame 的 `SpriteFont`/`Font`（见 `Fonts.cs`）。
- IME 是唯一"不得不"偏离历史写法的地方：Web 端用透明 DOM `<input>` 叠加层（`JSBind_InputHtmlIme` + TS overlay）替代原生 TextBox。
- WASM 下避免大对象常驻、避免每帧 new 大集合；改动若引入额外每帧分配/额外 RT，要评估。

## 3. 动手前的检查清单（每次都过一遍）

1. **先查参考源**：以《原始传奇》页游的实际表现与 `Tools\原始传奇-资源提取\` 下的逆向记录为准，
   确认"本来怎么做"（字段名、调用点、事件签名、绘制时机）。
   ⚠ 不要再去 `D:\OpenSource\Crystal` grep——本工程已不再以它为标准答案。
2. **判定差异类别**：属于 (a) 渲染 / (b) 输入 / (c) 资源 / (d) 运行时 哪一类？列出会失效的点。
3. **最小忠实改动**：照参考结构搬，只在 Web 约束处适配；不要重写、不要"顺手优化"。
4. **编译验证**：`dotnet build`（客户端工程）确认 0 错误；留意 CS0104 类型歧义
   （`MirEngine.Color` vs `KFramework.MonoGame` 同名类型要用全限定）。
5. **边界/副作用**：改输入要检查"左键 vs 右键""按住 vs 单击"是否都被正确覆盖；
   改渲染要检查是否双 draw / 尺寸不更新；改资源要检查冷库首帧空白。
6. **写注释说明偏离**：凡与页游写法不同的地方（不挂 Parent、显式 DisposeTexture、预热库、
   F12 被浏览器吃掉等）都加注释写清"为什么 Web 要这样"，方便后人对照。

## 4. 已知 Web 改编事实（易错，已踩过）

- **落点特效**：`GameScene.cs` 里的落点环（Magic3 第 500 帧起的 10 帧动画）原先只在
  "右键 + `Settings.NewMove` 为真"的寻路分支里放；移植把它抽出来，让点空地都有落点反馈，
  但仍要确认左键分支没有提前 `return`——这是"看不到了"的常见原因。
- **FPS/Debug 浮层**：`CMain.CreateDebugLabel()` / `CreateHintLabel()` 建的是**无父浮层**、
  由 `MirScene.Draw()` 单独 Draw；切勿把浮层挂到场景上，否则会被 UILayer 烘焙后再画一次。
- **KeyPress 语义**：移植的 `KeyPressEventArgs` 现在给 `Keys` 而非 `char`，所有按键比较要改成
  `e.KeyCode` + 修饰键判断。
- **类型歧义**：`CMain.cs` 同时 `using MirEngine` 与 `using KFramework.MonoGame`，
  `Color`/`Point` 等要用 `MirEngine.` 全限定。
- **帧宿主误判**：游戏程序集（如 `CMain`）绝不能带 `[JSExport]` 的 `Frame`/`Step`，
  否则引擎 `findHost` 的递归兜底会把它当帧宿主，绕过 `Game.TickFrame` / 上屏导致黑屏。
  所有 JS 互操作一律走 `KFramework.MonoGame` 的 `JSBind_*`。
