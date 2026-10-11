namespace KFramework.MonoGame
{
    /// <summary>
    /// 垂直溢出处理（照 Unity 的 <c>VerticalWrapMode</c>）。
    /// <list type="bullet">
    ///   <item><description><see cref="Truncate"/> —— 对应 WinForms 的省略号标志
    ///   （<c>EndEllipsis</c> / <c>WordEllipsis</c> / <c>PathEllipsis</c>）：超出给定高度就截断不再画。
    ///   本引擎只做"截断"，不额外绘制省略号字符。</description></item>
    ///   <item><description><see cref="Overflow"/> —— 不截断，超出部分照原样画出。</description></item>
    /// </list>
    /// </summary>
    public enum VerticalWrapMode
    {
        Truncate = 0,
        Overflow = 1,
    }
}
