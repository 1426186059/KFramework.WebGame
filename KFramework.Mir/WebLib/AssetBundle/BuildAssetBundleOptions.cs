namespace WebLib;

/// <summary>
/// 构建选项（对齐 Unity <c>BuildAssetBundleOptions</c> 的常用位）。
/// 默认 ChunkBasedCompression | Deterministic：zip deflate 压缩 + 固定时间戳/排序保证可复现。
/// </summary>
[Flags]
public enum BuildAssetBundleOptions
{
    /// <summary>无额外选项（采用 zip deflate 默认压缩）。</summary>
    None = 0,

    /// <summary>不压缩（zip store）。包体积大、加载零解压开销。</summary>
    Uncompressed = 1 << 0,

    /// <summary>分块压缩（zip deflate），默认值之一。</summary>
    ChunkBasedCompression = 1 << 1,

    /// <summary>确定性：固定 zip 时间戳并排序条目，保证相同内容产出逐字节一致的包（利于热更比对）。</summary>
    Deterministic = 1 << 2,

    /// <summary>强制重建（忽略任何复用缓存）。本库默认每次都重建，保留以对齐 API。</summary>
    ForceRebuild = 1 << 3,
}
