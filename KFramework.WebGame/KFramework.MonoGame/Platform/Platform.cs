using System.Runtime.InteropServices.JavaScript;

namespace KFramework;

/// <summary>浏览器平台服务（画布尺寸、主循环驱动、地址栏参数等）。</summary>
internal static partial class Platform
{
    /// <summary>
    /// 读取画布尺寸。out 布局：[0]=CSS 宽 [1]=CSS 高 [2]=绘制缓冲宽 [3]=绘制缓冲高 [4]=DPR*1000
    /// </summary>
    [JSImport("getCanvasSize", "platform")]
    internal static partial void GetCanvasSize([JSMarshalAs<JSType.MemoryView>] Span<int> size);

    /// <summary>启动 requestAnimationFrame 主循环，之后每帧回调 <c>KFramework.GameHost.Frame</c>。</summary>
    [JSImport("startRenderLoop", "platform")]
    internal static partial void StartRenderLoop();

    [JSImport("setTitle", "platform")]
    internal static partial void SetTitle(string title);

    [JSImport("getQueryParameter", "platform")]
    internal static partial string GetQueryParameter(string name);

    [JSImport("isMobile", "platform")]
    internal static partial bool IsMobile();

    /// <summary>页面基址（document.baseURI），用于把内容包的相对路径拼成绝对 URL。</summary>
    [JSImport("getBaseUri", "platform")]
    internal static partial string GetBaseUri();
}
