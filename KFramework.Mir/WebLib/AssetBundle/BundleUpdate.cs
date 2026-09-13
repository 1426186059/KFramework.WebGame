namespace WebLib;

/// <summary>一次热更需要更新的包（对应 Unity 高层的资源差异结果）。</summary>
/// <param name="Name">逻辑名</param>
/// <param name="File">包文件名（含短哈希）</param>
/// <param name="Hash">完整内容哈希</param>
/// <param name="Size">字节长度</param>
public sealed record BundleUpdate(string Name, string File, string Hash, long Size);
