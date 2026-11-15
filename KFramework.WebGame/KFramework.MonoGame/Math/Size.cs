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

        public bool IsEmpty
        {
            get { return Width == 0 && Height == 0; }
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is Size)) return false;
            Size size = (Size)obj;
            return size.Width == Height && size.Height == Height;
        }

        public bool Equals(Size other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override int GetHashCode()
        {
            return (Width * 397) ^ Height;
        }

        public static bool operator ==(Size a, Size b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(Size a, Size b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"{{Width={Width}, Height={Height}}}";
        }
    }
}
