namespace KFramework.MonoGame
{
    /// <summary>
    /// 水平换行模式（照 Unity 的 <c>HorizontalWrapMode</c>）。
    /// <list type="bullet">
    ///   <item><description><see cref="Wrap"/> —— 对应 WinForms 的 <c>TextFormatFlags.WordBreak</c>：
    ///   在给定宽度内折行（连续中文 / 超长串退化为逐字折行）。</description></item>
    ///   <item><description><see cref="Overflow"/> —— 不折行（含 <c>SingleLine</c> / <c>NoWrap</c>），
    ///   只按显式的 <c>'\n'</c> 分行，超出部分照原样画出。</description></item>
    /// </list>
    /// </summary>
    public enum HorizontalWrapMode
    {
        Wrap = 0,
        Overflow = 1,
    }
}
