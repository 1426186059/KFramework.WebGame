using System;

namespace MirEngine
{
    // GDI+ 类型占位：浏览器 / WASM 没有 GDI+，真实绘制已由 Canvas / WebGL 管线接管。
    // 这些类型仅用于满足原版代码的签名（相关方法多为 shim 空实现，或在浏览器路径下不会被调用）。
    public class Image : IDisposable
    {
        public int Width => 0;
        public int Height => 0;
        public Size Size => new Size(0, 0);
        public virtual void Dispose() { }
    }

    public sealed class Bitmap : Image
    {
        public Bitmap(string path) { }
        public Bitmap(int width, int height) { }

        public new int Width => 0;
        public new int Height => 0;
        public new Size Size => new Size(0, 0);

        public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format)
            => new BitmapData();

        public void UnlockBits(BitmapData data) { }
    }

    public sealed class Graphics : IDisposable
    {
        public void Dispose() { }
        public void Clear(Color colour) { }
        public void DrawImage(Image image, Rectangle rect) { }
        public void FillRectangle(Brush brush, Rectangle rect) { }
        public void DrawString(string s, Font font, Brush brush, PointF point) { }
    }

    public abstract class Brush : IDisposable
    {
        public virtual void Dispose() { }
    }

    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color colour) { Colour = colour; }
        public Color Colour { get; }
    }

    public sealed class StringFormat : IDisposable
    {
        public void Dispose() { }
    }

    // 文本串范围（原版用于 MeasureCharacterRanges / 聊天文本按钮命中区域）
    public struct CharacterRange : IEquatable<CharacterRange>
    {
        public int First { get; set; }
        public int Length { get; set; }

        public CharacterRange(int first, int length)
        {
            First = first;
            Length = length;
        }

        public bool Equals(CharacterRange other) => First == other.First && Length == other.Length;
        public override bool Equals(object obj) => obj is CharacterRange c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(First, Length);
        public static bool operator ==(CharacterRange left, CharacterRange right) => left.Equals(right);
        public static bool operator !=(CharacterRange left, CharacterRange right) => !left.Equals(right);
    }
}

namespace MirEngine
{
    public enum ImageLockMode
    {
        ReadOnly = 1,
        WriteOnly = 2,
        ReadWrite = 3,
    }

    public sealed class BitmapData
    {
        public IntPtr Scan0 => IntPtr.Zero;
        public int Stride => 0;
        public int Width => 0;
        public int Height => 0;
        public PixelFormat PixelFormat => PixelFormat.Format32bppArgb;
    }
}
