using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 触摸输入的 JS 绑定。映射到 tsengine/src/input_touch.ts 的 "input_touch" 模块；
    /// 编译产物 input_touch.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// 模块名必须与 main.ts 的 setModuleImports 一致。
    /// </summary>
    public static partial class JSBind_Input_Touch
    {
        /// <summary>激活装置：建立/恢复触摸事件监听（绑定到画布）。由 <see cref="Input_Touch.Activate"/> 调用。</summary>
        [JSImport("bindTouch", "input_touch")]
        public static partial void BindTouch();

        /// <summary>把触摸点状态（坐标/压力/接触中）写入 state。</summary>
        // 事件不再由本模块单独 poll：统一由 JSBind_GameFrameData.TakeFrameData 每帧取回后分发
        //（见 input_touch.ts 的 writeTouchEvents）。

        /// <summary>关闭装置：解绑触摸事件监听。由 <see cref="Input_Touch.Deactivate"/> 调用。</summary>
        [JSImport("unbindTouch", "input_touch")]
        public static partial void UnbindTouch();
    }
}
