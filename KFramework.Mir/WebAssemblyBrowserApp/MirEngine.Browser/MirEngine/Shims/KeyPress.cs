using System;
namespace MirEngine
{
	public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);

	public class KeyPressEventArgs : EventArgs
	{
		private char keyChar;

		private bool handled;

		public char KeyChar
		{
			get
			{
				return keyChar;
			}
			set
			{
				keyChar = value;
			}
		}

		public bool Handled
		{
			get
			{
				return handled;
			}
			set
			{
				handled = value;
			}
		}

		public KeyPressEventArgs(char keyChar)
		{
			this.keyChar = keyChar;
		}
	}

}
