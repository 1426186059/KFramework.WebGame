using System.Runtime.InteropServices.JavaScript;

namespace KFramework;

/// <summary>
/// requestAnimationFrame 回调的入口。JS 侧每帧调用 <c>KFramework.GameHost.Frame(timestamp)</c>。
/// </summary>
public static partial class GameHost
{
    internal static Game? Current;

    /// <summary>由 wwwroot/main.js 的渲染循环调用。</summary>
    [JSExport]
    internal static void Frame(double timestampMs)
    {
        Current?.TickFrame(timestampMs);
    }
}
