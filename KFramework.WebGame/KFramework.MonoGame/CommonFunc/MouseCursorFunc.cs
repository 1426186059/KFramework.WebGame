namespace KFramework.MonoGame.CommonFunc
{

    /// <summary>
    /// 画布 CSS 光标（通用封装，调用 <see cref="JSBind_Cursor"/>）。
    /// 把桌面端「切换 .CUR 文件」的语义改成「切换 canvas 的 style.cursor」，接受任意合法 CSS cursor 值。
    /// </summary>
    /// <example>
    /// <code>
    /// MouseCursor.Set("pointer");                 // 整个默认画布变手型
    /// MouseCursor.Set("game", "crosshair");        // 指定画布
    /// MouseCursor.Reset();                         // 复位默认箭头
    /// </code>
    /// </example>
    public static class MouseCursorFunc
    {
        /// <summary>默认画布 id（与 TSEngine 的 DEFAULT_CANVAS_ID 对齐）。</summary>
        public static string DefaultCanvasId { get; set; } = "game";

        /// <summary>设置光标（作用于默认画布）。</summary>
        public static void Set(string name)
        {
            Set(DefaultCanvasId, name);
        }

        /// <summary>设置光标（作用于指定画布 id 或 "#id" 选择器）。</summary>
        public static void Set(string canvasId, string name) => JSBind_Cursor.SetCursor(canvasId, name);

        /// <summary>复位为默认箭头（默认画布）。</summary>
        public static void Reset() => Reset(DefaultCanvasId);

        /// <summary>复位为默认箭头（指定画布）。</summary>
        public static void Reset(string canvasId) => JSBind_Cursor.ResetCursor(canvasId);
    }
}
