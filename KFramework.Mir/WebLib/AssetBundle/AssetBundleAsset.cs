namespace WebLib;

/// <summary>
/// 一个待打入 <see cref="AssetBundle"/> 的资源（已是最终字节，如 png 已转 webp）。
/// 对应 Unity <c>AssetBundleBuild.assetNames</c> 指向的单个资源。
/// </summary>
/// <param name="Path">包内相对路径，如 textures/floor/t1.webp</param>
/// <param name="Mime">MIME 类型，浏览器端据此构造 Blob</param>
/// <param name="Bytes">资源字节</param>
public sealed record AssetBundleAsset(string Path, string Mime, byte[] Bytes);
