using System;
using KFramework.MonoGame;

namespace WebGame.Mir2.MonoGame.Client
{
    /// <summary>
    /// WinForms / GDI 风格文本排版标志（位值与 System.Windows.Forms.TextFormatFlags 保持一致）。
    /// <para>
    /// <b>本类型不属于引擎</b>：引擎（KFramework.MonoGame）只认 Unity 的三元组
    /// （<see cref="TextAnchor"/> / <see cref="HorizontalWrapMode"/> / <see cref="VerticalWrapMode"/>）。
    /// 传奇业务代码沿用了 WinForms 的写法（<c>TextFormatFlags.HorizontalCenter | VerticalCenter</c> 之类），
    /// 故把该枚举留在本工程，并由 <see cref="TextFormatFlagsExtensions.ToTextStyle"/> 折算成三元组，
    /// 在 TextRenderer 适配层的入口处调用一次 —— 150 处业务调用点因此无需改动。
    /// </para>
    /// <para>
    /// 用法（业务代码保持原样）：
    /// <code>
    ///   TextRenderer.DrawText(tex, text, font, bounds, color, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
    /// </code>
    /// </para>
    /// </summary>
    [Flags]
    public enum TextFormatFlags
    {
        DirectionRightToLeft = 0x1,
        DirectionVertical = 0x2,
        DisplayControlText = 0x4,
        NoPadding = 0x8,
        NoClipping = 0x10,
        ExternalLeading = 0x20,
        WordBreak = 0x40,
        SingleLine = 0x80,
        ExpandTabs = 0x100,
        TabStop = 0x200,
        NoPrefix = 0x400,
        Internal = 0x800,
        TextBoxControl = 0x1000,
        PathEllipsis = 0x2000,
        EndEllipsis = 0x4000,
        ModifyString = 0x8000,
        Right = 0x10000,
        Left = 0x20000,
        Center = 0x40000,
        Top = 0x80000,
        Bottom = 0x100000,
        VerticalCenter = 0x200000,
        WordEllipsis = 0x400000,
        HidePrefix = 0x800000,
        PrefixOnly = 0x1000000,
        PreserveGraphicsClipping = 0x2000000,
        PreserveGraphicsTranslateTransform = 0x4000000,
        NoWrap = 0x8000000,
        LeftAndRightPadding = 0x10000000,
        RightToLeft = 0x20000000,
        HorizontalCenter = 0x400000,
        LinkMeasureFlags = 0x2000000,
        Default = 0x0
    }

    /// <summary>
    /// GDI 迁移桥：把 <see cref="TextFormatFlags"/> 折算成引擎的 Unity 三元组。
    /// 折算规则：
    /// <list type="bullet">
    ///   <item><description>水平：<c>Right</c> → <c>*Right</c>；<c>Center</c> / <c>HorizontalCenter</c> → <c>*Center</c>；
    ///   其余 → <c>*Left</c>。</description></item>
    ///   <item><description>垂直：<c>Bottom</c> → <c>Lower*</c>；<c>VerticalCenter</c> → <c>Middle*</c>；
    ///   其余 → <c>Upper*</c>。</description></item>
    ///   <item><description>水平换行：<c>WordBreak</c>（且不含 <c>SingleLine</c> / <c>NoWrap</c>）
    ///   → <see cref="HorizontalWrapMode.Wrap"/>，否则 <see cref="HorizontalWrapMode.Overflow"/>。</description></item>
    ///   <item><description>垂直溢出：任一省略号标志（<c>EndEllipsis</c> / <c>WordEllipsis</c> / <c>PathEllipsis</c>）
    ///   → <see cref="VerticalWrapMode.Truncate"/>，否则 <see cref="VerticalWrapMode.Overflow"/>。</description></item>
    /// </list>
    /// 其余标志（<c>NoPrefix</c> / <c>HidePrefix</c> / <c>ExpandTabs</c> 等）引擎不实现，折算时忽略。
    /// </summary>
    public static class TextFormatFlagsExtensions
    {
        /// <summary>把 GDI 风格标志折算成引擎的排版三元组。</summary>
        public static TextStyle ToTextStyle(this TextFormatFlags flags)
        {
            bool hCenter = (flags & (TextFormatFlags.Center | TextFormatFlags.HorizontalCenter)) != 0;
            bool hRight = (flags & TextFormatFlags.Right) != 0;
            bool vCenter = (flags & TextFormatFlags.VerticalCenter) != 0;
            bool vBottom = (flags & TextFormatFlags.Bottom) != 0;

            // TextAnchor 是行优先布局：0..2 = Upper、3..5 = Middle、6..8 = Lower；列偏移 +0/+1/+2 = Left/Center/Right。
            int col = hRight ? 2 : hCenter ? 1 : 0;
            int row = vBottom ? 2 : vCenter ? 1 : 0;
            var anchor = (TextAnchor)(row * 3 + col);

            bool singleLine = (flags & (TextFormatFlags.SingleLine | TextFormatFlags.NoWrap)) != 0;
            HorizontalWrapMode hWrap = !singleLine && (flags & TextFormatFlags.WordBreak) != 0
                ? HorizontalWrapMode.Wrap
                : HorizontalWrapMode.Overflow;

            bool ellipsis = (flags & (TextFormatFlags.EndEllipsis | TextFormatFlags.WordEllipsis | TextFormatFlags.PathEllipsis)) != 0;
            VerticalWrapMode vWrap = ellipsis ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;

            return new TextStyle(anchor, hWrap, vWrap);
        }
    }
}
