# KFramework 内容系统架构

> **核心约定（务必遵守）**：`ContentManager` 只负责 AssetBundle 的**异步加载 / 卸载**；
> 具体资源的取出（纹理 / JSON / 文本 / 图集切片）全部在 `AssetBundle` 上以**同步**方式完成。
> `ContentManager` 不提供任何“取资源”的同步或异步方法（唯一的例外是下面说明的松散文件下载）。

---

## 1. 两个类的职责边界

### `ContentManager`（位于 `KFramework.MonoGame/Content/ContentManager.cs`）
只做一件事：**把 AssetBundle 拉进内存**。

- `LoadManifestAsync()` —— 拉取总清单 `version.manifest`（仅包列表 + 哈希）。
- `LoadAsync()` —— 拉取并加载清单里的全部 Bundle。
- `LoadBundleAsync(name)` / `LoadBundlesAsync(names)` —— 异步加载单个 / 多个 Bundle。
- `UnloadBundle(name)` / `Dispose()` —— 卸载。
- `GetBundle(name)` / `TryGetBundle(name, out ...)` —— 取出一个**已加载**的 `AssetBundle`。
- `LoadedBundles` —— 已加载的 Bundle 逻辑名列表。
- `DownloadTextAsync` / `DownloadBytesAsync` —— **唯一例外**：异步下载**不在任何 Bundle 内**的松散文件
  （如关卡文本 `Levels/00.txt`），按页面基址直接 HTTP 获取。这不是 Bundle 资源，故不违反上面的边界。

> `ContentManager` 的构造函数**不再接收 `GraphicsDevice`**——上传 GPU 的职责已下沉到 `AssetBundle`。

### `AssetBundle`（位于 `KFramework.MonoGame/Content/AssetBundle/AssetBundle.cs`）
包一旦驻留内存，取资源是即时操作（对齐 Unity 的 `AssetBundle.LoadAsset` 同步语义）。
真正的异步只发生在“拉包”本身，取资源不需要网络，因此全部为**同步**方法：

- `byte[] LoadAsset(name)` / `bool TryGetAsset(name, out byte[])` —— 原始字节（Unity `LoadAsset<TextAsset>`）。
- `string LoadText(name)` —— 文本（UTF-8，去 BOM）。
- `T? LoadJson<T>(name)` —— JSON 反序列化。
- `Texture2D LoadTexture(name, GraphicsDevice device)` —— 取一张纹理：
  - 图集子图（清单里 `Page ≥ 0`）→ 按 `Page/X/Y/Width/Height` 从对应图集页切片；
  - 整张（图集原始页等）→ 按 RGBA8 直接 `device.CreateTexture` 上传 GPU。
- `bool TryLoadTexture(name, GraphicsDevice device, out Texture2D? tex)` —— 取不到返回 `false`。
- `Texture2D LoadAtlasPage(int page, GraphicsDevice device)` —— 上传指定图集页。
- `bool Contains(name)` / `AssetBundleEntry? GetAssetInfo(name)` / `IReadOnlyList<string> AssetNames` —— 元信息。

因为上传 GPU 需要设备，所以**所有取纹理的方法都接收 `GraphicsDevice` 参数**（由调用方在加载阶段传入）。

---

## 2. 为什么要这样分（与 Unity 对齐）

Unity 的 `AssetBundle` 设计就是：
- `AssetBundle.LoadFromFileAsync` / `LoadAssetAsync` 负责“把包载进内存”（异步、可能走磁盘/网络）；
- `AssetBundle.LoadAsset` 负责“从已加载的包里取资源”（同步、纯内存、即时）。

我们把同一套语义映射到：
- `ContentManager` ≈ Unity 的“加载包”（异步 `LoadBundleAsync`）；
- `AssetBundle` ≈ Unity 的“包内取资源”（同步 `LoadAsset` / `LoadTexture`）。

好处：
1. 加载阶段集中、可上报进度（`IProgress<float>`），与渲染/逻辑解耦；
2. 运行时取资源零等待、写法与 Unity 一致，不需要到处 `await`；
3. `ContentManager` 不依赖 `GraphicsDevice`，可在无 GPU 环境（如纯打包/测试）使用。

---

## 3. 标准使用范式

