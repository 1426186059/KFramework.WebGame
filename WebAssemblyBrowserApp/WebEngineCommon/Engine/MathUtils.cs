#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>常用数学工具（float 精度）。</summary>
public static class MathUtils
{
    public const float Deg2Rad = 0.017453292f;
    public const float Rad2Deg = 57.2957795f;
    public const float TwoPi = 6.28318548f;

    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>带死区的线性插值。</summary>
    public static float LerpClamped(float a, float b, float t) => a + (b - a) * Clamp(t, 0, 1);

    /// <summary>角度（弧度）平滑插值，自动取最短路径。</summary>
    public static float LerpAngle(float a, float b, float t)
    {
        float diff = (float)Math.IEEERemainder(b - a, TwoPi);
        return a + diff * Clamp(t, 0, 1);
    }

    public static float Rand(float min, float max) => min + (max - min) * (float)Random.Shared.NextDouble();

    public static float RandAngle() => (float)Random.Shared.NextDouble() * TwoPi;

    /// <summary>min（含）~ max（含）随机整数。</summary>
    public static int RandInt(int min, int max) => Random.Shared.Next(min, max + 1);

    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta) return target;
        return current + Math.Sign(target - current) * maxDelta;
    }

    /// <summary>平滑阻尼插值（帧率无关，lambda 为响应速度）。</summary>
    public static float Damp(float current, float target, float lambda, float dt)
        => Lerp(current, target, 1 - MathF.Exp(-lambda * dt));
}
