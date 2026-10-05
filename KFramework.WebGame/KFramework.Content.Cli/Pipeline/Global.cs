using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class Global
    {
        public static readonly HashSet<string> supportTextureFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
        };

        public static BuildConfig mBuildConfig;
        public static BuildOptions mBuildOptions;

        public static void GetTextureFormat_Default_SuffixName(ContentTextureDataFormat mFormat)
        {
            switch(mFormat)
            {
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
                case ContentTextureDataFormat.Png:
                    return ".png";
            }
        }
    }


}
