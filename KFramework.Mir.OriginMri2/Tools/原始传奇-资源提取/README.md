# 原始传奇（复古页游）资源提取工具

针对 Egret 引擎的传奇页游（构建 `fugu_20201203`，主包 `main10.25341.1.min.js`）。
已逆向的地图资源加载逻辑见下，工具按此实现。

## 地图核心数据 = 两部分

### 1. 视觉地图（哪个 Cell 显示哪个图片）—— 纯位置映射，无查找表
- 地图被切成 **zipmap 瓦片**，每张 **512×256 px**。
- 瓦片 URL：
  `https://cdn.yscq-wy.yscq.com/fugu_20201203/assets/resource/zipmap/{img}/{index}.jpg?ver=1.0.2`
  - `{img}` = 配置里的 `img` 字段（盟重省154 = `154`）
  - `{index}` = `1` ~ `N`，**无零补零**（已验证 `1.jpg`=200，`0001.jpg`=404，`9300.jpg`=200，`9301`=404）
- 网格：`cols = ceil(width / 512)`，`rows = ceil(height / 256)`，`N = cols*rows`
  - 盟重省154：`width=47616 height=25600` → `cols=93 rows=100 N=9300`
- **Cell→图片映射（行列优先，1-based）**：
  - `index = row * cols + col + 1`
  - 反过来：`col = (index-1) % cols`，`row = floor((index-1) / cols)`
  - 即 cell(col,row) 用 `((row*93)+col+1).jpg`
- 整图像素尺寸 = `cols*512 × rows*256` = `47616 × 25600`

### 2. 逻辑地图（真正的“地图核心数据文件”）
- 配置里每个地图有 `data` 字段（盟重省154 的 `data=154`）。
- 该字段指向逻辑地图文件，由 `ResUrl` 的 case 13/14/12 拼出：
  - `.bin`：`{configUrl}{data}.bin`        （case 13, ConfigDataBinExt）
  - `.dat`：`{configUrl}{data}.dat?ver=x`  （case 14, ConfigZipExt）
  - `.csv`：`{configUrl}{data}.csv`        （case 12, ConfigDataExt）
- 内容：每格的**可走性、门/传送事件、内嵌对象/传送点/NPC/怪物**等。
- **注意**：`configUrl`（数据服务器）与图片 CDN 是**不同主机**。
  - JS 里默认值 `e.Data="http://192.168.5.66:8082/"`（本地 dev），生产值由游戏宿主页注入 `js_gameVars.configUrl`。
  - 公网图片 CDN 上没有这个文件，需要用 `Extract-MapData.ps1 -ConfigUrl <真实地址>` 拉取。
  - 真实 `configUrl` 获取方式：进游戏加载盟重省154 时在 DevTools→Network 找 `.bin`/`.dat` 那条请求，复制其 URL 的目录部分；或看宿主页里 `js_gameVars.configUrl="..."`。

## 全局配置文件
`0config10.25341.1.json`（约 7MB）顶层有 `.map`，按地图 id 为键：
```json
"154":{"id":154,"img":154,"data":154,"name":"盟重","width":47616,"height":25600,"cls":1,...}
```
字段：`id/img/data/name/width/height/cls/homeXY` 等。

## 工具用法
```powershell
# 1) 列出全部地图 / 查看单个地图的网格信息
.\Read-Config.ps1                                    # 列出所有地图
.\Read-Config.ps1 -MapId 154                         # 盟重省详情

# 2) 下载某地图的全部 zipmap 瓦片（默认 154）
.\Extract-MapTiles.ps1 -MapId 154 -OutDir .\map_154_tiles
#    -> 下载 1..9300.jpg，并写入 meta.json（含 cols/rows）

# 3) 拼成完整大图（自动分块以避开 GDI+ 32767 像素上限）
.\Stitch-Map.ps1 -TileDir .\map_154_tiles -MetaJson .\map_154_tiles\meta.json -OutFile map_154

# 4) 下载逻辑地图数据文件（需提供真实 configUrl）
.\Extract-MapData.ps1 -MapId 154 -ConfigUrl "https://<数据服务器>/"
```

## 配置表提取（0config&lt;版本&gt;.json 顶层每个键 = 一张表）

实际配置只有一个分片 `0config<版本>.json`（如 `0config10.25341.1.json`，约 7MB），
不是 0/1/2/3 多个分片。其 URL 拼法与游戏主程序 JS 的 `onloadCfgAgain` 完全一致：

```js
// 游戏主 JS：ResUrl.Data + "0config" + resVersion + ".json" + resver
var t = ResUrl.Data + ("0config" + (js_gameVars.noCache ? "" : js_gameVars.resVersion) + ".json") + js_gameVars.resver;
```

