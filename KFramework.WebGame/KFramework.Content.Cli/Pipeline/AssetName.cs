namespace KFramework.Content.Build;

/// <summary>资源名规范化：统一小写、反斜杠转正斜杠，保证跨平台 / 跨大小写一致。
/// 打包端写入包内名时使用，运行端按相同规则查找已规范化后的名。</summary>
public static class AssetName
{
    public static string Normalize(string name)
        => name.Replace('\\', '/').Trim().ToLowerInvariant();
}
