// 浏览器工程不再引用 WinForms，原本由 <UseWindowsForms> 隐式引入的命名空间在此补齐。
// System.Windows.Forms 的真实类型由本工程 Shims/SystemWindowsForms.cs 提供（含 Keys/Application/MessageBox/Form 等）。
global using MirEngine;
global using System.Runtime.InteropServices.JavaScript; // [JSExport]/[JSImport]

// 文本渲染分层：
//   - 引擎基础库 KFramework.MonoGame/TextRenderer 只使用 IFont：TextRenderer / TextCaret /
//     TextInputOverlay / TextFormatFlags / Size；
//   - 本工程 Shims/MirEngineFont.cs 提供 WinForms 兼容的字体描述符 Font / GraphicsUnit / FontFactory
//     （Font 实现 IFont，故可直接传给基础库）。
// Font / GraphicsUnit 位于 MirEngine 命名空间，由上面的 global using MirEngine; 直接可见，无需别名；
// TextFormatFlags 在引擎基础库中，用别名映射，使既有代码无需逐处修改。
global using TextFormatFlags = KFramework.MonoGame.TextRenderer.TextFormatFlags;
