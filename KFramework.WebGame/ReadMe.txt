AI 协作约定（代码注释与命名）
================================

【注释】
- 类（class）或文件头（file header）的注释可以写长：说明整体设计、为什么这么写、
  跨语言（C# <-> TS）对应关系、关键约束等，越清楚越好。
- 方法（method / 模块函数）的注释顶多一行：一句话说清“做什么 / 关键不变量”，
  不要展开原理、逐个列参数说明、也不要写长篇注意事项。
  - 需要强调的坑（如 MemoryView 必须拷成 Uint8Array、byte[] 复制语义会把字节丢在 JS 副本）
    浓缩进那一行，或只写在实现里的行内注释（//）。
  - 适用范围：库代码（KFramework.MonoGame、KFramework.TSEngine）严格遵守；纯工具/脚本可放宽。

【命名：JS <-> C# 互操作】
- TS 侧导出的模块函数名 == C# 的 [JSImport] 字符串 == C# JSBind_* 类的静态方法名，
  三者完全一致（PascalCase + Async 后缀）。
  例：C# JSBind_CacheStorage.GetCacheSizeAsync
    <-> [JSImport("GetCacheSizeAsync", "cachestorage")]
    <-> TS export async function GetCacheSizeAsync(...)
- 不要给 TS 模块函数加 Caching 前缀（Caching 是 C# 封装类名）；动词直接开头：
  Get / Load / Save / Remove + 对象 + Async。
- C# 的 Caching 封装类只持有“缓存名（string）”，不持有 JS 对象句柄
  （避免句柄泄漏、且能继续走 [JSImport] 的源生成 / AOT）；
  一切调用都经 JSBind_* 的模块函数，由 TS 侧按名池化真正的 Cache 句柄。

【C# 写法】
- 禁止使用表达式体成员（=> 简写）：属性 getter、方法、lambda 之外的 => 一律展开成
  带 { } 的语句体。
  例：
    // 禁止
    public int Count => _n;
    public Task<int> GetAsync() => JSBind.X();
    // 必须写成
    public int Count { get { return _n; } }
    public Task<int> GetAsync() { return JSBind.X(); }
  原因：统一可读性、避免“每次访问是否执行”“是否为同一引用”这类隐含语义被忽略。

