using KFramework.MonoGame.CommonFunc;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 画布 CSS 光标绑定（module: "cursor"，见 KFramework.TSEngine/src/cursor.ts）。
    /// 业务不要直接调用，请用 <see cref="MouseCursorFunc"/>。
    /// </summary>
    public static partial class JSBind_Cursor
    {
        /// <summary>设置画布 CSS 光标（name 为合法 CSS cursor 值）。</summary>
        [JSImport("setCursor", "cursor")]
        public static partial void SetCursor(string canvasId, string name);

        /// <summary>复位为默认光标。</summary>
        [JSImport("resetCursor", "cursor")]
        public static partial void ResetCursor(string canvasId);
    }
}
