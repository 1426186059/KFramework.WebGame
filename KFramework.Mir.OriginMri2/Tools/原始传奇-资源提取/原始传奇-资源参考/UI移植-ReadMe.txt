================================================================================
UI 移植指南 —— 从《原始传奇》白鹭(Egret)引擎 移植到 本工程 Mir2 客户端
================================================================================
适用：本工程（KFramework.Mir.OriginMri2）后续要批量移植线上页游的各种 UI 面板。
作者注：本文件基于"传送员面板(DeliverDialog)"的实际移植经验整理，方法可复制。

================================================================================
★ 一句话核心原则：取布局，直接去 JS 里查！不要只信 thm.json 内嵌的 gjs！
================================================================================
Egret 皮肤的几何（x/y/w/h）最终都编译进【实际运行的主 JS bundle】。
thm.json 里的 exmls[].gjs 只是【构建时的快照】，可能陈旧、可能与线上当前布局
不一致。本次移植的血泪教训：最新构建的 thm.json（hash a30aba8a）内嵌的
DeliverDialogSkin gjs 仍是【旧布局】（顶部横排分类 tab + 下方 3 列网格），
但线上当前真实布局是【左竖排一级菜单 + 右二级入口】——这个差异 thm.json 完全不会
体现，只有去运行的 JS 里搜皮肤名才看得到。所以：

    thm.json 可作参考/兜底，但【以 JS 里查到的几何为准】。

================================================================================
一、定位并下载主 JS（布局的真正来源，先搞这个）
--------------------------------------------------------------------------------
1) 先抓 CDN 入口页，看脚本加载顺序：
       https://cdn.yscq-wy.yscq.com/fugu_20201203/index.html
   里面 <script> 列表的【最后一个游戏逻辑脚本】就是主 JS（本例是 qufu1.min.js）。
   另有主逻辑 bundle 在 无 /js/ 前缀、无版本号 的路径，例如：
       https://cdn.yscq-wy.yscq.com/fugu_20201203/main10.25487.1.min.js
   （注意：它不在 /js/ 下，也没有 ?v= 查询串；manifest 里查不到，由 index.html 直引。）

2) 把所有相关 JS 下载到本地（已存：原始传奇-资源参考\js\）：
       eui.min.js / game.min.js / assetsmanager.min.js / qufu1.min.js
       main10.25487.1.min.js（≈5.9MB，当前主逻辑，含 DeliverDialog 逻辑）
   主 JS 多半是【单行压缩】，要在里面 grep 皮肤名。

3) 资源现状（已两次构建 + 直接探测三重验证）：
   · 两个构建的 manifest 都【没有 gameui3】；直接 HEAD 探测 gameui3 路径全 404。
   · 实际服务的资源集是 gameui4（都在 manifest 里）。线上 EXML 里写的 gameui3 是
     旧管线遗留引用，当前 CDN 不提供 gameui3，运行时由 JS bundle 把 gameui3 重映射
     到 gameui4。⇒ 逐像素用原图 gameui3 做不到，但【坐标/尺寸/结构】可 1:1 移植，
     图一律用 gameui4 里现成/就近帧替代。

================================================================================
二、在 JS 里抠布局（关键步骤，不要偷懒跳到 thm.json）
--------------------------------------------------------------------------------
1) 在 main<ver>.min.js / qufu1.min.js 里搜目标皮肤类名，例如 "DeliverDialogSkin"。
   · 逻辑类里通常只 uiSkin("DeliverDialogSkin") 按【名】引用；
   · 真正的几何 gjs（this.elementsContent / this.width= / this.height= /
     skinParts=[...]）要么内嵌在 JS 里，要么由 thm.json 加载——但无论哪种，
     都要用 JS 作为校验基准，确认它和 thm.json 一致；不一致时以 JS 为准。

2) 定位皮肤定义的特征串（每个皮肤唯一，用它 grep 最稳）：
       DeliverDialogSkin   → skinParts=["btn_close","list_page","list_content"]
       DeliverPageRenderSkin→ skinParts=["btn"]
       DeliverBtnRenderSkin → skinParts=["btn_go","txt_name"]
   用这些串在 JS（或 thm.json）里 grep，取其前后约 ±9KB 窗口即可拿到当前几何。

3) 把 x/y/width/height/source/scale9Grid 原样搬到 Mir2 的
   MirControl.Location / Size / AtlasNameKey。

4) 列表布局参数原样保留，翻页用 ▲▼/◀▶ 模拟（本引擎无内置 Scroller）：
       HorizontalLayout          → 横排控件 + ◀▶ 翻页（Clip 容器裁剪）
       TileLayout requestedColumnCount=N → N 列网格；gap → 控件间距，用 ▲▼ 翻页

================================================================================
三、thm.json 角色说明（辅助，非权威）
--------------------------------------------------------------------------------
· 旧 thm.json（10.25341.1, hash 7b917e47）：内嵌 exmls[].gjs，含精确 x/y/w/h/source，
  曾是提取布局的黄金来源（已存 原始传奇-资源参考\default.thm.json）。
· 新 thm.json（10.25487.1, hash a30aba8a）：只有 "styles"，不再内嵌 exmls/gjs——
  新版本把 EXML 皮肤编译进了主 JS bundle（见第一节）。即便如此，它的快照仍可能
  比线上当前布局旧（本次 DeliverDialog 即此例）。
