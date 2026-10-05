namespace KFramework.Content.Cli
{
    public static class Global
    {
        public static readonly HashSet<string> supportTextureFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
        };

        public static BuildConfig mBuildConfig;
    }


}
