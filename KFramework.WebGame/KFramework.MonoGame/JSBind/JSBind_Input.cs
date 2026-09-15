using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入的 JS 绑定（对应 tsengine/src/input.ts）。
    ///
    /// <para>为什么单独成一类：输入与 GL / Audio / Text 一样是独立的浏览器模块，
    /// 放在 <c>JSBind_Platform</c> 里会让平台层不断膨胀，也让模块名与 C# 类名对不上。
    /// 模块名 "input" 必须与 main.ts 的 <c>setModuleImports('input', input)</c> 一致。</para>
    ///
    /// <para>这一层是【薄绑定】：只把事件队列原样拉回，不做任何语义处理，
    /// 全部逻辑在 <see cref="Input"/> 中实现。</para>
    /// </summary>
    internal static partial class JSBind_Input
    {
        /// <summary>
        /// 一次性拉回本帧的输入事件队列（键盘 / 鼠标 / 触摸）。
        /// 缓冲布局：前 4 字节是事件数量，随后每条事件 20 字节（5 个 i32）。
        /// </summary>
        [JSImport("pollInput", "input")]
        internal static partial void PollInput([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        /// <summary>解绑 JS 侧注册的全部输入监听（切场景 / 销毁时调用）。</summary>
        [JSImport("unbindInput", "input")]
        internal static partial void UnbindInput();
    }
}
