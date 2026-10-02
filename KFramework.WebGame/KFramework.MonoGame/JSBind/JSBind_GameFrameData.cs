using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 每帧统一事件入口的绑定（模块名 <c>"game_frame_take_js_data"</c>，实现见
    /// KFramework.TSEngine/src/game_frame_take_js_data.ts；编译产物由各示例 SyncJsEngine 复制）。
    /// <para>
    /// 取代原先"键盘 / 鼠标 / 触摸各 poll 一次"的做法：C# 每帧只跨界【一次】，
    /// 由 <c>takeFrameData</c> 取回所有输入模块本帧的事件，再按模块编号分发。
    /// 各输入模块依旧独立（各自注册监听、各自维护状态、各自定义 payload 格式），
    /// 合并的只是这条传输通道 —— 因此 IME 那种走 [JSExport] 推送的模块不在其中。
    /// </para>
    /// </summary>
    public static partial class JSBind_GameFrameData
    {
        /// <summary>绑定系统级监听（窗口焦点 / 页面可见性）。应在输入装置初始化时调用一次。</summary>
        [JSImport("bindFrameEvents", "game_frame_take_js_data")]
        public static partial void BindFrameEvents();

        // 事件流不再由 C# 主动拉取：它随帧回调 JSBind_GameUpdate.Frame(timestamp, events) 一并送入，
        // 每帧只需一次跨界。布局见 GameFrameData 的说明。
        // 这里只保留 bindFrameEvents（挂窗口焦点 / 页面可见性监听）。
    }
}
