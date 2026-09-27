using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘输入的 JS 绑定。映射到 tsengine/src/input_keyboard.ts 的 "input_keyboard" 模块；
    /// 编译产物 input_keyboard.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// 模块名必须与 main.ts 的 setModuleImports 一致。
    /// </summary>
    public static partial class JSBind_Input_Keyboard
    {
        [JSImport("pollKeyboard", "input_keyboard")]
        public static partial void PollKeyboard([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        [JSImport("bindKeyboard", "input_keyboard")]
        public static partial void BindKeyboard(string canvasId = null);

        [JSImport("unbindKeyboard", "input_keyboard")]
        public static partial void UnbindKeyboard();
    }
}
