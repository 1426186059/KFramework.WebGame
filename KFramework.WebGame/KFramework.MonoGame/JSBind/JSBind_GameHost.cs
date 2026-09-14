using System.Runtime.InteropServices.JavaScript;

namespace KFramework.JSBind;

/// <summary>
/// requestAnimationFrame 回调的入口。JS 侧每帧调用 <c>JSBind_GameHost.Frame(timestamp)</c>。
/// 注意：类名与命名空间都被 wwwroot/jsengine/main.js 硬编码查找，改名时务必同步改 JS。
/// </summary>
public static partial class JSBind_GameHost
{
    internal static Game? Current;

    /// <summary>由 wwwroot/main.js 的渲染循环调用。</summary>
    [JSExport]
    internal static void Frame(double timestampMs)
    {
        Current?.TickFrame(timestampMs);
    }
}
