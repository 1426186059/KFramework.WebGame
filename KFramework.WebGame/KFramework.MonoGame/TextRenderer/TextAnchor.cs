namespace KFramework.MonoGame
{
    /// <summary>
    /// 文本锚点（照 Unity 的 <c>TextAnchor</c>）：<b>一个值同时决定水平与垂直对齐</b>，
    /// 取代 WinForms <c>TextFormatFlags</c> 里 <c>Left/Center/Right</c> 与
    /// <c>Top/VerticalCenter/Bottom</c> 的两两组合。
    /// <para>
    /// 枚举顺序与 Unity 一致，且是<b>行优先</b>的：<c>0..2</c> = Upper 行、<c>3..5</c> = Middle 行、
    /// <c>6..8</c> = Lower 行；每行内 <c>+0/+1/+2</c> = Left/Center/Right。
    /// 排版实现依赖这个布局（<c>(int)anchor / 3</c> 取行、<c>% 3</c> 取列），改顺序前请先看 TextRenderer。
    /// </para>
    /// </summary>
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
}
