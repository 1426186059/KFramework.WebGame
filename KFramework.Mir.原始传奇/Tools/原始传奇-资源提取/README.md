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
