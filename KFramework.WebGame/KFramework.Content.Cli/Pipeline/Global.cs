using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class Global
    {
        public static readonly HashSet<string> supportTextureFileType = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
        };

        /// <summary>
        /// 返回指定纹理数据格式落库时的默认文件后缀名（含点，小写）。
        /// 用于构建端在不指定具体输出名时，按格式生成默认扩展名。
        /// </summary>
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
