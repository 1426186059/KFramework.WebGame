namespace KFramework.MonoGame
{
    /// <summary>
    /// 纹理过滤方式（<b>后端无关</b>）。Point = 最近邻（像素风首选，放大不模糊），Linear = 双线性。
    /// </summary>
    public enum TextureFilter
    {
        Point,
        Linear,
    }

    /// <summary>纹理寻址方式（<b>后端无关</b>）。</summary>
    public enum TextureAddressMode
    {
        /// <summary>边缘钳制（GL 的 CLAMP_TO_EDGE / WebGPU 的 clamp-to-edge）。</summary>
        Clamp,

        /// <summary>重复平铺，用于背景滚动（GL 的 REPEAT / WebGPU 的 repeat）。</summary>
        Wrap,

        /// <summary>镜像重复（GL 的 MIRRORED_REPEAT / WebGPU 的 mirror-repeat）。</summary>
        Mirror,
    }

    /// <summary>
    /// 纹理采样方式。<b>后端无关</b>：字段是 <see cref="TextureFilter"/> /
    /// <see cref="TextureAddressMode"/> 中立枚举，不含任何 WebGL / WebGPU 常量，
    /// 因此同一套状态能被两个后端共用（各后端自行翻译）。
    /// </summary>
    public sealed class SamplerState
    {
        public readonly TextureFilter MinFilter;
        public readonly TextureFilter MagFilter;
        public readonly TextureAddressMode WrapMode;

        private SamplerState(TextureFilter minFilter, TextureFilter magFilter, TextureAddressMode wrapMode)
        {
            MinFilter = minFilter; MagFilter = magFilter; WrapMode = wrapMode;
        }

        /// <summary>最近邻采样，像素风游戏的首选，放大后不模糊。</summary>
        public static readonly SamplerState Point =
            new(TextureFilter.Point, TextureFilter.Point, TextureAddressMode.Clamp);

        /// <summary>双线性采样，适合需要平滑缩放的美术风格。</summary>
        public static readonly SamplerState Linear =
            new(TextureFilter.Linear, TextureFilter.Linear, TextureAddressMode.Clamp);

        /// <summary>线性采样 + 平铺，用于背景滚动。</summary>
        public static readonly SamplerState LinearWrap =
            new(TextureFilter.Linear, TextureFilter.Linear, TextureAddressMode.Wrap);

        /// <summary>点采样 + 边缘钳制（MonoGame 命名，等价于 <see cref="Point"/>）。</summary>
        public static readonly SamplerState PointClamp = Point;
    }
}
