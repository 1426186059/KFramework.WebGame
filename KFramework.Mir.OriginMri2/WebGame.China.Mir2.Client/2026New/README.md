# 远程图片内存缓存（Mir2OriginResource）

## 背景
之前每个远程图（模型图集 / UI 图集 / 人物立绘 / 物品图标 / 地图瓦片）每次取纹理都走
`ContentManager.LoadTexture2DAsync`：重新下载字节（即便命中 Cache Storage 也新建 `byte[]`）
+ `createImageBitmap` 解码 + `device.CreateTexture` 建一张新 GPU 纹理。同一 URL 被多业务
重复请求时会重复解码、重复建纹理，浪费 CPU 与显存；且全 Client 没有任何 `Texture2D.Dispose()`
调用，换装 / 切图后 GPU 纹理句柄只增不减。

## 方案（对齐原始传奇 Egret：RES.host + CacheCollection）
缓存统一放在**资源门面层 `Mir2OriginResource`**（所有远程图的单一入口），不放在引擎
`ContentManager`，也不散落到各业务库。

- **缓存对象**：解码后的 `Texture2D`，`key = Norm(relativePath)`（规范化相对路径）。
- **并发合并**：`ConcurrentDictionary<string, TexEntry>` + `GetOrAdd`，同一 URL 只在首次
  下载 + 解码，之后所有调用方共享同一份，天然 in-flight 去重（对齐原版 `promiseHash`）。
- **引用计数**：`TexEntry.RefCount`，`GetTextureAsync` 时 +1，上层不再需要时 `ReleaseTexture`
  -1。保证「上层 `Mir2AtlasSheet` 持有的同一纹理」不会被本层单方面 `Dispose` 而悬空。
- **淘汰**：`TrimTextureCache()` 在容量超 `TexCacheMaxEntries`(1500) 且距上次扫描 ≥
  `TexTrimIntervalMs`(1500ms) 时，把「`refCount==0` 且超过 `TexTrimAgeMs`(30s) 未再使用」
  的纹理 `Dispose()` 并移除。只释放无人引用的纹理，绘制不会崩（对齐原版
  `CacheManager.update` 每 1500ms 定量 `texture.dispose()`）。
- **显式释放**：`ReleaseTextures(Func<string,bool> match)` 按前缀 / 场景批量释放（对齐原版
  `RES.destroyRes` 按组 / 场景卸载），供切图、关 UI 时调用。

## 接口
- `Task<Texture2D> GetTextureAsync(path, device, priority, ct)` —— 取纹理（自动 Acquire +1）。
- `void ReleaseTexture(path)` —— 释放某纹理引用（refCount-1）。
- `void ReleaseTextures(Func<string,bool>)` —— 按匹配批量释放。

## 上层使用约定
- 模型图集 / 物品图标：经 `Mir2OriginObjectLibrary` 加载时内部调用 `GetTextureAsync`
  （Acquire）；其 `_sheets/_images` 缓存淘汰或加载失败移除时，必须调用
  `Mir2OriginResource.ReleaseTexture(对应路径)`，否则该纹理引用计数不归零、不会被释放
  （会常驻，行为与原版 `RES.host` 一致）。
- 显式释放场景：地图切换、角色 / 怪物大量离开时，调用
  `ReleaseTextures(p => p.StartsWith(...))` 批量回收显存。

## 调参
`TexCacheMaxEntries` / `TexTrimAgeMs` / `TexTrimIntervalMs` 在 `Mir2OriginResource.cs`
顶部常量，可按实机显存 / 内存曲线调整（WASM 显存受限，宁小勿大）。

## 与原版对照
| | 我们 | 原版 Egret |
|---|---|---|
| 缓存层 | Mir2OriginResource（资源门面） | RES.host（root+name→解码 Texture）+ 自定义 CacheCollection |
| 去重 | GetOrAdd in-flight 合并 | promiseHash + sourceId.inuse |
| 释放 | 引用计数 + 定时定量 Dispose | 引用计数 + CacheManager.update 每 1500ms 定量 dispose |
| GPU 释放 | Texture2D.Dispose | texture.dispose |

## 编码规范（2026New 目录）
本目录统一禁用以下写法，新代码与后续重构都必须遵守：

1. **禁止 `#region` / `#endregion`**：代码分区用空行 + `// =====` 注释即可，不允许用 `#region` 折叠。
2. **禁止表达式体成员（用 `=>` 定义方法 / 属性 / 索引器 / 运算符 / 构造器）**：
   一律用传统大括号方法体。例如：
   - 禁止：`public int Foo => Bar;` 或 `public int Foo() => Bar();`
   - 必须：`public int Foo() { return Bar(); }` 或 `public int Foo { get { return Bar; } }`
   - 含 `get` / `set` 的也展开为 `{ get { ... } }` / `{ set { ... } }`。

说明：上述禁令针对**成员定义**的 `=>` 简写（expression-bodied members）。
匿名函数（lambda，如 `x => x.Foo()`、`(a, b) => ...`、事件回调 `() => { ... }`）
是语言级函数字面量，不在禁令范围，仍可使用 `=>`。请勿将二者混淆。
