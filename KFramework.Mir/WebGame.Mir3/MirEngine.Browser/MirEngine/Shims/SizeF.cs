namespace MirEngine
{

    public struct SizeF
    {
        public static readonly SizeF Empty;

        private float width;

        private float height;

        public bool IsEmpty
        {
            get
            {
                if (width == 0f)
                {
                    return height == 0f;
                }
                return false;
            }
        }

        public float Width
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

        public float Height
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

        public SizeF(SizeF size)
        {
            width = size.width;
            height = size.height;
        }

        public SizeF(PointF pt)
        {
            width = pt.X;
            height = pt.Y;
        }

        public SizeF(float width, float height)
        {
            this.width = width;
            this.height = height;
        }

        public static SizeF operator +(SizeF sz1, SizeF sz2)
        {
            return Add(sz1, sz2);
        }

        public static SizeF operator -(SizeF sz1, SizeF sz2)
        {
            return Subtract(sz1, sz2);
        }

        public static bool operator ==(SizeF sz1, SizeF sz2)
        {
            if (sz1.Width == sz2.Width)
            {
                return sz1.Height == sz2.Height;
            }
            return false;
        }

        public static bool operator !=(SizeF sz1, SizeF sz2)
        {
            return !(sz1 == sz2);
        }

        public static explicit operator PointF(SizeF size)
        {
            return new PointF(size.Width, size.Height);
        }

        public static SizeF Add(SizeF sz1, SizeF sz2)
        {
            return new SizeF(sz1.Width + sz2.Width, sz1.Height + sz2.Height);
        }

        public static SizeF Subtract(SizeF sz1, SizeF sz2)
        {
            return new SizeF(sz1.Width - sz2.Width, sz1.Height - sz2.Height);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SizeF))
            {
                return false;
            }
            SizeF sizeF = (SizeF)obj;
            if (sizeF.Width == Width && sizeF.Height == Height)
            {
                return sizeF.GetType().Equals(GetType());
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public PointF ToPointF()
        {
            return (PointF)this;
        }

        public Size ToSize()
        {
            return Size.Truncate(this);
        }

        public override string ToString()
        {
            return $"{{Width={Width}, Height={Height}}}";
        }
    }

}
