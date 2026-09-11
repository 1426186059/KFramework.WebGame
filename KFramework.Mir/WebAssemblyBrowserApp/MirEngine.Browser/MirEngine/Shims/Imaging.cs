using System;

namespace MirEngine
{
	public enum PixelFormat
	{
		Alpha,
		Canonical,
		DontCare,
		Extended,
		Format16bppArgb1555,
		Format16bppGrayScale,
		Format16bppRgb555,
		Format16bppRgb565,
		Format1bppIndexed,
		Format24bppRgb,
		Format32bppArgb,
		Format32bppPArgb,
		Format32bppRgb,
		Format48bppRgb,
		Format4bppIndexed,
		Format64bppArgb,
		Format64bppPArgb,
		Format8bppIndexed,
		Gdi,
		Indexed,
		Max,
		PAlpha,
		Undefined
	}

	public struct DataRectangle
	{
		public int Pitch;               // 纹理数据的每行字节数
		public IntPtr DataPointer;      // 指向纹理数据的指针
		// public DataStream Data;         // 纹理数据的流
	}
}
