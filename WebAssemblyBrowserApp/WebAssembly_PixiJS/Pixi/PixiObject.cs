namespace PixiJS;

/// <summary>
/// 所有 PixiJS 对象的 C# 基类：持有一个 JS 侧句柄（id），
/// 通用属性（位置/透明度/可见性/旋转）set 时即时同步到 JS 对象。
/// </summary>
public abstract class PixiObject : IDisposable
{
    public int Handle { get; }
    public bool IsDisposed { get; private set; }

    private float _x, _y, _alpha = 1f, _rotation;
    private bool _visible = true;

    internal PixiObject(int handle) => Handle = handle;

    public float X { get => _x; set { _x = value; PixiApi.SetProp(Handle, "x", value); } }
    public float Y { get => _y; set { _y = value; PixiApi.SetProp(Handle, "y", value); } }
    public float Alpha { get => _alpha; set { _alpha = value; PixiApi.SetProp(Handle, "alpha", value); } }
    public bool Visible { get => _visible; set { _visible = value; PixiApi.SetProp(Handle, "visible", value ? 1 : 0); } }
    public float Rotation { get => _rotation; set { _rotation = value; PixiApi.SetProp(Handle, "rotation", value); } }

    public void SetScale(float sx, float sy) => PixiApi.SetProp2(Handle, "scale", sx, sy);
    public void SetPivot(float px, float py) => PixiApi.SetProp2(Handle, "pivot", px, py);

    public void Destroy()
    {
        if (IsDisposed) return;
        PixiApi.Destroy(Handle);
        IsDisposed = true;
    }

    public void Dispose() => Destroy();
}
