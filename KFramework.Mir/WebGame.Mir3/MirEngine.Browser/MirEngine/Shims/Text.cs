namespace MirEngine
{
    public enum TextRenderingHint
	{
		AntiAlias,//在无提示的情况下使用每个字符的抗锯齿效果标志符号位图来绘制字符。 由于采用了 AntiAlias，质量会得到改善。 由于关闭了提示，主干宽度差可能会比较明显。
		AntiAliasGridFit,//在有提示的情况下使用每个字符的抗锯齿效果标志符号位图来绘制字符。 由于采用了 AntiAlias，质量会得到大大改善，但同时会增加性能成本。
		ClearTypeGridFit,//在有提示的情况下使用每个字符的标志符号 ClearType 位图来绘制字符。 这是质量最高的设置。 用于利用 ClearType 字体功能。
		SingleBitPerPixel,//使用每个字符的标志符号位图来绘制字符。 不使用提示。
		SingleBitPerPixelGridFit,//使用每个字符的标志符号位图来绘制字符。 提示用于改善字符在主干和弯曲部分的外观。
		SystemDefault,//在有系统默认呈现提示的情况下使用每个字符的标志符号位图来绘制字符。 将采用用户为系统选择的任何字体修匀设置来绘制文本。
	}
}
