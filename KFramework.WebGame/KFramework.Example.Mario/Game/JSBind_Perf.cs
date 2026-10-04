using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Example.Mario
{
    /// <summary>
    /// 性能统计用的跨界绑定（马里奥是测试工程，这里放开加）。
    /// <para>
    /// 只为一件事：把 JS 侧 <c>custom_data_byte_cache.js</c> 里的零拷贝统计取回来，
    /// 用来确认"零拷贝到底生效没有"。
    /// 关键是 <c>sharedBytesOf</c> 里有个分支判断 —— 入参若已是 TypedArray，
    /// 说明它在跨界封送时就<b>已经拷过一次</b>，那样压根谈不上零拷贝。
    /// 光看代码看不出来，得让运行时自己报数。
    /// </para>
    /// <para>
    /// 模块名用 <c>render_webgl20</c>：它一定被注册过（main.ts 的 setModuleImports），
    /// 而 <c>copyFromStats</c> 是纯函数、不挑图形上下文，
    /// 所以即便当前跑的是 WebGPU 后端，取到的也是同一份模块级统计。
    /// </para>
    /// </summary>
    public static partial class JSBind_Perf
    {
        /// <summary>
        /// 取 JS 侧的零拷贝统计文本。
        /// 形如「零拷贝 1234 / 入参已是副本 0 / 无共享视图 0 / 退回拷贝 0（共 1234 次；入参类型：Span×1234）」。
        /// <para>
        /// 怎么读：<b>要的是「零拷贝」那一栏在涨、「入参已是副本」是 0</b>。
        /// 若「入参已是副本」在涨，说明 C# 传过来的不是 MemoryView，
        /// 跨界时就已经拷过，此时再怎么优化也是白搭 —— 得先改那个调用点的 <c>[JSMarshalAs]</c>。
        /// </para>
        /// </summary>
        [JSImport("copyFromStats", "render_webgl20")]
        public static partial string CopyFromStats();
    }
}
