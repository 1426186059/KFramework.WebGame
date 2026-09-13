namespace WebLib;

/// <summary>
/// 描述一个待构建的资源包（对齐 Unity <c>AssetBundleBuild</c>）。
/// 在 <see cref="BuildPipeline.BuildAssetBundles"/> 中作为构建单元。
/// </summary>
public sealed class AssetBundleBuild
{
    /// <summary>逻辑包名（对应 Unity 的 assetBundleName，如 3-1）。</summary>
    public string AssetBundleName { get; set; } = "";

    /// <summary>包内资源（对应 Unity 的 assetNames / addressableNames）。</summary>
    public List<AssetBundleAsset> Assets { get; set; } = new();
}
