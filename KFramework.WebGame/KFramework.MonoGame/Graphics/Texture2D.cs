using System.Runtime.InteropServices.JavaScript;

using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 2D 纹理（对应底层的一张 WebGL 纹理）。图集里的某个子图通过「单张整页纹理 + source rect」
    /// 的方式绘制（照官方 MonoGame），这样同一张图集页能被 SpriteBatch 合并进同一个 draw call。
    /// </summary>
    public sealed class Texture2D : Texture
    {
        internal readonly JSObject Handle;

        /// <summary>本纹理在底层 WebGL 纹理上的像素区域（整图时为 0,0,Width,Height）。</summary>
        public readonly Rectangle Bounds;

        /// <summary>底层 WebGL 纹理尺寸。</summary>
        public readonly int TextureWidth;
        public readonly int TextureHeight;

        /// <summary>是否负责释放底层纹理。</summary>
        internal readonly bool OwnsHandle;

        public int Width => Bounds.Width;
        public int Height => Bounds.Height;

        internal Texture2D(GraphicsDevice device, JSObject handle, int width, int height, bool ownsHandle)
        {
            graphicsDevice = device;
            Handle = handle;
            TextureWidth = width;
            TextureHeight = height;
            Bounds = new Rectangle(0, 0, width, height);
            OwnsHandle = ownsHandle;
        }

        /// <summary>上传 RGBA8 像素数据到整张纹理。</summary>
        public void SetData(byte[] rgba)
            => SetData(rgba, 0, 0, TextureWidth, TextureHeight);

        /// <summary>更新纹理的局部区域（用于动态字形图集等）。</summary>
        public void SetData(byte[] rgba, int x, int y, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(rgba);
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, Handle);
            JSBind_GL.PixelStorei(JSBind_GL.UNPACK_ALIGNMENT, 1);
            if (x == 0 && y == 0 && width == TextureWidth && height == TextureHeight)
                JSBind_GL.TexImage2D(JSBind_GL.TEXTURE_2D, 0, JSBind_GL.RGBA8, width, height, 0, JSBind_GL.RGBA, JSBind_GL.UNSIGNED_BYTE, rgba);
            else
                JSBind_GL.TexSubImage2D(JSBind_GL.TEXTURE_2D, 0, x, y, width, height, JSBind_GL.RGBA, JSBind_GL.UNSIGNED_BYTE, rgba);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && OwnsHandle) JSBind_GL.DeleteTexture(Handle);
        }
    }
}
