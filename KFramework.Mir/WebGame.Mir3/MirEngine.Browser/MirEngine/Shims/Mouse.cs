using System;
namespace MirEngine
{
	public delegate void MouseEventHandler(object sender, MouseEventArgs e);

	public enum MouseButtons
	{
		Left = 0x100000,
		None = 0,
		Right = 0x200000,
		Middle = 0x400000,
		XButton1 = 0x800000,
		XButton2 = 0x1000000
	}

	public class MouseEventArgs : EventArgs
	{
		private readonly MouseButtons button;

		private readonly int clicks;

		private readonly int x;

		private readonly int y;

		private readonly int delta;

		public MouseButtons Button => button;

		public int Clicks => clicks;

		public int X => x;

		public int Y => y;

		public int Delta => delta;

		public Point Location => new Point(x, y);

		public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
		{
			this.button = button;
			this.clicks = clicks;
			this.x = x;
			this.y = y;
			this.delta = delta;
		}
	}

}
