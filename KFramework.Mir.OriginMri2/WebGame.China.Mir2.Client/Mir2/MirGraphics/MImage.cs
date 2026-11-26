namespace Client.MirGraphics
{
    /// <summary>
    /// 轻量贴图容器。经典 .Lib 字节解析（BinaryReader 构造 / CreateTexture / 像素数据 / 蒙版）已随
    /// .Lib 资源移除而删除；现仅作为 MosaicManager 马赛克占位图的载体，贴图释放由持有方自行管理。
    /// </summary>
    public sealed class MImage : System.IDisposable
    {
        public short Width, Height;
        public bool TextureValid;
        public KFramework.MonoGame.Texture2D Image;

        public void DisposeTexture()
        {
            if (Image != null)
            {
                Image.Dispose();
                Image = null;
            }
            TextureValid = false;
        }

        public void Dispose()
        {
            DisposeTexture();
        }
    }
}
