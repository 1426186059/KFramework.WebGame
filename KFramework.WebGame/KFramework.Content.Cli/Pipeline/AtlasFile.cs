namespace KFramework.Content.Build;

public static class AtlasFile
{
    /// <summary>判断给定路径是否为已切好的图集描述文件（以 .atlas 结尾）。</summary>
    public static bool IsAtlas(string path)
        => path.Contains(".atlas.", StringComparison.OrdinalIgnoreCase);
}
