using System;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>挡板：键盘 / 鼠标控制。</summary>
public sealed class Paddle : GameObject
{
    private const float MoveSpeed = 540;

    public Paddle()
    {
        Width = 116;
        Height = 16;
        Position = new Vector2(GameEngine.Width / 2, GameEngine.Height - 46);
    }

    public override void Update(float dt)
    {
        float vx = 0;
        if (Input.IsKeyDown(Input.ArrowLeft) || Input.IsKeyDown(Input.KeyA)) vx -= MoveSpeed;
        if (Input.IsKeyDown(Input.ArrowRight) || Input.IsKeyDown(Input.KeyD)) vx += MoveSpeed;

        float x = Position.X + vx * dt;

        // 无键盘输入时平滑跟随鼠标
        if (MathF.Abs(vx) < 1)
            x = MathUtils.Lerp(Position.X, Input.MouseX(), MathF.Min(1, 22 * dt));

        Position = new Vector2(
            MathUtils.Clamp(x, Width / 2, GameEngine.Width - Width / 2),
            Position.Y);
    }
}
