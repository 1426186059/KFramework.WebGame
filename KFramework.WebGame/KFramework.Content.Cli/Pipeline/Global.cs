using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class Global
    {
        public static readonly HashSet<string> supportTextureFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
        };

        public static readonly HashSet<string> supportAudioFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".ogg", ".flac", ".aac", ".m4a"
        };

        /// <summary>
        /// 按文本处理的数据类资源（运行端用 LoadText / LoadJson 读，不做任何解码）。
        /// 不在此列的未知扩展名一律归为 <see cref="ContentAssetType.Binary"/>（如 .ttf / .bin / .wasm）。
        /// </summary>
        public static readonly HashSet<string> supportTextFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".txt", ".xml", ".csv", ".ini", ".fnt", ".atlas"
        };

        public static readonly HashSet<string> supportVideoFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mov", ".avi"
        };

        public static string GetTextureFormat_Default_SuffixName(ContentTextureDataFormat mFormat)
        {
            switch (mFormat)
            {
                case ContentTextureDataFormat.Png:  return ".png";
                case ContentTextureDataFormat.Webp: return ".webp";
                case ContentTextureDataFormat.Jpg:  return ".jpg";
                case ContentTextureDataFormat.Bmp:  return ".bmp";
                case ContentTextureDataFormat.Gif:  return ".gif";
                case ContentTextureDataFormat.Tiff: return ".tiff";
                case ContentTextureDataFormat.Rgba: return ".rgba";
                case ContentTextureDataFormat.Ktx2: return ".ktx2";
                default: throw new NotSupportedException();
            }
        }
    }


}
