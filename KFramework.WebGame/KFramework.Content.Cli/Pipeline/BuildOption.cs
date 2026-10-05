using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class BuildOptions
    {
        public static int AutoAtlasMaxSize { get; set; } = 2048;

        public static int AutoAtlasPadding { get; set; } = 2;

        public static bool AutoAtlasWritePreviewPng { get; set; } = true;

        public static bool AutoAtlasTrimSprites { get; set; } = false;

        public static int Ktx2Quality { get; set; } = 2;

        public static ContentTextureDataFormat AutoAtlasDefaultPixelFormat { get; set; } = ContentTextureDataFormat.Webp;
    }
}
