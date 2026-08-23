using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>游戏对象基类：位置 / 速度 / 尺寸 / 生命周期。</summary>
public abstract class GameObject
{
    public Vector2 Position;
    public Vector2 Velocity;
    public double Width;
    public double Height;
    public bool IsActive = true;

    public virtual void Update(float dt) => Position += Velocity * dt;

    public virtual void Render() { }

    /// <summary>AABB 相交检测。</summary>
    public bool Intersects(GameObject other) =>
        Math.Abs(Position.X - other.Position.X) < (Width + other.Width) / 2 &&
        Math.Abs(Position.Y - other.Position.Y) < (Height + other.Height) / 2;
}