【渲染资源与生成产物】
- JS 是 TS 的自动生成产物：修改渲染逻辑只改 KFramework.TSEngine/src/*.ts 源文件，
  由构建（tsc）生成 dist/jsengine/*.js，再由 SyncJsEngine 复制到各 wwwroot/jsengine；
  不要手改生成的 .js（它们是产物，会被重新生成覆盖）。

================================
KFramework 内容系统架构
================================

核心约定（务必遵守）：ContentManager 只负责 AssetBundle 的异步加载 / 卸载；
具体资源的取出（纹理 / JSON / 文本 / 图集切片）全部在 AssetBundle 上以同步方式完成。
ContentManager 不提供任何“取资源”的同步或异步方法（唯一的例外是下面说明的松散文件下载）。

1. 两个类的职责边界
====================

ContentManager（位于 KFramework.MonoGame/Content/ContentManager.cs）
只做一件事：把 AssetBundle 拉进内存。

- LoadManifestAsync() —— 拉取总清单 version.manifest（仅包列表 + 哈希）。
- LoadAsync() —— 拉取并加载清单里的全部 Bundle。
- LoadBundleAsync(name) / LoadBundlesAsync(names) —— 异步加载单个 / 多个 Bundle。
- UnloadBundle(name) / Dispose() —— 卸载。
- GetBundle(name) / TryGetBundle(name, out ...) —— 取出一个已加载的 AssetBundle。
- LoadedBundles —— 已加载的 Bundle 逻辑名列表。
- DownloadTextAsync / DownloadBytesAsync —— 唯一例外：异步下载不在任何 Bundle 内的松散文件
  （如关卡文本 Levels/00.txt），按页面基址直接 HTTP 获取。这不是 Bundle 资源，故不违反上面的边界。

注意：ContentManager 的构造函数不再接收 GraphicsDevice——上传 GPU 的职责已下沉到 AssetBundle。

AssetBundle（位于 KFramework.MonoGame/Content/AssetBundle/AssetBundle.cs）
包一旦驻留内存，取资源是即时操作（对齐 Unity 的 AssetBundle.LoadAsset 同步语义）。
真正的异步只发生在“拉包”本身，取资源不需要网络，因此全部为同步方法：

- byte[] LoadAsset(name) / bool TryGetAsset(name, out byte[]) —— 原始字节（Unity LoadAsset<TextAsset>）。
- string LoadText(name) —— 文本（UTF-8，去 BOM）。
- T? LoadJson<T>(name) —— JSON 反序列化（图集的 AtlasData 即由此读取）。
- Texture2D LoadTexture(name, GraphicsDevice device) —— 取整张纹理（按 RGBA8 直接 device.CreateTexture 上传 GPU）。
  注意：图集不再以“子图切片”形式存在，故 AssetBundle 不再支持 Page >= 0 的切片；
  若对带 Page 标记的旧资源名调用，会抛 InvalidOperationException。整页纹理（如图集页 atlas_0）正常上传。
- bool TryLoadTexture(name, GraphicsDevice device, out Texture2D? tex) —— 取不到返回 false。
- 图集精灵请改用 SpriteSheetLoader（见第 6 节），不要直接用 LoadTexture 取子图。
- bool Contains(name) / AssetBundleEntry? GetAssetInfo(name) / IReadOnlyList<string> AssetNames —— 元信息。

因为上传 GPU 需要设备，所以所有取纹理的方法都接收 GraphicsDevice 参数（由调用方在加载阶段传入）。

2. 为什么要这样分（与 Unity 对齐）
==================================

Unity 的 AssetBundle 设计就是：
- AssetBundle.LoadFromFileAsync / LoadAssetAsync 负责“把包载进内存”（异步、可能走磁盘/网络）；
- AssetBundle.LoadAsset 负责“从已加载的包里取资源”（同步、纯内存、即时）。

我们把同一套语义映射到：
- ContentManager ≈ Unity 的“加载包”（异步 LoadBundleAsync）；
- AssetBundle ≈ Unity 的“包内取资源”（同步 LoadAsset / LoadTexture）。

好处：
1. 加载阶段集中、可上报进度（IProgress<float>），与渲染/逻辑解耦；
2. 运行时取资源零等待、写法与 Unity 一致，不需要到处 await；
3. ContentManager 不依赖 GraphicsDevice，可在无 GPU 环境（如纯打包/测试）使用。

3. 标准使用范式
==============

    // 1) 异步把包载进内存（在 Game.LoadContentAsync 或场景初始化里）
    await Content.LoadAsync(new Progress<float>(p => _progress = p));

    // 2) 取出已加载的包
    AssetBundle bundle = Content.GetBundle("content")
        ?? throw new InvalidOperationException("内容包 content 尚未加载");

    // 3) 之后所有取资源都是同步的
    GameConfig cfg   = bundle.LoadJson<GameConfig>("data/game");
    Texture2D player = bundle.LoadTexture("sprites/player", GraphicsDevice);   // 整张纹理
    string    level  = bundle.LoadText("levels/00");

    // 4) 图集：用 SpriteSheetLoader 取整页图集，得到可批处理的 KSpriteInfo
    SpriteSheet sheet = new SpriteSheetLoader(bundle, GraphicsDevice).Load("atlas");
    KSpriteInfo playerSprite = sheet.Sprite("sprites/player");

4. 内容管线（raw -> kfc -> content）
===================================

资源由 KFramework.Content.Cli（kfc）构建：kfc --root <示例目录>/Content（见各示例 csproj 的 prebuild 目标）。

- 源：各示例/Content/raw/
  - *.png / *.sprite.json（矢量形状）-> 进入图集，资源名 = 去扩展名小写（如 sprites/player）；
  - *.json（非 .sprite.json）/ *.txt / *.csv -> 作为数据资源（如 data/game）；
  - *.wav / *.mp3 / *.ogg -> 作为音频字节资源（如 audio/shoot）。

4.1 打包目录（AssetBundle 拆分，推荐）
--------------------------------------
为更通用，raw/ 下用打包配置文件指定一个「打包根目录」，其下每个含资源的子文件夹分别打成一个 AssetBundle（Unity 风格：包名 = 文件夹相对路径）：

- 配置文件：raw/bundles.json（或 pack.json）。bundlesDir（别名 bundleDirs）字段支持字符串或字符串数组，可指定多个打包根目录：
  - 单目录： { "bundlesDir": "Bundles" }
  - 多目录： { "bundlesDir": ["Bundles", "UI"] }
  - 缺省默认 Bundles；若 bundles.json/pack.json 都不存在，kfc 会自动生成一个默认的 bundles.json（打包目录 Bundles），并回退为整包 content。
  - 多个根目录下的子文件夹包名必须唯一（包名 = 子文件夹相对其根目录的路径），出现同名会报错。
- 输出目录与发布方式（同样写在该配置文件中）：
  - outDir：打包产物目录，相对 root（即 --root 指向的目录），默认 content；CLI --out 可临时覆盖。
  - deploy：发布方式，默认 www；可选 www（把产物整体镜像复制到 wwwDir，默认 www）/ serve（在产物目录上启动本地 HTTP 服务，端口 port 默认 8080）/ none（不发布）。
  - 完整示例：{ "bundlesDir": "Bundles", "outDir": "content", "deploy": "www", "wwwDir": "www", "port": 8080 }
- Bundles/data/game.json -> 包 data，资源 data/game；
  Bundles/sprites/player.sprite.json -> 包 sprites，资源 sprites/player。
- 每个文件夹只打包其直接资源，不含子目录资源；子目录本身是独立的 AssetBundle。
- 用法：Content.GetBundle("data").LoadJson<GameConfig>("data/game") / Content.GetBundle("sprites").LoadTexture("sprites/player", GraphicsDevice)。

4.2 兼容模式
------------
若 raw/ 下不存在该打包根目录，则回退为整包：把整个 raw/ 作为单个 content 包（旧用法，资源名相对 raw/）。此时用 Content.GetBundle("content") 取资源。

- 产物：wwwroot/content/version.manifest + 每个包一个 *.web.lib（zip，内含整张纹理 PNG、AtlasData JSON 等资源）。总清单列出全部 Bundle，ContentManager.LoadBundleAsync(name) 按名加载任意包；图集页用 SpriteSheetLoader.Load("atlas") 取出。

5. 图集格式与 SpriteSheetLoader（统一加载方式）
===============================================

图集打包复用 KTexturePacker（KTexturePacker.Core）的同一份代码：ContentBuilder（即 kfc）直接引用
Need_DLL/KTexturePacker.Core.dll，用 AtlasPacker.PackPages + AtlasExporter.ToGenericJson 产出与
KTexturePacker 一致的 AtlasData JSON。框架不再自带重复的打包实现。

打包产物（每个图集）由两部分组成，统一以资源名 atlas 暴露：

- atlas —— AtlasData JSON（Pages[].Image/Width/Height/Regions[]，Region 含 Name/X/Y/W/H/Rotated/SourceW/SourceH）。
- atlas_0、atlas_1 …… —— 各图集页的整张 PNG（资源类型为 texture）。

注意：资源名由 ContentBuilder 的 AtlasBaseName（默认 "atlas"）决定，多页时页序号追加到页 PNG 名（atlas_{i}.png），
JSON 里的 Image 字段即指向这些页 PNG。

运行时统一用 SpriteSheetLoader 加载（所有图集都用这种方式，不要用 LoadTexture 取子图）：

    SpriteSheet sheet = new SpriteSheetLoader(bundle, GraphicsDevice).Load("atlas");
    KSpriteInfo playerSprite = sheet.Sprite("sprites/player");   // 资源名 = 去扩展名小写
    if (sheet.Contains("sprites/enemy")) { /* ... */ }

    // 绘制：以精灵中心点对齐（自动处理 Rotated）
    _batch.DrawCentered(playerSprite, position, Color.White);
    // 或左上角对齐
    _batch.Draw(playerSprite, position, Color.White);

KSpriteInfo 携带 .Texture（整页纹理）、.SourceRectangle（页内矩形）、.IsRotated（旋转 90° 标记）、
.Origin（默认左上）。因整页纹理可被多个精灵共享并一起批处理，旋转精灵的绘制由扩展方法在 origin/rotation 上做换算。

6. 已知约束 / TODO
==================

- 构建阶段（kfc）只打包 raw/ 中存在的资源；缺失的资源在运行时才会 KeyNotFoundException，
  因此每个示例都需要一份对应的 Content/raw/ 样例内容（见各示例 Content/raw/）。
- 图集页 PNG 由 KTexturePacker.Core 经 SkiaSharp 合成；Need_DLL/KTexturePacker.Core.dll 需与
  SkiaSharp 版本匹配（见 KFramework.Content.Cli.csproj 的 PackageReference）。
