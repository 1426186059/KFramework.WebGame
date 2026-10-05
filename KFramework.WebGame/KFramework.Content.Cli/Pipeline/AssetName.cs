namespace KFramework.Content.Cli
{

    public static class AssetName
    {
        public static string Normalize(string name)
        {
            return name.Replace('\\', '/').Trim().ToLowerInvariant();
        }
    }

}
