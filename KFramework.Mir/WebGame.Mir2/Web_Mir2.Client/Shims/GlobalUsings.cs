// 浏览器工程不再引用 WinForms，原本由 <UseWindowsForms> 隐式引入的命名空间在此补齐。
// System.Windows.Forms 的真实类型由本工程 Shims/SystemWindowsForms.cs 提供（含 Keys/Application/MessageBox/Form 等）。
global using MirEngine;
global using System.Runtime.InteropServices.JavaScript; // [JSExport]/[JSImport]
