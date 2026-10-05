namespace KFramework.Content.Cli
{

    public static class AtlasFile
    {
        public static bool IsAtlas(string path)
        {
            return path.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase);
        }
    }

}
