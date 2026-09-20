using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
/// 浏览器平台服务（画布尺寸、主循环驱动、地址栏参数等）。
/// 依赖 KFramework.TSEngine 项目：本类 JSImport 全部映射到 src/platform.ts 的 "platform" 模块；
/// 编译产物 platform.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
/// </summary>
    public static partial class JSBind_Platform
    {
        /// <summary>
        /// 读取画布尺寸。out 布局：[0]=CSS 宽 [1]=CSS 高 [2]=绘制缓冲宽 [3]=绘制缓冲高 [4]=DPR*1000
        /// </summary>
        [JSImport("getCanvasSize", "platform")]
        public static partial void GetCanvasSize([JSMarshalAs<JSType.MemoryView>] Span<int> size);

        /// <summary>启动 requestAnimationFrame 主循环，之后每帧回调 <c>KFramework.MonoGame.Frame</c>。</summary>
        [JSImport("startRenderLoop", "platform")]
        public static partial void StartRenderLoop();

        /// <summary>
        /// 设置呈现间隔：每 N 个垂直同步（rAF）回调一帧（N ≥ 1）。
        /// 对应 MonoGame 的 swapInterval，浏览器里由主循环跳帧实现。
        /// </summary>
        [JSImport("setFrameInterval", "platform")]
        public static partial void SetFrameInterval(int interval);

        /// <summary>当前呈现间隔（1 = 每个垂直同步都画）。</summary>
        [JSImport("getFrameInterval", "platform")]
        public static partial int GetFrameInterval();

        /// <summary>设置页面标题（浏览器标签页文字）。</summary>
        [JSImport("setTitle", "platform")]
        public static partial void SetTitle(string title);

        /// <summary>读取地址栏查询参数（?key=value 中的 value）。</summary>
        [JSImport("getQueryParameter", "platform")]
        public static partial string GetQueryParameter(string name);

        /// <summary>当前是否移动端（触屏优先设备）。</summary>
        [JSImport("isMobile", "platform")]
        public static partial bool IsMobile();

        /// <summary>页面基址（document.baseURI），用于把内容包的相对路径拼成绝对 URL。</summary>
        [JSImport("getBaseUri", "platform")]
        public static partial string GetBaseUri();

        // 输入相关的绑定已移到 JSBind_Input（对应独立的 "input" 模块）
    }
}
