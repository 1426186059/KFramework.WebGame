namespace KFramework.MonoGame
{
    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8,
    }

    public enum HorizontalWrapMode
    {
        Wrap = 0,
        Overflow = 1,
    }

    public enum VerticalWrapMode
    {
        Truncate = 0,
        Overflow = 1,
    }
    

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
