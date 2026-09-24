using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 文本排版标志。位值与 System.Windows.Forms.TextRenderer 使用的 GDI DT_* / WinForms 标志保持一致，
    /// 便于把既有 WinForms 代码（DrawText/MeasureText 的 flags 参数）原样迁移。
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
}
