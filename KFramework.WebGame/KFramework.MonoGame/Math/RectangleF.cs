using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    public struct RectangleF : IEquatable<RectangleF>
    {
        private static RectangleF emptyRectangle;

        public float X;
        public float Y;
        public float Width;
        public float Height;

        public static RectangleF Empty => emptyRectangle;


        public float Left { get { return X; } set { X = value; } }
        public float Right { get { return X + Width; } set { Width = value - X; } }
        public float Top { get { return Y; } set { Y = value; } }
        public float Bottom { get { return Y + Height; } set { Height = value - Y; } }
        


        public bool IsEmpty
        {
            get
            {
                if (Width == 0 && Height == 0 && X == 0)
                {
                    return Y == 0;
                }

                return false;
            }
        }

        public Vector2 Location
        {
            get
            {
                return new Vector2(X, Y);
            }
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        public Vector2 Size
        {
            get
            {
                return new Vector2(Width, Height);
            }
            set
            {
                Width = value.X;
                Height = value.Y;
            }
        }

        public Vector2 Center => new Vector2(X + Width / 2, Y + Height / 2);

        public RectangleF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public RectangleF(Vector2 location, Vector2 size)
        {
            X = location.X;
            Y = location.Y;
            Width = size.X;
            Height = size.Y;
        }

        public static RectangleF MinMax(Vector2 minPos, Vector2 maxPos)
        {
            RectangleF m = new RectangleF();
            m.X = minPos.X;
            m.Y = minPos.Y;
            m.Width = maxPos.X - minPos.X;
            m.Height = maxPos.Y - minPos.Y;
            return m;
        }

        public static RectangleF MinMax(float minPosX, float minPosY, float maxPosX, float maxPosY)
        {
            RectangleF m = new RectangleF();
            m.X = minPosX;
            m.Y = minPosY;
            m.Width = maxPosX - minPosX;
            m.Height = maxPosY - minPosY;
            return m;
        }

        public bool Contains(int x, int y)
        {
            if (X <= x && x < X + Width && Y <= y)
            {
                return y < Y + Height;
            }

            return false;
        }

        public bool Contains(float x, float y)
        {
            if (X <= x && x < X + Width && Y <= y)
            {
                return y < Y + Height;
            }

            return false;
        }

        public bool Contains(Vector2 value)
        {
            if ((float)X <= value.X && value.X < (float)(X + Width) && (float)Y <= value.Y)
            {
                return value.Y < (float)(Y + Height);
            }

            return false;
        }

        public bool Contains(RectangleF value)
        {
            if (X <= value.X && value.X + value.Width <= X + Width && Y <= value.Y)
            {
                return value.Y + value.Height <= Y + Height;
            }

            return false;
        }

        public void Offset(float offsetX, float offsetY)
        {
            X += offsetX;
            Y += offsetY;
        }

        public void Offset(Vector2 amount)
        {
            X += amount.X;
            Y += amount.Y;
        }

        //隐式转换

        public static implicit operator Rectangle(RectangleF a)
        {
            return new Rectangle((int)a.X, (int)a.Y, (int)a.Width, (int)a.Height);
        }

        //强制 转换
        public static explicit operator RectangleF(Rectangle a)
        {
            return new RectangleF(a.X, a.Y, a.Width, a.Height);
        }

        public static bool operator ==(RectangleF a, RectangleF b)
        {
            return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
        }

        public static bool operator !=(RectangleF a, RectangleF b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is RectangleF)
            {
                return this == (RectangleF)obj;
            }

            return false;
        }

        public bool Equals(RectangleF other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return (((17 * 23 + X.GetHashCode()) * 23 + Y.GetHashCode()) * 23 + Width.GetHashCode()) * 23 + Height.GetHashCode();
        }

        public override string ToString()
        {
            return "{X:" + X + " Y:" + Y + " Width:" + Width + " Height:" + Height + "}";
        }
    }
}