```csharp
// 1) 异步把包载进内存（在 Game.LoadContentAsync 或场景初始化里）
await Content.LoadAsync(new Progress<float>(p => _progress = p));

// 2) 取出已加载的包
AssetBundle bundle = Content.GetBundle("content")
    ?? throw new InvalidOperationException("内容包 content 尚未加载");

// 3) 之后所有取资源都是同步的
GameConfig cfg   = bundle.LoadJson<GameConfig>("data/game");
Texture2D player = bundle.LoadTexture("sprites/player", GraphicsDevice);
string    level  = bundle.LoadText("levels/00");
```

---

## 4. 内容管线（raw → `kfc` → content）

资源由 `KFramework.Content.Cli`（`kfc`）构建：`kfc --root <示例目录>/Content`（见各示例 csproj 的 prebuild 目标）。

- 源：`各示例/Content/raw/`
  - `*.png` / `*.sprite.json`（矢量形状）→ 进入图集，资源名 = 去扩展名小写（如 `sprites/player`）；
  - `*.json`（非 `.sprite.json`）/ `*.txt` / `*.csv` → 作为数据资源（如 `data/game`）；
  - `*.wav` / `*.mp3` / `*.ogg` → 作为音频字节资源（如 `audio/shoot`）。

### 4.1 打包目录（AssetBundle 拆分，推荐）
为更通用，`raw/` 下用**打包配置文件**指定一个「打包根目录」，其下每个**含资源的子文件夹**分别打成一个 `AssetBundle`（Unity 风格：包名 = 文件夹相对路径）：

- 配置文件：`raw/bundles.json`（或 `pack.json`）。`bundlesDir`（别名 `bundleDirs`）字段支持**字符串或字符串数组**，可指定多个打包根目录：
  - 单目录： `{ "bundlesDir": "Bundles" }`
  - 多目录： `{ "bundlesDir": ["Bundles", "UI"] }`
  - 缺省默认 `Bundles`；若 `bundles.json`/`pack.json` 都不存在，`kfc` 会**自动生成**一个默认的 `bundles.json`（打包目录 Bundles），并回退为整包 `content`。
  - **多个根目录下的子文件夹包名必须唯一**（包名 = 子文件夹相对其根目录的路径），出现同名会报错。
- 输出目录与发布方式（同样写在该配置文件中）：
  - `outDir`：打包产物目录，相对 `root`（即 `--root` 指向的目录），默认 `content`；CLI `--out` 可临时覆盖。
  - `deploy`：发布方式，默认 `www`；可选 `www`（把产物整体镜像复制到 `wwwDir`，默认 `www`）/ `serve`（在产物目录上启动本地 HTTP 服务，端口 `port` 默认 8080）/ `none`（不发布）。
  - 完整示例：`{ "bundlesDir": "Bundles", "outDir": "content", "deploy": "www", "wwwDir": "www", "port": 8080 }`
- `Bundles/data/game.json` → 包 `data`，资源 `data/game`；
  `Bundles/sprites/player.sprite.json` → 包 `sprites`，资源 `sprites/player`。
- **每个文件夹只打包其直接资源，不含子目录资源**；子目录本身是独立的 AssetBundle。
- 用法：`Content.GetBundle("data").LoadJson<GameConfig>("data/game")` / `Content.GetBundle("sprites").LoadTexture("sprites/player", GraphicsDevice)`。

### 4.2 兼容模式
若 `raw/` 下不存在该打包根目录，则**回退为整包**：把整个 `raw/` 作为单个 `content` 包（旧用法，资源名相对 `raw/`）。此时用 `Content.GetBundle("content")` 取资源。

- 产物：`wwwroot/content/version.manifest` + 每个包一个 `*.web.lib`（zip，内含图集页 + 切片索引）。总清单列出全部 Bundle，`ContentManager.LoadBundleAsync(name)` 按名加载任意包。

---

## 5. 已知约束 / TODO

- **Example3 的旧图集加载方式待对齐**：`SpriteSheetLoader` 当前按“AtlasData JSON + 整张 PNG”的老格式取图，
  与新管线的“单个 sprite → 自动打包成图集页 → 子图切片”不一致。需要把 Example3 也改为
  “每个精灵一个 `*.sprite.json` + 用 `AssetBundle.LoadTexture` 取子图”，或在 `ContentBuilder` 中保留老图集描述格式。
- 构建阶段（`kfc`）只打包 `raw/` 中存在的资源；缺失的资源在**运行时**才会 `KeyNotFoundException`，
  因此每个示例都需要一份对应的 `Content/raw/` 样例内容（见各示例 `Content/raw/`）。
