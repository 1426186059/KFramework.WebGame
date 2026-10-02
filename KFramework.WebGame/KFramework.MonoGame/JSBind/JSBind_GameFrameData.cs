using System;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 每帧事件流的绑定（模块名 <c>"game_frame_take_js_data"</c>，实现见
    /// KFramework.TSEngine/src/game_frame_take_js_data.ts；编译产物由各示例 SyncJsEngine 复制）。
    /// <para>
    /// <b>一推一拉</b>：JS 只推帧（<c>host.Frame(timestamp)</c>，<b>不带</b>事件数据），
    /// C# 每帧再用 <see cref="TakeFrameData"/> 回头取一次。
    /// 分开的理由不是省时间（多一次跨界只值几微秒），而是取的那个方向是 C#→JS，
    /// 能走 MemoryView 零拷贝 —— 每帧零分配；而"一趟带参数"是 JS→C# 的 byte[]，
    /// 每帧都要新建一个托管数组，60fps 下就是每秒 60 个垃圾。
    /// </para>
    /// </summary>
    public static partial class JSBind_GameFrameData
    {
        /// <summary>绑定系统级监听（窗口焦点 / 页面可见性）。应在首次取数据前调用一次。</summary>
        [JSImport("bindFrameEvents", "game_frame_take_js_data")]
        public static partial void BindFrameEvents();

        /// <summary>
        /// 取本帧事件流：C# 提供缓冲、JS 直写（<b>零拷贝</b>），返回写入的字节数。
        /// <para>
        /// 用 MemoryView 而不是让 JS 返回 byte[]：后者每帧新建一次托管数组，
        /// WASM 的 GC 是停止世界的，那比一次跨界贵得多。
        /// 布局见 <see cref="Input_GameFrameData"/>（对照 TS 的 html_event_type）。
        /// </para>
        /// </summary>
        [JSImport("takeFrameData", "game_frame_take_js_data")]
        public static partial int TakeFrameData([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);
    }
}
