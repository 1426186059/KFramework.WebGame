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
        // 事件不再由本模块单独 poll：统一由 JSBind_GameFrameData.TakeFrameData 每帧取回后分发
        //（见 input_keyboard.ts 的 writeKeyboardEvents）。该模块只保留 BindKeyboard / UnbindKeyboard。

        [JSImport("bindKeyboard", "input_keyboard")]
        public static partial void BindKeyboard(string canvasId = null);

        [JSImport("unbindKeyboard", "input_keyboard")]
        public static partial void UnbindKeyboard();
    }
}
