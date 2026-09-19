namespace KFramework.MonoGame
{
    /// <summary>
    /// 安全触发事件的辅助方法（照 MonoGame 的 Microsoft.Xna.Framework.EventHelpers）。
    /// </summary>
    internal static class EventHelpers
    {
        /// <summary>复制委托引用后再判空调用，避免多线程下事件被置空。</summary>
        internal static void Raise<TEventArgs>(object sender, EventHandler<TEventArgs>? handler, TEventArgs e)
        {
            if (handler != null)
                handler(sender, e);
        }

        /// <summary>复制委托引用后再判空调用，避免多线程下事件被置空。</summary>
        internal static void Raise(object sender, EventHandler? handler, EventArgs e)
        {
            if (handler != null)
                handler(sender, e);
        }
    }
}
