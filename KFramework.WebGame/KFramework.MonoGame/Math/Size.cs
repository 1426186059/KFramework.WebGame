namespace KFramework.MonoGame
{
    public struct Size : System.IEquatable<Size>
    {
        public int Width;
        public int Height;

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static Size Empty => new Size(0, 0);
        public bool IsEmpty => Width == 0 && Height == 0;

        public bool Equals(Size other) => Width == other.Width && Height == other.Height;
        public override int GetHashCode() => (Width * 397) ^ Height;
        public static bool operator ==(Size a, Size b) => a.Equals(b);
        public static bool operator !=(Size a, Size b) => !a.Equals(b);
        public override string ToString() => $"{{Width={Width}, Height={Height}}}";
    }
}
