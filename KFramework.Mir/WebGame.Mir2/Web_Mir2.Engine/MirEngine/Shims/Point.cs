using System;

namespace MirEngine
{
	public struct Point
	{
		public static readonly Point Empty;

		private int x;

		private int y;

		public bool IsEmpty
		{
			get
			{
				if (x == 0)
				{
					return y == 0;
				}
				return false;
			}
		}

		public int X
		{
			get
			{
				return x;
			}
			set
			{
				x = value;
			}
		}

		public int Y
		{
			get
			{
				return y;
			}
			set
			{
				y = value;
			}
		}

		public Point(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		public Point(Size sz)
		{
			x = sz.Width;
			y = sz.Height;
		}

		public Point(int dw)
		{
			x = (short)LOWORD(dw);
			y = (short)HIWORD(dw);
		}

		public static implicit operator PointF(Point p)
		{
			return new PointF(p.X, p.Y);
		}

		public static explicit operator Size(Point p)
		{
			return new Size(p.X, p.Y);
		}

		public static Point operator +(Point pt, Size sz)
		{
			return Add(pt, sz);
		}

		public static Point operator -(Point pt, Size sz)
		{
			return Subtract(pt, sz);
		}

		public static bool operator ==(Point left, Point right)
		{
			if (left.X == right.X)
			{
				return left.Y == right.Y;
			}
			return false;
		}

		public static bool operator !=(Point left, Point right)
		{
			return !(left == right);
		}

		public static Point Add(Point pt, Size sz)
		{
			return new Point(pt.X + sz.Width, pt.Y + sz.Height);
		}

		public static Point Subtract(Point pt, Size sz)
		{
			return new Point(pt.X - sz.Width, pt.Y - sz.Height);
		}

		public static Point Ceiling(PointF value)
		{
			return new Point((int)Math.Ceiling(value.X), (int)Math.Ceiling(value.Y));
		}

		public static Point Truncate(PointF value)
		{
			return new Point((int)value.X, (int)value.Y);
		}

		public static Point Round(PointF value)
		{
			return new Point((int)Math.Round(value.X), (int)Math.Round(value.Y));
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Point))
			{
				return false;
			}
			Point point = (Point)obj;
			if (point.X == X)
			{
				return point.Y == Y;
			}
			return false;
		}

		public override int GetHashCode()
		{
			return x ^ y;
		}

		public void Offset(int dx, int dy)
		{
			X += dx;
			Y += dy;
		}

		public void Offset(Point p)
		{
			Offset(p.X, p.Y);
		}

		public override string ToString()
		{
			return $"({X}, {Y})";
		}

		private static int HIWORD(int n)
		{
			return (n >> 16) & 0xFFFF;
		}

		private static int LOWORD(int n)
		{
			return n & 0xFFFF;
		}

	}


}
