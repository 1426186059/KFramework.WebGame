using System;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>粒子：用于砖块破碎、碰撞迸溅等特效。</summary>
public sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public double Size;
    public float Life;
    public float MaxLife;
    public string Color;

    public Particle(Vector2 position, Vector2 velocity, double size, float life, string color)
    {
        Position = position;
        Velocity = velocity;
        Size = size;
        Life = life;
        MaxLife = life;
        Color = color;
    }

    /// <summary>返回 false 表示粒子已消亡。</summary>
    public bool Update(float dt)
    {
        Position += Velocity * dt;
        Velocity *= Math.Pow(0.55, dt);
        Life -= dt;
        return Life > 0;
    }

    public float Alpha => Life / MaxLife;
}
