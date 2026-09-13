using System;
namespace MirEngine
{
	public static class ColorTranslator
	{
		// 将 HTML 格式的颜色字符串转换为 Color 对象
		public static Color FromHtml(string htmlColor)
		{
			if (string.IsNullOrEmpty(htmlColor) || htmlColor.Length < 7 || htmlColor[0] != '#')
			{
				throw new ArgumentException("Invalid HTML color string.", nameof(htmlColor));
			}

			int red = int.Parse(htmlColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
			int green = int.Parse(htmlColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
			int blue = int.Parse(htmlColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);

			return Color.FromArgb((byte)red, (byte)green, (byte)blue);
		}

		// 将 Color 对象转换为 HTML 格式的颜色字符串
		public static string ToHtml(Color color)
		{
			return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		}
	}
}
