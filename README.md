# KFramework.WebGame

直接利用 C# 构建的 Web 游戏引擎 —— Web 游戏引擎，是最简单的游戏引擎，没有之一。

基于 **.NET 10 WebAssembly（WASM）**：游戏逻辑全部用 C# 编写，浏览器底层能力（渲染 / 输入 / 音效 / 存储 / 平台信息）通过一层**极薄的 JS 桥**（`[JSImport]` / `[JSExport]`）暴露给 C#，渲染后端可切换 **Canvas2D / WebGL / WebGPU** 三种实现。

## 目录结构

```
KFramework.WebGame/
├── WebAssemblyBrowserApp/        ★ 主项目：自研 WASM 游戏引擎（三渲染后端）
│   ├── WebAssemblyBrowserApp.slnx
│   ├── WebEngineCommon/          通用引擎类库（三端共同引用）
│   │   └── Engine/               数学 / Color / 场景 / 输入 / 音频 / 存储 / 平台
│   ├── WebAssembly_Canvas2D/     Canvas2D 后端（2D Context 直绘）
│   ├── WebAssembly_WebGL/        WebGL 后端（GPU 合批渲染，示例游戏最全）
│   ├── WebAssembly_WebGPU/       WebGPU 后端（WGSL 着色器，下一代 GPU API）
│   └── WebAssembly_BrowserApp/   微软官方 "Hello Browser" 秒表示例（开发起点模板）
├── KNI.Engine/                   KNI（MonoGame 的 Web 移植版）实验分支
│   └── TankGame/                 KNI 版坦克游戏
└── KFramework.WebGame/           （预留目录）
```

## 通用引擎工程 `WebEngineCommon`

三个渲染后端共享一个类库，公共代码只维护一份，消除三端重复：

| 模块 | 内容 |
|---|---|
| `Vector2` / `Vector3` | 向量运算、旋转、叉积、插值 |
| `MathUtils` | 角度插值、阻尼、平滑移动、随机数 |
| `Matrix3x2` | 2D 仿射变换（平移 / 旋转 / 缩放 / TRS 组合） |
| `Matrix4x4` | 3D 变换、正交 / 透视投影、视图矩阵、求逆、列主序导出（直传 GPU uniform） |
| `Color` / `Color32` | 16 进制 / HSB 解析、预置色、混合、CSS 输出；8bit 像素打包 |
| `GameScene` / `GameObject` | 场景基类与游戏对象（AABB 碰撞、生命周期） |
| `Input` / `Audio` / `Storage` | 键盘 / 鼠标 / 触摸、WebAudio 音效、localStorage 存档 |
| `Platform` | 浏览器能力薄层：窗口尺寸、设备像素比、性能计时、UA、语言、标题 |

C# 与 JS 的交互桥：

```csharp
// C# 侧：调用 JS 薄层（浏览器能力，带安全兜底，缺失时返回默认值）
double dpr = Platform.DevicePixelRatio;
var m = Matrix3x2.CreateTRS(x, y, rad, s, s);   // 2D 变换
var c = Color.FromHex("#ff8800").WithAlpha(0.5f);

// JS 侧（wwwroot/main.js）：注册桥对象，导出薄 API
setModuleImports('main.js', { gl, input, audio, storage, platform, engine });
```

## 三个渲染后端

| 后端 | 渲染方式 | 特点 |
|---|---|---|
| **Canvas2D** | Canvas 2D Context 直接绘制 | 最简单，JS 桥为单文件，适合教学 / 调试 |
| **WebGL** | WebGL 1.0 + 自定义着色器 | GPU 合批渲染，性能好，示例游戏最完整（坦克 / 打砖块 / 主菜单） |
| **WebGPU** | WebGPU + WGSL 着色器 | 下一代 GPU API，现代浏览器（Chrome / Edge）可用 |

三个后端共享同一套游戏逻辑 API（`GameEngine`、`GameScene`、`TankScene` 等），切换后端即可对比不同渲染实现。

## 快速开始

```bash
cd WebAssemblyBrowserApp

# 构建整个解决方案（通用类库 + 三个后端）
dotnet build WebAssemblyBrowserApp.slnx

# 运行某个后端（任选其一）
dotnet run --project WebAssembly_WebGL
dotnet run --project WebAssembly_Canvas2D
dotnet run --project WebAssembly_WebGPU

# 浏览器访问 https://localhost:<端口>
# WebGL / WebGPU：主菜单按 1 进打砖块、按 2 进坦克
# 坦克：方向键 / WASD 移动，空格 / Enter / 鼠标开火，Esc 返回主菜单
# 打砖块：A / D 或方向键移动挡板，空格发球
```

## 游戏示例

每个后端都内置示例游戏（C# 实现，位于各项目的 `Games/`、`Scenes/`）：

- **Tank（坦克大战）**：瓦片地图、子弹与砖块碰撞、道具、粒子特效 —— 三个后端均有
- **Breakout（打砖块）**：挡板、小球、砖块、粒子 —— Canvas2D / WebGL / WebGPU 均有
- **MainMenuScene**：主菜单场景（WebGL / WebGPU）

## 扩展指南

- **新增游戏**：继承 `Engine.GameScene`，在 `Program.cs` 中 `RegisterScene(...)` 注册即可
- **新增渲染后端**：实现一套 JS 薄 API（`wwwroot/` 下的渲染模块），其余全部复用
- **扩展引擎**：通用数学 / 颜色等能力加进 `WebEngineCommon`，三端自动共享

## 环境要求

- .NET 10 SDK（含 WebAssembly 工作负载）
- 现代浏览器（WebGPU 后端需 Chrome 113+ / Edge 113+）
