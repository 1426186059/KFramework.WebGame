================================================================================
UI 移植指南 —— 从《原始传奇》白鹭(Egret)引擎 移植到 本工程 Mir2 客户端
================================================================================
适用：本工程（KFramework.Mir.OriginMri2）后续要批量移植线上页游的各种 UI 面板。
作者注：本文件基于"传送员面板(DeliverDialog)"的实际移植经验整理，方法可复制。

--------------------------------------------------------------------------------
一、资源现状（务必先读，这是最大的坑）—— 已用两个构建 + 直接探测三重验证
--------------------------------------------------------------------------------
线上游戏是 Egret 引擎，UI 布局写在 EXML 皮肤里，编译进主题文件 default.thm.json。

【验证结论：gameui3 不在本工程 CDN，gameui4 才是实际服务资源集】
  · 构建 10.25341.1 的 allmanifest：搜 gameui3/delieve = 0 命中；gameui4 有。
  · 构建 10.25487.1 的 allmanifest（用户给的新地址）：搜 gameui3/delieve 仍是 0 命中；
    gameui4 有。两个构建的 manifest 都没有 gameui3。
  · 直接 HEAD 探测 gameui3 字面路径（如
    https://.../fugu_20201203/resource/assets/gameui3/jpg/delieve_bgi.png
    及若干 gameui3/sheet/*.json|png）→ 全部 404。即 gameui3 在该 CDN 上根本不提供。
  ⇒ 用户说的"资源都在 manifest 里"应理解为：本构建实际服务的资源是 gameui4（确实都在
    manifest 里）；而线上 EXML 里写的 gameui3 是**旧资产管线遗留引用**，当前 CDN 不提供
    gameui3，运行时由 JS bundle 把 gameui3 重映射到 gameui4。所以"逐像素用原图 gameui3"
    做不到，但【坐标/尺寸/结构】可 1:1 移植，图一律用 gameui4 里现成/就近帧替代。

【新构建 thm.json 格式变化（重要）】
  · 旧 thm.json（10.25341.1, hash 7b917e47）内嵌 exmls[].gjs（含精确 x/y/w/h/source），
    是提取布局的黄金来源（已存到本目录 default.thm.json）。
  · 新 thm.json（10.25487.1, hash a30aba8a）只有 "styles"（文本样式定义），**不再内嵌
    exmls/gjs**——新版本把 EXML 皮肤编译进了主 JS bundle（main<ver>.min.js）。
    ⇒ 若要从【新构建】取布局，需从 main10.25487.1.min.js 里抠 EXML（比 thm.json 麻烦），
      且其中资源引用大概率是 gameui4。布局本身是游戏设计，跨构建通常不变，旧 thm.json
    的 gjs 已足够作为坐标基准。

【就近替代原则】（少数传送员专用帧无精确 gameui4 等价物）
  com_z_btn_6 → com_z_btn_1 ； z_boss_img0 → com_z_btn_2 ；
  z_com_line1 → com_line_h ； z_com_btn_close7 → com_btn_close0 ；
  delieve_bgi.png（gameui3 背景）→ y_cs_bg.jpg（gameui4/jpg，manifest 有）。
  替代时在同目录 UI移植-ReadMe 的"第六节"登记对应关系。

--------------------------------------------------------------------------------
二、获取布局（坐标/尺寸/结构）—— 这一步要 100% 准确
--------------------------------------------------------------------------------
1) 拿 default.thm.json
   - manifest key：resource/default.thm.json
   - CDN 哈希布局地址公式：
       https://cdn.yscq-wy.yscq.com/fugu_20201203/resource/{v的前2位}/{v}_{s}.json
     其中 v、s 从 manifest 里该 key 的条目读取（例：v=7b917e47,s=5163231
     → .../resource/7b/7b917e47_5163231.json）。
   - 已下载放到：原始传奇-资源参考\default.thm.json

2) 文件结构
   {
     "skins": { "eui.Button":"...", ... },               // 系统控件皮肤映射（不用管）
     "exmls": [
        { "path":"resource/eui_skins/.../XxxSkin.exml",
          "gjs" : "(function(_super){ ... t.x=..; t.y=..; t.width=..; t.height=..; ... })"
        }, ...
     ]
   }
   每个 exml 的 "gjs" 字段，就是 Egret 把 EXML 编译成的布局代码，包含【精确】的：
     t.x= / t.y= / t.width= / t.height= / t.source= / t.scale9Grid=
     以及布局容器类型（HorizontalLayout / TileLayout 的 requestedColumnCount、gap 等）。

3) 提取方法（PowerShell，已验证）
   - 定位 "path":".../目标Skin.exml" 后，取紧随其后的 "gjs":"..." 字符串；
   - 把 gjs 里的 \" 反义成 " 即可得到可直接阅读的布局代码；
   - 把里面的 x/y/width/height/source 原样搬到 Mir2 的 MirControl.Location / Size 即可。

   （注：min.js 是单行压缩的运行时逻辑，不含 EXML 字符串；皮肤字符串只在 thm.json 里。
    不要去 min.js 里抠布局，那是浪费时间。）

--------------------------------------------------------------------------------
三、镜像对照表（Egret 概念 → 本工程 Mir2 控件）
--------------------------------------------------------------------------------
  eui.Image                      → MirImageControl   （背景/图标/分隔线）
  eui.Button / eui.RadioButton   → MirButton         （AtlasNameKey="图集#帧"；
                                                       三态用 HoverAtlasFrame / PressedAtlasFrame）
  eui.Label                      → MirLabel         （居中 CenterText=true）
  eui.List + HorizontalLayout    → 横排控件 + 自实现翻页
                                   （本工程无内置横滑 Scroller，用 ◀▶ 箭头 + Clip 容器模拟）
  eui.List + TileLayout          → N 列网格 + ▲▼ 翻页
                                   （requestedColumnCount=N → N 列；gap → 控件间距）
  eui.Scroller                   → 裁剪容器(Clip=true) + 翻页箭头 模拟

--------------------------------------------------------------------------------
四、图片资源怎么取（第二个大坑）
--------------------------------------------------------------------------------
A. 背景单图（如 gameui4/jpg/*.jpg、gameui3/jpg/*.png）
   ⚠ 绝对不要直接用 ImageUrl = "resource/assets/.../xxx.jpg"！
     因为 CDN 是【哈希布局】，直接拼 key 原路径会 404。
     必须走 manifest 解析：调用 MirImageControl 内置通用方法
         _ = someImageControl.SetManifestImageAsync("resource/assets/.../xxx.jpg");
     该方法内部先 ResolveManifestAssetPath 解析成 resource/{hash前2}/{hash}_{s}.ext
     再下载，并复用 _remoteCts 取消在途加载、下载完赋 Texture 并 Redraw。

B. 图集帧（按钮/图标等）
   用 AtlasNameKey = "图集名#帧名"，例如 "y_common-sheet#com_z_btn_1"。
   图集由 Mir2AtlasLibrary 经 manifest 加载，天然走哈希布局，不需要特殊处理。
   查某帧是否在某个图集：下载该图集的 .json（同哈希公式），里面是
     { "帧名": {"x":..,"y":..,"w":..,"h":..}, ... } 直接 grep 帧名即可。

C. 找不到精确帧时（就近替代，并在代码注释标注）
   例（传送员面板实际替代）：
     com_z_btn_6   → com_z_btn_1      （分类标签按钮）
     z_boss_img0   → com_z_btn_2      （目的地格子底图）
     z_com_line1   → com_line_h       （分隔线）
     z_com_btn_close7 → com_btn_close0 （关闭按钮）
   替代原则：优先同名/同类；没有就挑形状最接近的，并在代码里写清"线上原帧 → 本工程替代帧"。

--------------------------------------------------------------------------------
五、标准移植流程（可复制粘贴执行）
--------------------------------------------------------------------------------
1. 在 default.thm.json 里 grep 目标皮肤名（如 DeliverDialogSkin），取出其 gjs。
2. 解析 gjs → 得到各控件 x/y/w/h、列表布局参数、背景 source、scale9Grid。
3. 在 Mir2/MirScenes/Dialogs/ 新建/修改 XxxDialog : MirImageControl，
   按坐标摆放 MirImageControl / MirButton / MirLabel。
4. 背景用 SetManifestImageAsync；按钮/图标用 AtlasNameKey。
5. 列表/翻页按第三节映射到 ◀▶ / ▲▼ + Clip 容器。
6. 关闭按钮放在【面板内角】并 BringToFront()，
   避免与面板边缘或其它控件互相遮挡导致"点击无响应"（血泪教训：原版关闭键在
   面板右上角外侧 right=-28，移植时若贴着边缘/与其它角标重叠就会点不动）。
7. 编译 → 运行 → 核对像素对齐。

--------------------------------------------------------------------------------
六、本次传送员面板（DeliverDialog）移植记录（备查）
--------------------------------------------------------------------------------
- 尺寸：510×312（原 width=510,height=312）。
- 结构（严格对齐 EXML）：
    顶部横向分类 tab（list_page, HorizontalLayout，用 ◀▶ 翻页）
    + 下方 3 列目的地网格（list_content, TileLayout requestedColumnCount=3，用 ▲▼ 翻页）
    + 分隔线 z_com_line1（替代 com_line_h）
    + 右上关闭 btn_close（替代 com_btn_close0，放内角并置顶）。
- 背景原图 delieve_bgi.png（gameui3，本工程无）→ 替代 y_cs_bg.jpg（manifest 存在）。
- 分类按钮 com_z_btn_6（无）→ com_z_btn_1；目的地 z_boss_img0（无）→ com_z_btn_2。
- 代码位置：WebGame.China.Mir2.Client/Mir2/MirScenes/Dialogs/NPCTeleporterDialog.cs
- 通用方法：WebGame.China.Mir2.Client/Mir2/MirControls/MirImageControl.cs
            → public async Task SetManifestImageAsync(string key)

================================================================================