· CDN 哈希布局地址公式（thm.json / 资源都适用）：
       https://cdn.yscq-wy.yscq.com/fugu_20201203/resource/{v前2位}/{v}_{s}.json
  v、s 从 manifest 里该 key 的条目读取。

================================================================================
四、镜像对照表（Egret 概念 → 本工程 Mir2 控件）
--------------------------------------------------------------------------------
  eui.Image                    → MirImageControl   （背景/图标/分隔线）
  eui.Button / eui.RadioButton → MirButton         （AtlasNameKey="图集#帧"；
                                                     三态用 HoverAtlasFrame / PressedAtlasFrame）
  eui.Label                    → MirLabel         （居中 CenterText=true）
  eui.List + HorizontalLayout  → 横排控件 + 自实现 ◀▶ 翻页
  eui.List + TileLayout        → N 列网格 + ▲▼ 翻页
  eui.Scroller                 → Clip 容器 + 翻页箭头 模拟

================================================================================
五、图片资源怎么取（第二大坑：CDN 是哈希布局）
--------------------------------------------------------------------------------
A. 背景单图（gameui4/jpg/*.jpg、gameui3/jpg/*.png）
   ⚠ 绝对不要 ImageUrl="resource/assets/.../xxx.jpg" 直接拼——会 404。
   必须走 manifest 解析：调用 MirImageControl.SetManifestImageAsync("resource/.../xxx.jpg")
   内部先 ResolveManifestAssetPath 解析成 resource/{hash前2}/{hash}_{s}.ext 再下载，
   并复用 _remoteCts 取消在途、下载完赋 Texture 并 Redraw。

B. 图集帧（按钮/图标）
   用 AtlasNameKey="图集名#帧名"，如 "y_common-sheet#com_z_btn_1"。
   图集由 Mir2AtlasLibrary 经 manifest 加载，天然走哈希布局。
   查某帧是否在某图集：下载该图集 .json（同哈希公式），grep 帧名即可。

C. 找不到精确帧时（就近替代，并在代码注释标注）
   传送员面板实际替代：
     com_z_btn_6   → com_z_btn_1      （一级菜单分类按钮）
     z_boss_img0   → com_z_btn_2      （二级入口目的地格子底图）
     z_com_line1   → com_line_h       （分隔线）
     z_com_btn_close7 → com_btn_close0 （关闭按钮）
     delieve_bgi.png（gameui3 背景）→ y_cs_bg.jpg（gameui4/jpg，manifest 有）
   替代原则：优先同名/同类；没有就挑形状最接近的，并在代码里写清"线上原帧→本工程替代帧"。

================================================================================
六、标准移植流程（以"查 JS"为核心，可复制）
--------------------------------------------------------------------------------
1. 抓 index.html，定位主 JS（qufu1.min.js / main<ver>.min.js），下载到 js\。
2. 在 JS 里 grep 目标皮肤名，取其几何（x/y/w/h/source/scale9Grid/列表参数）。
3. 同时对照 thm.json 内嵌 gjs 做校验：两者一致则用；不一致【以 JS 为准】。
4. 新建/修改 Mir2/MirScenes/Dialogs/XxxDialog : MirImageControl，按坐标摆放控件。
5. 背景用 SetManifestImageAsync；按钮/图标用 AtlasNameKey（就近替代见第五节 C）。
6. 列表/翻页按第四节映射到 ◀▶ / ▲▼ + Clip 容器。
7. 关闭按钮放在【面板内角】并 BringToFront()，避免与边缘/角标重叠导致"点不动"。
8. 编译 → 运行 → 和线上游戏逐像素核对（若有偏差，回去第 2 步在 JS 里再查，别改 thm.json）。

================================================================================
七、本次传送员面板（DeliverDialog）移植记录（更正版）
--------------------------------------------------------------------------------
- 代码位置：WebGame.China.Mir2.Client/Mir2/MirScenes/Dialogs/NPCTeleporterDialog.cs
- 【当前真实布局（线上）＝ 左竖排一级菜单 + 右二级入口】：
    · 面板 510×312（background 原图 delieve_bgi.png→替代 y_cs_bg.jpg）。
    · 左侧：竖排一级菜单（分类列表），按钮 110×21，纵向堆叠，选中态 com_z_btn_1_1。
    · 右侧：二级入口（目的地）2 列网格，格子 175×56，上下 ▲▼ 翻页，选中态 com_z_btn_2_1。
    · 标题下方分隔线 com_line_h；右上关闭 com_btn_close0（放内角并置顶）。
- 数据模型（分类→目的地过滤）不变：
    分类来自各目的地的 Category 字段（无分类时归"全部"）；
    右侧显示当前选中分类下的目的地，点击触发 SelectDestination → CMSG_TELEPORT。
- ⚠ 重要更正：旧 ReadMe 记录的"顶部横排 tab + 3 列网格"是 thm.json(a30aba8a)
  内嵌 gjs 的【陈旧快照】，并非线上当前布局。当前左竖排+右二级入口的结构，是
  核对线上运行表现后重建的；若后续能在主 JS 里抠到完全一致的几何，以 JS 为准微调。
- 资源替代映射见第五节 C；背景/按钮均用 gameui4（gameui3 在本 CDN 不可用）。

================================================================================
