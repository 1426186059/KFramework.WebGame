using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标输入的 JS 绑定。映射到 tsengine/src/input_mouse.ts 的 "input_mouse" 模块；
    /// 编译产物 input_mouse.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// 模块名必须与 main.ts 的 setModuleImports 一致。
    /// </summary>
    public static partial class JSBind_Input_Mouse
    {
        /// <summary>把鼠标状态（位置/按键/滚轮）写入 state。</summary>
        [JSImport("pollMouse", "input_mouse")]
        public static partial void PollMouse([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

        /// <summary>解绑鼠标事件监听。</summary>
        [JSImport("unbindMouse", "input_mouse")]
        public static partial void UnbindMouse();
    }
}