即：**数据服务器地址 + "0config" + 版本号 + ".json?v=" + 版本号**。
`ResUrl.Data` = `js_gameVars.configUrl`（数据服务器根，与镜像 CDN `fugu_20201203` 同根）。
页面“提取配置表”可手动改「数据服务器地址 / 版本号」，并实时预览拼出的 URL。

该文件顶层有数百个“配置表”键（`map` / `global` / `level` / `conditions` / `compose` /
`vip` / `starSoulSuit` ……），每张表的取值通常是「以 id 为键的对象映射」或「裸数组」，
内部是平铺的字段（多为 `53#1000#1000`、`1#19|36#15` 这类 `#`/`|` 分隔的字符串，也有纯数字字段）。

在 `Mir2ReverseWeb` 逆向工坊左侧「提取配置表」页（启动后先经「提取总览」进入）：

1. **加载配置表**：按上面的拼法下载 `0config<版本>.json` 进内存（不落盘），
   把顶层每个键识别为一张配置表。
2. 列表展示每张表的 **表名 / 形态(object|array) / 条目数 / 字段数**，
   字段类型由数据最佳努力推断（`int` / `long` / `double` / `bool` / `string`，
   对象/数组/混合字段用 `JsonElement` 兜底），即把 JS 对象映射为 C# 类。
3. **提取选中 / 提取全部**：为每张表在输出目录写出两份产物
   （默认 `系统下载目录/Mir2Config`，可在页面改）：
   - `<表名>Config.cs`：由字段自动生成的 C# 类（`[JsonPropertyName]` 绑定原始键名，
     反序列化无需额外配置）；
   - `<表名>.json`：按 URL 下载提取后保持原始 object/array 形态的结构化数据（中文保留原文）。

对应接口：`POST /api/config/load`（可带 `baseUrl` / `version`）、`/api/config/tables`、`/api/config/extract`。

## 常量速查
- `MAP_TILE_WIDTH = 512`，`MAP_TILE_HEIGHT = 256`
- `MapBackGround = "zipmap/"`，`ImageServer = js_gameVars.resUrl`
- `ConfigDataBinExt=".bin"`, `ConfigZipExt=".dat"`, `ConfigDataExt=".csv"`
- 瓦片命名函数：`ResUrl.url(index+".jpg", 3, img)` → `ImageServer + "zipmap/" + img + "/" + index + ".jpg"`

## 游戏主程序（main10.25341.1.min.js）资源加载架构逆向

> 结论由对 `main10.25341.1.min.js` 的 token 探查 + 运行时日志 + C# 逆向实现
> （`Mir2OriginResource` / `Mir2AtlasLibrary` / `Mir2AtlasManifest`）互相印证得出。
> 探查方法：把 5.58MB 包整读进内存，统计关键 token 出现次数并提取少量上下文
> （`search_content` 因 ripgrep 把大包当二进制跳过，改用 PowerShell 内存探针）。

### 1. 引擎
- **Egret（白鹭引擎）**，资源系统走 `RES`：`RES.getRes` / `RES.getResAsync` / `RES.loadGroup` /
  `RES.ResourceEvent.ITEM_LOAD_ERROR` / `RES.off|on` 在包内大量出现。
- 图片走 Egret 的 `ImageLoader`（浏览器原生解码 → RGBA）；纹理类型 `egret.Texture`；
  加载完成事件 `egret.Event.COMPLETE`。

### 2. CDN 基址不硬编码 —— 由宿主页注入（部署与代码解耦）
- 包内**没有** `fugu_20201203` 这类 CDN 字面量（探查计数 = 0）。CDN 根由宿主页运行时注入 `js_gameVars`：
  - `js_gameVars.resUrl`   → 图片 CDN（ImageServer）
  - `js_gameVars.configUrl`→ 数据服务器（ResUrl.Data，地图 .bin/.dat 在这里；dev 默认 `192.168.5.66:8082`）
  - `js_gameVars.resVersion` / `publish` / `ip` / `mgPath` 等
- 所有 URL 由 `ResUrl`（包内出现 **268** 次）按 `js_gameVars` + 相对路径现拼
  （上下文证据：`assets/gameui4/...`、`assets/zClientResourceVersion/{ver}/...`、
  `ResUrl.mgResName[js_gameVars.mgPath]`）。
- → **同一份 bundle 换 `js_gameVars` 即可对接不同区服 / CDN**。这是它“做得好”的第一点。

