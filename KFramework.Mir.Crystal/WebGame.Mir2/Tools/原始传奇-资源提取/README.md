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

## 常量速查
- `MAP_TILE_WIDTH = 512`，`MAP_TILE_HEIGHT = 256`
- `MapBackGround = "zipmap/"`，`ImageServer = js_gameVars.resUrl`
- `ConfigDataBinExt=".bin"`, `ConfigZipExt=".dat"`, `ConfigDataExt=".csv"`
- 瓦片命名函数：`ResUrl.url(index+".jpg", 3, img)` → `ImageServer + "zipmap/" + img + "/" + index + ".jpg"`
