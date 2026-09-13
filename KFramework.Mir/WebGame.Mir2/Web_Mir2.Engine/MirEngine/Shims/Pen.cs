namespace MirEngine
{
	public class Pen
	{
		private Color color;
		private float width;

		public Pen(Color color, float width)
		{
			this.color = color;
			this.width = width;
		}

		public Color Color
		{
			get { return color; }
			set { color = value; }
		}

		public float Width
		{
			get { return width; }
			set { width = value; }
		}

		// 释放资源的方法
		public void Dispose()
		{
			// 如果有需要，在此处释放资源
		}
	}
}
