using System;

namespace MirEngine
{
    public struct Size
    {
        public static readonly Size Empty;

        private int width;

        private int height;

        public bool IsEmpty
        {
            get
            {
                if (width == 0)
                {
                    return height == 0;
                }
                return false;
            }
        }

        public int Width
        {
            get
            {
                return width;
            }
            set
            {
                width = value;
            }
        }

        public int Height
        {
            get
            {
                return height;
            }
            set
            {
                height = value;
            }
        }

        public Size(Point pt)
        {
            width = pt.X;
            height = pt.Y;
        }

        public Size(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public static implicit operator SizeF(Size p)
        {
            return new SizeF(p.Width, p.Height);
        }

        public static Size operator +(Size sz1, Size sz2)
        {
            return Add(sz1, sz2);
        }

        public static Size operator -(Size sz1, Size sz2)
        {
            return Subtract(sz1, sz2);
        }

        public static bool operator ==(Size sz1, Size sz2)
        {
            if (sz1.Width == sz2.Width)
            {
                return sz1.Height == sz2.Height;
            }
            return false;
        }

        public static bool operator !=(Size sz1, Size sz2)
        {
            return !(sz1 == sz2);
        }

        public static explicit operator Point(Size size)
        {
            return new Point(size.Width, size.Height);
        }

        public static Size Add(Size sz1, Size sz2)
        {
            return new Size(sz1.Width + sz2.Width, sz1.Height + sz2.Height);
        }

        public static Size Ceiling(SizeF value)
        {
            return new Size((int)Math.Ceiling(value.Width), (int)Math.Ceiling(value.Height));
        }

        public static Size Subtract(Size sz1, Size sz2)
        {
            return new Size(sz1.Width - sz2.Width, sz1.Height - sz2.Height);
        }

        public static Size Truncate(SizeF value)
        {
            return new Size((int)value.Width, (int)value.Height);
        }

        public static Size Round(SizeF value)
        {
            return new Size((int)Math.Round(value.Width), (int)Math.Round(value.Height));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Size))
            {
                return false;
            }
            Size size = (Size)obj;
            if (size.width == width)
            {
                return size.height == height;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return width ^ height;
        }

        public override string ToString()
        {
            return $"{{Width={Width}, Height={Height}}}";
        }
    }

}
