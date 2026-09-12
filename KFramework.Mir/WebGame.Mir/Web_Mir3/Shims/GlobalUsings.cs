// 兼容壳全局 using：还原被排除的原版 Mir3/Client/GlobalUsings.cs 内容，
// 并补 System.Windows.Forms，使原版游戏层在浏览器 WASM 下能编过。
// 真实交互/显示后续用 Canvas 逐个替换（见 Shims/README）。
global using Shared.Envir;
global using Shared.Rendering;
global using MirEngine;
// 用浏览器安全的 MirEngine.Font / MirEngine.FontStyle 覆盖 System.Drawing 的同名 GDI 类型，
// 避免任何 new Font(...) 在 WASM 下触发 gdiplus.dll（Font 构造即 P/Invoke GDI+）。
global using Font = MirEngine.Font;
global using FontStyle = MirEngine.FontStyle;
