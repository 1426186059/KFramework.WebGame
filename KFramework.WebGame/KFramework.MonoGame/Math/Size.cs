namespace KFramework.MonoGame
{
    public struct Size : System.IEquatable<Size>
    {
        public int Width;
        public int Height;

        public Size(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        public static Size Empty => new Size(0, 0);

        public bool IsEmpty
        {
            get { return Width == 0 && Height == 0; }
        }

        public override bool Equals(object? other)
        {
            if (!(other is Size))
            {
                return false;
            }

            return this == (Size)other;
        }

        public bool Equals(Size other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        public static bool operator ==(Size a, Size b)
        {
            return a.Width == b.Width && a.Height == b.Height;
        }

        public static bool operator !=(Size a, Size b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return $"(Width={Width}, Height={Height})";
        }
    }
}
