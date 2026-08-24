namespace PixiJS;

/// <summary>对应 PixiJS Container：可挂子对象，变换（位置/缩放/旋转）由 Pixi 场景图自动合成。</summary>
public sealed class Container : PixiObject
{
    public static Container Create() => new(PixiApi.Create("container"));

    /// <summary>句柄 0 保留给 app.stage（PixiApp.Stage）。</summary>
    internal Container(int handle) : base(handle) { }

    public void AddChild(PixiObject child) => PixiApi.AddChild(Handle, child.Handle);
    public void RemoveChild(PixiObject child) => PixiApi.RemoveChild(Handle, child.Handle);
    public void RemoveChildren() => PixiApi.RemoveChildren(Handle);
}
