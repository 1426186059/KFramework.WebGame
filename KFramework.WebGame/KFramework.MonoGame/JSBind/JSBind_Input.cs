using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入的 JS 绑定。按模块拆分：键盘 / 鼠标 / 触摸各自一个 JS 模块、各自一次 poll。
    ///
    /// 一帧 3 次跨界调用（每模块一次）—— 输入本身是低频操作，
    /// 换来的是三个模块完全独立、互不耦合，且 C# 与 TS 的模块名一一对应。
    /// 依赖 KFramework.TSEngine 项目：本类各 JSImport 分别映射到 src/input_keyboard.ts / input_mouse.ts / input_touch.ts；
/// 编译产物经 SyncJsEngine 复制到 wwwroot/jsengine。
/// 模块名必须与 main.ts 的 setModuleImports 一致。
    /// </summary>
    internal static partial class JSBind_Input
    {
        // ===== 键盘（tsengine/src/input_keyboard.ts） =====

        [JSImport("pollKeyboard", "input_keyboard")]
        internal static partial void PollKeyboard([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        [JSImport("unbindKeyboard", "input_keyboard")]
        internal static partial void UnbindKeyboard();

        // ===== 鼠标（tsengine/src/input_mouse.ts） =====

        [JSImport("pollMouse", "input_mouse")]
        internal static partial void PollMouse([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        [JSImport("unbindMouse", "input_mouse")]
        internal static partial void UnbindMouse();

        // ===== 触摸（tsengine/src/input_touch.ts） =====

        [JSImport("pollTouch", "input_touch")]
        internal static partial void PollTouch([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        [JSImport("unbindTouch", "input_touch")]
        internal static partial void UnbindTouch();
    }
}
