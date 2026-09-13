using System;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 浏览器端鼠标光标。对应 tsengine/src/core/cursor.ts（mir.setCursor）。
/// 原版用 Win32 .CUR 设置窗体光标，浏览器端改为切换 canvas 的 CSS cursor。
/// </summary>
public static partial class BrowserCursor
{
    [JSImport("mir.setCursor", "main.js")]
    private static partial void SetCursorImpl(string name);

    /// <summary>设置 canvas 的 CSS 光标名（default / crosshair / pointer / text ...）。</summary>
    public static void Set(string name)
    {
        try { SetCursorImpl(string.IsNullOrEmpty(name) ? "default" : name); }
        catch (Exception) { /* JS 绑定未就绪时忽略，不影响主流程 */ }
    }
}
