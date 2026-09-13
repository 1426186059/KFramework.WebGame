namespace MirEngine
{
    public enum CompositingQuality
    {
        Default = 0,// 假设线性值。
        HighSpeed = 1,//默认质量。
        HighQuality = 2,//使用伽马校正。
        GammaCorrected = 3,//高质量，低速合成。
        AssumeLinear = 4,//高速，低质量合成。
        Invalid = -1 //无效的质量。
    }

    public enum SmoothingMode
    {
        AntiAlias = 4,//指定抗锯齿的呈现。抗锯齿模式，用于平滑曲线和对图形进行抗锯齿处理。
        Default = 0,//指定不抗锯齿。默认平滑模式
        HighQuality = 2,//指定抗锯齿的呈现。高质量模式，适用于更平滑的绘制，但可能更慢。
        HighSpeed = 1,//指定不抗锯齿。高速模式，适用于快速绘制，可能不够平滑。
        Invalid = -1, //	指定无效模式。
        None = 3,//	指定不抗锯齿。不使用平滑处理。
    }

    public enum InterpolationMode
    {
        Bicubic = 4,
        // 指定双三次插值。不进行预过滤。此模式不适用于将图像缩小到其原始大小的 25% 以下。

        Bilinear = 3,
        // 指定双线性插值。不进行预过滤。此模式不适用于将图像缩小到其原始大小的 50% 以下。

        Default = 0,
        // 指定默认模式。

        High = 2,
        // 指定高质量插值。

        HighQualityBicubic = 7,
        // 指定高质量、双三次插值。执行预过滤以确保高质量缩小。此模式产生最高质量的转换图像。

        HighQualityBilinear = 6,
        // 指定高质量、双线性插值。执行预过滤以确保高质量缩小。

        Invalid = -1,
        // 等效于 QualityMode 枚举的 Invalid 元素。

        Low = 1,
        // 指定低质量插值。

        NearestNeighbor = 5
        // 指定最近邻插值。
    }
}
