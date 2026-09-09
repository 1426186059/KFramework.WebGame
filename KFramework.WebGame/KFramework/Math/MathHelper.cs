using System.Runtime.CompilerServices;

namespace KFramework;

/// <summary>常用数学工具。</summary>
public static class MathHelper
{
    public const float Pi = MathF.PI;
    public const float TwoPi = MathF.PI * 2f;
    public const float PiOver2 = MathF.PI / 2f;
    public const float PiOver4 = MathF.PI / 4f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToDegrees(float radians) => radians * (180f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>帧率无关的指数平滑，用于跟随/缓动。</summary>
    public static float Damp(float current, float target, float smoothing, float dt)
        => Lerp(current, target, 1f - MathF.Exp(-smoothing * dt));

    public static float Min(float a, float b) => a < b ? a : b;
    public static float Max(float a, float b) => a > b ? a : b;
}
