using System.Text.Json.Serialization;

namespace KFramework.MonoGame;

/// <summary>
/// 包内纹理资源的最终编码格式。在内容构建阶段（下游）由 <c>BuildOptions.TextureFormat</c> 决定，
/// 写入包内 manifest.json 的条目 <c>Format</c> 字段；运行端按此字段在加载阶段解码，<see cref="AssetBundle.LoadTexture"/> 只做 GPU 上传。
/// </summary>
/// <remarks>
/// 上游 KTexturePacker 只产出 <b>RGBA8 中间格式</b>；下游（KFramework.Content.Cli）据此自由转码为目标格式。
/// 选择原则（WASM/浏览器目标）：
/// <list type="bullet">
///   <item><see cref="Rgba"/>：裸 RGBA8，运行端零解码、直接上传 GPU；体积由 .web.lib 的 zip 容器承担（deflate）。默认。</item>
///   <item><see cref="Png"/>：经 PNG 编码，体积更小；运行端在 LoadBundle 阶段用 PngDecoder 解码为 RGBA8。</item>
///   <item><see cref="Webp"/>：经 WebP 编码（有损/无损，体积更小）；运行端在 LoadBundle 阶段借浏览器原生 createImageBitmap 解码为 RGBA8。</item>
///   <item><see cref="Ktx2"/>：GPU 压缩纹理（如 ASTC/Basis），显存与上传开销最低；本仓库暂未实现编码/解码，仅预留枚举位。</item>
/// </list>
/// 编码统一在 <c>KFramework.Content.Cli</c>（SkiaSharp 负责 Png/Webp）；解码在运行端：
/// Png 走托管 PngDecoder，Webp 走浏览器原生解码（需经 LoadBundleAsync 预解码，不能同步兜底）。
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetTextureFormat
{
    /// <summary>裸 RGBA8 像素（行优先，长度 = Width*Height*4）。不压缩、无图像头，运行端直接上传 GPU。</summary>
    Rgba = 0,

    /// <summary>PNG 编码（8 位、非隔行）。运行端用 PngDecoder 解码为 RGBA8 后上传。</summary>
    Png = 1,

    /// <summary>WebP 编码（有损 q90）。运行端在 LoadBundle 阶段借浏览器原生 createImageBitmap 解码为 RGBA8 后上传。</summary>
    Webp = 2,

    /// <summary>KTX2（GPU 压缩纹理）。需对应平台转码器，本仓库暂未实现编码/解码，预留枚举位。</summary>
    Ktx2 = 3,
}
