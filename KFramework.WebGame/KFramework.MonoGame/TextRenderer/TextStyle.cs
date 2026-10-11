namespace KFramework.MonoGame
{
    /// <summary>
    /// 文本排版风格：把 Unity 的三元组（<see cref="TextAnchor"/> + <see cref="HorizontalWrapMode"/> +
    /// <see cref="VerticalWrapMode"/>）打成一个值，便于整体传递与传参。
    /// <para>
    /// 引擎只认这三元组。WinForms 的 <c>TextFormatFlags</c> <b>不属于引擎</b> ——
    /// GDI 标志到三元组的折算由需要的调用方自行提供
    /// （传奇客户端在 MirEngine 侧给了 <c>ToTextStyle()</c> 扩展，见其 Shims）。
    /// </para>
    /// </summary>
    public readonly struct TextStyle
    {
        /// <summary>对齐锚点（同时决定水平与垂直对齐）。</summary>
        public readonly TextAnchor Anchor;

        /// <summary>水平换行模式。</summary>
        public readonly HorizontalWrapMode HorizontalWrap;

        /// <summary>垂直溢出模式。</summary>
        public readonly VerticalWrapMode VerticalWrap;

        public TextStyle(TextAnchor anchor = TextAnchor.UpperLeft,
                         HorizontalWrapMode horizontalWrap = HorizontalWrapMode.Overflow,
                         VerticalWrapMode verticalWrap = VerticalWrapMode.Overflow)
        {
            Anchor = anchor;
            HorizontalWrap = horizontalWrap;
            VerticalWrap = verticalWrap;
        }

        /// <summary>默认风格：左上对齐、不折行、不截断。</summary>
        public static TextStyle Default => new TextStyle(TextAnchor.UpperLeft);

    }
}
