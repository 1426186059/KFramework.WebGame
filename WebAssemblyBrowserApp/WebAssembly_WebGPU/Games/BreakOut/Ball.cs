using System;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>弹球：粘在挡板上等待发射，发射后按速度方向移动。</summary>
public sealed class Ball : GameObject
{
    public float Radius { get; set; } = 7;
    public bool IsStuck { get; set; } = true;
    public float Speed { get; set; } = 330;

    public Ball()
    {
        Width = Radius * 2;
        Height = Radius * 2;
    }

    public override void Update(float dt)
    {
        if (IsStuck) return;
        Position += Velocity * dt;
    }

    /// <summary>以给定角度（度，0 = 竖直向上）发射。</summary>
    public void Launch(float angleDegrees)
    {
        float rad = angleDegrees * MathF.PI / 180.0f;
        Velocity = new Vector2(MathF.Sin(rad), -MathF.Cos(rad)) * Speed;
        IsStuck = false;
    }

    public void Reset()
    {
        IsStuck = true;
        Velocity = Vector2.Zero;
    }
}
