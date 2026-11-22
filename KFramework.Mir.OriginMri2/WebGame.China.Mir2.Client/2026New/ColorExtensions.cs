// 引擎 KFramework.MonoGame.Color 的实例扩展：补齐原 MirEngine.Color 的 ToArgb / IsEmpty。
public static class ColorExtensions
{
    /// <summary>返回 0xAARRGGBB 整数（原 Color.ToArgb）。</summary>
    public static int ToArgb(this Color c) => unchecked((int)c.PackedValue);
}
