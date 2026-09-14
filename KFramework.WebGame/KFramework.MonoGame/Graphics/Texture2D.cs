using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Graphics;

/// <summary>
/// 2D 纹理。可以表示整张纹理，也可以是图集中的一块子区域（共享底层 WebGL 纹理对象，
/// 因此图集内的所有精灵都能被 SpriteBatch 合并进同一个 draw call）。
/// </summary>
public sealed class Texture2D : IDisposable
{
    internal readonly JSObject Handle;

    /// <summary>底层 WebGL 纹理的序号，用于批处理时判断是否可合并。</summary>
    internal readonly int BatchKey;

    /// <summary>本纹理在底层 WebGL 纹理上的像素区域（图集子图时不是整张）。</summary>
    public readonly Rectangle Bounds;

    /// <summary>底层 WebGL 纹理尺寸。</summary>
    public readonly int TextureWidth;
    public readonly int TextureHeight;

    /// <summary>是否负责释放底层纹理（图集子图为 false）。</summary>
    internal readonly bool OwnsHandle;

    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    public float U0 => Bounds.X / (float)TextureWidth;
    public float V0 => Bounds.Y / (float)TextureHeight;
    public float U1 => Bounds.Right / (float)TextureWidth;
    public float V1 => Bounds.Bottom / (float)TextureHeight;

    internal Texture2D(JSObject handle, int width, int height, int batchKey, bool ownsHandle)
    {
        Handle = handle;
        TextureWidth = width;
        TextureHeight = height;
        Bounds = new Rectangle(0, 0, width, height);
        BatchKey = batchKey;
        OwnsHandle = ownsHandle;
    }

    private Texture2D(Texture2D atlas, Rectangle bounds)
    {
        Handle = atlas.Handle;
        TextureWidth = atlas.TextureWidth;
        TextureHeight = atlas.TextureHeight;
        Bounds = bounds;
        BatchKey = atlas.BatchKey;
        OwnsHandle = false;
    }

    /// <summary>从图集中切出一块子图。</summary>
    internal Texture2D CreateSubtexture(Rectangle bounds) => new(this, bounds);

    /// <summary>上传 RGBA8 像素数据到整张纹理。</summary>
    public void SetData(byte[] rgba)
        => SetData(rgba, 0, 0, TextureWidth, TextureHeight);

    /// <summary>更新纹理的局部区域（用于动态字形图集等）。</summary>
    public void SetData(byte[] rgba, int x, int y, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        GL.BindTexture(GL.TEXTURE_2D, Handle);
        GL.PixelStorei(GL.UNPACK_ALIGNMENT, 1);
        if (x == 0 && y == 0 && width == TextureWidth && height == TextureHeight)
            GL.TexImage2D(GL.TEXTURE_2D, 0, GL.RGBA8, width, height, 0, GL.RGBA, GL.UNSIGNED_BYTE, rgba);
        else
            GL.TexSubImage2D(GL.TEXTURE_2D, 0, x, y, width, height, GL.RGBA, GL.UNSIGNED_BYTE, rgba);
    }

    public void Dispose()
    {
        if (OwnsHandle) GL.DeleteTexture(Handle);
    }
}
