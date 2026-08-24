namespace PixiJS;

/// <summary>对应 PixiJS Sprite：纹理精灵。</summary>
public sealed class Sprite : PixiObject
{
    private float _w, _h;

    public static Sprite Create() => new(PixiApi.Create("sprite"));
    internal Sprite(int handle) : base(handle) { }

    public float Width { get => _w; set { _w = value; PixiApi.SetProp(Handle, "width", value); } }
    public float Height { get => _h; set { _h = value; PixiApi.SetProp(Handle, "height", value); } }

    public void SetTexture(PixiTexture tex)
    {
        if (tex is null) return;
        PixiApi.SpriteTex(Handle, tex.Handle);
        _w = tex.Width;
        _h = tex.Height;
    }

    public void SetAnchor(float ax, float ay) => PixiApi.SetProp2(Handle, "anchor", ax, ay);
}
