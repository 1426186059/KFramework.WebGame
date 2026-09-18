KFramework.MonoGame —— 仿 MonoGame 引擎（面向 AI 智能体）
================================================

本库是 MonoGame 游戏引擎的 C# 仿制实现，运行目标为 WebAssembly（浏览器），
通过 JSImport 桥接到 WebGL / WebAudio / 浏览器平台能力。

为了让代码结构、命名与 MonoGame 尽量保持一致，请以官方仓库为权威参照：

    MonoGame 官方仓库： https://github.com/MonoGame/MonoGame
    MonoGame 文档：     https://docs.monogame.net/

约定（请严格遵守，保持与上游一致）
-----------------------------------
1. 公共 API 尽量对齐 MonoGame：
   Game / GameTime / GraphicsDevice / SpriteBatch / Texture2D / Vector2、
   SoundEffect / SoundEffectInstance / MediaPlayer / SoundState / MediaState 等。
   命名、参数顺序、行为以 MonoGame 为准。

2. 跨语言调用（C# ⇄ JS）集中放在 JSBind/ 目录。
   业务封装类不要直接写 [JSImport]，一律调用 JSBind_* 静态类。

3. 本库只放“通用游戏引擎能力”，不放任何具体游戏的业务与资源：
   - 不应出现关卡逻辑、具体数值平衡；
   - 不应出现合成音效、具体音色等“杂七杂八”的内容；
     合成音效属于使用方（Example 工程）自己实现的工具，不是引擎的一部分。
   - 需要集中控制浏览器 AudioContext（解锁 / 主音量 / 静音）用 AudioMaster。

4. 资源通过 KFramework.Content 内容管线加载：
   图片走 Texture2D，文本/JSON 走 LoadText/LoadJson，
   原始字节（如 wav/mp3）走 LoadBytes，再交给 SoundEffect.FromBytes 解码。

命名空间约定
------------
- 本库源码统一使用单一命名空间 KFramework.MonoGame（不再细分 Graphics / JSBind / Content / CoroutineIEnumerator 等子命名空间，所有类型都直接位于 KFramework.MonoGame 之下），
  一律采用传统大括号块式（namespace KFramework.MonoGame { }）；程序集名为 KFramework.MonoGame，外部以 using KFramework.MonoGame 引用。
- 工程的 csproj <RootNamespace> 设为 KFramework.MonoGame（仅作为在 IDE 中新建文件的默认命名空间元数据，不影响已有源码命名空间与任何引用）。
- 一律使用传统的“大括号块式”命名空间声明：
      namespace KFramework.MonoGame
      {
          ...
      }
  不使用 C# 10 的文件作用域写法（行尾带分号的 namespace KFramework;）。
- 本库不分子命名空间：游戏宿主（Game / GameTime）、图形（GraphicsDevice / SpriteBatch / Texture2D）、音频（SoundEffect / MediaPlayer / AudioMaster）、输入（Input / Keys）、数学（Vector2 / Color / Rectangle）、JS 绑定（JSBind_*）、协程（Coroutine*）、内容管理（ContentManager）等全部直接位于 KFramework.MonoGame 之下。
- 上层扩展层 KFramework.MonoGameExtend 使用命名空间 KFramework.MonoGameExtend（亦为大括号块式），
  与本库互不冲突；本库只提供基础引擎能力，详见 KFramework.MonoGameExtend/ReadMe.txt。

目录速览
--------
  Core/      Game、GameTime、GameHost 等生命周期
  Graphics/  GraphicsDevice、SpriteBatch、Texture2D、效果与状态
  Fonts/     SpriteFont（系统/自定义字体）、KFont（引擎内自绘矢量字体）、BitmapFont（美术字）、BmFontData（.fnt 解析）、FontStyle、IFont
  Audio/     SoundEffect、SoundEffectInstance、MediaPlayer、AudioMaster（通用音频）
  Input/     Input
  Content/   ContentManager（运行时取资源，含 LoadBytes）
  Math/      向量与数学
  JSBind/    C# ⇄ JS 绑定（JSImport 集中地，业务不要碰）
  KFramework.TSEngine/  JS 侧引擎（gl / text / audio / platform / main），被所有 Example 共享

音频说明
--------
  真实音频（wav/mp3/ogg）：SoundEffect.FromBytes(bytes, mime) → 浏览器 decodeAudioData 解码 →
                           SoundEffect.Play / CreateInstance / MediaPlayer 播放。
  合成音效：不属于本库；若 Example 需要零资源原型音效，可自行用 WebAudio 合成
          （audio 模块的 playSynth），但本仓库示例统一使用真实 wav（见 Example2 的 SoundCenter），
          不内置合成示例，以保持引擎通用、干净。

