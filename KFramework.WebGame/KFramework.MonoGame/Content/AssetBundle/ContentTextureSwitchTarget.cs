using System.Text.Json.Serialization;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 构建端“统一转换目标”配置（由 <c>BuildOptions.TextureFormat</c> 设定，对应 build.config.json 的 textureFormat 字段）。
    /// 决定整包图统一转成什么格式；其中 <see cref="None"/> 表示不走统一转换、保留每张图自身的 <see cref="ContentTextureDataFormat"/>。
    /// <para>本枚举只是<b>配置意图</b>，不会直接写入包内 manifest——真正落库的数据格式是 <see cref="ContentTextureDataFormat"/>
    /// （含 Png / Webp / Jpg / Bmp / Gif / Tiff / Rgba / Ktx2），manifest 的 <c>Format</c> 字段类型即为 <see cref="ContentTextureDataFormat"/>。</para>
    /// </summary>
    /// <remarks>
    /// 上游 KTexturePacker 只产出 <b>RGBA8 中间格式</b>；下游（KFramework.Content.Cli）据此自由转码为目标格式。
    /// 选择原则（WASM/浏览器目标）：
    /// <list type="bullet">
    ///   <item><see cref="Rgba"/>：裸 RGBA8，运行端零解码、直接上传 GPU；体积由 .web.lib 的 zip 容器承担（deflate）。</item>
    ///   <item><see cref="Webp"/>：经 WebP 编码，体积更小；运行端在 LoadBundle 阶段借浏览器原生 createImageBitmap 解码为 RGBA8。</item>
    ///   <item><see cref="Ktx2"/>：GPU 压缩纹理；构建端用 basisu 编码（仅当宽高皆 4 倍数时），运行端借浏览器 Basis 转码器转码后上传 GPU。</item>
    /// </list>
    /// 注意：Png 等具体数据格式不在此枚举——Png 并非“统一转换目标”，而是 <see cref="ContentTextureDataFormat"/> 中的一种可被原样保留的数据格式。
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContentTextureSwitchTarget
    {
        /// <summary>不转码：原样保留原始图像。整图纹理保留源文件格式字节（png/webp/jpg/bmp/gif/tiff 等）；图集页保留烘焙出的 RGBA 裸像素。仅影响构建端编码策略，manifest 中不会写入 None（会解析为实际 ContentTextureDataFormat）。</summary>
        None = 4,

        /// <summary>裸 RGBA8 像素（行优先，长度 = Width*Height*4）。不压缩、无图像头，运行端直接上传 GPU。</summary>
        Rgba = 0,

        /// <summary>WebP 编码（有损 q90）。运行端在 LoadBundle 阶段借浏览器原生 createImageBitmap 解码为 RGBA8 后上传。</summary>
        Webp = 2,

        /// <summary>KTX2（Basis Universal 超压缩 GPU 纹理）。构建端用 basisu 编码，运行端借浏览器 Basis 转码器转码为设备原生压缩格式（ASTC/BC7/DXT/ETC2…）后直接上传 GPU；无转码器时回退为 RGBA8。</summary>
        Ktx2 = 3,
    }

}
