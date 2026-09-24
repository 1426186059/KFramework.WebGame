// 浏览器工程不再引用 WinForms，原本由 <UseWindowsForms> 隐式引入的命名空间在此补齐。
// System.Windows.Forms 的真实类型由本工程 Shims/SystemWindowsForms.cs 提供（含 Keys/Application/MessageBox/Form 等）。
global using MirEngine;
global using System.Runtime.InteropServices.JavaScript; // [JSExport]/[JSImport]

// 文本渲染相关类型已上移到引擎通用底层库 KFramework.MonoGame/TextRenderer，
// 这里用全局别名把 WinForms 风格的类型名映射到引擎实现，使客户端既有代码无需逐处修改：
//   Font             -> KFramework.MonoGame.TextRenderer.Font             （对齐 System.Drawing.Font）
//   TextFormatFlags  -> KFramework.MonoGame.TextRenderer.TextFormatFlags  （位值与原 shim 定义一致）
//   GraphicsUnit     -> KFramework.MonoGame.TextRenderer.GraphicsUnit
global using Font = KFramework.MonoGame.TextRenderer.Font;
global using TextFormatFlags = KFramework.MonoGame.TextRenderer.TextFormatFlags;
global using GraphicsUnit = KFramework.MonoGame.TextRenderer.GraphicsUnit;