### 3. 何时加载（加载时序 / 状态机）
1. 宿主页注入 `js_gameVars` → 加载并执行 `main.min.js`。
2. 引导：引擎初始化 + 工具库（文件头即为 `getNetworkType`、整套 `md5` 函数等）。
3. 拉**引导元数据**（文件名在包内是**拼接**的：`"0config"+版本` 计 1 次、`"allmanifest"+版本` 计 0 次但确由拼接生成）：
   - `allmanifest<版本>.json` —— 图集 + UI + 配置 **总索引**（每条 `{v:hash, s:size}`）
   - `resourceVersion.json` —— model / icon 版本映射（计 5 次）
   - `0config<版本>.json`   —— 游戏 / 地图配置表
4. `RES.loadConfig` 注册 manifest → 逻辑名可解析。
5. **预加载资源组**：`RES.loadGroup(name)` 按场景 / 阶段分组加载，`loadingState` 状态机推进；
   日志 `loadingState 预加载资源组加载成功` → `createScene()`（包内 `loadingState` 计 4 次，多状态）。
   → **只加载当前阶段需要的组**，不做一次全量下载，加载屏平滑。这是“做得好”的第四点（分阶段流式加载）。
6. 进场后：精灵按名字 `RES.getRes`（已加载则同步返回）/ `getResAsync`（否则异步 + COMPLETE 回调）取；
   图集按需加载；帧按名引用。
7. 游戏内：地图瓦片 `zipmap/{img}/{index}.jpg` 随移动**流式**拉取（cols×rows 网格，不一次全下）；
   地图逻辑 `.bin/.dat` 从数据服务器取并客户端解压（~14MB）；模型精灵 `model/{id}/{srcId}.png` 按实体加载。

### 4. 资源寻址规则（manifest → 内容哈希 URL）
- 总索引 `allmanifest` 每条：`key` = 逻辑资源名（如 `.../y_homeUI-sheet.png`），`value = {v:8位hash, s:字节数}`。
- 实际 CDN 文件 **不在 key 路径**，而由 `v/s` 拼出：
  `resource/{v前2位}/{v}_{s}.{key扩展名}`
  例：`y_homeUI-sheet.png` v=68b4b394 s=354142 → `resource/68/68b4b394_354142.png`
- 因为**文件名就是内容哈希** → 资源不可变，CDN 可永久缓存、无需 revalidation、无需 cache-busting；
  版本升级只改 manifest，旧资源仍命中缓存。这是“做得好”的第二点（不可变内容寻址）。
- 地图瓦片 / 模型精灵走独立规则（不进 manifest），见上文“地图核心数据”与“模型精灵”段。

### 5. 图集（atlas / sprite sheet）
- 包内无 `atlas` 字面量（计 0），但 UI 以 **`*-sheet.png` + `*-sheet.json`** 的 Egret 图集形态打包
  （探查见 `m.name.indexOf("_json")`、`m.name.slice(0, m.name.indexOf("_json"))`、
  `assets/gameui4/mGameRes...`、`assets/zClientResourceVersion/...`）。
- 帧按名引用（如 `y_homeUI-sheet` 内的 `bg_zhukuang_1`）。
  → 把成千上万张小 UI 图压成少数大纹理：**HTTP 请求少 + GPU 合批友好**。这是“做得好”的第三点。

### 6. 容错
- 每资源错误独立处理：`RES.ResourceEvent.ITEM_LOAD_ERROR` / `onItemLoadError`
  （日志 `Url:... has failed`）→ 单个资源失败不拖垮整批加载。

### 7. 与 C# 客户端（WebGame.China.Mir2.Client）的对应（逆向落点）
- `Mir2OriginResource`：复刻 `ResUrl` 的「相对路径 → 资源」映射，经 `ContentManager` 走 HTTP 异步
  + Cache Storage + 优先级调度；CDN / 本地镜像一行 `Configure(...)` 切换。
- `Mir2AtlasLibrary` / `Mir2AtlasManifest`：复刻 manifest 解析（`key → resource/{v前2}/{v}_{s}.{ext}`）
  与 `*-sheet.json` 帧布局解析，喂给 `MirImageControl` 的 `AtlasSheet` / `AtlasFrame`。

### 附：包内关键 token 计数（内存探针）
```
js_gameVars=591  resUrl=3  configUrl=2  ResUrl=268
allmanifest=0（拼接） zipmap=2  resourceVersion=5  loadConfig=5
loadGroup=3  getRes=89  loadingState=4  ImageLoader=1  RES=235  0config=1（拼接）
atlas=0（用 *-sheet.json） fugu_20201203=0（由 js_gameVars 注入，不在包内）
```
