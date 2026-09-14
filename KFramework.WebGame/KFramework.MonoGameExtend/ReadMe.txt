KFramework.MonoGameExtend
==========================

KFramework.MonoGame 之上的“扩展层”，提供更贴近上层游戏的丰富能力。

定位
----
- KFramework.MonoGame：只提供游戏引擎的【基础能力】
  （渲染抽象、输入抽象、音频、内容管线、JS 互操作等通用底座）。
  它尽量保持精简与通用，以便能适配各种不同的游戏框架。
- KFramework.MonoGameExtend：在基础引擎之上，提供更丰富、更“开箱即用”的能力，
  让上层游戏直接用节点树来组织场景与 UI，而无需从零搭建。

提供的扩展能力
--------------
- 节点树（Scene Graph）：KTransform / KSceneBase / KSceneMgr，用父子层级组织游戏对象；
- 相机：KCamera；
- UI 组件：KWidget / KImage / KLabel / KButton / KCanvas 等；
- 精灵组件：KSprite（带动画帧的精灵）；
- 图集：基于 TexturePacker 的图集加载与按帧名取图；
- 其它常用辅助：KTime、KInputMgr、KDefaultRes、协程（Coroutine）等。

适用场景
--------
把“基础引擎”与“扩展层”分离后，KFramework.MonoGame 可以适配更多不同的游戏框架，例如：
- 传奇类游戏（MMORPG 客户端，如 WebGame.Mir2）；
- 坦克大战这类基于关卡 / 实体的休闲游戏（见 KFramework.Example2）。

两者关系
--------
KFramework.MonoGameExtend 通过 ProjectReference 引用 KFramework.MonoGame；
其根命名空间为 KFramework.MonoGameExtend，程序集名为 KFramework.MonoGameExtend。

命名空间约定（重要）
--------------------
- 基础引擎 KFramework.MonoGame：根命名空间 KFramework，采用传统“大括号块式”声明
  （namespace KFramework { }），子命名空间如 KFramework.Graphics / KFramework.JSBind 同理；
  不使用文件作用域写法（namespace KFramework;）。
- 扩展层 KFramework.MonoGameExtend：根命名空间 KFramework.MonoGameExtend，同样为大括号块式。
- 二者命名空间不同，互不冲突；基础引擎保持精简通用，扩展层在其上叠加节点树 / UI / 精灵等能力。
