namespace KFramework.Content.Build;

public static class AtlasFile
{
    /// <summary>判断给定路径是否为已切好的图集描述文件
    public static bool IsAtlas(string path)
    {
        return path.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase);
    }
}
