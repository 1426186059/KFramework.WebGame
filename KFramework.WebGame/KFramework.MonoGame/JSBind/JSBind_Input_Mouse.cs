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
        /// <summary>
        /// 激活装置：建立/恢复鼠标事件监听（绑定到画布）。由 <see cref="Input_Mouse.Activate"/> 调用。
        /// 显式传画布 id —— 与 <see cref="JSBind_Input_Keyboard.BindKeyboard"/> 一致，
        /// 这样输入模块不必依赖某个具体渲染后端（WebGL / WebGPU）已初始化。
        /// </summary>
        [JSImport("bindMouse", "input_mouse")]
        public static partial void BindMouse(string canvasId);

        // 事件不再由本模块单独 poll：统一由 JSBind_GameFrameData.TakeFrameData 每帧取回后分发，
        // 模块只负责提供事件（见 input_mouse.ts 的 writeMouseEvents）。

        /// <summary>关闭装置：解绑鼠标事件监听。由 <see cref="Input_Mouse.Deactivate"/> 调用。</summary>
        [JSImport("unbindMouse", "input_mouse")]
        public static partial void UnbindMouse();
    }
}
