namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 字体度量单位。对齐 System.Drawing.GraphicsUnit：Font 用它把 em 字号换算成像素。
    /// </summary>
    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6
    }
}
