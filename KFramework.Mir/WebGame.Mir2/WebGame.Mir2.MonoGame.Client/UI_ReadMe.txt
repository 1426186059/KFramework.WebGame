================================================================================
Mir2 (WebGL) UI 编写指南 —— UI_ReadMe.txt
================================================================================
适用代码：WebGame.Mir2.MonoGame.Client
最后核对：2026-09-21（基于当前 2026New 分层渲染 + 恒等变换）

--------------------------------------------------------------------------------
0. 一句话结论
--------------------------------------------------------------------------------
UI 是「场景(MirScene) → 两个层(WorldLayerControl / UILayerControl) → 控件树」
的三层结构。所有界面控件挂在 UILayer 下，按画布原生分辨率 1:1 布局，窗口
尺寸变化由「锚点重排」负责，不做任何 x/y 拉伸缩放。

--------------------------------------------------------------------------------
1. 三层结构
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
2. 怎么挂一个控件（最关键的一步）
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
3. 常用控件
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
4. 绘制原理（为什么画图片"不用 Size"）
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
5. 尺寸与坐标（重点坑）
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
6. 自适应布局（窗口变化）
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
7. 常见坑清单
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
8. 最小示例
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
9. 排查口诀
--------------------------------------------------------------------------------
- 谁决定最终像素：FullScreenSize（RT 尺寸）+ GetLayerTransform（映射）+ PresentToScreen（1:1 铺）
- Size 只决定：自身纹理大小、命中矩形、底图 RT
- 看到「只占一角 / 突然不画 / 能画不能点」先查三件事：
    RT 是不是按 FullScreenSize 建的？
    是不是撞上 Size > Settings.ScreenWidth 的守卫？
    命中坐标是不是多转/少转了一次？
================================================================================
