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

目录速览
--------
  Core/      Game、GameTime、GameHost 等生命周期
  Graphics/  GraphicsDevice、SpriteBatch、Texture2D、效果与状态
  Audio/     SoundEffect、SoundEffectInstance、MediaPlayer、AudioMaster（通用音频）
  Input/     Input
  Content/   ContentManager（运行时取资源，含 LoadBytes）
  Math/      向量与数学
  JSBind/    C# ⇄ JS 绑定（JSImport 集中地，业务不要碰）
  tsengine/  JS 侧引擎（gl / text / audio / platform / main），被所有 Example 共享

音频说明
--------
  真实音频（wav/mp3/ogg）：SoundEffect.FromBytes(bytes, mime) → 浏览器 decodeAudioData 解码 →
                           SoundEffect.Play / CreateInstance / MediaPlayer 播放。
  合成音效：不属于本库；若 Example 需要零资源原型音效，可自行用 WebAudio 合成
           （audio 模块的 playSynth），但本仓库示例统一使用真实 wav（见 Example2 的 SoundCenter），
           不内置合成示例，以保持引擎通用、干净。
