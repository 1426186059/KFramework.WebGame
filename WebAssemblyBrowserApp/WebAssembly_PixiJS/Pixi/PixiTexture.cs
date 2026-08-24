using System.Runtime.InteropServices.JavaScript;

namespace PixiJS;

/// <summary>对应 PixiJS Texture：图片 / 视频纹理（句柄注册在 JS 侧）。</summary>
public sealed class PixiTexture
{
    public int Handle { get; }
    public float Width { get; }
    public float Height { get; }

    private PixiTexture(int handle, float w, float h)
    {
        Handle = handle;
        Width = w;
        Height = h;
    }

    public static async Task<PixiTexture?> LoadAsync(string url)
    {
        using var obj = await PixiApi.LoadTexture(url);
        int id = obj.GetPropertyAsInt32("id");
        if (id < 0) return null;
        return new PixiTexture(id, (float)obj.GetPropertyAsDouble("w"), (float)obj.GetPropertyAsDouble("h"));
    }

    public static async Task<PixiTexture?> LoadVideoAsync(string url)
    {
        using var obj = await PixiApi.LoadVideo(url);
        int id = obj.GetPropertyAsInt32("id");
        if (id < 0) return null;
        return new PixiTexture(id, (float)obj.GetPropertyAsDouble("w"), (float)obj.GetPropertyAsDouble("h"));
    }
}