字体说明
--------
  四类字体来源，统一走 IFont（SpriteBatch.DrawString / KLabel / KDefaultRes.DefaultSpriteFont 都接受它）。
  字体相关实现集中在 Fonts/ 目录。字形位图的来源有两条路：1)~2) 借浏览器 Canvas2D（JSBind/JSBind_Text.cs），
  3) 由引擎自己在 C# 侧解析字体轮廓并光栅化（不再碰 Canvas），4) 直接用美术产出的图集。

  1) 系统字体 —— SpriteFont：给 Canvas2D 一个 CSS font 串实时光栅化，不需要任何字体资源文件。
       new SpriteFont(device, 24f, "system-ui, sans-serif")
       new SpriteFont(device, 24f, "Arial", FontStyle.Bold | FontStyle.Italic, weight: 600, letterSpacing: 1f)
     样式参数：FontStyle（Bold / Italic / Oblique，可位组合）、weight（1~1000，0 表示由 Bold 决定）、
     FontStretch（font-stretch 各档）、letterSpacing（字距，浏览器不支持时自动忽略）。

  2) 自定义字体（ttf / otf / woff）—— 先注册到 document.fonts，再当普通 family 用：
       await SpriteFont.RegisterFontAsync("MyFont", bytes);        // 字节可来自 AssetBundle / LoadBytesAsync
       var font = new SpriteFont(device, 24f, "MyFont");
     或一步到位：await SpriteFont.FromFontAsync(device, "MyFont", 24f, bytes);（另有 URL 重载）
     注意：字体必须在光栅化之前注册完成，故注册是异步的（通常放在 LoadAsync 阶段做一次）。

  3) 引擎内自绘（ttf / otf / woff 字节）—— KFont：C# 自己解析字形轮廓并光栅化，全程不碰 Canvas2D：
       var font = KFont.FromBundle(ab, GraphicsDevice, "myres/fonts/hud.ttf", 24f);
       var font = KFont.FromBytes(GraphicsDevice, 24f, bytes, atlasSize: 2048, obliqueDegrees: 12f, boldPixels: 1);
       batch.DrawString(font, "你好 SCORE 0123", pos, Color.White);
     绘制仍走 SpriteBatch（WebGL 合批），只是字形位图不再来自浏览器；因此不受浏览器字体环境影响，
     也不需要异步注册（构造即就绪）。代价是必须拿到字体文件字节，故只吃自定义字体，吃不到系统字体。
     支持：ttf / otf（TrueType 轮廓 glyf，含复合字形）/ woff / ttc（取第一个字体）；
     不支持：OTTO（CFF 轮廓）与 woff2。中文等字形多的字体把 atlasSize 调大（2048），图集满后新字退化为空白。
     合成样式：obliqueDegrees（斜切）、boldPixels（膨胀加粗）、letterSpacing（字距）；不做自动 kerning。

  4) 美术字（BMFont 图集，对应 Unity 的 Custom Font）—— BitmapFont：
       var font = await BitmapFont.LoadAsync(Content, "myres", "myres/fonts/hud.fnt", GraphicsDevice);
       batch.DrawString(font, "SCORE 0123", pos, Color.White);   // 美术字自带颜色，用 White 保留原色
     打包：raw/ 里放 .fnt + 它引用的图集页 png 即可，kfc 会识别 .fnt 的 page 页并按「已切图集」原样入包
     （不参与自动装箱），否则重排像素会让字形坐标全部失效。
     支持 BMFont 的文本与 XML 两种导出格式；二进制 .fnt（BMF 头）不支持。

WASM 工程约束：禁止使用反射
--------------------------
（硬性规定）本库编译为浏览器 wasm（Mono 解释器 / AOT），运行时反射开销极大且发布裁剪后极易崩溃，故：

1. 禁止在运行时使用 System.Reflection 做任何动态行为：
   - Type.GetType / Assembly.GetType 按名解析类型；
   - Activator.CreateInstance / FormatterServices.GetUninitializedObject 动态构造实例；
   - Type.GetMethod / GetProperty / GetField / GetConstructor 而后 Invoke；
   - PropertyInfo / FieldInfo / MethodInfo 的 GetValue / SetValue / Invoke；
   - Expression 树 Compile()（走反射发射，wasm 不可用或极慢）；
   - 依赖运行时反射的通用反序列化（如默认 JsonSerializer 对未知/多态类型的反射构造）。

2. 为什么禁用：
   - wasm 解释/AOT 下反射路径（元数据查找、IL 解释、动态分发）比原生 JIT 慢一到数个数量级；
   - 发布默认开启 IL 裁剪（trim）与 AOT，未被静态引用到的类型/成员会被移除，运行时按字符串反射取类型在发布版里直接抛 NullReferenceException / TypeLoadException，而开发期正常，极难排查；
   - AOT 无 JIT，任何依赖运行时发射（Emit / Expression.Compile）的方案都会失败。

3. 替代做法：
   - 按“名字/枚举/ID”构造对象：用显式 switch / if 分支 new，或静态 Dictionary<string, Func<T>> 启动时手动注册，不用 Activator.CreateInstance；
   - 按类型名查找：用静态字典 / 工厂手写映射，不用 Type.GetType；
   - 通用属性读写：显式强类型访问器，或源生成器生成访问代码，不用 PropertyInfo；
   - 多态 / 组件分发：接口 + 显式 new，或编译期源生成器生成注册表；
   - 序列化：已知类型显式读写，或使用 source-generated 序列化。
   首选 Roslyn 源生成器（Source Generator）：编译期读取符号生成强类型注册/访问代码，运行时零反射、零开销，且对裁剪/AOT 友好。

4. 例外：
   - 仅限编译期的分析与代码生成（源生成器、analyzer）可使用符号 API（ISymbol 等），那不是运行时反射；
   - 纯编辑器 / 构建工具（不进入 wasm 的程序集，如 kfc CLI、构建任务）不受此限。

